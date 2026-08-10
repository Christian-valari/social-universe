# LandBuilding Hex-Grid Redesign Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the 8-slot UI plot in the LandBuilding scene with a 19-hexatile board (5 free, 14 purchasable outward), with drag-drop building placement and server-priced tile unlocking.

**Architecture:** A pure `HexBoardMath` utility generates the fixed radius-2 hex board (geometry, canonical spiral indexing, neighbor topology, price formula), mirrored by the server for adjacency/pricing. The shared `land_registry` entry gains an `Unlocked bool[19]` mask alongside the widened `Slots string[19]`. The scene renders a procedural 3D hex board; tap unlocks/removes, drag places/moves; all mutations go through server Cloud Code.

**Tech Stack:** Unity 6 (URP), C#, VContainer DI, NUnit EditMode tests, UGS Cloud Code (Node.js) with Economy 2.5 + Cloud Save 1.4 SDKs.

## Global Constraints

- Namespaces/assemblies per folder: pure math + services in `SocialUniverse.Economy`; DI/handoff/events in `SocialUniverse.Core`; ScriptableObjects in `SocialUniverse.Config`; scene MonoBehaviours in `SocialUniverse.UI`; scene-scope handlers in `SocialUniverse.App`. Server in `ServerCode/` (not in Unity build).
- Server-authoritative economy: client never computes hexatile price or grants unlocks — `PurchaseHexatile` computes price server-side. (Item *placement* cost stays client-supplied — pre-existing debt, out of scope.)
- Board constants are duplicated between client (`HexBoardMath`, `EconomyConfig`) and server (`PurchaseHexatile.js`); each side carries a comment pointing at the other (same convention as the existing `SLOT_COUNT` mirror).
- `land_registry` is Cloud Save custom data keyed by `planet.name.toLowerCase()`, per-tile by `tileId`. Currency ops use `getPlayerCurrencies` (read) and `decrementPlayerCurrencyBalance` with a `configAssignmentHash` from `ConfigurationApi` (write) — never `getPlayerCurrencyBalance`.
- Board defaults: radius 2 (19 cells), free 5, hexatile base price 200, step 100. `buildLevel` = filled count of `Slots`. `MaxBuildLevel` = 19.
- EditMode tests run via Unity Test Runner (EditMode) or MCP `run_tests` (mode=EditMode). The test assembly `SocialUniverse.Tests` references Core/Config/Economy/UI but NOT `SocialUniverse.App` — keep unit-tested logic out of App.
- Every commit message ends with the `Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>` trailer.

## File Structure

**Create:**
- `Assets/_Project/Scripts/Economy/HexBoardMath.cs` — pure board geometry, indexing, neighbors, free-set, layout, price.
- `Assets/_Project/Scripts/UI/PlotHexBoard.cs` — renders the 19 cells + per-cell state; pure `HexCellVisual.Resolve` helper.
- `Assets/_Project/Scripts/UI/PlotBoardInputController.cs` — routes taps/drags to cells.
- `Assets/_Project/Scripts/UI/HexBuildPopup.cs` — shared confirm popup (purchase / remove).
- `ServerCode/PurchaseHexatile.js` — new unlock function.
- Tests: `HexBoardMathTests.cs`, `HexCellVisualTests.cs`, `LandBuildServiceHexTests.cs` (extend existing `LandBuildServiceTests.cs`).

**Modify:**
- `Assets/_Project/Scripts/Config/EconomyConfig.cs` (+ `EconomyConfig.asset`) — hex config, drop `PlotSlotCount`.
- `Assets/_Project/Scripts/Economy/LandRegistryService.cs` — `LandTileEntry.Unlocked`.
- `Assets/_Project/Scripts/Economy/LandBuildService.cs` — hex-keyed args + `PurchaseHexatileAsync`.
- `Assets/_Project/Scripts/Core/LandBuildingHandoff.cs` — carry `Unlocked[]`.
- `Assets/_Project/Scripts/Core/ViewLandRequestedEvent.cs` — carry `Unlocked[]`.
- `Assets/_Project/Scripts/App/ViewLandRequestHandler.cs` — pass `Unlocked[]`.
- `Assets/_Project/Scripts/UI/TileInfoModal.cs` — build+pass `Unlocked[]`, use hex count.
- `Assets/_Project/Scripts/UI/LandBuildingController.cs` — host `PlotHexBoard`; keep Back.
- `Assets/_Project/Scripts/UI/LandBuildPaletteView.cs` — drag-source palette + flows.
- `Assets/_Project/Scripts/App/LandBuildingSceneScope.cs` — register new components/service bits.
- `ServerCode/PlaceBuild.js`, `RemoveBuild.js`, `MoveBuild.js`, `GetLandRegistry.js` — hex index + unlocked.
- `Assets/Scenes/LandBuilding.unity` — replace SlotAnchors with hex board root + popups.
- Tests: `LandBuildingHandoffTests.cs`, `BuildPaletteServiceTests.cs` (PlotSlotCount removal).

---

### Task 1: HexBoardMath — board geometry, indexing, neighbors, price

**Files:**
- Create: `Assets/_Project/Scripts/Economy/HexBoardMath.cs`
- Test: `Assets/_Project/Tests/EditMode/Economy/HexBoardMathTests.cs`

**Interfaces:**
- Produces:
  - `int HexBoardMath.HexCount(int radius)` → `3r²+3r+1`
  - `IReadOnlyList<Vector2Int> HexBoardMath.Cells(int radius)` → axial (q,r) in canonical spiral order (index 0 = center)
  - `int[][] HexBoardMath.Neighbors(int radius)` → adjacent indices per cell
  - `bool HexBoardMath.IsAdjacentToUnlocked(int index, bool[] unlocked, int radius)`
  - `bool[] HexBoardMath.EnsureUnlocked(bool[] src, int radius, int freeCount)` (first `freeCount` true when src null/short)
  - `Vector3[] HexBoardMath.LocalPositions(int radius, float size)` (pointy-top, XZ plane)
  - `int HexBoardMath.HexatilePrice(int unlockedCount, int freeCount, int basePrice, int step)` → `basePrice + step*(unlockedCount-freeCount)`

- [ ] **Step 1: Write the failing test**

```csharp
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using SocialUniverse.Economy;

namespace SocialUniverse.Tests
{
    public class HexBoardMathTests
    {
        [Test] public void HexCount_radius2_is_19() => Assert.AreEqual(19, HexBoardMath.HexCount(2));

        [Test] public void Cells_radius2_has_19_center_first()
        {
            var cells = HexBoardMath.Cells(2);
            Assert.AreEqual(19, cells.Count);
            Assert.AreEqual(new Vector2Int(0, 0), cells[0]);
            Assert.AreEqual(19, cells.Distinct().Count()); // no duplicates
        }

        [Test] public void Free5_is_contiguous_center_cluster()
        {
            var nb = HexBoardMath.Neighbors(2);
            // indices 1..4 are each adjacent to center (0)
            for (int i = 1; i <= 4; i++)
                Assert.Contains(0, nb[i], $"cell {i} should neighbor center");
        }

        [Test] public void Neighbors_are_symmetric()
        {
            var nb = HexBoardMath.Neighbors(2);
            for (int i = 0; i < nb.Length; i++)
                foreach (var j in nb[i])
                    Assert.Contains(i, nb[j], $"{j} lists {i}? symmetry");
        }

        [Test] public void IsAdjacentToUnlocked_true_next_to_free()
        {
            var unlocked = HexBoardMath.EnsureUnlocked(null, 2, 5); // 0..4 true
            // some ring-1 cell not in free set (index 5) must touch an unlocked one
            Assert.IsTrue(HexBoardMath.IsAdjacentToUnlocked(5, unlocked, 2));
        }

        [Test] public void EnsureUnlocked_defaults_free_true_rest_false()
        {
            var u = HexBoardMath.EnsureUnlocked(null, 2, 5);
            Assert.AreEqual(19, u.Length);
            Assert.IsTrue(u.Take(5).All(b => b));
            Assert.IsTrue(u.Skip(5).All(b => !b));
        }

        [Test] public void Price_escalates_linearly()
        {
            Assert.AreEqual(200, HexBoardMath.HexatilePrice(5, 5, 200, 100));   // first buy
            Assert.AreEqual(300, HexBoardMath.HexatilePrice(6, 5, 200, 100));
            Assert.AreEqual(1500, HexBoardMath.HexatilePrice(18, 5, 200, 100)); // 14th buy
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run EditMode tests filtered to `HexBoardMathTests`. Expected: FAIL — `HexBoardMath` does not exist.

- [ ] **Step 3: Write minimal implementation**

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace SocialUniverse.Economy
{
    // Pure geometry + economy for the LandBuilding hex board. MIRRORED by
    // ServerCode/PurchaseHexatile.js (canonical spiral order, neighbors, price) —
    // keep the two in sync. Canonical order: index 0 = center, then ring 1..radius
    // walked as a spiral, so the first (freeCount) indices form a contiguous cluster.
    public static class HexBoardMath
    {
        // Axial neighbor directions (pointy-top). Direction 4 is the ring start corner.
        static readonly Vector2Int[] Dirs =
        {
            new(1, 0), new(1, -1), new(0, -1), new(-1, 0), new(-1, 1), new(0, 1)
        };

        public static int HexCount(int radius) => 3 * radius * radius + 3 * radius + 1;

        public static IReadOnlyList<Vector2Int> Cells(int radius)
        {
            var cells = new List<Vector2Int> { Vector2Int.zero };
            for (int k = 1; k <= radius; k++)
            {
                var hex = Dirs[4] * k;                 // start corner of ring k
                for (int side = 0; side < 6; side++)
                    for (int step = 0; step < k; step++)
                    {
                        cells.Add(hex);
                        hex += Dirs[side];
                    }
            }
            return cells;
        }

        public static int[][] Neighbors(int radius)
        {
            var cells = Cells(radius);
            var index = new Dictionary<Vector2Int, int>();
            for (int i = 0; i < cells.Count; i++) index[cells[i]] = i;

            var result = new int[cells.Count][];
            for (int i = 0; i < cells.Count; i++)
            {
                var list = new List<int>();
                foreach (var d in Dirs)
                    if (index.TryGetValue(cells[i] + d, out var n)) list.Add(n);
                result[i] = list.ToArray();
            }
            return result;
        }

        public static bool IsAdjacentToUnlocked(int index, bool[] unlocked, int radius)
        {
            var nb = Neighbors(radius);
            if (index < 0 || index >= nb.Length) return false;
            foreach (var n in nb[index])
                if (n < unlocked.Length && unlocked[n]) return true;
            return false;
        }

        public static bool[] EnsureUnlocked(bool[] src, int radius, int freeCount)
        {
            int n = HexCount(radius);
            var result = new bool[n];
            if (src != null)
                for (int i = 0; i < n && i < src.Length; i++) result[i] = src[i];
            else
                for (int i = 0; i < freeCount && i < n; i++) result[i] = true;
            return result;
        }

        public static Vector3[] LocalPositions(int radius, float size)
        {
            var cells = Cells(radius);
            var pos = new Vector3[cells.Count];
            for (int i = 0; i < cells.Count; i++)
            {
                float q = cells[i].x, r = cells[i].y;
                float x = size * (Mathf.Sqrt(3f) * q + Mathf.Sqrt(3f) / 2f * r);
                float z = size * (1.5f * r);
                pos[i] = new Vector3(x, 0f, z);
            }
            return pos;
        }

        public static int HexatilePrice(int unlockedCount, int freeCount, int basePrice, int step) =>
            basePrice + step * (unlockedCount - freeCount);
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run EditMode `HexBoardMathTests`. Expected: PASS (all 7).

- [ ] **Step 5: Commit**

```bash
git add Assets/_Project/Scripts/Economy/HexBoardMath.cs Assets/_Project/Scripts/Economy/HexBoardMath.cs.meta Assets/_Project/Tests/EditMode/Economy/HexBoardMathTests.cs Assets/_Project/Tests/EditMode/Economy/HexBoardMathTests.cs.meta
git commit -m "feat(economy): HexBoardMath — hex board geometry, neighbors, price

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

### Task 2: EconomyConfig — hex config, drop PlotSlotCount

**Files:**
- Modify: `Assets/_Project/Scripts/Config/EconomyConfig.cs`
- Modify: `Assets/_Project/ScriptableObjects/EconomyConfig.asset` (set serialized values)
- Modify call sites of `PlotSlotCount`: `Assets/_Project/Scripts/UI/TileInfoModal.cs`, and any tests referencing it.

**Interfaces:**
- Consumes: none.
- Produces: `EconomyConfig.HexBoardRadius`, `.FreeHexCount`, `.HexatileBasePrice`, `.HexatilePriceStep`, `.HexCount`, `.MaxBuildLevel` (=HexCount).

- [ ] **Step 1: Replace the Build header block in `EconomyConfig.cs`**

Replace the `[Header("Build")] ... _plotSlotCount ...` field and its `PlotSlotCount`/`MaxBuildLevel` accessors with:

```csharp
        [Header("Build — Hex Board")]
        [SerializeField] private int _hexBoardRadius    = 2;   // radius-2 hexagon = 19 hexatiles
        [SerializeField] private int _freeHexCount      = 5;   // central hexatiles unlocked for free
        [SerializeField] private int _hexatileBasePrice = 200; // coins for the first purchased tile
        [SerializeField] private int _hexatilePriceStep = 100; // added per already-purchased tile
```

And in the accessors region replace the `PlotSlotCount`/`MaxBuildLevel` lines with:

```csharp
        public int HexBoardRadius    => _hexBoardRadius;
        public int FreeHexCount      => _freeHexCount;
        public int HexatileBasePrice => _hexatileBasePrice;
        public int HexatilePriceStep => _hexatilePriceStep;
        // Mirror of HexBoardMath.HexCount — Config can't reference Economy (cycle), so inline it.
        public int HexCount     => 3 * _hexBoardRadius * _hexBoardRadius + 3 * _hexBoardRadius + 1;
        public int MaxBuildLevel => HexCount;
```

- [ ] **Step 2: Update `TileInfoModal.cs` call site**

In `OnViewLandClicked`, change `_economyConfig.PlotSlotCount` to `_economyConfig.HexCount`:

```csharp
            var slots = LandBuildMath.EnsureSize(entry?.Slots, _economyConfig.HexCount);
```

- [ ] **Step 3: Set values on the EconomyConfig asset**

Via Unity (Inspector or MCP `manage_scriptable_object`): on `Assets/_Project/ScriptableObjects/EconomyConfig.asset` set `_hexBoardRadius=2`, `_freeHexCount=5`, `_hexatileBasePrice=200`, `_hexatilePriceStep=100`. (The serialized `_plotSlotCount` key is now ignored/removed on next save.)

- [ ] **Step 4: Compile & run full EditMode suite**

Run EditMode all. Expected: PASS. If `BuildPaletteServiceTests` or others reference `PlotSlotCount`/`MaxBuildLevel`, update them to `HexCount` (value 19). Fix any compile errors surfaced in the console.

- [ ] **Step 5: Commit**

```bash
git add Assets/_Project/Scripts/Config/EconomyConfig.cs Assets/_Project/ScriptableObjects/EconomyConfig.asset Assets/_Project/Scripts/UI/TileInfoModal.cs
git commit -m "feat(config): hex-board economy config; retire PlotSlotCount

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

### Task 3: LandTileEntry.Unlocked + registry flow + GetLandRegistry.js

**Files:**
- Modify: `Assets/_Project/Scripts/Economy/LandRegistryService.cs` (add `Unlocked` to `LandTileEntry`)
- Modify: `ServerCode/GetLandRegistry.js` (return `unlocked` per tile)
- Test: `Assets/_Project/Tests/EditMode/Economy/LandRegistryServiceTests.cs` (create if absent, else extend)

**Interfaces:**
- Consumes: `LandTileEntry` (existing).
- Produces: `LandTileEntry.Unlocked : bool[]` populated by `RefreshAsync`.

- [ ] **Step 1: Write the failing test**

```csharp
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using SocialUniverse.Core;
using SocialUniverse.Economy;

namespace SocialUniverse.Tests
{
    public class LandRegistryServiceUnlockedTests
    {
        class FakeBackend : IBackendClient
        {
            public LandRegistryData ToReturn;
            public Task<T> CallAsync<T>(string fn, IDictionary<string, object> args) =>
                Task.FromResult((T)(object)ToReturn);
        }

        [Test]
        public async Task RefreshAsync_maps_unlocked_from_response()
        {
            var backend = new FakeBackend
            {
                ToReturn = new LandRegistryData
                {
                    Tiles = new Dictionary<string, LandTileEntry>
                    {
                        ["t1"] = new LandTileEntry { OwnerId = "p", Unlocked = new[] { true, false, true } }
                    }
                }
            };
            var svc = new LandRegistryService(backend);
            await svc.RefreshAsync("Planet_Earth");
            Assert.AreEqual(new[] { true, false, true }, svc.GetEntry("t1").Unlocked);
        }
    }
}
```

> Note: match `IBackendClient.CallAsync`'s real signature — check `Assets/_Project/Scripts/Net/IBackendClient.cs` and mirror the existing fake used in `LandBuildServiceTests.cs`.

- [ ] **Step 2: Run test to verify it fails**

Run EditMode `LandRegistryServiceUnlockedTests`. Expected: FAIL — `LandTileEntry` has no `Unlocked`.

- [ ] **Step 3: Add the field**

In `LandRegistryService.cs`, add to `LandTileEntry`:

```csharp
        public bool[] Unlocked;   // hexIndex -> unlocked. null on legacy entries; caller defaults via HexBoardMath.EnsureUnlocked.
```

- [ ] **Step 4: Run test to verify it passes**

Run EditMode `LandRegistryServiceUnlockedTests`. Expected: PASS.

- [ ] **Step 5: Update `GetLandRegistry.js` to return `unlocked`**

Ensure the per-tile object returned by `GetLandRegistry.js` includes `unlocked: entry.unlocked ?? null` (and `slots: entry.slots ?? null`) alongside existing fields. (Deploy is user-owned.)

- [ ] **Step 6: Commit**

```bash
git add Assets/_Project/Scripts/Economy/LandRegistryService.cs Assets/_Project/Tests/EditMode/Economy/LandRegistryServiceUnlockedTests.cs* ServerCode/GetLandRegistry.js
git commit -m "feat(economy): carry per-hexatile unlocked state through land registry

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

### Task 4: Handoff + event + TileInfoModal carry Unlocked[]

**Files:**
- Modify: `Assets/_Project/Scripts/Core/LandBuildingHandoff.cs`
- Modify: `Assets/_Project/Scripts/Core/ViewLandRequestedEvent.cs`
- Modify: `Assets/_Project/Scripts/App/ViewLandRequestHandler.cs`
- Modify: `Assets/_Project/Scripts/UI/TileInfoModal.cs`
- Test: `Assets/_Project/Tests/EditMode/Core/LandBuildingHandoffTests.cs` (extend)

**Interfaces:**
- Consumes: `HexBoardMath.EnsureUnlocked`, `EconomyConfig.HexBoardRadius/FreeHexCount/HexCount`.
- Produces: `LandBuildingHandoff.Unlocked : bool[]`; `ViewLandRequestedEvent.Unlocked : bool[]`.

- [ ] **Step 1: Extend the handoff test**

In `LandBuildingHandoffTests.cs`, update the `Begin` call to add an `unlocked` argument (after `slots`) and assert it round-trips:

```csharp
            var unlocked = new[] { true, true, false };
            handoff.Begin("12", "earth", "Planet_Earth", "player_a", true, slots, unlocked, 500);
            Assert.AreSame(unlocked, handoff.Unlocked);
```
Also assert `handoff.Unlocked` is null after `Clear()`.

- [ ] **Step 2: Run test to verify it fails**

Run EditMode `LandBuildingHandoffTests`. Expected: FAIL — `Begin` has no `unlocked` param / no `Unlocked` prop.

- [ ] **Step 3: Add `Unlocked` to the handoff**

In `LandBuildingHandoff.cs` add the property and the `Begin`/`Clear` handling:

```csharp
        public bool[]   Unlocked         { get; private set; }
```
Insert `bool[] unlocked` into `Begin(...)` after `string[] slots`, set `Unlocked = unlocked;`, and null it in `Clear()`.

- [ ] **Step 4: Add `Unlocked` to the event and pass it through**

In `ViewLandRequestedEvent.cs` add `public bool[] Unlocked;`.

In `ViewLandRequestHandler.OnViewLandRequested`, pass it through:

```csharp
            _handoff.Begin(e.TileId, _planetState.TargetPlanetId, _planet.name, e.OwnerId, e.CanEdit, e.Slots, e.Unlocked, e.Coins);
```

In `TileInfoModal.OnViewLandClicked`, build the unlocked mask and include it:

```csharp
            var slots    = LandBuildMath.EnsureSize(entry?.Slots, _economyConfig.HexCount);
            var unlocked = HexBoardMath.EnsureUnlocked(entry?.Unlocked, _economyConfig.HexBoardRadius, _economyConfig.FreeHexCount);

            EventBus.Publish(new ViewLandRequestedEvent
            {
                TileId   = _currentTile.TileId,
                OwnerId  = _currentTile.OwnerId,
                CanEdit  = _currentTile.State == TileState.OwnedByPlayer,
                Slots    = slots,
                Unlocked = unlocked,
                Coins    = _wallet.Coins,
            });
```
Add `using SocialUniverse.Economy;` to `TileInfoModal.cs` if not already present.

- [ ] **Step 5: Run tests to verify they pass**

Run EditMode `LandBuildingHandoffTests` then the full suite. Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add Assets/_Project/Scripts/Core/LandBuildingHandoff.cs Assets/_Project/Scripts/Core/ViewLandRequestedEvent.cs Assets/_Project/Scripts/App/ViewLandRequestHandler.cs Assets/_Project/Scripts/UI/TileInfoModal.cs Assets/_Project/Tests/EditMode/Core/LandBuildingHandoffTests.cs
git commit -m "feat(core): carry hexatile unlocked mask into LandBuilding scene

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

### Task 5: LandBuildService — hex-keyed calls + PurchaseHexatileAsync

**Files:**
- Modify: `Assets/_Project/Scripts/Economy/LandBuildService.cs`
- Test: `Assets/_Project/Tests/EditMode/Economy/LandBuildServiceTests.cs` (extend)

**Interfaces:**
- Consumes: `IBackendClient`.
- Produces: `PurchaseHexatileResult { bool Success; string Reason; int NewBalance; int UnlockedCount; }`; `LandBuildService.PurchaseHexatileAsync(tileId, planetId, hexIndex)`. `PlaceAsync`/`RemoveAsync`/`MoveAsync` keep signatures but their index arg now means hexIndex (rename param only).

- [ ] **Step 1: Write the failing test** (mirror the existing fake-backend style in this file)

```csharp
        [Test]
        public async Task PurchaseHexatileAsync_success_returns_balance_and_count()
        {
            var backend = new FakeBackend<PurchaseHexatileResult>(
                new PurchaseHexatileResult { Success = true, NewBalance = 300, UnlockedCount = 6 });
            var svc = new LandBuildService(backend);
            var r = await svc.PurchaseHexatileAsync("t1", "Planet_Earth", 5);
            Assert.IsTrue(r.Success);
            Assert.AreEqual(300, r.NewBalance);
            Assert.AreEqual(6, r.UnlockedCount);
            Assert.AreEqual("PurchaseHexatile", backend.LastFunction);
            Assert.AreEqual(5, backend.LastArgs["hexIndex"]);
        }
```
> Reuse whatever fake-backend helper `LandBuildServiceTests.cs` already defines; adapt names to match.

- [ ] **Step 2: Run test to verify it fails**

Run EditMode `LandBuildServiceTests`. Expected: FAIL — `PurchaseHexatileResult`/`PurchaseHexatileAsync` undefined.

- [ ] **Step 3: Implement**

Add the result type near the others in `LandBuildService.cs`:

```csharp
    public class PurchaseHexatileResult { public bool Success; public string Reason; public int NewBalance = -1; public int UnlockedCount = -1; }
```
Add the method (mirror the try/catch shape of `PlaceAsync`):

```csharp
        public async Task<PurchaseHexatileResult> PurchaseHexatileAsync(string tileId, string planetId, int hexIndex)
        {
            try
            {
                var res = await _backend.CallAsync<PurchaseHexatileResult>("PurchaseHexatile",
                    new Dictionary<string, object>
                    {
                        { "tileId",   tileId   },
                        { "planetId", planetId },
                        { "hexIndex", hexIndex },
                    });
                return res ?? new PurchaseHexatileResult { Success = false, Reason = "No response" };
            }
            catch (Exception ex)
            {
                SULog.Error($"LandBuildService.PurchaseHexatile failed — {ex.Message}", SULog.Channel.Economy);
                return new PurchaseHexatileResult { Success = false, Reason = "Network error" };
            }
        }
```
Rename the `slotIndex` parameters/keys in `PlaceAsync`/`RemoveAsync`/`MoveAsync` to `hexIndex`/`fromHex`/`toHex` (arg dictionary keys become `hexIndex`, `fromHex`, `toHex`). Keep the server function names.

- [ ] **Step 4: Run tests to verify they pass**

Run EditMode `LandBuildServiceTests`. Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add Assets/_Project/Scripts/Economy/LandBuildService.cs Assets/_Project/Tests/EditMode/Economy/LandBuildServiceTests.cs
git commit -m "feat(economy): LandBuildService PurchaseHexatile + hex-keyed build calls

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

### Task 6: Server PurchaseHexatile.js (new)

**Files:**
- Create: `ServerCode/PurchaseHexatile.js`

**Interfaces:**
- Params: `{ tileId, planetId, hexIndex }`. Returns `{ success, reason?, newBalance?, unlockedCount? }`.

- [ ] **Step 1: Write the function** (no automated test — server; verify by manual invocation)

```javascript
// PurchaseHexatile — unlocks hexatile[hexIndex] on an owned tile if it is adjacent
// to an already-unlocked hexatile. Price is computed SERVER-SIDE from the current
// unlocked count (base + step*(unlocked - FREE)); never trusts a client price.
// Board geometry (radius, free count, spiral order, neighbors) MIRRORS
// Assets/_Project/Scripts/Economy/HexBoardMath.cs — keep in sync.
const { CurrenciesApi, ConfigurationApi } = require("@unity-services/economy-2.5");
const { DataApi }                         = require("@unity-services/cloud-save-1.4");

const CURRENCY_ID  = "COINS";
const REGISTRY_KEY = "land_registry";
const RADIUS = 2, FREE_COUNT = 5, BASE_PRICE = 200, PRICE_STEP = 100;
const DIRS = [[1,0],[1,-1],[0,-1],[-1,0],[-1,1],[0,1]];

function cells(radius) {
  const out = [[0,0]];
  for (let k = 1; k <= radius; k++) {
    let hex = [DIRS[4][0]*k, DIRS[4][1]*k];
    for (let side = 0; side < 6; side++)
      for (let step = 0; step < k; step++) { out.push([hex[0], hex[1]]); hex = [hex[0]+DIRS[side][0], hex[1]+DIRS[side][1]]; }
  }
  return out;
}
function neighbors(radius) {
  const c = cells(radius), key = (a) => `${a[0]},${a[1]}`, idx = {};
  c.forEach((a, i) => idx[key(a)] = i);
  return c.map(a => DIRS.map(d => idx[key([a[0]+d[0], a[1]+d[1]])]).filter(n => n !== undefined));
}
function hexCount(radius) { return 3*radius*radius + 3*radius + 1; }
function filledCount(slots) { return Array.isArray(slots) ? slots.filter(s => s !== null && s !== undefined && s !== "").length : 0; }

module.exports = async ({ params, context, logger }) => {
  const { tileId, planetId, hexIndex } = params;
  const N = hexCount(RADIUS);
  if (!tileId || !planetId || !Number.isInteger(hexIndex) || hexIndex < 0 || hexIndex >= N) {
    throw new Error("Invalid params: tileId, planetId, hexIndex required");
  }

  const { projectId, playerId, accessToken } = context;
  const econApi = new CurrenciesApi({ accessToken });
  const config  = new ConfigurationApi({ accessToken });
  const dataApi = new DataApi(context);
  const customId = planetId.toLowerCase();

  try {
    let registry = {};
    try {
      const r = await dataApi.getCustomItems(projectId, customId, [REGISTRY_KEY]);
      const item = r.data.results.find(x => x.key === REGISTRY_KEY);
      if (item?.value) registry = item.value;
    } catch (_) {}

    const entry = registry[tileId];
    if (!entry || entry.ownerId !== playerId) return { success: false, reason: "NOT_OWNER" };

    // Normalize unlocked mask: free indices default true.
    let unlocked = Array.isArray(entry.unlocked) ? entry.unlocked.slice(0, N) : [];
    while (unlocked.length < N) unlocked.push(false);
    if (!Array.isArray(entry.unlocked)) for (let i = 0; i < FREE_COUNT; i++) unlocked[i] = true;

    if (unlocked[hexIndex]) return { success: false, reason: "ALREADY_UNLOCKED" };

    const nb = neighbors(RADIUS);
    const adjacent = nb[hexIndex].some(n => unlocked[n]);
    if (!adjacent) return { success: false, reason: "NOT_ADJACENT" };

    const unlockedCount = unlocked.filter(Boolean).length;
    const price = BASE_PRICE + PRICE_STEP * (unlockedCount - FREE_COUNT);

    const balances = await econApi.getPlayerCurrencies({ projectId, playerId });
    const coins = balances.data.results.find(c => c.currencyId === CURRENCY_ID);
    if ((coins ? coins.balance : 0) < price) return { success: false, reason: "INSUFFICIENT_FUNDS" };

    const cfg = await config.getPlayerConfiguration({ projectId, playerId });
    const configAssignmentHash = cfg.data.metadata.configAssignmentHash;
    const deduct = await econApi.decrementPlayerCurrencyBalance({
      projectId, playerId, currencyId: CURRENCY_ID, configAssignmentHash,
      currencyModifyBalanceRequest: { currencyId: CURRENCY_ID, amount: price }
    });

    unlocked[hexIndex] = true;
    entry.unlocked = unlocked;
    registry[tileId] = entry;
    await dataApi.setCustomItem(projectId, customId, { key: REGISTRY_KEY, value: registry });

    logger.info(`PurchaseHexatile: ${playerId} unlocked hex ${hexIndex} of ${tileId} (${planetId}) for ${price}`);
    return { success: true, newBalance: deduct.data.balance, unlockedCount: unlocked.filter(Boolean).length };
  } catch (err) {
    logger.error("PurchaseHexatile failed", { "error.message": err.message });
    throw err;
  }
};
```

- [ ] **Step 2: Commit**

```bash
git add ServerCode/PurchaseHexatile.js
git commit -m "feat(server): PurchaseHexatile — adjacency-gated, server-priced hexatile unlock

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

> Manual verification (after user deploys): call with an owned tile and a locked-but-adjacent hexIndex → success + newBalance dropped by the computed price; a non-adjacent index → `NOT_ADJACENT`; an already-unlocked index → `ALREADY_UNLOCKED`.

---

### Task 7: Server PlaceBuild/RemoveBuild/MoveBuild — hex index + unlocked checks

**Files:**
- Modify: `ServerCode/PlaceBuild.js`, `ServerCode/RemoveBuild.js`, `ServerCode/MoveBuild.js`

**Interfaces:**
- `PlaceBuild` params: `{ tileId, planetId, hexIndex, itemId, cost }`. `RemoveBuild`: `{ tileId, planetId, hexIndex }`. `MoveBuild`: `{ tileId, planetId, fromHex, toHex }`.

- [ ] **Step 1: PlaceBuild.js — rename slot→hex, widen to 19, add unlocked check**

Change `SLOT_COUNT = 8` to `const HEX_COUNT = 19;` (comment: mirrors HexBoardMath.HexCount(2)). Rename `slotIndex` param to `hexIndex` and validate against `HEX_COUNT`. After the ownership check and before the balance read, normalize `entry.unlocked` (free defaults, same helper as PurchaseHexatile) and add:

```javascript
    if (!Array.isArray(entry.unlocked) || !entry.unlocked[hexIndex]) {
      return { success: false, reason: "TILE_LOCKED" };
    }
```
Ensure `entry.slots` is sized to `HEX_COUNT` (`new Array(HEX_COUNT).fill(null)`). Keep the item `cost` client-supplied (pre-existing debt, noted). Write `entry.slots[hexIndex] = itemId`, recompute `buildLevel = filledCount(entry.slots)`.

- [ ] **Step 2: RemoveBuild.js — rename slot→hex, widen to 19**

Change `SLOT_COUNT`→`HEX_COUNT = 19`, `slotIndex`→`hexIndex`, validate range against 19. Logic otherwise unchanged (clear `slots[hexIndex]`, recompute `buildLevel`).

- [ ] **Step 3: MoveBuild.js — rename slot→hex, widen to 19, require toHex unlocked**

Change `SLOT_COUNT`→`HEX_COUNT = 19`, params to `fromHex`/`toHex`. Keep the existing `fromHex` occupied / `toHex` empty checks and add a `toHex` unlocked check (normalize `entry.unlocked` first):

```javascript
    if (!Array.isArray(entry.unlocked) || !entry.unlocked[toHex]) {
      return { success: false, reason: "TILE_LOCKED" };
    }
```

- [ ] **Step 4: Commit**

```bash
git add ServerCode/PlaceBuild.js ServerCode/RemoveBuild.js ServerCode/MoveBuild.js
git commit -m "feat(server): re-key build ops to hex index; enforce unlocked hexatiles

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

> Manual verification (after deploy): place on unlocked empty hex → success; place on locked hex → `TILE_LOCKED`; move to locked hex → `TILE_LOCKED`.

---

### Task 8: PlotHexBoard — cell rendering + pure visual-state resolver

**Files:**
- Create: `Assets/_Project/Scripts/UI/PlotHexBoard.cs` (MonoBehaviour `PlotHexBoard` + pure static `HexCellVisual`)
- Test: `Assets/_Project/Tests/EditMode/UI/HexCellVisualTests.cs`

**Interfaces:**
- Consumes: `HexBoardMath.LocalPositions`, `DatabaseRegistry.GetItem`, `LandBuildingHandoff`.
- Produces: `enum HexCellVisual.State { Locked, Empty, Occupied }`; `HexCellVisual.Resolve(bool unlocked, string itemId) : State`; `PlotHexBoard.Build(bool[] unlocked, string[] slots)`, `PlotHexBoard.SetCell(int index, bool unlocked, string itemId)`, `PlotHexBoard.CellWorldPosition(int index) : Vector3`, `PlotHexBoard.CellCount : int`.

- [ ] **Step 1: Write the failing test for the pure resolver**

```csharp
using NUnit.Framework;
using SocialUniverse.UI;

namespace SocialUniverse.Tests
{
    public class HexCellVisualTests
    {
        [Test] public void Locked_when_not_unlocked() =>
            Assert.AreEqual(HexCellVisual.State.Locked, HexCellVisual.Resolve(false, null));
        [Test] public void Empty_when_unlocked_no_item() =>
            Assert.AreEqual(HexCellVisual.State.Empty, HexCellVisual.Resolve(true, null));
        [Test] public void Occupied_when_unlocked_with_item() =>
            Assert.AreEqual(HexCellVisual.State.Occupied, HexCellVisual.Resolve(true, "hut"));
        [Test] public void Locked_ignores_item_if_locked() =>
            Assert.AreEqual(HexCellVisual.State.Locked, HexCellVisual.Resolve(false, "hut"));
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run EditMode `HexCellVisualTests`. Expected: FAIL — type missing.

- [ ] **Step 3: Implement `HexCellVisual` + `PlotHexBoard`**

```csharp
using System.Collections.Generic;
using UnityEngine;
using VContainer;
using SocialUniverse.Config;
using SocialUniverse.Core;
using SocialUniverse.Economy;

namespace SocialUniverse.UI
{
    public static class HexCellVisual
    {
        public enum State { Locked, Empty, Occupied }
        public static State Resolve(bool unlocked, string itemId)
        {
            if (!unlocked) return State.Locked;
            return string.IsNullOrEmpty(itemId) ? State.Empty : State.Occupied;
        }
    }

    // Renders the hex board: one cell GameObject per hexatile (from HexBoardMath layout),
    // each with a collider (tag target for input) and a building anchor. Locked cells show
    // the lock visual; occupied cells instantiate the item prefab.
    public class PlotHexBoard : MonoBehaviour
    {
        [SerializeField] private GameObject _cellPrefab;   // a hexatile: mesh + collider + "Anchor" child + "Lock" child
        [SerializeField] private float      _cellSize = 0.6f;
        [SerializeField] private Material    _lockedMat;
        [SerializeField] private Material    _unlockedMat;

        [Inject] private LandBuildingHandoff _handoff;
        [Inject] private DatabaseRegistry    _registry;
        [Inject] private EconomyConfig       _config;

        private readonly List<HexCell> _cells = new();

        public int CellCount => _cells.Count;
        public Vector3 CellWorldPosition(int index) =>
            (index >= 0 && index < _cells.Count) ? _cells[index].transform.position : Vector3.zero;

        public void Build(bool[] unlocked, string[] slots)
        {
            foreach (var c in _cells) if (c != null) Destroy(c.gameObject);
            _cells.Clear();

            var positions = HexBoardMath.LocalPositions(_config.HexBoardRadius, _cellSize);
            for (int i = 0; i < positions.Length; i++)
            {
                var go = Instantiate(_cellPrefab, transform);
                go.transform.localPosition = positions[i];
                var cell = go.GetComponent<HexCell>() ?? go.AddComponent<HexCell>();
                cell.Index = i;
                _cells.Add(cell);
                SetCell(i, i < unlocked.Length && unlocked[i], (slots != null && i < slots.Length) ? slots[i] : null);
            }
        }

        public void SetCell(int index, bool unlocked, string itemId)
        {
            if (index < 0 || index >= _cells.Count) return;
            var cell  = _cells[index];
            var state = HexCellVisual.Resolve(unlocked, itemId);
            cell.SetLockVisual(state == HexCellVisual.State.Locked, _lockedMat, _unlockedMat);

            for (int c = cell.Anchor.childCount - 1; c >= 0; c--) Destroy(cell.Anchor.GetChild(c).gameObject);
            if (state == HexCellVisual.State.Occupied)
            {
                var item = _registry.GetItem(itemId);
                if (item != null && item.Prefab != null)
                    Instantiate(item.Prefab, cell.Anchor.position, cell.Anchor.rotation, cell.Anchor);
            }
        }
    }

    // Attached to each cell prefab instance; identifies the hex index for raycast hits.
    public class HexCell : MonoBehaviour
    {
        public int Index;
        [SerializeField] private Transform _anchor;
        [SerializeField] private GameObject _lock;
        [SerializeField] private Renderer   _renderer;
        public Transform Anchor => _anchor != null ? _anchor : transform;
        public void SetLockVisual(bool locked, Material lockedMat, Material unlockedMat)
        {
            if (_lock != null) _lock.SetActive(locked);
            if (_renderer != null && lockedMat != null && unlockedMat != null)
                _renderer.sharedMaterial = locked ? lockedMat : unlockedMat;
        }
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run EditMode `HexCellVisualTests`. Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add Assets/_Project/Scripts/UI/PlotHexBoard.cs Assets/_Project/Scripts/UI/PlotHexBoard.cs.meta Assets/_Project/Tests/EditMode/UI/HexCellVisualTests.cs Assets/_Project/Tests/EditMode/UI/HexCellVisualTests.cs.meta
git commit -m "feat(ui): PlotHexBoard renders hexatile board + cell state resolver

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

### Task 9: PlotBoardInputController — tap/drag routing

**Files:**
- Create: `Assets/_Project/Scripts/UI/PlotBoardInputController.cs` (MonoBehaviour + pure static `PointerGesture`)
- Test: `Assets/_Project/Tests/EditMode/UI/PointerGestureTests.cs`

**Interfaces:**
- Consumes: `PlotHexBoard` (raycast → `HexCell.Index`), `LandBuildPaletteView` (drag payload), scene flows.
- Produces: `PointerGesture.IsTap(Vector2 down, Vector2 up, float thresholdPx) : bool`; `PlotBoardInputController` events `OnCellTapped(int index)`, `OnBuildingDragged(int fromIndex, int toIndex)`.

- [ ] **Step 1: Write the failing test for the gesture helper**

```csharp
using NUnit.Framework;
using UnityEngine;
using SocialUniverse.UI;

namespace SocialUniverse.Tests
{
    public class PointerGestureTests
    {
        [Test] public void Small_move_is_tap() =>
            Assert.IsTrue(PointerGesture.IsTap(new Vector2(100,100), new Vector2(104,103), 10f));
        [Test] public void Large_move_is_not_tap() =>
            Assert.IsFalse(PointerGesture.IsTap(new Vector2(100,100), new Vector2(140,100), 10f));
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run EditMode `PointerGestureTests`. Expected: FAIL.

- [ ] **Step 3: Implement**

```csharp
using System;
using UnityEngine;
using SocialUniverse.Core;

namespace SocialUniverse.UI
{
    public static class PointerGesture
    {
        public static bool IsTap(Vector2 down, Vector2 up, float thresholdPx) =>
            (up - down).sqrMagnitude <= thresholdPx * thresholdPx;
    }

    // Raycasts pointer down/up against the hex board and classifies tap vs drag.
    // Emits high-level intents; the scene flow (LandBuildPaletteView) subscribes.
    public class PlotBoardInputController : MonoBehaviour
    {
        [SerializeField] private Camera     _camera;
        [SerializeField] private PlotHexBoard _board;
        [SerializeField] private float      _tapThresholdPx = 12f;

        public event Action<int>      CellTapped;         // hexIndex (tap)
        public event Action<int, int> BuildingDragged;    // fromHex, toHex (drag between cells)

        private Vector2 _downPos;
        private int     _downCell = -1;

        private void Update()
        {
            if (Input.GetMouseButtonDown(0)) { _downPos = Input.mousePosition; _downCell = Raycast(); }
            else if (Input.GetMouseButtonUp(0))
            {
                int upCell = Raycast();
                if (PointerGesture.IsTap(_downPos, Input.mousePosition, _tapThresholdPx))
                {
                    if (upCell >= 0) CellTapped?.Invoke(upCell);
                }
                else if (_downCell >= 0 && upCell >= 0 && upCell != _downCell)
                {
                    BuildingDragged?.Invoke(_downCell, upCell);
                }
                _downCell = -1;
            }
        }

        private int Raycast()
        {
            var ray = _camera.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out var hit, 100f))
            {
                var cell = hit.collider.GetComponentInParent<HexCell>();
                if (cell != null) return cell.Index;
            }
            return -1;
        }
    }
}
```
> Note: palette→cell drag (placing a *new* building) is handled in Task 10 via the palette's own drag begin/end; this controller covers cell taps and cell→cell moves.

- [ ] **Step 4: Run test to verify it passes**

Run EditMode `PointerGestureTests`. Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add Assets/_Project/Scripts/UI/PlotBoardInputController.cs Assets/_Project/Scripts/UI/PlotBoardInputController.cs.meta Assets/_Project/Tests/EditMode/UI/PointerGestureTests.cs Assets/_Project/Tests/EditMode/UI/PointerGestureTests.cs.meta
git commit -m "feat(ui): PlotBoardInputController — tap vs cell-drag routing

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

### Task 10: Wire scene flows — palette drag-source, purchase/place/move/remove, popups

**Files:**
- Modify: `Assets/_Project/Scripts/UI/LandBuildPaletteView.cs` (rework)
- Create: `Assets/_Project/Scripts/UI/HexBuildPopup.cs`
- Modify: `Assets/_Project/Scripts/UI/LandBuildingController.cs` (host board; keep Back)
- Modify: `Assets/_Project/Scripts/App/LandBuildingSceneScope.cs` (register `PlotHexBoard`, `PlotBoardInputController`)

**Interfaces:**
- Consumes: `PlotHexBoard`, `PlotBoardInputController`, `LandBuildService`, `BuildPaletteService`, `LandBuildingHandoff`, `HexBoardMath`.
- Produces: complete interaction wiring (no new public types beyond `HexBuildPopup`).

- [ ] **Step 1: Add `HexBuildPopup`** (simple confirm modal)

```csharp
using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace SocialUniverse.UI
{
    // Reusable confirm popup for purchase / remove. Show() wires the confirm callback.
    public class HexBuildPopup : MonoBehaviour
    {
        [SerializeField] private GameObject _root;
        [SerializeField] private TMP_Text   _message;
        [SerializeField] private Button      _confirm;
        [SerializeField] private Button      _cancel;

        private Action _onConfirm;

        private void Awake()
        {
            _confirm.onClick.AddListener(() => { _onConfirm?.Invoke(); Hide(); });
            _cancel.onClick.AddListener(Hide);
            _root.SetActive(false);
        }

        public void Show(string message, Action onConfirm)
        {
            _message.text = message;
            _onConfirm = onConfirm;
            _root.SetActive(true);
        }

        public void Hide() => _root.SetActive(false);
    }
}
```

- [ ] **Step 2: Rework `LandBuildPaletteView`** into the scene flow controller

Replace the slot-tap logic with: build the affordable-items palette as drag sources; subscribe to `PlotBoardInputController.CellTapped`/`BuildingDragged`; own the two `HexBuildPopup`s. Key behaviors (full method bodies):

```csharp
        // Injected: LandBuildingHandoff _handoff; LandBuildService _buildService;
        // BuildPaletteService _palette; PlotHexBoard _board; PlotBoardInputController _input;
        // DatabaseRegistry _registry; EconomyConfig _config;
        // Serialized: HexBuildPopup _purchasePopup, _removePopup; palette UI refs; TMP_Text _statusText;
        // Local: int _localCoins; ItemDefinition _dragItem; bool[] _unlocked; string[] _slots;

        private void Start()
        {
            _localCoins = _handoff.Coins;
            _unlocked   = _handoff.Unlocked;
            _slots      = _handoff.Slots;

            _board.Build(_unlocked, _slots);

            bool canEdit = _handoff.CanEdit;
            _paletteRoot.SetActive(canEdit);
            if (!canEdit) return;

            _input.CellTapped     += OnCellTapped;
            _input.BuildingDragged += OnBuildingDragged;
            BuildPalette();
        }

        private void OnCellTapped(int hexIndex)
        {
            bool unlocked = _unlocked[hexIndex];
            bool occupied = unlocked && !string.IsNullOrEmpty(_slots[hexIndex]);

            if (!unlocked)
            {
                if (!HexBoardMath.IsAdjacentToUnlocked(hexIndex, _unlocked, _config.HexBoardRadius))
                { _statusText.text = "Expand from your unlocked tiles"; return; }
                int unlockedCount = CountUnlocked();
                int price = HexBoardMath.HexatilePrice(unlockedCount, _config.FreeHexCount, _config.HexatileBasePrice, _config.HexatilePriceStep);
                if (price > _localCoins) { _statusText.text = "Not enough coins"; return; }
                _purchasePopup.Show($"Unlock this hexatile for {price} coins?", () => Purchase(hexIndex));
            }
            else if (occupied)
            {
                _removePopup.Show("Remove this building?", () => Remove(hexIndex));
            }
        }

        private async void Purchase(int hexIndex)
        {
            var r = await _buildService.PurchaseHexatileAsync(_handoff.TileId, _handoff.RegistryPlanetId, hexIndex);
            if (!r.Success) { _statusText.text = $"Unlock failed: {r.Reason}"; return; }
            _unlocked[hexIndex] = true;
            if (r.NewBalance >= 0) _localCoins = r.NewBalance;
            _board.SetCell(hexIndex, true, null);
            _statusText.text = "";
            BuildPalette();
        }

        private async void OnBuildingDragged(int fromHex, int toHex)
        {
            // cell->cell drag = move an existing building to an unlocked empty cell
            if (string.IsNullOrEmpty(_slots[fromHex])) return;
            if (!_unlocked[toHex] || !string.IsNullOrEmpty(_slots[toHex])) { _statusText.text = "Can't move there"; return; }
            var r = await _buildService.MoveAsync(_handoff.TileId, _handoff.RegistryPlanetId, fromHex, toHex);
            if (!r.Success) { _statusText.text = $"Move failed: {r.Reason}"; return; }
            _slots[toHex] = _slots[fromHex]; _slots[fromHex] = null;
            _board.SetCell(fromHex, true, null);
            _board.SetCell(toHex, true, _slots[toHex]);
            _statusText.text = "";
        }

        // Called by a palette item's drag-end when released over a cell (see Step 3).
        public async void PlaceFromPalette(ItemDefinition item, int hexIndex)
        {
            if (!_unlocked[hexIndex] || !string.IsNullOrEmpty(_slots[hexIndex])) { _statusText.text = "Pick an unlocked empty tile"; return; }
            if (item.Cost > _localCoins) { _statusText.text = "Not enough coins"; return; }
            var r = await _buildService.PlaceAsync(_handoff.TileId, _handoff.RegistryPlanetId, hexIndex, item.ItemId, item.Cost);
            if (!r.Success) { _statusText.text = $"Place failed: {r.Reason}"; return; }
            _slots[hexIndex] = item.ItemId;
            if (r.NewBalance >= 0) _localCoins = r.NewBalance;
            _board.SetCell(hexIndex, true, item.ItemId);
            _statusText.text = "";
            BuildPalette();
        }

        private int CountUnlocked() { int n = 0; foreach (var b in _unlocked) if (b) n++; return n; }
```
`BuildPalette()` keeps its current shape (instantiate an item button per `_palette.GetAvailableItems(tile, _localCoins)`), but each button gets a drag handler that, on release, raycasts the board for a `HexCell` and calls `PlaceFromPalette(item, cell.Index)`. Use Unity `IBeginDragHandler`/`IEndDragHandler` on the item button, screen-ray via the board's camera.

- [ ] **Step 3: Register components in `LandBuildingSceneScope`**

Add to `Configure` (production + standalone):

```csharp
            builder.RegisterComponentInHierarchy<SocialUniverse.UI.PlotHexBoard>();
            builder.RegisterComponentInHierarchy<SocialUniverse.UI.PlotBoardInputController>();
```

- [ ] **Step 4: Compile & run full EditMode suite**

Run EditMode all. Expected: PASS (no unit tests target this MonoBehaviour wiring directly; ensure no compile errors in the console).

- [ ] **Step 5: Commit**

```bash
git add Assets/_Project/Scripts/UI/LandBuildPaletteView.cs Assets/_Project/Scripts/UI/HexBuildPopup.cs* Assets/_Project/Scripts/UI/LandBuildingController.cs Assets/_Project/Scripts/App/LandBuildingSceneScope.cs
git commit -m "feat(ui): hex board interaction flows — purchase, place, move, remove

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

### Task 11: Scene authoring — LandBuilding.unity hex board

**Files:**
- Modify: `Assets/Scenes/LandBuilding.unity`
- Create: `Assets/_Project/Prefabs/UI/Hexatile.prefab` (cell prefab: mesh + collider + "Anchor" child + "Lock" child + Renderer)

**Interfaces:** none (Unity authoring). Do this in the editor (or via MCP).

- [ ] **Step 1: Build the Hexatile cell prefab**

Create a flat hex mesh (or a scaled cylinder placeholder) with a `MeshCollider`/`BoxCollider`, a child empty named `Anchor` (building mount point), a child `Lock` (lock icon quad, initially active), and a `Renderer`. Add the `HexCell` component; wire `_anchor`, `_lock`, `_renderer`.

- [ ] **Step 2: Restructure the scene**

Remove `SlotAnchors` and the old slot-tap overlay objects. Add an empty `HexBoardRoot` with the `PlotHexBoard` component; assign `_cellPrefab` = Hexatile prefab, `_lockedMat`/`_unlockedMat`, `_cellSize`. Add `PlotBoardInputController` (assign `_camera` = Main Camera, `_board` = HexBoardRoot). Add two `HexBuildPopup` objects under the Canvas (purchase, remove) with message text + Confirm/Cancel buttons; wire them and the palette drag-source refs + `_statusText` on the reworked `LandBuildPaletteView`. Keep the Back button and `LandBuildingController`.

- [ ] **Step 3: Verify scene scope wiring**

Confirm `LandBuildingSceneScope`'s `parentReference` is still `SocialUniverse.App.RootLifetimeScope` and that `PlotHexBoard`/`PlotBoardInputController`/`LandBuildPaletteView`/`LandBuildingController` are all present in the hierarchy so `RegisterComponentInHierarchy` resolves them. Confirm an `EventSystem` exists (UI clicks).

- [ ] **Step 4: Save scene & smoke test in Play mode**

Enter Play via the normal flow (own a tile → View Land). Expected: 5 central hexatiles unlocked, 14 locked; tapping a locked adjacent tile shows the purchase popup; dragging a palette item onto an unlocked empty tile places it; dragging a building to another unlocked empty tile moves it; tapping a building shows the remove popup; Back returns to the planet.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scenes/LandBuilding.unity Assets/_Project/Prefabs/UI/Hexatile.prefab*
git commit -m "feat(scene): LandBuilding hex board — cells, popups, input wiring

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

### Task 12: Retire slot remnants + full-suite green

**Files:**
- Modify: `Assets/_Project/Scripts/Economy/LandBuildMath.cs` (keep `EnsureSize`/`FilledCount`/`IsEmpty`; remove any slot-count coupling), `Assets/_Project/Scripts/UI/LandBuildingController.cs` (drop old SlotAnchors render path), plus any lingering `PlotSlotCount`/slot references surfaced by the compiler.
- Test: full EditMode suite.

- [ ] **Step 1: Search & remove dead slot code**

Grep the codebase for `SlotAnchors`, `PlotSlotCount`, `_slotButtons`, `SetSlotVisual`, and the old slot-tap overlay fields; delete what the hex board replaced. Keep `LandBuildMath.FilledCount`/`EnsureSize`/`IsEmpty` (still used for `Slots`).

- [ ] **Step 2: Run the full EditMode suite**

Run EditMode all. Expected: PASS (should be the prior 268 minus removed slot tests plus the new HexBoardMath/HexCellVisual/PointerGesture/handoff/service tests). Fix any red.

- [ ] **Step 3: Commit**

```bash
git add -A
git commit -m "refactor(ui): remove retired slot-plot code; full suite green

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Self-Review

**Spec coverage:**
- §2 decisions → tiles+buildings cost (Tasks 6,7,10), yield=buildings (unchanged; `buildLevel`=filled in Tasks 7,10), tap-to-remove (Task 10), escalating server price (Tasks 1,6), adjacency (Tasks 1,6,10), 19/5 board (Tasks 1,2), procedural render (Task 8). ✓
- §3 geometry/indexing → Task 1. ✓
- §4 interactions → Tasks 9,10,11. ✓
- §5 schema (`Unlocked`+widened `Slots`, migration) → Tasks 3,4,7 (server free-default normalization = migration). ✓
- §6 server functions → Tasks 6,7 (+ GetLandRegistry Task 3). ✓
- §7 client components → Tasks 8,9,10,11. ✓
- §8 config → Task 2. ✓
- §9 testing → unit tests in Tasks 1,3,4,5,8,9; manual/playmode in Tasks 6,7,11. ✓
- §10 debt (item-cost tampering out of scope) → noted in Task 7. ✓

**Placeholder scan:** No TBD/TODO; every code step has concrete code; manual-verification notes are explicit, not hand-wavy. ✓

**Type consistency:** `hexIndex`/`fromHex`/`toHex` used consistently across client service (Task 5), server (Tasks 6,7), and flows (Task 10). `PurchaseHexatileResult` fields (`Success/Reason/NewBalance/UnlockedCount`) match between Task 5 and Task 6 return. `HexBoardMath` signatures used in Tasks 2 (mirror formula), 4, 8, 10 match Task 1. `HexCellVisual.State`/`Resolve` match between Tasks 8 and 10. `RegistryPlanetId` (not `PlanetId`) used for all server calls in Task 10, per the fix earlier this branch. ✓

**Note for implementers:** confirm the exact `IBackendClient.CallAsync` signature and the existing fake-backend helper in `LandBuildServiceTests.cs`/`LandBuildingHandoffTests.cs` before writing Task 3/5 tests — mirror them rather than the illustrative fakes above.
