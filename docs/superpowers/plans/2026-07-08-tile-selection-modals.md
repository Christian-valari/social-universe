# Tile Selection Modals (Land Purchase & Owner Info) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the current auto-buy-on-select flow with a purchase-confirmation modal for `Available` tiles, and add a read-only info modal (with a Sell action for owned tiles) for `OwnedByPlayer` / `OwnedByOther` / `Landmark` tiles.

**Architecture:** `HUDController` subscribes to the existing `TileSelectedEvent` and routes to one of two new `SocialUniverse.UI` modal `MonoBehaviour`s based on `tile.State`. The modals communicate confirmation/completion with the existing `App`-layer handlers (`TilePurchaseHandler`, `LandSaleHandler`) through three new `EventBus` event types, keeping server calls and `TileData` mutation in the handlers and UI/confirmation logic in the modals.

**Tech Stack:** Unity 6, C#, VContainer (DI), `SocialUniverse.Core.EventBus` (static pub/sub), TextMeshPro (`TMPro`), UGUI (`UnityEngine.UI`).

## Global Constraints

- The current auto-buy-on-select behavior (`TilePurchaseHandler` buying immediately on `TileSelectedEvent`) is removed and replaced by confirm-first.
- New event types are declared alongside the existing tile events at the top of `Assets/_Project/Scripts/World/HexasphereManager.cs` (matches existing precedent — `TileSelectedEvent`, `BuildItemRequestedEvent`, `TileSellRequestedEvent` all live there regardless of consumer layer).
- New UI components use the `*Modal` suffix (`LandPurchaseModal`, `TileInfoModal`), matching `DisplayNameModal`/`AvatarSelectionModal`/`EmailVerificationModal` — a deliberate, already-approved deviation from CLAUDE.md's literal `*Screen`/`*View` naming table.
- `VisitorTrackingController`'s existing `TileSelectedEvent` subscription is untouched.
- No new automated tests: this codebase unit-tests `Economy/` service classes but not `App/`-layer event handlers or UI `MonoBehaviour` modals, and the approved design spec (`docs/superpowers/specs/2026-07-08-tile-selection-modals-design.md`) explicitly follows that existing boundary. Each task is instead verified by a clean Unity compile (via the UnityMCP `refresh_unity` + `read_console` tools) — the same verification method already used earlier in this session for a HUDController change.
- Price/refund formulas stay duplicated as one-liners (matches existing precedent in `LandPurchaseService`/`LandSaleService`); no shared helper is introduced.
- Building the actual button/text UI layout inside the modal prefabs (positions, styling, assigning `[SerializeField]` references in the Inspector) is manual Unity Editor work outside this plan's scope — this plan produces the C# scripts and wiring; a human needs to build/assign the prefab UI afterward, the same as for the existing modals.

---

### Task 1: Add new tile-purchase/sale events

**Files:**
- Modify: `Assets/_Project/Scripts/World/HexasphereManager.cs:10-14`

**Interfaces:**
- Produces: `TilePurchaseConfirmedEvent { TileData Tile }`, `TilePurchaseCompletedEvent { TileData Tile; bool Success; string FailureReason }`, `TileSaleCompletedEvent { TileData Tile; bool Success; string FailureReason }` — all plain POCOs in `SocialUniverse.World`, used by Tasks 2–5.

- [ ] **Step 1: Add the three event classes**

In `Assets/_Project/Scripts/World/HexasphereManager.cs`, replace lines 10-14:

```csharp
    public class TileSelectedEvent { public TileData Tile; }

    public class BuildItemRequestedEvent { public TileData Tile; public ItemDefinition Item; }

    public class TileSellRequestedEvent { public TileData Tile; }
```

with:

```csharp
    public class TileSelectedEvent { public TileData Tile; }

    public class BuildItemRequestedEvent { public TileData Tile; public ItemDefinition Item; }

    public class TileSellRequestedEvent { public TileData Tile; }

    public class TilePurchaseConfirmedEvent { public TileData Tile; }

    public class TilePurchaseCompletedEvent { public TileData Tile; public bool Success; public string FailureReason; }

    public class TileSaleCompletedEvent { public TileData Tile; public bool Success; public string FailureReason; }
```

- [ ] **Step 2: Compile and verify no errors**

Use the UnityMCP tools: call `refresh_unity` with `compile: "request"`, `scope: "scripts"`, `wait_for_ready: true`, then call `read_console` with `action: "get"`, `types: ["error"]`.
Expected: `read_console` returns 0 error entries.

- [ ] **Step 3: Commit**

```bash
git add Assets/_Project/Scripts/World/HexasphereManager.cs
git commit -m "Add tile purchase/sale confirmation and completion events"
```

---

### Task 2: Switch TilePurchaseHandler to confirm-based purchase flow

**Files:**
- Modify: `Assets/_Project/Scripts/App/TilePurchaseHandler.cs` (full file rewrite)

**Interfaces:**
- Consumes: `TilePurchaseConfirmedEvent` (Task 1).
- Produces: publishes `TilePurchaseCompletedEvent` (Task 1) on every code path — consumed by `LandPurchaseModal` in Task 4.

- [ ] **Step 1: Replace the file contents**

Replace the entire contents of `Assets/_Project/Scripts/App/TilePurchaseHandler.cs` with:

```csharp
using System;
using VContainer.Unity;
using SocialUniverse.Core;
using SocialUniverse.Economy;
using SocialUniverse.Config;
using SocialUniverse.World;

namespace SocialUniverse.App
{
    public class TilePurchaseHandler : IStartable, IDisposable
    {
        private readonly LandPurchaseService _purchaseService;
        private readonly TileColorizer       _colorizer;
        private readonly PlanetDefinition    _planet;
        private readonly IAuthService        _auth;
        private readonly LandRegistryService _landRegistryService;

        public TilePurchaseHandler(LandPurchaseService purchaseService, TileColorizer colorizer,
            PlanetDefinition planet, IAuthService auth, LandRegistryService landRegistryService)
        {
            _purchaseService     = purchaseService;
            _colorizer           = colorizer;
            _planet              = planet;
            _auth                = auth;
            _landRegistryService = landRegistryService;
        }

        public void Start()   => EventBus.Subscribe<TilePurchaseConfirmedEvent>(OnTilePurchaseConfirmed);
        public void Dispose() => EventBus.Unsubscribe<TilePurchaseConfirmedEvent>(OnTilePurchaseConfirmed);

        private async void OnTilePurchaseConfirmed(TilePurchaseConfirmedEvent e)
        {
            var tile = e.Tile;
            if (tile.State != TileState.Available)
            {
                EventBus.Publish(new TilePurchaseCompletedEvent
                    { Tile = tile, Success = false, FailureReason = "Tile is already owned" });
                return;
            }

            string playerId = _auth.IsSignedIn ? _auth.PlayerId : "local_player";
            var request = new LandPurchaseRequest { TileId = tile.TileId, PlayerId = playerId };
            var result  = await _purchaseService.PurchaseAsync(request, _planet);

            if (!result.Success)
            {
                SULog.Warn($"Buy tile {tile.TileId} failed: {result.FailureReason}", SULog.Channel.Economy);
                EventBus.Publish(new TilePurchaseCompletedEvent
                    { Tile = tile, Success = false, FailureReason = result.FailureReason });
                return;
            }

            tile.State   = TileState.OwnedByPlayer;
            tile.OwnerId = playerId;
            _colorizer.RefreshTile(tile);
            _landRegistryService.SetOwner(tile.TileId, playerId);
            EventBus.Publish(new TilePurchaseCompletedEvent { Tile = tile, Success = true });
        }
    }
}
```

This changes only two things from the original: the subscription is now on `TilePurchaseConfirmedEvent` instead of `TileSelectedEvent`, a stale-state guard publishes an immediate failure if the tile is no longer `Available`, and every exit path now publishes `TilePurchaseCompletedEvent`.

- [ ] **Step 2: Compile and verify no errors**

Call `refresh_unity` (`compile: "request"`, `scope: "scripts"`, `wait_for_ready: true`), then `read_console` (`types: ["error"]`).
Expected: 0 error entries.

- [ ] **Step 3: Commit**

```bash
git add Assets/_Project/Scripts/App/TilePurchaseHandler.cs
git commit -m "Trigger land purchase from explicit confirmation instead of tile selection"
```

---

### Task 3: Publish sale completion from LandSaleHandler

**Files:**
- Modify: `Assets/_Project/Scripts/App/LandSaleHandler.cs` (full file rewrite)

**Interfaces:**
- Consumes: `TileSellRequestedEvent` (existing, unchanged).
- Produces: publishes `TileSaleCompletedEvent` (Task 1) on every code path — consumed by `TileInfoModal` in Task 5.

- [ ] **Step 1: Replace the file contents**

Replace the entire contents of `Assets/_Project/Scripts/App/LandSaleHandler.cs` with:

```csharp
using System;
using VContainer.Unity;
using SocialUniverse.Core;
using SocialUniverse.Economy;
using SocialUniverse.Config;
using SocialUniverse.World;

namespace SocialUniverse.App
{
    public class LandSaleHandler : IStartable, IDisposable
    {
        private readonly LandSaleService   _saleService;
        private readonly TileColorizer     _colorizer;
        private readonly TileExtrusionView _extrusionView;
        private readonly PlanetDefinition  _planet;

        public LandSaleHandler(LandSaleService saleService, TileColorizer colorizer,
            TileExtrusionView extrusionView, PlanetDefinition planet)
        {
            _saleService   = saleService;
            _colorizer     = colorizer;
            _extrusionView = extrusionView;
            _planet        = planet;
        }

        public void Start()   => EventBus.Subscribe<TileSellRequestedEvent>(OnTileSellRequested);
        public void Dispose() => EventBus.Unsubscribe<TileSellRequestedEvent>(OnTileSellRequested);

        private async void OnTileSellRequested(TileSellRequestedEvent e)
        {
            var tile = e.Tile;
            if (tile.State != TileState.OwnedByPlayer)
            {
                SULog.Warn($"LandSaleHandler: cannot sell tile {tile.TileId} — not owned by player", SULog.Channel.Economy);
                EventBus.Publish(new TileSaleCompletedEvent
                    { Tile = tile, Success = false, FailureReason = "Not your tile" });
                return;
            }

            var result = await _saleService.SellAsync(tile.TileId, _planet);

            if (!result.Success)
            {
                SULog.Warn($"Sell tile {tile.TileId} failed: {result.Reason}", SULog.Channel.Economy);
                EventBus.Publish(new TileSaleCompletedEvent
                    { Tile = tile, Success = false, FailureReason = result.Reason });
                return;
            }

            tile.State      = TileState.Available;
            tile.OwnerId    = null;
            tile.BuildLevel = 0;
            _colorizer.RefreshTile(tile);
            _extrusionView.RefreshTile(tile);

            SULog.Info($"Sold tile {tile.TileId} (balance {result.NewBalance})", SULog.Channel.Economy);
            EventBus.Publish(new TileSaleCompletedEvent { Tile = tile, Success = true });
        }
    }
}
```

- [ ] **Step 2: Compile and verify no errors**

Call `refresh_unity` (`compile: "request"`, `scope: "scripts"`, `wait_for_ready: true`), then `read_console` (`types: ["error"]`).
Expected: 0 error entries.

- [ ] **Step 3: Commit**

```bash
git add Assets/_Project/Scripts/App/LandSaleHandler.cs
git commit -m "Publish TileSaleCompletedEvent from LandSaleHandler"
```

---

### Task 4: Create LandPurchaseModal

**Files:**
- Create: `Assets/_Project/Scripts/UI/LandPurchaseModal.cs`

**Interfaces:**
- Consumes: `TilePurchaseCompletedEvent` (Task 1); `[Inject] Wallet` (`Coins` property, `CanAfford(int)` method — `Assets/_Project/Scripts/Economy/Wallet.cs`); `[Inject] PlanetDefinition` (`LandPriceMultiplier` property); `[Inject] EconomyConfig` (`BaseLandPrice` property).
- Produces: `public void Open(TileData tile)`, `public void Close()` — consumed by `HUDController` in Task 6. Publishes `TilePurchaseConfirmedEvent` (Task 1) on Confirm.

- [ ] **Step 1: Create the file**

```csharp
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using TMPro;
using SocialUniverse.Core;
using SocialUniverse.Config;
using SocialUniverse.Economy;
using SocialUniverse.World;

namespace SocialUniverse.UI
{
    // Confirmation modal for purchasing an Available tile. Opened by HUDController
    // when a TileSelectedEvent arrives for an Available tile; this is what replaced
    // the old auto-buy-on-select behavior in TilePurchaseHandler.
    public class LandPurchaseModal : MonoBehaviour
    {
        [SerializeField] private TMP_Text _priceText;
        [SerializeField] private TMP_Text _balanceText;
        [SerializeField] private TMP_Text _statusText;
        [SerializeField] private Button   _confirmButton;
        [SerializeField] private Button   _cancelButton;

        [Inject] private Wallet           _wallet;
        [Inject] private PlanetDefinition _planet;
        [Inject] private EconomyConfig    _economyConfig;

        private TileData _currentTile;

        private void Awake()
        {
            _confirmButton.onClick.AddListener(OnConfirmClicked);
            _cancelButton.onClick.AddListener(Close);
            gameObject.SetActive(false);
        }

        private void OnEnable()  => EventBus.Subscribe<TilePurchaseCompletedEvent>(OnTilePurchaseCompleted);
        private void OnDisable() => EventBus.Unsubscribe<TilePurchaseCompletedEvent>(OnTilePurchaseCompleted);

        public void Open(TileData tile)
        {
            _currentTile = tile;

            int  price     = Mathf.RoundToInt(_economyConfig.BaseLandPrice * _planet.LandPriceMultiplier);
            bool canAfford = _wallet.CanAfford(price);

            _priceText.text   = $"{price} coins";
            _balanceText.text = $"Balance: {_wallet.Coins} coins";
            _statusText.text  = canAfford ? "" : "Not enough coins";
            _confirmButton.interactable = canAfford;

            gameObject.SetActive(true);
        }

        public void Close()
        {
            _currentTile = null;
            gameObject.SetActive(false);
        }

        private void OnConfirmClicked()
        {
            if (_currentTile == null) return;
            SetBusy(true);
            _statusText.text = "Purchasing…";
            EventBus.Publish(new TilePurchaseConfirmedEvent { Tile = _currentTile });
        }

        private void OnTilePurchaseCompleted(TilePurchaseCompletedEvent e)
        {
            if (e.Tile != _currentTile) return;

            SetBusy(false);
            if (e.Success)
            {
                _statusText.text = "Purchased!";
                Close();
            }
            else
            {
                _statusText.text = e.FailureReason;
            }
        }

        private void SetBusy(bool busy)
        {
            _confirmButton.interactable = !busy;
            _cancelButton.interactable  = !busy;
        }
    }
}
```

- [ ] **Step 2: Compile and verify no errors**

Call `refresh_unity` (`compile: "request"`, `scope: "scripts"`, `wait_for_ready: true`), then `read_console` (`types: ["error"]`).
Expected: 0 error entries.

- [ ] **Step 3: Commit**

```bash
git add Assets/_Project/Scripts/UI/LandPurchaseModal.cs
git commit -m "Add LandPurchaseModal for confirming tile purchases"
```

---

### Task 5: Create TileInfoModal

**Files:**
- Create: `Assets/_Project/Scripts/UI/TileInfoModal.cs`

**Interfaces:**
- Consumes: `TileSaleCompletedEvent` (Task 1); `[Inject] ProfileService` (`Task<PlayerProfile> GetProfileAsync(string playerId)` — `Assets/_Project/Scripts/Social/ProfileService.cs`); `[Inject] DatabaseRegistry` (`AvatarDefinition GetAvatar(string avatarId)` — `Assets/_Project/Scripts/Config/DatabaseRegistry.cs`); `PlayerProfile` fields `DisplayName`, `AvatarId`, `Level`, `TilesOwned`, `Badges` (`Assets/_Project/Scripts/Social/PlayerProfile.cs`); `AvatarDefinition.Sprite` property.
- Produces: `public void Open(TileData tile)`, `public void Close()` — consumed by `HUDController` in Task 6. Publishes the existing `TileSellRequestedEvent` on Sell.

- [ ] **Step 1: Create the file**

```csharp
using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using TMPro;
using SocialUniverse.Core;
using SocialUniverse.Config;
using SocialUniverse.Social;
using SocialUniverse.World;

namespace SocialUniverse.UI
{
    // Read-only tile info for OwnedByPlayer/OwnedByOther/Landmark tiles, with a
    // Sell action shown only for tiles the player owns. Opened by HUDController
    // when a TileSelectedEvent arrives for a non-Available tile.
    public class TileInfoModal : MonoBehaviour
    {
        [SerializeField] private TMP_Text _titleText;
        [SerializeField] private Image    _avatarImage;
        [SerializeField] private TMP_Text _ownerInfoText;
        [SerializeField] private TMP_Text _tileStatsText;
        [SerializeField] private Button   _sellButton;
        [SerializeField] private Button   _closeButton;
        [SerializeField] private TMP_Text _statusText;

        [Inject] private ProfileService   _profileService;
        [Inject] private DatabaseRegistry _registry;

        private TileData _currentTile;

        private void Awake()
        {
            _sellButton.onClick.AddListener(OnSellClicked);
            _closeButton.onClick.AddListener(Close);
            gameObject.SetActive(false);
        }

        private void OnEnable()  => EventBus.Subscribe<TileSaleCompletedEvent>(OnTileSaleCompleted);
        private void OnDisable() => EventBus.Unsubscribe<TileSaleCompletedEvent>(OnTileSaleCompleted);

        public async void Open(TileData tile)
        {
            _currentTile = tile;
            _statusText.text    = "";
            _tileStatsText.text = $"Build level {tile.BuildLevel} · Yield {tile.YieldRate:0.0}/hr";
            _sellButton.gameObject.SetActive(tile.State == TileState.OwnedByPlayer);
            _ownerInfoText.gameObject.SetActive(false);
            _avatarImage.gameObject.SetActive(false);

            gameObject.SetActive(true);

            switch (tile.State)
            {
                case TileState.OwnedByPlayer:
                    _titleText.text = "Your Tile";
                    break;
                case TileState.OwnedByOther:
                    await LoadOwnerProfileAsync(tile);
                    break;
                default:
                    _titleText.text = "Landmark";
                    break;
            }
        }

        private async Task LoadOwnerProfileAsync(TileData tile)
        {
            _titleText.text = "Loading…";

            if (string.IsNullOrEmpty(tile.OwnerId))
            {
                _titleText.text = "Owned by another player";
                return;
            }

            try
            {
                var profile = await _profileService.GetProfileAsync(tile.OwnerId);
                if (_currentTile != tile) return;

                _titleText.text = $"{profile.DisplayName}'s Tile";
                _ownerInfoText.gameObject.SetActive(true);
                _ownerInfoText.text = $"Level {profile.Level} · {profile.TilesOwned} tiles owned"
                    + (profile.Badges is { Length: > 0 } ? $"\n{string.Join(", ", profile.Badges)}" : "");

                var avatar = _registry.GetAvatar(profile.AvatarId);
                if (avatar != null)
                {
                    _avatarImage.gameObject.SetActive(true);
                    _avatarImage.sprite = avatar.Sprite;
                }
            }
            catch (Exception ex)
            {
                if (_currentTile != tile) return;
                _titleText.text  = "Owned by another player";
                _statusText.text = "Couldn't load profile";
                SULog.Warn($"TileInfoModal: profile fetch failed for {tile.OwnerId} — {ex.Message}", SULog.Channel.Social);
            }
        }

        public void Close()
        {
            _currentTile = null;
            gameObject.SetActive(false);
        }

        private void OnSellClicked()
        {
            if (_currentTile == null) return;
            SetBusy(true);
            _statusText.text = "Selling…";
            EventBus.Publish(new TileSellRequestedEvent { Tile = _currentTile });
        }

        private void OnTileSaleCompleted(TileSaleCompletedEvent e)
        {
            if (e.Tile != _currentTile) return;

            SetBusy(false);
            if (e.Success) Close();
            else _statusText.text = $"Sell failed: {e.FailureReason}";
        }

        private void SetBusy(bool busy)
        {
            _sellButton.interactable  = !busy;
            _closeButton.interactable = !busy;
        }
    }
}
```

- [ ] **Step 2: Compile and verify no errors**

Call `refresh_unity` (`compile: "request"`, `scope: "scripts"`, `wait_for_ready: true`), then `read_console` (`types: ["error"]`).
Expected: 0 error entries.

- [ ] **Step 3: Commit**

```bash
git add Assets/_Project/Scripts/UI/TileInfoModal.cs
git commit -m "Add TileInfoModal for owned/other/landmark tile info and selling"
```

---

### Task 6: Wire HUDController routing and register modals in DI

**Files:**
- Modify: `Assets/_Project/Scripts/UI/HUDController.cs:20-48` (fields), `:50-88` (Start), `:90-98` (OnDestroy)
- Modify: `Assets/_Project/Scripts/App/PlanetSceneScope.cs:120-126`

**Interfaces:**
- Consumes: `TileSelectedEvent` (existing); `LandPurchaseModal.Open(TileData)` (Task 4); `TileInfoModal.Open(TileData)` (Task 5).

- [ ] **Step 1: Add modal fields to HUDController**

In `Assets/_Project/Scripts/UI/HUDController.cs`, add two new `[SerializeField]` fields right after the existing `_planetNameText` field (line 39):

```csharp
        [SerializeField] private TMP_Text _planetNameText;
        [SerializeField] private LandPurchaseModal _landPurchaseModal;
        [SerializeField] private TileInfoModal     _tileInfoModal;
```

- [ ] **Step 2: Subscribe/unsubscribe and add the routing method**

In `Start()`, add a subscription alongside the existing `EventBus.Subscribe<ShowEmailVerificationPromptEvent>(OnShowEmailVerificationPrompt);` line:

```csharp
            EventBus.Subscribe<ShowEmailVerificationPromptEvent>(OnShowEmailVerificationPrompt);
            EventBus.Subscribe<TileSelectedEvent>(OnTileSelectedForModal);
```

In `OnDestroy()`, add the matching unsubscribe:

```csharp
            EventBus.Unsubscribe<ShowEmailVerificationPromptEvent>(OnShowEmailVerificationPrompt);
            EventBus.Unsubscribe<TileSelectedEvent>(OnTileSelectedForModal);
```

Add the routing method next to `OnShowEmailVerificationPrompt`:

```csharp
        private void OnTileSelectedForModal(TileSelectedEvent e)
        {
            var tile = e.Tile;
            if (tile.State == TileState.Available)
                _landPurchaseModal?.Open(tile);
            else
                _tileInfoModal?.Open(tile);
        }
```

- [ ] **Step 3: Register the new modals in PlanetSceneScope**

In `Assets/_Project/Scripts/App/PlanetSceneScope.cs`, add two lines after the existing `EmailVerificationModal` registration (line 126):

```csharp
            builder.RegisterComponentInHierarchy<SocialUniverse.UI.EmailVerificationModal>();
            builder.RegisterComponentInHierarchy<SocialUniverse.UI.LandPurchaseModal>();
            builder.RegisterComponentInHierarchy<SocialUniverse.UI.TileInfoModal>();
```

- [ ] **Step 4: Compile and verify no errors**

Call `refresh_unity` (`compile: "request"`, `scope: "scripts"`, `wait_for_ready: true`), then `read_console` (`types: ["error"]`).
Expected: 0 error entries.

- [ ] **Step 5: Commit**

```bash
git add Assets/_Project/Scripts/UI/HUDController.cs Assets/_Project/Scripts/App/PlanetSceneScope.cs
git commit -m "Route tile selection to purchase/info modals from HUDController"
```

- [ ] **Step 6: Manual verification (requires Editor scene/prefab setup first)**

The two new `[SerializeField]` fields on `HUDController` (`_landPurchaseModal`, `_tileInfoModal`) need modal GameObjects built and assigned in the Planet scene before this is testable end-to-end — build the UI (Text/Button elements matching the `[SerializeField]` fields in each script) and wire the Inspector references, the same as the existing `DisplayNameModal`/`EmailVerificationModal` prefabs. Once wired, verify in Play Mode:
  - Select an `Available` tile → purchase modal opens showing price/balance; Confirm buys and closes; Cancel closes with no side effects.
  - Select an `Available` tile priced above your coin balance → Confirm is disabled with "Not enough coins".
  - Select a tile you own → info modal opens with a Sell button; Sell succeeds and closes, or shows a failure reason.
  - Select another player's tile → info modal opens, loads and displays their profile (name/avatar/level/tiles-owned/badges).
  - Select a Landmark tile → info modal opens read-only, no Sell button, no profile fetch.

---

## Self-Review Notes

- **Spec coverage:** Events (Task 1) → handler changes (Tasks 2–3) → both modals (Tasks 4–5) → routing + DI (Task 6) covers every section of the design spec (events, data flow, both components, handler changes, error handling via existing `Success`/reason DTOs, naming, and the manual-verification testing approach).
- **Placeholder scan:** No TBD/TODO; every step has complete code.
- **Type consistency:** `TileData`, `TileState`, `TilePurchaseConfirmedEvent`, `TilePurchaseCompletedEvent`, `TileSaleCompletedEvent` are used with identical field names and types across Tasks 1–6. `LandPurchaseModal.Open(TileData)` / `TileInfoModal.Open(TileData)` signatures in Tasks 4–5 match the calls added in Task 6.
