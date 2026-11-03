# Drone Purchase Popup Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Buying a drone or a fleet slot from the Drone Garage asks for confirmation in a popup, then shows the success or the failure reason in the same popup.

**Architecture:** The garage's buy button opens a new `DronePurchaseModal` instead of publishing the purchase request. The modal publishes the existing request event on Confirm; `DroneGarageHandler` answers with a new `DronePurchaseCompletedEvent` (instead of the failure toast) for these two actions. Wording lives in a pure helper (`DronePurchasePrompt`). The popup prefab is built from the GUI pack in a scratch project copy and placed in `Planet.unity` there, then copied back.

**Tech Stack:** Unity 6000.3.12f1, C#, VContainer, UGUI + TextMeshPro, Ultimate Clean GUI Pack (SciFi theme), Unity Test Framework (NUnit), Unity CLI (`unity test`, `unity run`).

**Spec:** `docs/superpowers/specs/2026-10-02-drone-purchase-modal-design.md`

## Global Constraints

- Server-authoritative economy: the popup only publishes the existing `DroneAcquireRequestedEvent` / `DroneSlotUnlockRequestedEvent`; nothing client-side grants drones, slots or coins.
- UI and App talk only through `EventBus` events. Namespaces: `SocialUniverse.UI` (`Scripts/UI/`), `SocialUniverse.App` (`Scripts/App/`), `SocialUniverse.Mining` (`Scripts/Mining/`).
- Popup copy, exactly: drone confirm title `Buy <DisplayName>?`, body `Mines up to Tier <n> asteroids`; slot confirm title `Unlock a fleet slot?`, body `Your fleet grows to <slots + 1> slots`; cost line `Cost <cost> · You have <coins>`; short balance `Not enough coins`; busy `Buying…`; confirm labels `Buy` / `Unlock`; drone success `<DisplayName> joined your fleet!` + `Select it on its card to mine with it.`; slot success `Fleet slot unlocked` + `You now have <slots + 1> slots.`. Numbers are plain integers (no thousands separators), like the rest of the garage.
- Drone and slot purchase failures go to the popup, not the toast. Upgrade and Select failures still raise `ServerActionFailedEvent`.
- One public type per file, except a small companion struct beside its static helper (precedent: `DroneCardPlan.cs`).
- **Do not commit.** The user commits and pushes manually.
- Never hand-edit `.unity` / `.prefab` YAML. Build assets with Unity's own APIs in the scratch copy, and copy `Planet.unity` back only if the project's copy is byte-identical to what was synced.

## Test-run recipe (used by every task)

The open Editor locks the project, so tests run in a scratch copy on a short drive letter, with its own product name so the Editor's PlayerPrefs are untouched.

Paths used below:
- `PROJECT` = `C:/Users/chris/UnityProjects/social-universe`
- `SCRATCHPAD` = `C:/Users/EJ-SHA~1/AppData/Local/Temp/claude/C--Users-chris-UnityProjects-social-universe/8c38ef43-c4fb-4d80-ab6b-db1323ff43aa/scratchpad` (this session's scratchpad; run `unity test` from here so `OUT.xml` lands here)
- `S:/` = the scratch project copy (mapped below)

```powershell
# PowerShell: map and sync (skip subst if S: is already mapped to the scratch copy)
$scratch = "C:\Users\EJ-Shadow-mode\AppData\Local\Temp\claude\C--Users-chris-UnityProjects-social-universe\5f06488f-2fee-4443-9ba2-276731e3ecb4\scratchpad\su"
if (-not (Test-Path S:\)) { subst S: $scratch }
$src = "C:\Users\chris\UnityProjects\social-universe"
foreach ($d in "Assets","Packages","ProjectSettings","ServerCode") { robocopy "$src\$d" "S:\$d" /MIR /NFL /NDL /NJH /NJS /NP /R:1 /W:1 | Out-Null }
```

```bash
# bash: isolate the copy (sed, not PowerShell, so no BOM lands in manifest.json)
sed -i '/com\.coplaydev\.unity-mcp/d' S:/Packages/manifest.json
sed -i 's/^\(\s*productName:\) Social Universe$/\1 Social Universe Tests/' S:/ProjectSettings/ProjectSettings.asset
# run (MODE = EditMode or PlayMode; FILTER = semicolon-separated full fixture names, or omit --filter for the whole suite)
unity test "S:/" --editor-version 6000.3.12f1 --mode MODE --filter "FILTER" --output ./OUT.xml --timeout 3000 --no-banner --format json -- -logFile S:/OUT.log
```

Exit codes: `0` all passed, `8` tests failed (read `OUT.xml`), `6` the run never produced a verdict (read `S:/OUT.log`, e.g. `error CS` lines). Always pass `-logFile`, or the batch Editor overwrites the open Editor's `Editor.log`. When finished: `subst S: /D`.

## File structure

| File | Status | Responsibility |
|---|---|---|
| `Assets/_Project/Scripts/Mining/DroneEvents.cs` | modify | add `DronePurchaseCompletedEvent` |
| `Assets/_Project/Scripts/App/DroneGarageHandler.cs` | modify | purchases publish the completion event instead of the toast |
| `Assets/_Project/Scripts/UI/DronePurchase.cs` | create | what is being bought (drone or slot) |
| `Assets/_Project/Scripts/UI/DronePurchasePrompt.cs` | create | `DronePurchaseScreen` + pure wording/button-state helper |
| `Assets/_Project/Scripts/UI/DronePurchaseModal.cs` (+ hand-written `.meta`) | create | thin popup view |
| `Assets/_Project/Scripts/UI/DroneGarageView.cs` | modify | `_purchaseModal` field; buy button opens the popup |
| `Assets/_Project/Scripts/App/PlanetSceneScope.cs` | modify | register the popup for injection |
| `Assets/Prefabs/UI/DronePurchaseModal.prefab` (+ `.meta`) | create (built in scratch) | popup UI from the GUI pack |
| `Assets/Scenes/Planet.unity` | modify (built in scratch) | popup instance under the garage panel; `_purchaseModal` assigned |
| `Assets/_Project/Tests/EditMode/App/DroneGarageHandlerTests.cs` | modify | success completions |
| `Assets/_Project/Tests/EditMode/App/ServerFailureFeedbackTests.cs` | modify | purchase failures go to the popup, not the toast |
| `Assets/_Project/Tests/EditMode/UI/DronePurchasePromptTests.cs` | create | wording and button state |
| `Assets/_Project/Tests/EditMode/UI/DronePurchaseModalPrefabTests.cs` | create | prefab references wired, saved hidden |
| `Assets/_Project/Tests/PlayMode/DroneGarageFlowTests.cs` | modify | confirm → result flow, rejection flow |
| `PROGRESS.md`, `CLAUDE.md` | modify | status row, test counts |

---

### Task 1: Purchase completion event from the handler

**Files:**
- Modify: `Assets/_Project/Scripts/Mining/DroneEvents.cs`
- Modify: `Assets/_Project/Scripts/App/DroneGarageHandler.cs`
- Test: `Assets/_Project/Tests/EditMode/App/DroneGarageHandlerTests.cs`, `Assets/_Project/Tests/EditMode/App/ServerFailureFeedbackTests.cs`

**Interfaces:**
- Produces: `public class DronePurchaseCompletedEvent { public string DroneId; public bool Success; public string Message; }` in namespace `SocialUniverse.Mining`. `DroneId` is null for a slot; `Message` is the readable failure text, `""` on success. Published by `DroneGarageHandler` for every `DroneAcquireRequestedEvent` and `DroneSlotUnlockRequestedEvent`, synchronously when the service task is already complete.

- [ ] **Step 1: Write the failing tests**

In `DroneGarageHandlerTests.cs`, add `using System.Collections.Generic;` and these tests inside the class:

```csharp
        [Test]
        public void A_successful_drone_purchase_publishes_a_successful_completion()
        {
            EventBus.Clear();
            var done = new List<DronePurchaseCompletedEvent>();
            EventBus.Subscribe<DronePurchaseCompletedEvent>(done.Add);
            var handler = new DroneGarageHandler(new CapturingDroneService());
            handler.Start();

            EventBus.Publish(new DroneAcquireRequestedEvent { DroneId = "extractor" });

            handler.Dispose();
            EventBus.Clear();
            Assert.AreEqual(1, done.Count);
            Assert.IsTrue(done[0].Success);
            Assert.AreEqual("extractor", done[0].DroneId);
            Assert.AreEqual("", done[0].Message);
        }

        [Test]
        public void A_successful_slot_unlock_publishes_a_successful_completion()
        {
            EventBus.Clear();
            var done = new List<DronePurchaseCompletedEvent>();
            EventBus.Subscribe<DronePurchaseCompletedEvent>(done.Add);
            var handler = new DroneGarageHandler(new CapturingDroneService());
            handler.Start();

            EventBus.Publish(new DroneSlotUnlockRequestedEvent());

            handler.Dispose();
            EventBus.Clear();
            Assert.AreEqual(1, done.Count);
            Assert.IsTrue(done[0].Success);
            Assert.IsNull(done[0].DroneId, "A slot purchase has no drone id");
        }
```

In `ServerFailureFeedbackTests.cs`:
1. Update the class comment's second sentence to: `The handlers now publish ServerActionFailedEvent with a player-facing message; drone and slot purchases send theirs to DronePurchaseModal instead (DronePurchaseCompletedEvent).`
2. Give `FailingDroneService` a null-result switch:

```csharp
        private class FailingDroneService : IDroneService
        {
            public string Reason;
            public bool   ReturnNull;
            public Task<DroneActionResult> AcquireDroneAsync(string droneId) => Fail();
            public Task<DroneActionResult> UnlockSlotAsync() => Fail();
            public Task<DroneActionResult> UpgradeAsync(string droneId, DroneStat stat) => Fail();
            public Task<DroneActionResult> SetActiveAsync(string droneId) => Fail();
            private Task<DroneActionResult> Fail() =>
                Task.FromResult(ReturnNull ? null : new DroneActionResult { Success = false, Reason = Reason });
        }
```

3. Capture purchase completions next to the toast messages:

```csharp
        private readonly List<string> _messages = new();
        private readonly List<DronePurchaseCompletedEvent> _purchases = new();
        private void Capture(ServerActionFailedEvent e) => _messages.Add(e.Message);

        [SetUp]
        public void SetUp()
        {
            EventBus.Clear();
            _messages.Clear();
            _purchases.Clear();
            EventBus.Subscribe<ServerActionFailedEvent>(Capture);
            EventBus.Subscribe<DronePurchaseCompletedEvent>(_purchases.Add);
        }
```

4. Replace `A_rejected_drone_purchase_publishes_a_readable_message` and `Every_drone_action_reports_its_failure` with:

```csharp
        [Test]
        public void A_rejected_drone_purchase_sends_a_readable_message_to_the_purchase_popup()
        {
            var handler = new DroneGarageHandler(new FailingDroneService { Reason = "INSUFFICIENT_FUNDS" });
            handler.Start();

            EventBus.Publish(new DroneAcquireRequestedEvent { DroneId = "extractor" });

            handler.Dispose();
            Assert.AreEqual(1, _purchases.Count);
            Assert.IsFalse(_purchases[0].Success);
            Assert.AreEqual("extractor", _purchases[0].DroneId);
            Assert.AreEqual("Not enough coins", _purchases[0].Message);
            CollectionAssert.IsEmpty(_messages, "The popup shows purchase failures, so no toast");
        }

        [Test]
        public void Upgrade_and_select_failures_still_toast_while_purchases_go_to_the_popup()
        {
            var handler = new DroneGarageHandler(new FailingDroneService { Reason = "Network error" });
            handler.Start();

            EventBus.Publish(new DroneAcquireRequestedEvent { DroneId = "extractor" });
            EventBus.Publish(new DroneSlotUnlockRequestedEvent());
            EventBus.Publish(new DroneUpgradeRequestedEvent { DroneId = "scout", Stat = DroneStat.Yield });
            EventBus.Publish(new SetActiveDroneRequestedEvent { DroneId = "scout" });

            handler.Dispose();
            Assert.AreEqual(2, _messages.Count, "upgrade + select toast");
            Assert.AreEqual(2, _purchases.Count, "drone + slot go to the popup");
            Assert.IsTrue(_purchases.TrueForAll(p => !p.Success && p.Message == "Couldn't reach the server. Try again."));
        }

        [Test]
        public void A_purchase_with_no_reply_still_completes_so_the_popup_never_hangs()
        {
            var handler = new DroneGarageHandler(new FailingDroneService { ReturnNull = true });
            handler.Start();

            EventBus.Publish(new DroneSlotUnlockRequestedEvent());

            handler.Dispose();
            Assert.AreEqual(1, _purchases.Count);
            Assert.IsFalse(_purchases[0].Success);
            Assert.AreEqual("Couldn't reach the server. Try again.", _purchases[0].Message);
        }
```

Add the event class so the tests compile (Step 3 fills in the behaviour) — in `DroneEvents.cs`, after `SetActiveDroneRequestedEvent`:

```csharp

    // App -> UI: the result of a drone or slot purchase, for DronePurchaseModal. DroneId is null for
    // a slot. Message is the player-facing failure text (ServerFailureMessages), "" on success.
    public class DronePurchaseCompletedEvent    { public string DroneId; public bool Success; public string Message; }
```

- [ ] **Step 2: Run the tests to verify they fail**

Recipe, `--mode EditMode --filter "SocialUniverse.Tests.DroneGarageHandlerTests;SocialUniverse.Tests.ServerFailureFeedbackTests"`.
Expected: exit 8. The five new/replaced tests fail (no `DronePurchaseCompletedEvent` is published; the rejected purchase still toasts). The existing routing and mineral/mining tests pass.

- [ ] **Step 3: Implement**

In `DroneGarageHandler.cs`, replace the `OnAcquire` and `OnUnlockSlot` lines and add `Complete` below `Report`:

```csharp
        private async void OnAcquire(DroneAcquireRequestedEvent e)   { var r = await _drones.AcquireDroneAsync(e.DroneId); Complete("Purchase", e.DroneId, r); }
        private async void OnUnlockSlot(DroneSlotUnlockRequestedEvent e) { var r = await _drones.UnlockSlotAsync();        Complete("Slot unlock", null, r); }
```

```csharp
        // Drone and slot purchases are confirmed in DronePurchaseModal, which shows the result
        // itself, so they answer it rather than raising the toast. Always publishes, even for a
        // missing reply, so the popup never waits forever.
        private static void Complete(string action, string droneId, DroneActionResult r)
        {
            bool   ok      = r is { Success: true };
            string message = "";
            if (!ok)
            {
                string reason = r?.Reason ?? "Empty response";
                SULog.Warn($"Drone {action} failed: {reason}", SULog.Channel.Economy);
                message = ServerFailureMessages.For(action, reason);
            }
            EventBus.Publish(new DronePurchaseCompletedEvent { DroneId = droneId, Success = ok, Message = message });
        }
```

Update the `Report` comment to: `// Upgrade and set-active failures are surfaced to the player as a toast (Known Issue #17).`

- [ ] **Step 4: Run the tests to verify they pass**

Same command as Step 2. Expected: exit 0, all tests in both fixtures pass.

---

### Task 2: Purchase description and popup wording

**Files:**
- Create: `Assets/_Project/Scripts/UI/DronePurchase.cs`
- Create: `Assets/_Project/Scripts/UI/DronePurchasePrompt.cs`
- Test: `Assets/_Project/Tests/EditMode/UI/DronePurchasePromptTests.cs`

**Interfaces:**
- Consumes: `DroneComparison.TierLine(int tier)` → `"Mines up to Tier {tier} asteroids"`.
- Produces:
  - `public readonly struct DronePurchase` with `string DroneId` (null for a slot), `string DisplayName`, `int Tier`, `Sprite Icon`, `int Cost`, `int CurrentSlots`, `bool IsSlot`; factories `DronePurchase.ForDrone(string droneId, string displayName, int tier, Sprite icon, int cost)` and `DronePurchase.ForSlot(int cost, int currentSlots)`.
  - `public readonly struct DronePurchaseScreen` with `string Title, Body, Cost, Status, ConfirmLabel` and `bool ConfirmEnabled`.
  - `public static class DronePurchasePrompt` with `const string Busy = "Buying…"`, `const string NotEnoughCoins = "Not enough coins"`, `DronePurchaseScreen Confirm(DronePurchase p, int coins)`, `DronePurchaseScreen Success(DronePurchase p)`.

- [ ] **Step 1: Write the failing tests**

```csharp
using NUnit.Framework;
using SocialUniverse.UI;

namespace SocialUniverse.Tests
{
    // The drone purchase popup's wording and button state (DronePurchaseModal shows these as-is).
    public class DronePurchasePromptTests
    {
        private static readonly DronePurchase Extractor = DronePurchase.ForDrone("extractor", "Extractor Drone", 2, null, 600);
        private static readonly DronePurchase ThirdSlot = DronePurchase.ForSlot(500, currentSlots: 2);

        [Test]
        public void Confirming_a_drone_names_it_and_its_tier()
        {
            var s = DronePurchasePrompt.Confirm(Extractor, coins: 1250);

            Assert.AreEqual("Buy Extractor Drone?", s.Title);
            Assert.AreEqual("Mines up to Tier 2 asteroids", s.Body);
            Assert.AreEqual("Buy", s.ConfirmLabel);
        }

        [Test]
        public void Confirming_a_slot_says_how_big_the_fleet_gets()
        {
            var s = DronePurchasePrompt.Confirm(ThirdSlot, coins: 1250);

            Assert.AreEqual("Unlock a fleet slot?", s.Title);
            Assert.AreEqual("Your fleet grows to 3 slots", s.Body);
            Assert.AreEqual("Unlock", s.ConfirmLabel);
        }

        [Test]
        public void Confirm_shows_the_cost_and_the_balance()
        {
            var s = DronePurchasePrompt.Confirm(Extractor, coins: 1250);

            Assert.AreEqual("Cost 600 · You have 1250", s.Cost);
            Assert.AreEqual("", s.Status);
            Assert.IsTrue(s.ConfirmEnabled);
        }

        [Test]
        public void Confirm_is_disabled_with_a_reason_when_the_wallet_is_short()
        {
            var s = DronePurchasePrompt.Confirm(Extractor, coins: 599);

            Assert.IsFalse(s.ConfirmEnabled);
            Assert.AreEqual("Not enough coins", s.Status);
        }

        [Test]
        public void Confirm_is_enabled_when_the_wallet_holds_exactly_the_cost()
        {
            Assert.IsTrue(DronePurchasePrompt.Confirm(Extractor, coins: 600).ConfirmEnabled);
        }

        [Test]
        public void Success_for_a_drone_welcomes_it_to_the_fleet()
        {
            var s = DronePurchasePrompt.Success(Extractor);

            Assert.AreEqual("Extractor Drone joined your fleet!", s.Title);
            Assert.AreEqual("Select it on its card to mine with it.", s.Body);
        }

        [Test]
        public void Success_for_a_slot_gives_the_new_slot_count()
        {
            var s = DronePurchasePrompt.Success(ThirdSlot);

            Assert.AreEqual("Fleet slot unlocked", s.Title);
            Assert.AreEqual("You now have 3 slots.", s.Body);
        }
    }
}
```

Add compile-only stubs so the run fails on assertions, not compilation:

`DronePurchase.cs`:

```csharp
using UnityEngine;

namespace SocialUniverse.UI
{
    public readonly struct DronePurchase
    {
        public readonly string DroneId;
        public readonly string DisplayName;
        public readonly int    Tier;
        public readonly Sprite Icon;
        public readonly int    Cost;
        public readonly int    CurrentSlots;

        public bool IsSlot => DroneId == null;

        public static DronePurchase ForDrone(string droneId, string displayName, int tier, Sprite icon, int cost) => default;
        public static DronePurchase ForSlot(int cost, int currentSlots) => default;
    }
}
```

`DronePurchasePrompt.cs`:

```csharp
namespace SocialUniverse.UI
{
    public readonly struct DronePurchaseScreen
    {
        public readonly string Title, Body, Cost, Status, ConfirmLabel;
        public readonly bool   ConfirmEnabled;
    }

    public static class DronePurchasePrompt
    {
        public const string Busy           = "Buying…";
        public const string NotEnoughCoins = "Not enough coins";

        public static DronePurchaseScreen Confirm(DronePurchase p, int coins) => default;
        public static DronePurchaseScreen Success(DronePurchase p) => default;
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Recipe, `--mode EditMode --filter "SocialUniverse.Tests.DronePurchasePromptTests"`.
Expected: exit 8, all 7 fail with `Expected: "Buy Extractor Drone?" But was: null` (or the equivalent for each test).

- [ ] **Step 3: Implement**

`DronePurchase.cs`:

```csharp
using UnityEngine;

namespace SocialUniverse.UI
{
    // What the garage's buy button is about to purchase: the centered drone, or the next fleet slot
    // (DroneBuyButton decides which). DronePurchaseModal shows it and sends the matching request.
    public readonly struct DronePurchase
    {
        public readonly string DroneId;       // null for a slot
        public readonly string DisplayName;
        public readonly int    Tier;
        public readonly Sprite Icon;
        public readonly int    Cost;
        public readonly int    CurrentSlots;  // fleet slots before a slot purchase

        public bool IsSlot => DroneId == null;

        private DronePurchase(string droneId, string displayName, int tier, Sprite icon, int cost, int currentSlots)
        {
            DroneId = droneId; DisplayName = displayName; Tier = tier; Icon = icon; Cost = cost; CurrentSlots = currentSlots;
        }

        public static DronePurchase ForDrone(string droneId, string displayName, int tier, Sprite icon, int cost) =>
            new DronePurchase(droneId, displayName, tier, icon, cost, 0);

        public static DronePurchase ForSlot(int cost, int currentSlots) =>
            new DronePurchase(null, null, 0, null, cost, currentSlots);
    }
}
```

`DronePurchasePrompt.cs`:

```csharp
namespace SocialUniverse.UI
{
    // One screen of the drone purchase popup: the text to show and whether Confirm can be pressed.
    public readonly struct DronePurchaseScreen
    {
        public readonly string Title, Body, Cost, Status, ConfirmLabel;
        public readonly bool   ConfirmEnabled;

        public DronePurchaseScreen(string title, string body, string cost, string status, string confirmLabel, bool confirmEnabled)
        {
            Title = title; Body = body; Cost = cost; Status = status; ConfirmLabel = confirmLabel; ConfirmEnabled = confirmEnabled;
        }
    }

    // Wording and button state for DronePurchaseModal, kept pure so it can be unit-tested. The
    // server still validates the purchase; "Not enough coins" here only saves a pointless request.
    public static class DronePurchasePrompt
    {
        public const string Busy           = "Buying…";
        public const string NotEnoughCoins = "Not enough coins";

        public static DronePurchaseScreen Confirm(DronePurchase p, int coins)
        {
            bool canAfford = coins >= p.Cost;
            return new DronePurchaseScreen(
                p.IsSlot ? "Unlock a fleet slot?" : $"Buy {p.DisplayName}?",
                p.IsSlot ? $"Your fleet grows to {p.CurrentSlots + 1} slots" : DroneComparison.TierLine(p.Tier),
                $"Cost {p.Cost} · You have {coins}",
                canAfford ? "" : NotEnoughCoins,
                p.IsSlot ? "Unlock" : "Buy",
                canAfford);
        }

        public static DronePurchaseScreen Success(DronePurchase p) => new DronePurchaseScreen(
            p.IsSlot ? "Fleet slot unlocked" : $"{p.DisplayName} joined your fleet!",
            p.IsSlot ? $"You now have {p.CurrentSlots + 1} slots." : "Select it on its card to mine with it.",
            "", "", "", false);
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Same command as Step 2. Expected: exit 0, 7/7 pass.

---

### Task 3: The popup view, prefab and scene placement

**Files:**
- Create: `Assets/_Project/Scripts/UI/DronePurchaseModal.cs` and `Assets/_Project/Scripts/UI/DronePurchaseModal.cs.meta` (hand-written GUID `75923df341654945878c0e8a096f06bc`)
- Modify: `Assets/_Project/Scripts/UI/DroneGarageView.cs` (field only), `Assets/_Project/Scripts/App/PlanetSceneScope.cs`
- Create (in the scratch copy, then copied back): `Assets/Prefabs/UI/DronePurchaseModal.prefab` + `.meta`; modify `Assets/Scenes/Planet.unity`
- Scratch only (never in the project): `BuildDronePurchaseModal.cs` in the session scratchpad
- Test: `Assets/_Project/Tests/EditMode/UI/DronePurchaseModalPrefabTests.cs`; existing `DroneGarageSceneWiringTests` covers the new field

**Interfaces:**
- Consumes: `DronePurchase`, `DronePurchaseScreen`, `DronePurchasePrompt` (Task 2); `DronePurchaseCompletedEvent` (Task 1).
- Produces: `public class DronePurchaseModal : MonoBehaviour` with `public void Open(DronePurchase purchase, int coins)` and serialized fields `_icon` (Image), `_titleText`, `_bodyText`, `_costText`, `_statusText`, `_confirmLabel` (TMP_Text), `_confirmButton`, `_cancelButton`, `_okButton` (Button). `DroneGarageView` gains `[SerializeField] private DronePurchaseModal _purchaseModal;`.

- [ ] **Step 1: Write the failing test**

`DronePurchaseModalPrefabTests.cs`:

```csharp
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using SocialUniverse.UI;

namespace SocialUniverse.Tests
{
    // DronePurchaseModal.prefab was built by a script (2026-10-02). A dropped reference would leave
    // the purchase popup blank or dead without an error, so check every one.
    public class DronePurchaseModalPrefabTests
    {
        private const string PrefabPath = "Assets/Prefabs/UI/DronePurchaseModal.prefab";

        [Test]
        public void Every_DronePurchaseModal_serialized_reference_is_wired()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.IsNotNull(prefab, $"Could not load {PrefabPath}");

            var modal = prefab.GetComponent<DronePurchaseModal>();
            Assert.IsNotNull(modal, "No DronePurchaseModal on the prefab root");

            var prop = new SerializedObject(modal).GetIterator();
            bool enterChildren = true;
            while (prop.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (prop.propertyType != SerializedPropertyType.ObjectReference || prop.name == "m_Script") continue;
                Assert.IsNotNull(prop.objectReferenceValue, $"DronePurchaseModal.{prop.name} is not wired in {PrefabPath}");
            }
        }

        [Test]
        public void The_popup_is_saved_hidden()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.IsNotNull(prefab, $"Could not load {PrefabPath}");
            Assert.IsFalse(prefab.activeSelf, "Open() shows the popup; it must not appear on its own");
        }
    }
}
```

Write `DronePurchaseModal.cs.meta` exactly (two lines, no trailing newline), so the prefab's script reference matches in both the scratch copy and the project:

```
fileFormatVersion: 2
guid: 75923df341654945878c0e8a096f06bc
```

Write `DronePurchaseModal.cs`:

```csharp
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using TMPro;
using SocialUniverse.Config;
using SocialUniverse.Core;
using SocialUniverse.Mining;
using SocialUniverse.Safety;

namespace SocialUniverse.UI
{
    // Confirm → "Buying…" → result popup for the Drone Garage's drone and slot purchases (stat
    // upgrades and Select stay one tap). Opened by DroneGarageView's buy button. Confirm publishes
    // the existing purchase request, and DroneGarageHandler answers with DronePurchaseCompletedEvent.
    // Saved inactive under the garage panel: Awake wires the buttons on first Open, and results are
    // only listened for while the popup is showing. Wording comes from DronePurchasePrompt.
    public class DronePurchaseModal : MonoBehaviour
    {
        [SerializeField] private Image    _icon;
        [SerializeField] private TMP_Text _titleText;
        [SerializeField] private TMP_Text _bodyText;
        [SerializeField] private TMP_Text _costText;
        [SerializeField] private TMP_Text _statusText;
        [SerializeField] private Button   _confirmButton;
        [SerializeField] private TMP_Text _confirmLabel;
        [SerializeField] private Button   _cancelButton;
        [SerializeField] private Button   _okButton;

        [Inject] private IAudioManager _audio;

        private DronePurchase _purchase;
        private bool          _busy;

        private void Awake()
        {
            _confirmButton.onClick.AddListener(OnConfirmClicked);
            _cancelButton.onClick.AddListener(() => Hide(SfxId.Cancel));
            _okButton.onClick.AddListener(() => Hide(SfxId.Confirm));
        }

        private void OnEnable()  => EventBus.Subscribe<DronePurchaseCompletedEvent>(OnCompleted);
        private void OnDisable() => EventBus.Unsubscribe<DronePurchaseCompletedEvent>(OnCompleted);

        public void Open(DronePurchase purchase, int coins)
        {
            _purchase = purchase;
            _busy     = false;

            var screen = DronePurchasePrompt.Confirm(purchase, coins);
            SetTexts(screen);
            _icon.sprite  = purchase.Icon;
            _icon.enabled = purchase.Icon != null;
            _confirmLabel.text = screen.ConfirmLabel;
            ShowButtons(result: false);
            _confirmButton.interactable = screen.ConfirmEnabled;
            _cancelButton.interactable  = true;

            _audio?.PlaySfx(SfxId.OpenPanel);
            gameObject.SetActive(true);
        }

        private void OnConfirmClicked()
        {
            if (_busy) return;
            _audio?.PlaySfx(SfxId.Confirm);
            SetBusy(true);
            _statusText.text = DronePurchasePrompt.Busy;

            if (_purchase.IsSlot) EventBus.Publish(new DroneSlotUnlockRequestedEvent());
            else                  EventBus.Publish(new DroneAcquireRequestedEvent { DroneId = _purchase.DroneId });
        }

        private void OnCompleted(DronePurchaseCompletedEvent e)
        {
            if (!_busy || e.DroneId != _purchase.DroneId) return;
            SetBusy(false);

            if (!e.Success)
            {
                _statusText.text = e.Message; // Confirm and Cancel are live again: retry or leave
                return;
            }

            _audio?.PlaySfx(SfxId.CoinsReward);
            SetTexts(DronePurchasePrompt.Success(_purchase));
            ShowButtons(result: true);
        }

        private void Hide(SfxId sfx)
        {
            if (_busy) return; // the request is in flight; its result must land somewhere
            _audio?.PlaySfx(sfx);
            gameObject.SetActive(false);
        }

        private void SetTexts(DronePurchaseScreen s)
        {
            _titleText.text  = s.Title;
            _bodyText.text   = s.Body;
            _costText.text   = s.Cost;
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
            _busy = busy;
            _confirmButton.interactable = !busy;
            _cancelButton.interactable  = !busy;
        }
    }
}
```

In `DroneGarageView.cs`, add the field after `_closeButton`:

```csharp
        [SerializeField] private DronePurchaseModal _purchaseModal;   // confirm + result popup for drone/slot purchases
```

In `PlanetSceneScope.cs`, after `builder.RegisterComponentInHierarchy<SocialUniverse.UI.DroneGarageView>();`:

```csharp
            builder.RegisterComponentInHierarchy<SocialUniverse.UI.DronePurchaseModal>();
```

- [ ] **Step 2: Run the tests to verify they fail**

Recipe, `--mode EditMode --filter "SocialUniverse.Tests.DronePurchaseModalPrefabTests;SocialUniverse.Tests.DroneGarageSceneWiringTests"`.
Expected: exit 8. Both prefab tests fail with `Could not load Assets/Prefabs/UI/DronePurchaseModal.prefab`; the scene test fails with `DroneGarageView._purchaseModal is not wired`.

- [ ] **Step 3: Write the scratch-only builder**

Save as `BuildDronePurchaseModal.cs` in the session scratchpad (not the project):

```csharp
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using SocialUniverse.UI;

// One-off, scratch-copy only: builds Assets/Prefabs/UI/DronePurchaseModal.prefab from the GUI
// pack's Popup Base and buttons, then places it under the Drone Garage panel in Planet.unity and
// assigns DroneGarageView._purchaseModal. Throws (non-zero exit) on anything unexpected.
public static class BuildDronePurchaseModal
{
    private const string PrefabPath = "Assets/Prefabs/UI/DronePurchaseModal.prefab";
    private const string ScenePath  = "Assets/Scenes/Planet.unity";
    private const string Pack       = "Assets/Plugins/UltimateCleanGUIPack/Themes/SciFi/Prefabs/UI Elements/";
    private const string PopupPath  = Pack + "Popup/Base/Popup Base.prefab";
    private const string BuyPath    = Pack + "Button/Filled (Outline Rollover)/Button - Filled (Outline Rollover) - Purple.prefab";
    private const string CancelPath = Pack + "Button/Filled (Outline Rollover)/Button - Filled (Outline Rollover) - Gray (Blue).prefab";

    public static void Run()
    {
        BuildPrefab();
        PlaceInScene();
    }

    private static void BuildPrefab()
    {
        var root = new GameObject("DronePurchaseModal", typeof(RectTransform));
        Stretch((RectTransform)root.transform);

        // Dim backdrop over the whole garage; it also swallows taps meant for the carousel.
        var backdrop = new GameObject("Backdrop", typeof(RectTransform), typeof(Image));
        backdrop.transform.SetParent(root.transform, false);
        Stretch((RectTransform)backdrop.transform);
        backdrop.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.7f);

        var popup = (GameObject)PrefabUtility.InstantiatePrefab(Load(PopupPath), root.transform);
        popup.name = "Popup";
        var popupRect = (RectTransform)popup.transform;
        popupRect.anchoredPosition = Vector2.zero;
        popupRect.sizeDelta        = new Vector2(900f, 900f);

        var title = Find(popup, "Title").GetComponent<TMP_Text>();
        if (title == null) throw new System.Exception("[BuildDronePurchaseModal] Popup Base 'Title' has no TMP_Text");
        Find(popup, "CloseButton").SetActive(false); // Cancel / OK close this popup
        var bottom = Find(popup, "Bottom").transform;

        var icon = new GameObject("DroneIcon", typeof(RectTransform), typeof(Image));
        Place(icon, bottom, new Vector2(0f, 200f), new Vector2(220f, 220f));
        icon.GetComponent<Image>().preserveAspect = true;
        icon.GetComponent<Image>().raycastTarget  = false;

        var body   = Text("Body",   bottom, title, new Vector2(0f, 40f),   new Vector2(800f, 80f), 40f);
        var cost   = Text("Cost",   bottom, title, new Vector2(0f, -40f),  new Vector2(800f, 70f), 36f);
        var status = Text("Status", bottom, title, new Vector2(0f, -110f), new Vector2(800f, 70f), 36f);
        status.color = new Color(1f, 0.55f, 0.55f);

        var cancel  = Button(CancelPath, "CancelButton",  bottom, new Vector2(-200f, -260f), "Cancel");
        var confirm = Button(BuyPath,    "ConfirmButton", bottom, new Vector2( 200f, -260f), "Buy");
        var ok      = Button(BuyPath,    "OkButton",      bottom, new Vector2(   0f, -260f), "OK");

        var modal = root.AddComponent<DronePurchaseModal>();
        var so = new SerializedObject(modal);
        so.FindProperty("_icon").objectReferenceValue          = icon.GetComponent<Image>();
        so.FindProperty("_titleText").objectReferenceValue     = title;
        so.FindProperty("_bodyText").objectReferenceValue      = body;
        so.FindProperty("_costText").objectReferenceValue      = cost;
        so.FindProperty("_statusText").objectReferenceValue    = status;
        so.FindProperty("_confirmButton").objectReferenceValue = confirm.GetComponent<Button>();
        so.FindProperty("_confirmLabel").objectReferenceValue  = confirm.GetComponentInChildren<TMP_Text>(true);
        so.FindProperty("_cancelButton").objectReferenceValue  = cancel.GetComponent<Button>();
        so.FindProperty("_okButton").objectReferenceValue      = ok.GetComponent<Button>();
        so.ApplyModifiedPropertiesWithoutUndo();

        root.SetActive(false); // Open() shows it
        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool saved);
        Object.DestroyImmediate(root);
        if (!saved) throw new System.Exception("[BuildDronePurchaseModal] could not save " + PrefabPath);
        Debug.Log("[BuildDronePurchaseModal] saved " + PrefabPath);
    }

    private static void PlaceInScene()
    {
        var scene  = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var garage = Object.FindFirstObjectByType<DroneGarageView>(FindObjectsInactive.Include);
        if (garage == null) throw new System.Exception("[BuildDronePurchaseModal] no DroneGarageView in " + ScenePath);

        var so    = new SerializedObject(garage);
        var panel = so.FindProperty("_root").objectReferenceValue as GameObject;
        if (panel == null) throw new System.Exception("[BuildDronePurchaseModal] DroneGarageView._root is not set");

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(Load(PrefabPath), panel.transform);
        instance.transform.SetAsLastSibling(); // drawn above the carousel and the buy button
        instance.SetActive(false);

        so.FindProperty("_purchaseModal").objectReferenceValue = instance.GetComponent<DronePurchaseModal>();
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new System.Exception("[BuildDronePurchaseModal] could not save " + ScenePath);
        Debug.Log($"[BuildDronePurchaseModal] placed under '{panel.name}' and saved {ScenePath}");
    }

    private static GameObject Load(string path)
    {
        var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (go == null) throw new System.Exception("[BuildDronePurchaseModal] missing asset " + path);
        return go;
    }

    private static GameObject Find(GameObject root, string name)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == name) return t.gameObject;
        throw new System.Exception($"[BuildDronePurchaseModal] no '{name}' under {root.name}");
    }

    private static void Stretch(RectTransform r)
    {
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
        r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
    }

    private static void Place(GameObject go, Transform parent, Vector2 pos, Vector2 size)
    {
        go.transform.SetParent(parent, false);
        var r = (RectTransform)go.transform;
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0.5f);
        r.anchoredPosition = pos;
        r.sizeDelta        = size;
    }

    private static TMP_Text Text(string name, Transform parent, TMP_Text style, Vector2 pos, Vector2 size, float fontSize)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        Place(go, parent, pos, size);
        var t = go.GetComponent<TextMeshProUGUI>();
        t.font          = style.font;
        t.fontSize      = fontSize;
        t.color         = style.color;
        t.alignment     = TextAlignmentOptions.Center;
        t.raycastTarget = false;
        t.text          = "";
        return t;
    }

    private static GameObject Button(string prefabPath, string name, Transform parent, Vector2 pos, string label)
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(Load(prefabPath), parent);
        go.name = name;
        var r = (RectTransform)go.transform;
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0.5f);
        r.anchoredPosition = pos;
        r.sizeDelta        = new Vector2(340f, 110f);
        var text = go.GetComponentInChildren<TMP_Text>(true);
        if (text == null) throw new System.Exception($"[BuildDronePurchaseModal] {prefabPath} has no TMP label");
        text.text = label;
        return go;
    }
}
```

- [ ] **Step 4: Build in the scratch copy**

1. Sync with the recipe (this carries the new scripts and the hand-written `.meta`). Before running Unity, back up the copy's scene: `mkdir -p SCRATCHPAD/backup && cp S:/Assets/Scenes/Planet.unity SCRATCHPAD/backup/Planet.unity.orig`.
2. `mkdir -p S:/Assets/ScratchOnlyEditor/Editor && cp SCRATCHPAD/BuildDronePurchaseModal.cs S:/Assets/ScratchOnlyEditor/Editor/`
3. `unity run "S:/" --editor-version 6000.3.12f1 --timeout 2400 --no-banner -- -executeMethod BuildDronePurchaseModal.Run -logFile S:/build-modal.log`
   Expected: exit 0; `S:/build-modal.log` contains `[BuildDronePurchaseModal] saved Assets/Prefabs/UI/DronePurchaseModal.prefab` and `[BuildDronePurchaseModal] placed under 'Panel' and saved Assets/Scenes/Planet.unity`. Pre-existing LunarConsole `NullReferenceException`s at startup are unrelated.
4. `rm -rf S:/Assets/ScratchOnlyEditor S:/Assets/ScratchOnlyEditor.meta`
5. Review the scene diff structurally. Save as `SCRATCHPAD/yamldiff.py` and run `python SCRATCHPAD/yamldiff.py SCRATCHPAD/backup/Planet.unity.orig S:/Assets/Scenes/Planet.unity`:

```python
import difflib, re, sys

def docs(path):
    parts = re.split(r'(?m)^(--- !u!\d+ &-?\d+(?: stripped)?)\n', open(path, encoding='utf-8').read())
    return {re.search(r'&(-?\d+)', parts[i]).group(1): (parts[i], parts[i + 1]) for i in range(1, len(parts), 2)}

old, new = docs(sys.argv[1]), docs(sys.argv[2])
for label, keys, src in (("REMOVED", [k for k in old if k not in new], old), ("ADDED", [k for k in new if k not in old], new)):
    for k in keys:
        name = re.search(r'm_Name: (.*)', src[k][1])
        print(label, src[k][0], '|', name.group(1) if name else '')
for k in old:
    if k in new and old[k] != new[k]:
        print('CHANGED', old[k][0])
        for line in difflib.unified_diff(old[k][1].splitlines(), new[k][1].splitlines(), lineterm='', n=1):
            if not line.startswith(('---', '+++')): print('   ', line)
```

   Expected: no `REMOVED` lines; `ADDED` lines only for the new `PrefabInstance` and its stripped objects; `CHANGED` only the `Panel` RectTransform (one new `m_Children` entry) and the `DroneGarageView` MonoBehaviour (`_purchaseModal` set). If anything else changed, stop and report the extra changes before copying anything back.

- [ ] **Step 5: Run the tests to verify they pass**

Run the Step 2 filter against the scratch copy **without re-syncing** (a sync would overwrite the built assets). Expected: exit 0, 3/3 pass.

- [ ] **Step 6: Copy back to the project**

1. `cmp PROJECT/Assets/Scenes/Planet.unity SCRATCHPAD/backup/Planet.unity.orig` must report identical. If not, stop and ask the user: their Planet.unity changed since the sync.
2. Copy `S:/Assets/Scenes/Planet.unity`, `S:/Assets/Prefabs/UI/DronePurchaseModal.prefab` and `S:/Assets/Prefabs/UI/DronePurchaseModal.prefab.meta` into the same paths under `PROJECT`, then `cmp` each pair.

---

### Task 4: The garage's buy button opens the popup

**Files:**
- Modify: `Assets/_Project/Scripts/UI/DroneGarageView.cs` (`OnBuy`)
- Test: `Assets/_Project/Tests/PlayMode/DroneGarageFlowTests.cs`

**Interfaces:**
- Consumes: `DronePurchaseModal.Open(DronePurchase, int)` (Task 3), `DronePurchase.ForDrone/ForSlot` (Task 2), `DronePurchaseCompletedEvent` via `DroneGarageHandler` (Task 1).
- Note: `FakeBackendClient` parses replies with `JsonUtility`. `DroneActionResult` deserializes like `MiningGrantResult` does in `PlanetSceneFlowTests`; its `Fleet` stays null (not a serializable type), which `DroneService` already handles by skipping the fleet update.

- [ ] **Step 1: Write the failing tests**

In `DroneGarageFlowTests.cs`:
1. Add a field `private FakeBackendClient _backend;` and in `SetUp` replace `PlanetTestRootScope.Backend = new FakeBackendClient();` with:

```csharp
            _backend = new FakeBackendClient();
            PlanetTestRootScope.Backend = _backend;
```

2. Replace the `Field<T>` helper with a general one plus a garage shortcut, and add the carousel helper:

```csharp
        private T Field<T>(string name) where T : class => Private<T>(_garage, name);

        private static T Private<T>(object target, string name) where T : class
        {
            var value = target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(target) as T;
            Assert.IsNotNull(value, $"{target.GetType().Name}.{name} is not set");
            return value;
        }

        // Open the garage and wait until droneId's card is centered and the buy button shows expectedLabel.
        private IEnumerator CenterGarageOn(List<string> owned, string droneId, string expectedLabel)
        {
            _garage.Open();
            var snap  = Field<SimpleScrollSnap>("_scrollSnap");
            var label = Field<TMP_Text>("_buyLabel");

            var cards = DroneCardPlan.Build(owned, _registry.AllDrones.Select(d => d.DroneId).ToList());
            yield return WaitUntil(() => snap.NumberOfPanels == cards.Count, "garage carousel to fill");

            snap.GoToPanel(DroneCardPlan.IndexOf(cards, droneId));
            yield return WaitUntil(() => label.text == expectedLabel, $"buy button to show '{expectedLabel}' for {droneId}");
        }
```

3. Refactor the existing test to use the helper — replace its block from `_garage.Open();` through the second `WaitUntil` with:

```csharp
            yield return CenterGarageOn(owned, target.DroneId, $"Slot {slotCost}");
            var button = Field<Button>("_buyButton");
            var label  = Field<TMP_Text>("_buyLabel");
```

4. Add the two flow tests:

```csharp
        [UnityTest]
        public IEnumerator Buying_a_drone_asks_first_then_shows_the_result()
        {
            var owned  = new List<string> { _registry.AllDrones.First().DroneId };
            _fleet.Apply(Fleet(slots: 2, owned), _registry);
            var target = _registry.AllDrones.First(d => !owned.Contains(d.DroneId));
            _wallet.SetCoins(target.UnlockCost + 100);
            _backend.On("AcquireDrone", _ => "{\"Success\":true,\"Reason\":\"\",\"NewBalance\":100}");

            yield return CenterGarageOn(owned, target.DroneId, $"{target.UnlockCost}");

            Field<Button>("_buyButton").onClick.Invoke();
            var modal = Field<DronePurchaseModal>("_purchaseModal");
            Assert.IsTrue(modal.gameObject.activeInHierarchy, "The buy button opens the purchase popup");
            Assert.AreEqual($"Buy {target.DisplayName}?", Private<TMP_Text>(modal, "_titleText").text);
            Assert.IsFalse(_backend.Calls.Any(c => c.Function == "AcquireDrone"), "Nothing is bought before the player confirms");

            Private<Button>(modal, "_confirmButton").onClick.Invoke();
            yield return null;

            Assert.IsTrue(_backend.Calls.Any(c => c.Function == "AcquireDrone" && (string)c.Args["droneId"] == target.DroneId),
                "Confirm sends the purchase to the server");
            Assert.AreEqual($"{target.DisplayName} joined your fleet!", Private<TMP_Text>(modal, "_titleText").text);
            var ok = Private<Button>(modal, "_okButton");
            Assert.IsTrue(ok.gameObject.activeSelf, "The result screen offers OK");

            ok.onClick.Invoke();
            Assert.IsFalse(modal.gameObject.activeSelf, "OK closes the popup");
        }

        [UnityTest]
        public IEnumerator A_rejected_purchase_shows_the_reason_and_lets_the_player_retry()
        {
            var owned  = new List<string> { _registry.AllDrones.First().DroneId };
            _fleet.Apply(Fleet(slots: 2, owned), _registry);
            var target = _registry.AllDrones.First(d => !owned.Contains(d.DroneId));
            _wallet.SetCoins(target.UnlockCost + 100);
            _backend.On("AcquireDrone", _ => "{\"Success\":false,\"Reason\":\"INSUFFICIENT_FUNDS\"}");

            yield return CenterGarageOn(owned, target.DroneId, $"{target.UnlockCost}");

            Field<Button>("_buyButton").onClick.Invoke();
            var modal   = Field<DronePurchaseModal>("_purchaseModal");
            var confirm = Private<Button>(modal, "_confirmButton");
            confirm.onClick.Invoke();
            yield return null;

            Assert.IsTrue(modal.gameObject.activeSelf, "A failure keeps the popup open");
            Assert.AreEqual("Not enough coins", Private<TMP_Text>(modal, "_statusText").text);
            Assert.IsTrue(confirm.gameObject.activeSelf && confirm.interactable, "Buy stays available to retry");
        }
```

- [ ] **Step 2: Run the tests to verify they fail**

Sync with the recipe (the project now has the built scene and prefab), then `--mode PlayMode --filter "SocialUniverse.Tests.DroneGarageFlowTests"`.
Expected: exit 8. The two new tests fail at `The buy button opens the purchase popup` (the buy button still publishes the request directly); the existing affordability test passes.

- [ ] **Step 3: Implement**

In `DroneGarageView.cs`, replace `OnBuy` (and its comment) with:

```csharp
        // Re-derived at click time so it acts on the card centered now, not when the label was drawn.
        // Drone and slot purchases are confirmed in DronePurchaseModal, which sends the request.
        private void OnBuy()
        {
            var state = CenteredBuyState(out string droneId);
            if (!state.Visible || _purchaseModal == null) return;

            int coins = _wallet?.Coins ?? 0;
            if (state.UnlocksSlot)
            {
                _purchaseModal.Open(DronePurchase.ForSlot(state.Cost, _fleet.UnlockedSlots), coins);
                return;
            }

            var def = _registry.GetDrone(droneId);
            _purchaseModal.Open(DronePurchase.ForDrone(def.DroneId, def.DisplayName, def.Tier, def.Icon, state.Cost), coins);
        }
```

- [ ] **Step 4: Run the tests to verify they pass**

Same command as Step 2 (after copying the changed `DroneGarageView.cs` into the copy, or re-syncing). Expected: exit 0, 3/3 pass.

---

### Task 5: Full verification and docs

**Files:**
- Modify: `PROGRESS.md` (the `DroneGarageView` row in the M6 Client table), `CLAUDE.md` (two test-count lines)

- [ ] **Step 1: Run the full suites**

Sync with the recipe, then run the recipe twice without `--filter`: `--mode EditMode` and `--mode PlayMode`.
Expected: both exit 0. EditMode was 416 before this plan; Tasks 1–3 add 2 + 1 + 7 + 2 tests (the two replaced failure tests keep the count), so expect 428. PlayMode was 3; Task 4 adds 2, so expect 5. Use the counts the runs actually report.

- [ ] **Step 2: Update the docs**

In `PROGRESS.md`, append to the `DroneGarageView` row's notes: `Drone and slot purchases go through DronePurchaseModal (2026-10-02): confirm → "Buying…" → success screen or the failure reason, in one popup; their failures no longer toast. Tests: DronePurchasePromptTests, DronePurchaseModalPrefabTests, PlayMode DroneGarageFlowTests.`
In `CLAUDE.md`, update `EditMode 416/416, PlayMode 3/3` (Current state) and `EditMode **416/416**; PlayMode **3/3**` (Running Tests) to the Step 1 counts.

- [ ] **Step 3: Clean up**

`subst S: /D`. Leave the scratch copy in place for reuse. Do not commit.
