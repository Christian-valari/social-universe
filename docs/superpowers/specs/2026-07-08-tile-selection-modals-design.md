# Tile Selection Modals — Land Purchase & Owner Info

**Date:** 2026-07-08
**Status:** Approved for planning

## Problem

Selecting a tile on the hexasphere currently does one of two things depending on state:

- `Available` tiles are **auto-purchased immediately** on selection (`TilePurchaseHandler`), with no confirmation UI.
- `OwnedByPlayer` / `OwnedByOther` / `Landmark` tiles do nothing UI-wise (aside from `VisitorTrackingController` silently recording a visit for `OwnedByOther`).

This ships two new modals: a purchase-confirmation modal for `Available` tiles, and an info modal (with a Sell action for tiles you own) for everything else.

## Behavior change

The existing auto-buy-on-select behavior is removed. Selecting an `Available` tile now opens a confirmation modal instead of instantly spending coins.

## Events

New events, declared alongside the existing tile events at the top of `World/HexasphereManager.cs`:

```csharp
public class TilePurchaseConfirmedEvent { public TileData Tile; }
public class TilePurchaseCompletedEvent { public TileData Tile; public bool Success; public string FailureReason; }
public class TileSaleCompletedEvent     { public TileData Tile; public bool Success; public string FailureReason; }
```

`TileSelectedEvent` and `TileSellRequestedEvent` are unchanged and reused as-is.

## Data flow

1. `HexasphereManager.SelectTile` publishes `TileSelectedEvent` (existing, unchanged).
2. `HUDController` subscribes to `TileSelectedEvent` and routes by `tile.State`:
   - `Available` → `_landPurchaseModal.Open(tile)`
   - `OwnedByPlayer` / `OwnedByOther` / `Landmark` → `_tileInfoModal.Open(tile)`
   - `VisitorTrackingController`'s existing subscription to the same event is untouched — visits are still recorded for `OwnedByOther` tiles alongside the new modal opening.
3. `LandPurchaseModal`'s Confirm button publishes `TilePurchaseConfirmedEvent`.
4. `TilePurchaseHandler` switches its subscription from `TileSelectedEvent` to `TilePurchaseConfirmedEvent` (this is what kills the auto-buy). Body is otherwise the same server call + `TileData`/colorizer/registry mutation as today, plus:
   - A guard: if `tile.State != Available` when the confirmed event arrives (tile was bought by someone else between modal-open and confirm-click), short-circuits to a failure without calling the server.
   - Publishes `TilePurchaseCompletedEvent` on both the success and failure exit paths.
5. `TileInfoModal`'s Sell button (visible only when `tile.State == OwnedByPlayer`) publishes the existing `TileSellRequestedEvent` — `LandSaleHandler` already listens for this, no trigger-side change needed.
6. `LandSaleHandler` gets one addition: publishes `TileSaleCompletedEvent` on its existing success and failure exit paths.
7. Both modals subscribe to their respective `*CompletedEvent` in `OnEnable`/unsubscribe in `OnDisable` (mirrors their `SetActive` toggling), and ignore completion events for a tile other than the one currently open (guards a stale/late event if the modal was reopened for a different tile in the meantime).

## Components

### `UI/LandPurchaseModal.cs`

Same shape as `EmailVerificationModal`: `[SerializeField]` UI refs (price text, balance text, status text, Confirm/Cancel buttons), `[Inject] Wallet`, `PlanetDefinition`, `EconomyConfig`.

- `Open(TileData tile)`: computes `price = Round(EconomyConfig.BaseLandPrice * PlanetDefinition.LandPriceMultiplier)` — the same formula `LandPurchaseService` computes server-side, duplicated as a one-liner for display purposes (matches the existing precedent of this formula already appearing independently in both `LandPurchaseService` and `LandSaleService`). Shows price and current `Wallet.Coins`; disables Confirm and shows "Not enough coins" if `!Wallet.CanAfford(price)`.
- Confirm → `SetBusy(true)`, publishes `TilePurchaseConfirmedEvent`.
- On `TilePurchaseCompletedEvent` (matching tile): success → status text + `Close()`; failure → shows `FailureReason` (already a friendly string from `LandPurchaseService`, e.g. "Insufficient coins", "Tile is already owned") and re-enables Confirm.
- Cancel → `Close()`, no event published.

### `UI/TileInfoModal.cs`

Handles all three non-`Available` states in one component.

- `Open(TileData tile)` (async void, following the same async-handler idiom already used for button clicks in this codebase, applied here to `Open` for the first time): always shows this tile's `BuildLevel`/`YieldRate`. Then branches on `tile.State`:
  - `OwnedByPlayer` → title "Your Tile", shows Sell button, no profile fetch.
  - `OwnedByOther` → title "Loading…", awaits `ProfileService.GetProfileAsync(tile.OwnerId)`, then fills in display name, avatar (via `DatabaseRegistry.GetAvatar`, same lookup `HUDController.SetAvatar` uses), level, tiles-owned, and badges (joined as text). No Sell button. Guards against being reopened for a different tile mid-fetch by checking `_currentTile == tile` after the `await` before touching UI. On fetch failure, falls back to "Owned by another player" with no crash.
  - `Landmark` → title "Landmark", no Sell button, no profile fetch.
- Sell button → `SetBusy(true)`, publishes `TileSellRequestedEvent { Tile }`.
- On `TileSaleCompletedEvent` (matching tile): success → `Close()`; failure → shows `FailureReason` and re-enables Sell.

### Wiring

- Both modals registered in `PlanetSceneScope` via `RegisterComponentInHierarchy<...>()`, same as the four existing modals.
- Both added as new `[SerializeField]` refs on `HUDController`, which gains a `TileSelectedEvent` subscription (`Start`/`OnDestroy`, same pattern as its other subscriptions) to route to the correct modal.

## Naming

`LandPurchaseModal` / `TileInfoModal`, matching the existing `*Modal` precedent (`DisplayNameModal`, `AvatarSelectionModal`, `EmailVerificationModal`) rather than the `*Screen`/`*View` suffixes in CLAUDE.md's naming table. This is a deliberate deviation from the doc, chosen for consistency with the four other pop-up dialogs already in `UI/` (all of which already deviate from that same table).

## Error handling

`LandPurchaseResult` and `LandSaleResult` already carry a `Success`/reason shape from the service layer — network errors are caught inside `LandPurchaseService`/`LandSaleService` and turned into a failure result, not an exception. Neither modal needs its own try/catch around the purchase/sale flow. `TileInfoModal` does need a try/catch around `ProfileService.GetProfileAsync` (that call can throw), same pattern as `EmailVerificationModal`.

## Testing / scope boundaries

This codebase unit-tests `Economy/` service classes (e.g. `LandSaleServiceTests.cs`) but not `App/`-layer event handlers (`TilePurchaseHandler`, `LandSaleHandler`, `VisitorTrackingController`) or UI `MonoBehaviour` modals — none of those have tests today. This feature follows that existing boundary rather than introducing new test coverage for the handler/modal layers. Verification is manual in-editor:

- Select an `Available` tile → purchase modal opens, shows price/balance, Confirm buys and closes, Cancel closes without side effects.
- Select an `Available` tile you can't afford → Confirm is disabled with a message.
- Select an owned-by-you tile → info modal opens with Sell button; Sell succeeds and closes, or shows a failure reason.
- Select another player's tile → info modal opens, fetches and displays their profile (name/avatar/level/tiles-owned/badges).
- Select a Landmark tile → info modal opens read-only, no Sell button, no profile fetch.

## Out of scope

- Badge icons (badges shown as plain joined text, not icon graphics).
- Live-updating wallet balance while the purchase modal is open (snapshot taken on `Open`).
- Any change to `VisitorTrackingController` or the visit-tracking/yield-bonus mechanic.
- Extracting the price formula into a shared helper (stays duplicated as a one-liner, matching existing precedent).
