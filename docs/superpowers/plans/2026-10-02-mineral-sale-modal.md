# Mineral Sale Popup Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Mineral sales (a row's Sell, Sell all) ask for confirmation in a popup and show the result there, using one shared popup (`TransactionModal`) that the Drone Garage also uses.

**Architecture:** `DronePurchaseModal` becomes a passive `TransactionModal` (screens in, `onConfirm` callback out, `ShowSuccess`/`ShowFailure` called by the owner). Each panel owns its transaction: it opens the popup with its prompt helper's screen, publishes its request in `onConfirm`, and answers its result event. `MineralSaleHandler` publishes `MineralSaleCompletedEvent` instead of the toast. A second popup instance is placed under the mineral panel by a scratch-only Unity script.

**Tech Stack:** Unity 6000.3.12f1, C#, VContainer 1.18, UGUI + TextMeshPro, Unity Test Framework, Unity CLI.

**Spec:** `docs/superpowers/specs/2026-10-02-mineral-sale-modal-design.md`

## Global Constraints

- Server-authoritative: the popup only publishes the existing `SellMineralsRequestedEvent` / drone request events. Coin figures in the popup are display estimates (`qty × MineralDefinition.SellValue`).
- Panels and handlers talk only through `EventBus`. Namespaces: `SocialUniverse.UI`, `SocialUniverse.App`, `SocialUniverse.Mining`.
- Mineral copy, exactly: row confirm `Sell <qty> <DisplayName>?` / `<SellValue> coins each` / `You'll get <value> coins` / `Sell`; Sell all confirm `Sell all minerals?` / `<n> minerals` (`1 mineral`) / `You'll get <value> coins` / `Sell all`; busy `Selling…`; success `Sold <qty> <DisplayName>` or `Sold <n> minerals` + `+<value> coins`. Drone copy is unchanged.
- Sale failures go to the popup, not the toast.
- Keep `TransactionModal`'s field names (`_titleText`, `_statusText`, `_confirmButton`, `_okButton`, …) so existing tests and the prefab keep working; only `_costText` becomes `_amountText` with `[FormerlySerializedAs("_costText")]`.
- **Do not commit.** Never hand-edit `.unity`/`.prefab` YAML; copy `Planet.unity` and the prefab back only if the project's copies are byte-identical to the synced ones.

## Test-run recipe

As in `docs/superpowers/plans/2026-10-02-drone-purchase-modal.md` § Test-run recipe, already scripted in `SCRATCHPAD` (`C:/Users/EJ-SHA~1/AppData/Local/Temp/claude/C--Users-chris-UnityProjects-social-universe/8c38ef43-c4fb-4d80-ab6b-db1323ff43aa/scratchpad`):

1. PowerShell: `& "SCRATCHPAD\sync.ps1"` (maps `S:` to the scratch copy if needed and mirrors `Assets`, `Packages`, `ProjectSettings`, `ServerCode`).
2. bash: `bash SCRATCHPAD/isolate.sh` (drops the MCP package, renames the product so PlayerPrefs are separate).
3. bash, from `SCRATCHPAD`: `unity test "S:/" --editor-version 6000.3.12f1 --mode MODE --filter "FILTER" --output ./OUT.xml --timeout 3000 --no-banner --format json -- -logFile S:/OUT.log` (omit `--filter` for a full suite).
4. `python SCRATCHPAD/summary.py SCRATCHPAD/OUT.xml` prints totals and every non-passing test with its message. Exit `0` pass, `8` failures, `6` no verdict (read the log).

`PROJECT` = `C:/Users/chris/UnityProjects/social-universe`. When done: `subst S: /D`.

## File structure

| File | Status | Responsibility |
|---|---|---|
| `Scripts/UI/TransactionModal.cs` (+ `.meta`) | rename from `DronePurchaseModal.cs` | shared passive popup |
| `Scripts/UI/TransactionScreen.cs` | create | shared screen struct (was `DronePurchaseScreen`) |
| `Scripts/UI/DronePurchasePrompt.cs` | modify | returns `TransactionScreen` |
| `Scripts/UI/DroneGarageView.cs` | modify | owns the drone transaction |
| `Scripts/App/PlanetSceneScope.cs` | modify | inject every `TransactionModal` |
| `Scripts/Mining/MineralSaleCompletedEvent.cs` | create | sale result event |
| `Scripts/App/MineralSaleHandler.cs` | modify | publish the result instead of the toast |
| `Scripts/UI/MineralSale.cs`, `Scripts/UI/MineralSalePrompt.cs` | create | what is sold; wording |
| `Scripts/UI/MineralInventoryView.cs` | modify | `_saleModal`; owns the sale transaction |
| `Assets/Prefabs/UI/TransactionModal.prefab` (+ `.meta`) | rename from `DronePurchaseModal.prefab`; re-saved in scratch | popup UI |
| `Assets/Scenes/Planet.unity` | scratch build | second popup under the mineral panel |
| Tests (EditMode): `TransactionModalPrefabTests` (rename), `DronePurchasePromptTests`, `MineralSalePromptTests`, `MineralSaleHandlerTests`, `ServerFailureFeedbackTests`, `MineralInventorySceneWiringTests` | create/modify | |
| Tests (PlayMode): `DroneGarageFlowTests`, `MineralSaleFlowTests` | modify/create | |
| `PROGRESS.md`, `CLAUDE.md` | modify | status, counts |

(`Scripts/` = `Assets/_Project/Scripts/`, tests under `Assets/_Project/Tests/`.)

---

### Task 1: Shared, passive `TransactionModal`

A refactor: drone behaviour is unchanged and its existing tests are the safety net (verified in Run A and, for PlayMode, Run C).

**Files:** rename `Scripts/UI/DronePurchaseModal.cs(.meta)` → `TransactionModal.cs(.meta)`, `Assets/Prefabs/UI/DronePurchaseModal.prefab(.meta)` → `TransactionModal.prefab(.meta)`, `Tests/EditMode/UI/DronePurchaseModalPrefabTests.cs(.meta)` → `TransactionModalPrefabTests.cs(.meta)`; create `Scripts/UI/TransactionScreen.cs`; modify `DronePurchasePrompt.cs`, `DroneGarageView.cs`, `PlanetSceneScope.cs`, comments in `DronePurchase.cs`, `DroneEvents.cs`, `DroneGarageHandler.cs`, `ServerFailureFeedbackTests.cs`, `DronePurchasePromptTests.cs`; `DroneGarageFlowTests.cs`.

**Interfaces — produces:**
- `public readonly struct TransactionScreen { string Title, Body, Amount, Status, ConfirmLabel; bool ConfirmEnabled; ctor(title, body, amount, status, confirmLabel, confirmEnabled) }`
- `public class TransactionModal : MonoBehaviour` with `public bool IsBusy { get; }`, `public void Open(TransactionScreen confirm, Sprite icon, string busyText, Action onConfirm)`, `public void ShowSuccess(TransactionScreen result)`, `public void ShowFailure(string message)`.

- [ ] **Step 1: Rename the files** (bash; `.meta` files move with their assets so GUIDs are kept)

```bash
P=C:/Users/chris/UnityProjects/social-universe
cd $P/Assets/_Project/Scripts/UI && mv DronePurchaseModal.cs TransactionModal.cs && mv DronePurchaseModal.cs.meta TransactionModal.cs.meta
cd $P/Assets/Prefabs/UI && mv DronePurchaseModal.prefab TransactionModal.prefab && mv DronePurchaseModal.prefab.meta TransactionModal.prefab.meta
cd $P/Assets/_Project/Tests/EditMode/UI && mv DronePurchaseModalPrefabTests.cs TransactionModalPrefabTests.cs && { [ -e DronePurchaseModalPrefabTests.cs.meta ] && mv DronePurchaseModalPrefabTests.cs.meta TransactionModalPrefabTests.cs.meta || true; }
grep -c 75923df341654945878c0e8a096f06bc $P/Assets/_Project/Scripts/UI/TransactionModal.cs.meta   # 1
```

- [ ] **Step 2: Shared screen struct** — `Scripts/UI/TransactionScreen.cs`:

```csharp
namespace SocialUniverse.UI
{
    // One screen of the shared transaction popup (TransactionModal): the text to show and whether
    // Confirm can be pressed. Built by a panel's prompt helper (DronePurchasePrompt, MineralSalePrompt).
    // Amount is the money line: a price for a purchase, the proceeds for a sale.
    public readonly struct TransactionScreen
    {
        public readonly string Title, Body, Amount, Status, ConfirmLabel;
        public readonly bool   ConfirmEnabled;

        public TransactionScreen(string title, string body, string amount, string status, string confirmLabel, bool confirmEnabled)
        {
            Title = title; Body = body; Amount = amount; Status = status; ConfirmLabel = confirmLabel; ConfirmEnabled = confirmEnabled;
        }
    }
}
```

Replace `Scripts/UI/DronePurchasePrompt.cs` with:

```csharp
namespace SocialUniverse.UI
{
    // Wording and button state for the Drone Garage's purchase popup (TransactionModal), kept pure
    // so it can be unit-tested. The server still validates the purchase; "Not enough coins" here
    // only saves a pointless request.
    public static class DronePurchasePrompt
    {
        public const string Busy           = "Buying…";
        public const string NotEnoughCoins = "Not enough coins";

        public static TransactionScreen Confirm(DronePurchase p, int coins)
        {
            bool canAfford = coins >= p.Cost;
            return new TransactionScreen(
                p.IsSlot ? "Unlock a fleet slot?" : $"Buy {p.DisplayName}?",
                p.IsSlot ? $"Your fleet grows to {p.CurrentSlots + 1} slots" : DroneComparison.TierLine(p.Tier),
                $"Cost {p.Cost} · You have {coins}",
                canAfford ? "" : NotEnoughCoins,
                p.IsSlot ? "Unlock" : "Buy",
                canAfford);
        }

        public static TransactionScreen Success(DronePurchase p) => new TransactionScreen(
            p.IsSlot ? "Fleet slot unlocked" : $"{p.DisplayName} joined your fleet!",
            p.IsSlot ? $"You now have {p.CurrentSlots + 1} slots." : "Select it on its card to mine with it.",
            "", "", "", false);
    }
}
```

- [ ] **Step 3: The passive popup** — replace `Scripts/UI/TransactionModal.cs` with:

```csharp
using System;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using VContainer;
using TMPro;
using SocialUniverse.Config;
using SocialUniverse.Safety;

namespace SocialUniverse.UI
{
    // Shared confirm → busy → result popup for coin transactions: drone and slot purchases in the
    // Drone Garage, mineral sales in the Mineral Inventory. A passive view: the owning panel supplies
    // each screen (from its prompt helper), sends its request in onConfirm, and calls ShowSuccess /
    // ShowFailure when its result event arrives. Saved inactive under its panel: Awake wires the
    // buttons on first Open. One instance per panel; PlanetSceneScope injects each.
    public class TransactionModal : MonoBehaviour
    {
        [SerializeField] private Image    _icon;
        [SerializeField] private TMP_Text _titleText;
        [SerializeField] private TMP_Text _bodyText;
        [FormerlySerializedAs("_costText")]
        [SerializeField] private TMP_Text _amountText;
        [SerializeField] private TMP_Text _statusText;
        [SerializeField] private Button   _confirmButton;
        [SerializeField] private TMP_Text _confirmLabel;
        [SerializeField] private Button   _cancelButton;
        [SerializeField] private Button   _okButton;

        [Inject] private IAudioManager _audio;

        private Action _onConfirm;
        private string _busyText;

        // True from Confirm until the owner reports the result; the popup can't be closed meanwhile.
        public bool IsBusy { get; private set; }

        private void Awake()
        {
            _confirmButton.onClick.AddListener(OnConfirmClicked);
            _cancelButton.onClick.AddListener(() => Hide(SfxId.Cancel));
            _okButton.onClick.AddListener(() => Hide(SfxId.Confirm));
        }

        public void Open(TransactionScreen confirm, Sprite icon, string busyText, Action onConfirm)
        {
            _onConfirm = onConfirm;
            _busyText  = busyText;
            IsBusy     = false;

            SetTexts(confirm);
            _icon.sprite  = icon;
            _icon.enabled = icon != null;
            _confirmLabel.text = confirm.ConfirmLabel;
            ShowButtons(result: false);
            _confirmButton.interactable = confirm.ConfirmEnabled;
            _cancelButton.interactable  = true;

            _audio?.PlaySfx(SfxId.OpenPanel);
            gameObject.SetActive(true);
        }

        public void ShowSuccess(TransactionScreen result)
        {
            SetBusy(false);
            _audio?.PlaySfx(SfxId.CoinsReward);
            SetTexts(result);
            ShowButtons(result: true);
        }

        public void ShowFailure(string message)
        {
            SetBusy(false);
            _statusText.text = message; // Confirm and Cancel are live again: retry or leave
        }

        private void OnConfirmClicked()
        {
            if (IsBusy) return;
            _audio?.PlaySfx(SfxId.Confirm);
            SetBusy(true);
            _statusText.text = _busyText;
            _onConfirm?.Invoke(); // the result may arrive synchronously, inside this call
        }

        private void Hide(SfxId sfx)
        {
            if (IsBusy) return; // the request is in flight; its result must land somewhere
            _audio?.PlaySfx(sfx);
            gameObject.SetActive(false);
        }

        private void SetTexts(TransactionScreen s)
        {
            _titleText.text  = s.Title;
            _bodyText.text   = s.Body;
            _amountText.text = s.Amount;
            _statusText.text = s.Status;
        }

        private void ShowButtons(bool result)
        {
            _confirmButton.gameObject.SetActive(!result);
            _cancelButton.gameObject.SetActive(!result);
            _okButton.gameObject.SetActive(result);
        }

        private void SetBusy(bool busy)
        {
            IsBusy = busy;
            _confirmButton.interactable = !busy;
            _cancelButton.interactable  = !busy;
        }
    }
}
```

- [ ] **Step 4: The garage owns its transaction** — in `DroneGarageView.cs`:
1. Field type: `[SerializeField] private TransactionModal _purchaseModal;   // confirm + result popup for drone/slot purchases`; add `private DronePurchase _pendingPurchase;` beside `_centeredDroneId`.
2. `OnEnable`: add `EventBus.Subscribe<DronePurchaseCompletedEvent>(OnPurchaseCompleted);` after the `DroneFleetChangedEvent` subscription; `OnDisable`: the matching `Unsubscribe`.
3. Replace `OnBuy` (and its comment) with:

```csharp
        // Re-derived at click time so it acts on the card centered now, not when the label was drawn.
        // Drone and slot purchases are confirmed in the popup; its Confirm sends the request.
        private void OnBuy()
        {
            var state = CenteredBuyState(out string droneId);
            if (!state.Visible || _purchaseModal == null) return;

            DronePurchase purchase;
            if (state.UnlocksSlot)
            {
                purchase = DronePurchase.ForSlot(state.Cost, _fleet.UnlockedSlots);
            }
            else
            {
                var def = _registry.GetDrone(droneId);
                purchase = DronePurchase.ForDrone(def.DroneId, def.DisplayName, def.Tier, def.Icon, state.Cost);
            }

            _pendingPurchase = purchase;
            _purchaseModal.Open(DronePurchasePrompt.Confirm(purchase, _wallet?.Coins ?? 0), purchase.Icon,
                DronePurchasePrompt.Busy, () => RequestPurchase(purchase));
        }

        private static void RequestPurchase(DronePurchase purchase)
        {
            if (purchase.IsSlot) EventBus.Publish(new DroneSlotUnlockRequestedEvent());
            else                 EventBus.Publish(new DroneAcquireRequestedEvent { DroneId = purchase.DroneId });
        }

        // The popup is busy only between its Confirm and this result, so any other result is ignored.
        private void OnPurchaseCompleted(DronePurchaseCompletedEvent e)
        {
            if (_purchaseModal == null || !_purchaseModal.IsBusy || e.DroneId != _pendingPurchase.DroneId) return;

            if (e.Success) _purchaseModal.ShowSuccess(DronePurchasePrompt.Success(_pendingPurchase));
            else           _purchaseModal.ShowFailure(e.Message);
        }
```

- [ ] **Step 5: Inject every popup** — in `PlanetSceneScope.cs`, replace `builder.RegisterComponentInHierarchy<SocialUniverse.UI.DronePurchaseModal>();` with:

```csharp
            // One TransactionModal per panel (Drone Garage, Mineral Inventory), so each is injected
            // here: RegisterComponentInHierarchy would inject only the first one it finds.
            builder.RegisterBuildCallback(container =>
            {
                foreach (var root in gameObject.scene.GetRootGameObjects())
                    foreach (var modal in root.GetComponentsInChildren<SocialUniverse.UI.TransactionModal>(true))
                        container.Inject(modal);
            });
```

- [ ] **Step 6: Comments and tests follow the rename**
- `DronePurchase.cs` comment: `DronePurchaseModal shows it and sends the matching request.` → `DroneGarageView shows it in the purchase popup (TransactionModal).`
- `DroneEvents.cs`: `for DronePurchaseModal.` → `for the Drone Garage's purchase popup.`
- `DroneGarageHandler.cs`: `confirmed in DronePurchaseModal, which shows the result` → `confirmed in the garage's purchase popup, which shows the result`
- `ServerFailureFeedbackTests.cs` comment: `send theirs to DronePurchaseModal instead` → `send theirs to their popup instead`
- `DronePurchasePromptTests.cs`: comment `(DronePurchaseModal shows these as-is)` → `(TransactionModal shows these as-is)`; `s.Cost` → `s.Amount`.
- `TransactionModalPrefabTests.cs`: class `TransactionModalPrefabTests`, `PrefabPath = "Assets/Prefabs/UI/TransactionModal.prefab"`, test `Every_TransactionModal_serialized_reference_is_wired`, `GetComponent<TransactionModal>()`, messages `No TransactionModal on the prefab root` / `TransactionModal.{prop.name} is not wired in {PrefabPath}`, comment `// The shared transaction popup prefab (built by a script, 2026-10-02). A dropped reference would leave the drone or mineral popup blank or dead without an error, so check every one.`
- `DroneGarageFlowTests.cs`: both `Field<DronePurchaseModal>("_purchaseModal")` → `Field<TransactionModal>("_purchaseModal")`.

Then `grep -rn "DronePurchaseModal\|DronePurchaseScreen" PROJECT/Assets/_Project` must print nothing.

Verification: Run A (Task 2 Step 2) for EditMode; Run C (Task 3 Step 3) for the drone PlayMode flows.

---

### Task 2: Sale result event, handler and wording

**Files:** create `Scripts/Mining/MineralSaleCompletedEvent.cs`, `Scripts/UI/MineralSale.cs`, `Scripts/UI/MineralSalePrompt.cs`, `Tests/EditMode/UI/MineralSalePromptTests.cs`; modify `Scripts/App/MineralSaleHandler.cs`, `Tests/EditMode/App/MineralSaleHandlerTests.cs`, `Tests/EditMode/App/ServerFailureFeedbackTests.cs`.

**Interfaces — produces:**
- `public class MineralSaleCompletedEvent { public bool Success; public string Message; }` (`SocialUniverse.Mining`), published once per handled `SellMineralsRequestedEvent`.
- `public readonly struct MineralSale { string MineralId (null = all); string DisplayName; Sprite Icon; int Qty; int UnitValue; int Value; bool IsAll; static ForMineral(string mineralId, string displayName, Sprite icon, int qty, int unitValue); static ForAll(int qty, int value) }`
- `public static class MineralSalePrompt { const string Busy = "Selling…"; TransactionScreen Confirm(MineralSale); TransactionScreen Success(MineralSale) }`

- [ ] **Step 1: Write the failing tests and compile-only stubs**

`MineralSalePromptTests.cs`:

```csharp
using NUnit.Framework;
using SocialUniverse.UI;

namespace SocialUniverse.Tests
{
    // The mineral sale popup's wording and button state (TransactionModal shows these as-is).
    public class MineralSalePromptTests
    {
        private static readonly MineralSale TwelveIron = MineralSale.ForMineral("iron", "Iron", null, 12, 30);
        private static readonly MineralSale Everything = MineralSale.ForAll(23, 1250);

        [Test]
        public void Selling_one_mineral_names_the_quantity_and_unit_value()
        {
            var s = MineralSalePrompt.Confirm(TwelveIron);

            Assert.AreEqual("Sell 12 Iron?", s.Title);
            Assert.AreEqual("30 coins each", s.Body);
            Assert.AreEqual("You'll get 360 coins", s.Amount);
            Assert.AreEqual("Sell", s.ConfirmLabel);
            Assert.AreEqual("", s.Status);
            Assert.IsTrue(s.ConfirmEnabled);
        }

        [Test]
        public void Selling_everything_counts_the_units()
        {
            var s = MineralSalePrompt.Confirm(Everything);

            Assert.AreEqual("Sell all minerals?", s.Title);
            Assert.AreEqual("23 minerals", s.Body);
            Assert.AreEqual("You'll get 1250 coins", s.Amount);
            Assert.AreEqual("Sell all", s.ConfirmLabel);
        }

        [Test]
        public void A_single_unit_reads_singular()
        {
            Assert.AreEqual("1 mineral", MineralSalePrompt.Confirm(MineralSale.ForAll(1, 30)).Body);
        }

        [Test]
        public void Nothing_to_sell_disables_confirm()
        {
            Assert.IsFalse(MineralSalePrompt.Confirm(MineralSale.ForAll(0, 0)).ConfirmEnabled);
        }

        [Test]
        public void Success_for_one_mineral_shows_what_sold_and_the_coins()
        {
            var s = MineralSalePrompt.Success(TwelveIron);

            Assert.AreEqual("Sold 12 Iron", s.Title);
            Assert.AreEqual("+360 coins", s.Body);
        }

        [Test]
        public void Success_for_everything_counts_the_units()
        {
            var s = MineralSalePrompt.Success(Everything);

            Assert.AreEqual("Sold 23 minerals", s.Title);
            Assert.AreEqual("+1250 coins", s.Body);
        }
    }
}
```

`MineralSaleHandlerTests.cs`: add `using System.Collections.Generic;` and:

```csharp
        [Test]
        public void A_successful_sale_publishes_a_successful_completion()
        {
            var done = new List<MineralSaleCompletedEvent>();
            EventBus.Subscribe<MineralSaleCompletedEvent>(done.Add);
            var svc = new PendingMineralService();
            var handler = new MineralSaleHandler(svc);
            handler.Start();

            EventBus.Publish(new SellMineralsRequestedEvent { MineralId = "iron", Qty = 12 });
            svc.Pending.SetResult(new SellResult { Success = true });

            handler.Dispose();
            Assert.AreEqual(1, done.Count);
            Assert.IsTrue(done[0].Success);
            Assert.AreEqual("", done[0].Message);
        }
```

`ServerFailureFeedbackTests.cs`:
1. `FailingMineralService` gains `public bool ReturnNull;` and `private Task<SellResult> Fail() => Task.FromResult(ReturnNull ? null : new SellResult { Success = false, Reason = Reason });`
2. Add `private readonly List<MineralSaleCompletedEvent> _sales = new();`, clear it in `SetUp`, and `EventBus.Subscribe<MineralSaleCompletedEvent>(_sales.Add);`.
3. Class comment's last line becomes: `// drone/slot purchases and mineral sales send theirs to their popup instead (DronePurchaseCompletedEvent, MineralSaleCompletedEvent).`
4. Replace `A_rejected_mineral_sale_publishes_a_readable_message` with:

```csharp
        [Test]
        public void A_rejected_mineral_sale_sends_a_readable_message_to_the_sale_popup()
        {
            var handler = new MineralSaleHandler(new FailingMineralService { Reason = "INSUFFICIENT_QTY" });
            handler.Start();

            EventBus.Publish(new SellMineralsRequestedEvent { All = true });

            handler.Dispose();
            Assert.AreEqual(1, _sales.Count);
            Assert.IsFalse(_sales[0].Success);
            Assert.AreEqual("Not enough minerals to sell", _sales[0].Message);
            CollectionAssert.IsEmpty(_messages, "The popup shows sale failures, so no toast");
        }

        [Test]
        public void A_sale_with_no_reply_still_completes_so_the_popup_never_hangs()
        {
            var handler = new MineralSaleHandler(new FailingMineralService { ReturnNull = true });
            handler.Start();

            EventBus.Publish(new SellMineralsRequestedEvent { MineralId = "iron", Qty = 1 });

            handler.Dispose();
            Assert.AreEqual(1, _sales.Count);
            Assert.IsFalse(_sales[0].Success);
            Assert.AreEqual("Couldn't reach the server. Try again.", _sales[0].Message);
        }
```

`Scripts/Mining/MineralSaleCompletedEvent.cs` (final form; data only):

```csharp
namespace SocialUniverse.Mining
{
    // App -> UI: the result of a mineral sale, for the Mineral Inventory's sale popup. Message is the
    // player-facing failure text (ServerFailureMessages), "" on success.
    public class MineralSaleCompletedEvent
    {
        public bool   Success;
        public string Message;
    }
}
```

Stubs — `Scripts/UI/MineralSale.cs`:

```csharp
using UnityEngine;

namespace SocialUniverse.UI
{
    public readonly struct MineralSale
    {
        public readonly string MineralId;
        public readonly string DisplayName;
        public readonly Sprite Icon;
        public readonly int    Qty;
        public readonly int    UnitValue;
        public readonly int    Value;

        public bool IsAll => MineralId == null;

        public static MineralSale ForMineral(string mineralId, string displayName, Sprite icon, int qty, int unitValue) => default;
        public static MineralSale ForAll(int qty, int value) => default;
    }
}
```

`Scripts/UI/MineralSalePrompt.cs`:

```csharp
namespace SocialUniverse.UI
{
    public static class MineralSalePrompt
    {
        public const string Busy = "Selling…";

        public static TransactionScreen Confirm(MineralSale s) => default;
        public static TransactionScreen Success(MineralSale s) => default;
    }
}
```

- [ ] **Step 2: Run A — Task 1 stays green, Task 2 fails**

Sync, isolate, `--mode EditMode --filter "SocialUniverse.Tests.TransactionModalPrefabTests;SocialUniverse.Tests.DronePurchasePromptTests;SocialUniverse.Tests.DroneGarageSceneWiringTests;SocialUniverse.Tests.DroneGarageHandlerTests;SocialUniverse.Tests.MineralSalePromptTests;SocialUniverse.Tests.MineralSaleHandlerTests;SocialUniverse.Tests.ServerFailureFeedbackTests"`.
Expected: exit 8. The Task 1 fixtures (`TransactionModalPrefabTests`, `DronePurchasePromptTests`, `DroneGarageSceneWiringTests`, `DroneGarageHandlerTests`) all pass. Failing: the 6 `MineralSalePromptTests` (`But was: null`), `A_successful_sale_publishes_a_successful_completion` and the two new `ServerFailureFeedbackTests` (`Expected: 1 But was: 0`). The existing drone tests in `ServerFailureFeedbackTests` pass.

- [ ] **Step 3: Implement**

`MineralSale.cs`:

```csharp
using UnityEngine;

namespace SocialUniverse.UI
{
    // What the Mineral Inventory is about to sell: every unit of one mineral (a row's Sell) or
    // everything held (Sell all). Values are the client's SellValue estimate; the server pays.
    public readonly struct MineralSale
    {
        public readonly string MineralId;    // null for Sell all
        public readonly string DisplayName;
        public readonly Sprite Icon;
        public readonly int    Qty;          // units; for Sell all, every unit held
        public readonly int    UnitValue;    // coins each; 0 for Sell all
        public readonly int    Value;        // coins expected in total

        public bool IsAll => MineralId == null;

        private MineralSale(string mineralId, string displayName, Sprite icon, int qty, int unitValue, int value)
        {
            MineralId = mineralId; DisplayName = displayName; Icon = icon; Qty = qty; UnitValue = unitValue; Value = value;
        }

        public static MineralSale ForMineral(string mineralId, string displayName, Sprite icon, int qty, int unitValue) =>
            new MineralSale(mineralId, displayName, icon, qty, unitValue, qty * unitValue);

        public static MineralSale ForAll(int qty, int value) =>
            new MineralSale(null, null, null, qty, 0, value);
    }
}
```

`MineralSalePrompt.cs`:

```csharp
namespace SocialUniverse.UI
{
    // Wording and button state for the Mineral Inventory's sale popup (TransactionModal), kept pure
    // so it can be unit-tested. The coin figures are estimates; the server decides the payout.
    public static class MineralSalePrompt
    {
        public const string Busy = "Selling…";

        public static TransactionScreen Confirm(MineralSale s) => new TransactionScreen(
            s.IsAll ? "Sell all minerals?" : $"Sell {s.Qty} {s.DisplayName}?",
            s.IsAll ? Units(s.Qty) : $"{s.UnitValue} coins each",
            $"You'll get {s.Value} coins",
            "",
            s.IsAll ? "Sell all" : "Sell",
            s.Qty > 0);

        public static TransactionScreen Success(MineralSale s) => new TransactionScreen(
            s.IsAll ? $"Sold {Units(s.Qty)}" : $"Sold {s.Qty} {s.DisplayName}",
            $"+{s.Value} coins",
            "", "", "", false);

        private static string Units(int qty) => $"{qty} mineral{(qty == 1 ? "" : "s")}";
    }
}
```

`MineralSaleHandler.cs` — replace everything after the `finally` block in `OnSellRequested` with:

```csharp

            // The sale popup shows the result. On success the service has already fired the wallet
            // and MineralInventoryChangedEvent updates. Always publishes, even for a missing reply,
            // so the popup never waits forever.
            bool   ok      = result is { Success: true };
            string message = "";
            if (!ok)
            {
                string reason = result?.Reason ?? "Empty response";
                SULog.Warn($"Sell minerals failed: {reason}", SULog.Channel.Economy);
                message = ServerFailureMessages.For("Sale", reason);
            }
            EventBus.Publish(new MineralSaleCompletedEvent { Success = ok, Message = message });
        }
```

- [ ] **Step 4: Run B** — same command as Step 2. Expected: exit 0, all pass.

---

### Task 3: A popup instance for the mineral panel

**Files:** modify `Scripts/UI/MineralInventoryView.cs` (field only); create `Tests/EditMode/UI/MineralInventorySceneWiringTests.cs`, `Tests/PlayMode/MineralSaleFlowTests.cs`; scratch-only `SCRATCHPAD/BuildMineralSaleModal.cs`; scratch-built `Assets/Prefabs/UI/TransactionModal.prefab`, `Assets/Scenes/Planet.unity`.

**Interfaces — produces:** `MineralInventoryView` field `[SerializeField] private TransactionModal _saleModal;`.

- [ ] **Step 1: Write the failing tests**

`MineralInventorySceneWiringTests.cs`:

```csharp
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using SocialUniverse.UI;

namespace SocialUniverse.Tests
{
    // MineralInventoryView's sale popup (_saleModal) was placed in the Planet scene by a script
    // (2026-10-02). A dropped reference leaves Sell dead with no error, so open the scene and check.
    public class MineralInventorySceneWiringTests
    {
        private const string ScenePath = "Assets/Scenes/Planet.unity";

        private SceneSetup[] _originalSetup;

        [SetUp]
        public void SetUp() => _originalSetup = EditorSceneManager.GetSceneManagerSetup();

        [TearDown]
        public void TearDown()
        {
            if (_originalSetup != null && _originalSetup.Length > 0)
                EditorSceneManager.RestoreSceneManagerSetup(_originalSetup);
        }

        [Test]
        public void Every_MineralInventoryView_serialized_reference_is_wired()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Assert.IsTrue(scene.IsValid(), $"Could not open {ScenePath}");

            var view = Object.FindFirstObjectByType<MineralInventoryView>(FindObjectsInactive.Include);
            Assert.IsNotNull(view, "No MineralInventoryView component in the Planet scene");

            var prop = new SerializedObject(view).GetIterator();
            bool enterChildren = true;
            while (prop.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (prop.propertyType != SerializedPropertyType.ObjectReference || prop.name == "m_Script") continue;
                Assert.IsNotNull(prop.objectReferenceValue,
                    $"MineralInventoryView.{prop.name} is not wired in {ScenePath} ({{fileID: 0}})");
            }
        }
    }
}
```

`Tests/PlayMode/MineralSaleFlowTests.cs`:

```csharp
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using TMPro;
using SocialUniverse.App;
using SocialUniverse.Config;
using SocialUniverse.Core;
using SocialUniverse.Mining;
using SocialUniverse.Safety;
using SocialUniverse.UI;

namespace SocialUniverse.Tests
{
    // The Mineral Inventory's sale popup in the real Planet scene (parent scope: PlanetTestRootScope,
    // scripted FakeBackendClient), plus a check that every transaction popup gets its audio.
    public class MineralSaleFlowTests
    {
        private const string PlanetScenePath = "Assets/Scenes/Planet.unity";

        private GameObject           _rootScopeObject;
        private FakeBackendClient    _backend;
        private MineralInventoryView _view;
        private DroneGarageView      _garage;
        private MineralInventory     _inventory;
        private DatabaseRegistry     _registry;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _backend = new FakeBackendClient();
            PlanetTestRootScope.Backend = _backend;
            _rootScopeObject = new GameObject("PlanetTestRootScope");
            UnityEngine.Object.DontDestroyOnLoad(_rootScopeObject);
            _rootScopeObject.AddComponent<PlanetTestRootScope>();

            bool sceneReady = false;
            Action<SceneReadyEvent> onReady = _ => sceneReady = true;
            EventBus.Subscribe(onReady);

            yield return SceneManager.LoadSceneAsync(PlanetScenePath, LoadSceneMode.Single);

            var scope = UnityEngine.Object.FindFirstObjectByType<PlanetSceneScope>();
            Assert.IsNotNull(scope?.Container, "PlanetSceneScope container not built");

            _view      = scope.Container.Resolve(typeof(MineralInventoryView)) as MineralInventoryView;
            _garage    = scope.Container.Resolve(typeof(DroneGarageView)) as DroneGarageView;
            _inventory = scope.Container.Resolve(typeof(MineralInventory)) as MineralInventory;
            _registry  = scope.Container.Resolve(typeof(DatabaseRegistry)) as DatabaseRegistry;

            float timeout = Time.realtimeSinceStartup + 10f;
            while (!sceneReady && Time.realtimeSinceStartup < timeout)
                yield return null;
            EventBus.Unsubscribe(onReady);
            Assert.IsTrue(sceneReady, "Planet scene never published SceneReadyEvent");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            PlanetTestRootScope.Backend = null;
            if (_rootScopeObject != null) UnityEngine.Object.Destroy(_rootScopeObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Every_transaction_popup_gets_its_audio()
        {
            Private<IAudioManager>(Private<TransactionModal>(_garage, "_purchaseModal"), "_audio");
            Private<IAudioManager>(Private<TransactionModal>(_view, "_saleModal"), "_audio");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Selling_a_mineral_asks_first_then_shows_the_result()
        {
            var mineral = _registry.AllMinerals.First(m => m != null);
            _inventory.SetAll(new Dictionary<string, int> { { mineral.MineralId, 12 } });
            _backend.On("SellMinerals", _ => "{\"Success\":true,\"Reason\":\"\",\"NewBalance\":5000}");

            _view.Open();
            yield return null;
            RowSellButton(mineral.MineralId).onClick.Invoke();

            var modal = Private<TransactionModal>(_view, "_saleModal");
            Assert.IsTrue(modal.gameObject.activeInHierarchy, "Sell opens the sale popup");
            Assert.AreEqual($"Sell 12 {mineral.DisplayName}?", Private<TMP_Text>(modal, "_titleText").text);
            Assert.IsFalse(_backend.Calls.Any(c => c.Function == "SellMinerals"), "Nothing is sold before the player confirms");

            Private<Button>(modal, "_confirmButton").onClick.Invoke();
            yield return null;

            Assert.IsTrue(_backend.Calls.Any(c => c.Function == "SellMinerals"
                    && (string)c.Args["mineralId"] == mineral.MineralId && (int)c.Args["qty"] == 12),
                "Confirm sells the quantity the popup showed");
            Assert.AreEqual($"Sold 12 {mineral.DisplayName}", Private<TMP_Text>(modal, "_titleText").text);
            var ok = Private<Button>(modal, "_okButton");
            Assert.IsTrue(ok.gameObject.activeSelf, "The result screen offers OK");

            ok.onClick.Invoke();
            Assert.IsFalse(modal.gameObject.activeSelf, "OK closes the popup");
        }

        [UnityTest]
        public IEnumerator A_rejected_sale_shows_the_reason_and_lets_the_player_retry()
        {
            var mineral = _registry.AllMinerals.First(m => m != null);
            _inventory.SetAll(new Dictionary<string, int> { { mineral.MineralId, 12 } });
            _backend.On("SellMinerals", _ => "{\"Success\":false,\"Reason\":\"INSUFFICIENT_QTY\"}");

            _view.Open();
            yield return null;
            RowSellButton(mineral.MineralId).onClick.Invoke();

            var modal   = Private<TransactionModal>(_view, "_saleModal");
            var confirm = Private<Button>(modal, "_confirmButton");
            confirm.onClick.Invoke();
            yield return null;

            Assert.IsTrue(modal.gameObject.activeSelf, "A failure keeps the popup open");
            Assert.AreEqual("Not enough minerals to sell", Private<TMP_Text>(modal, "_statusText").text);
            Assert.IsTrue(confirm.gameObject.activeSelf && confirm.interactable, "Sell stays available to retry");
        }

        // Rows are index-aligned with _mineralIds (registry order).
        private Button RowSellButton(string mineralId)
        {
            var ids  = Private<List<string>>(_view, "_mineralIds");
            var rows = Private<List<MineralRowView>>(_view, "_rows");
            int i = ids.IndexOf(mineralId);
            Assert.GreaterOrEqual(i, 0, $"No row for {mineralId}");
            return Private<Button>(rows[i], "_sellButton");
        }

        private static T Private<T>(object target, string name) where T : class
        {
            var value = target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(target) as T;
            Assert.IsNotNull(value, $"{target.GetType().Name}.{name} is not set");
            return value;
        }
    }
}
```

Add the field to `MineralInventoryView.cs` after `_totalValueLabel`:

```csharp
        [SerializeField] private TransactionModal _saleModal;   // confirm + result popup for sales
```

- [ ] **Step 2: Run C — RED** (sync, isolate; two runs)
- `--mode EditMode --filter "SocialUniverse.Tests.MineralInventorySceneWiringTests"` → exit 8: `MineralInventoryView._saleModal is not wired`.
- `--mode PlayMode --filter "SocialUniverse.Tests.DroneGarageFlowTests;SocialUniverse.Tests.MineralSaleFlowTests"` → exit 8: the 3 `DroneGarageFlowTests` **pass** (Task 1's refactor verified end to end); the 3 `MineralSaleFlowTests` fail with `MineralInventoryView._saleModal is not set`.

- [ ] **Step 3: Build in the scratch copy**

Save as `SCRATCHPAD/BuildMineralSaleModal.cs`:

```csharp
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using SocialUniverse.UI;

// One-off, scratch-copy only: re-saves the shared popup prefab with its root named TransactionModal
// (which also writes _costText under its new name _amountText), then places a second instance under
// the Mineral Inventory panel in Planet.unity and assigns MineralInventoryView._saleModal.
public static class BuildMineralSaleModal
{
    private const string PrefabPath = "Assets/Prefabs/UI/TransactionModal.prefab";
    private const string ScenePath  = "Assets/Scenes/Planet.unity";

    public static void Run()
    {
        RenamePrefabRoot();
        PlaceInScene();
    }

    private static void RenamePrefabRoot()
    {
        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        if (root.GetComponent<TransactionModal>() == null)
            throw new System.Exception("[BuildMineralSaleModal] no TransactionModal on " + PrefabPath);
        root.name = "TransactionModal";
        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool saved);
        PrefabUtility.UnloadPrefabContents(root);
        if (!saved) throw new System.Exception("[BuildMineralSaleModal] could not save " + PrefabPath);
        Debug.Log("[BuildMineralSaleModal] re-saved " + PrefabPath);
    }

    private static void PlaceInScene()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var view  = Object.FindFirstObjectByType<MineralInventoryView>(FindObjectsInactive.Include);
        if (view == null) throw new System.Exception("[BuildMineralSaleModal] no MineralInventoryView in " + ScenePath);

        var so    = new SerializedObject(view);
        var panel = so.FindProperty("_root").objectReferenceValue as GameObject;
        if (panel == null) throw new System.Exception("[BuildMineralSaleModal] MineralInventoryView._root is not set");

        var prefab   = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, panel.transform);
        instance.transform.SetAsLastSibling(); // drawn above the mineral list and its buttons
        instance.SetActive(false);

        so.FindProperty("_saleModal").objectReferenceValue = instance.GetComponent<TransactionModal>();
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new System.Exception("[BuildMineralSaleModal] could not save " + ScenePath);
        Debug.Log($"[BuildMineralSaleModal] placed under '{panel.name}' and saved {ScenePath}");
    }
}
```

Then (scratch copy is still synced from Run C):
1. `mkdir -p SCRATCHPAD/backup && cp S:/Assets/Scenes/Planet.unity SCRATCHPAD/backup/Planet.unity.pre-mineral && cp S:/Assets/Prefabs/UI/TransactionModal.prefab SCRATCHPAD/backup/TransactionModal.prefab.pre-mineral`
2. `mkdir -p S:/Assets/ScratchOnlyEditor/Editor && cp SCRATCHPAD/BuildMineralSaleModal.cs S:/Assets/ScratchOnlyEditor/Editor/`
3. From `SCRATCHPAD`: `unity run "S:/" --editor-version 6000.3.12f1 --timeout 2400 --no-banner -- -executeMethod BuildMineralSaleModal.Run -logFile S:/build-mineral.log` → exit 0, log has `[BuildMineralSaleModal] re-saved …` and `[BuildMineralSaleModal] placed under 'Panel' and saved …`. (LunarConsole startup `NullReferenceException` is unrelated.)
4. `rm -rf S:/Assets/ScratchOnlyEditor S:/Assets/ScratchOnlyEditor.meta`
5. Review: `python SCRATCHPAD/yamldiff.py SCRATCHPAD/backup/Planet.unity.pre-mineral S:/Assets/Scenes/Planet.unity` and the same for the prefab. Expected scene changes: ADDED the new `PrefabInstance` and its stripped objects; CHANGED the mineral `Panel` RectTransform (one new child) and the `MineralInventoryView` MonoBehaviour (`_saleModal` set); and, already approved as harmless, Hexasphere's `WireFrame`/`ShadedFrame` objects regenerated with new IDs (identical otherwise). Expected prefab changes: root `m_Name` → `TransactionModal`, `_costText` → `_amountText`. Anything else: stop and report.

- [ ] **Step 4: Run D — Task 3 GREEN, Task 4 RED** (no sync: it would overwrite the build)
- EditMode `MineralInventorySceneWiringTests` → exit 0.
- PlayMode `MineralSaleFlowTests` → exit 8: `Every_transaction_popup_gets_its_audio` passes; the two flow tests fail at `Sell opens the sale popup` (the panel still sells directly).

- [ ] **Step 5: Copy back**

`cmp` the project's `Planet.unity` and `TransactionModal.prefab` against the two `.pre-mineral` backups; both must be identical, else stop and ask. Then copy `S:/Assets/Scenes/Planet.unity` and `S:/Assets/Prefabs/UI/TransactionModal.prefab` into `PROJECT` and `cmp` each pair.

---

### Task 4: The mineral panel opens the popup

**Files:** modify `Scripts/UI/MineralInventoryView.cs`.

- [ ] **Step 1: Implement** (Run D was this task's RED)

1. Class comment's first sentence becomes: `// Mineral inventory panel: one row per mineral in the game — held or not — showing the owned count, plus a Sell-all button. Sales are confirmed in the shared TransactionModal popup. Opened from the HUD. Re-binds on MineralInventoryChangedEvent.`
2. Add `private MineralSale _pendingSale;` after `_mineralIds`.
3. `OnEnable`: add `EventBus.Subscribe<MineralSaleCompletedEvent>(OnSaleCompleted);` after the inventory subscription; `OnDisable`: the matching `Unsubscribe`.
4. Replace `OnSellAll`:

```csharp
        private void OnSellAll() =>
            OfferSale(MineralSale.ForAll(_inventory.TotalOwned(), _inventory.TotalSellValue(_registry)));
```

5. In `BindRows`, replace the `row.Bind(...)` call (and its comment) with:

```csharp
                // Rows outlive a single bind, so the sale is described when the button is pressed
                // rather than captured here.
                row.Bind(def?.Icon, label, qty > 0,
                    () => OfferSale(MineralSale.ForMineral(id, def?.DisplayName ?? id, def?.Icon, _inventory.Get(id),
                        def != null ? def.SellValue : 0)));
```

6. Add after `OnSellAll`:

```csharp
        // Sales are confirmed in the popup; its Confirm sends the quantity it showed.
        private void OfferSale(MineralSale sale)
        {
            if (_saleModal == null) return;

            _pendingSale = sale;
            _saleModal.Open(MineralSalePrompt.Confirm(sale), sale.Icon, MineralSalePrompt.Busy, () => EventBus.Publish(
                sale.IsAll ? new SellMineralsRequestedEvent { All = true }
                           : new SellMineralsRequestedEvent { MineralId = sale.MineralId, Qty = sale.Qty }));
        }

        // The popup is busy only between its Confirm and this result, so any other result is ignored.
        private void OnSaleCompleted(MineralSaleCompletedEvent e)
        {
            if (_saleModal == null || !_saleModal.IsBusy) return;

            if (e.Success) _saleModal.ShowSuccess(MineralSalePrompt.Success(_pendingSale));
            else           _saleModal.ShowFailure(e.Message);
        }
```

- [ ] **Step 2: Verify** — covered by Task 5's full suites (`MineralSaleFlowTests` must pass).

---

### Task 5: Full verification and docs

- [ ] **Step 1: Run E — full suites.** Sync (the project now has the built scene and prefab), isolate, then full `--mode EditMode` and full `--mode PlayMode`. Expected: both exit 0; EditMode 437 (428 + 6 prompt + 1 handler + 1 failure-feedback + 1 scene wiring), PlayMode 8 (5 + 3). Use the counts the runs report.
- [ ] **Step 2: Docs.** `PROGRESS.md`, the `MineralInventoryView + MineralRowView` row's notes: append `Sales (a row's Sell, Sell all) go through the shared TransactionModal popup (2026-10-02): confirm → "Selling…" → "Sold …" or the failure reason; sale failures no longer toast. Tests: MineralSalePromptTests, MineralInventorySceneWiringTests, PlayMode MineralSaleFlowTests.` In the `DroneGarageView` row, `DronePurchaseModal` → `the shared TransactionModal`. `CLAUDE.md`: both test-count lines to the Step 1 counts. Spec status → `implemented 2026-10-02`.
- [ ] **Step 3: Clean up.** `subst S: /D`. Do not commit.
