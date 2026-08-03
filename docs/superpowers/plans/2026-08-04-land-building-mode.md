# LandBuilding Mode Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a dedicated LandBuilding scene where a tile owner places decorations/buildings into fixed plot slots (each placement raising the tile's build level) and any player can visit an owned tile to view its layout read-only.

**Architecture:** A tile's plot layout is persisted as a fixed-length `Slots` array (`slotIndex → itemId`) in the server land registry; `buildLevel` is derived as the filled-slot count. "View Land" on the tile info modal publishes an event that a Planet-scene handler turns into an FSM transition to a new `LandBuildingState` (full scene swap, mirroring `ActiveMiningState`). Because the Planet-scoped `LandRegistryService` is destroyed on the swap, the layout snapshot travels through a root-level `LandBuildingHandoff` (the same philosophy as `ActiveMiningHandoff`). All coin spend, ownership checks, and slot mutations happen in server functions; the client requests and reflects.

**Tech Stack:** Unity 6 (URP), C#, VContainer DI, Unity Test Framework (NUnit EditMode), UGS Cloud Code (JavaScript), Cloud Save Custom Data for the land registry.

## Global Constraints

- **Server-authoritative economy** — the client never mints currency, grants ownership, or changes build level directly; every mutation goes through a `ServerCode/` function called via `IBackendClient`. (CLAUDE.md Rule 1)
- **Backend behind interfaces** — gameplay code depends only on `IBackendClient` / `I*Service`; never a backend SDK. (Rule 2)
- **Tunables in ScriptableObjects** — `PlotSlotCount` lives in `EconomyConfig`; buildables are `ItemDefinition` assets. (Rule 3)
- **Decouple via events** — cross-scene/entry triggers go through `EventBus`, not direct cross-namespace calls. (Rule 4)
- **Assembly graph is one-directional** — `Economy → World → {Core, Config}`. `World` must NOT reference `Economy` (would cycle). Scene controllers that touch `Economy` live in the `UI` assembly (which references everything) or `App`. (Verified from asmdefs.)
- **Namespaces mirror folders**; one public type per file, file named after the type; interfaces `I`-prefixed; services `Service`-suffixed. (CLAUDE.md)
- **`buildLevel` is always the count of non-null slots** — never an independently incremented counter, on client or server.
- **Deploying/testing the `ServerCode/` JS against UGS is user-owned.** This plan delivers the JS files and the client wiring behind `IBackendClient` (which tests fake).

---

## Task 1: Slot data foundation (Config + Economy)

Introduce the persisted slot layout, the derived-build-level math, and the plot slot count. No behavior wired yet — this is the data substrate every later task builds on.

**Files:**
- Modify: `Assets/_Project/Scripts/Config/EconomyConfig.cs`
- Modify: `Assets/_Project/Scripts/Economy/LandRegistryService.cs` (the `LandTileEntry` DTO at the top)
- Create: `Assets/_Project/Scripts/Economy/LandBuildMath.cs`
- Test: `Assets/_Project/Tests/EditMode/Economy/LandBuildMathTests.cs`

**Interfaces:**
- Consumes: nothing from earlier tasks.
- Produces:
  - `EconomyConfig.PlotSlotCount : int` (property) and `EconomyConfig.MaxBuildLevel : int` (now returns the same backing field).
  - `LandTileEntry.Slots : string[]` (public field; each element is `null`/empty or an `itemId`).
  - `static class LandBuildMath` with:
    - `string[] EnsureSize(string[] slots, int size)`
    - `int FilledCount(string[] slots)`
    - `bool IsEmpty(string[] slots, int index)`

- [ ] **Step 1: Write the failing test**

Create `Assets/_Project/Tests/EditMode/Economy/LandBuildMathTests.cs`:

```csharp
using NUnit.Framework;
using SocialUniverse.Economy;

namespace SocialUniverse.Tests
{
    public class LandBuildMathTests
    {
        [Test]
        public void EnsureSize_returns_array_of_requested_length_when_null()
        {
            var result = LandBuildMath.EnsureSize(null, 8);
            Assert.AreEqual(8, result.Length);
        }

        [Test]
        public void EnsureSize_preserves_existing_entries()
        {
            var slots = new[] { "a", null, "b" };
            var result = LandBuildMath.EnsureSize(slots, 8);
            Assert.AreEqual(8, result.Length);
            Assert.AreEqual("a", result[0]);
            Assert.AreEqual("b", result[2]);
        }

        [Test]
        public void EnsureSize_returns_same_instance_when_already_correct_length()
        {
            var slots = new string[8];
            Assert.AreSame(slots, LandBuildMath.EnsureSize(slots, 8));
        }

        [Test]
        public void FilledCount_counts_non_empty_entries()
        {
            Assert.AreEqual(2, LandBuildMath.FilledCount(new[] { "a", null, "", "b" }));
        }

        [Test]
        public void FilledCount_of_null_is_zero()
        {
            Assert.AreEqual(0, LandBuildMath.FilledCount(null));
        }

        [Test]
        public void IsEmpty_true_for_null_empty_or_out_of_range()
        {
            var slots = new[] { "a", null };
            Assert.IsFalse(LandBuildMath.IsEmpty(slots, 0));
            Assert.IsTrue(LandBuildMath.IsEmpty(slots, 1));
            Assert.IsTrue(LandBuildMath.IsEmpty(slots, 5));
            Assert.IsTrue(LandBuildMath.IsEmpty(null, 0));
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run in Unity Test Runner (EditMode) the `LandBuildMathTests` class, or headless:
```
"C:\Program Files\Unity\Hub\Editor\<version>\Editor\Unity.exe" -runTests -projectPath . -testResults results.xml -testPlatform EditMode
```
Expected: FAIL to compile — `LandBuildMath` does not exist.

- [ ] **Step 3: Create `LandBuildMath`**

Create `Assets/_Project/Scripts/Economy/LandBuildMath.cs`:

```csharp
using System;

namespace SocialUniverse.Economy
{
    // Pure slot-array helpers shared by the client build flow and mirrored by the
    // ServerCode PlaceBuild/RemoveBuild/MoveBuild functions. buildLevel is always
    // FilledCount(slots) — never an independently tracked counter.
    public static class LandBuildMath
    {
        public static string[] EnsureSize(string[] slots, int size)
        {
            if (slots != null && slots.Length == size) return slots;
            var result = new string[size];
            if (slots != null)
                Array.Copy(slots, result, Math.Min(slots.Length, size));
            return result;
        }

        public static int FilledCount(string[] slots)
        {
            if (slots == null) return 0;
            int n = 0;
            foreach (var s in slots)
                if (!string.IsNullOrEmpty(s)) n++;
            return n;
        }

        public static bool IsEmpty(string[] slots, int index) =>
            slots == null || index < 0 || index >= slots.Length || string.IsNullOrEmpty(slots[index]);
    }
}
```

- [ ] **Step 4: Add `Slots` to `LandTileEntry`**

In `Assets/_Project/Scripts/Economy/LandRegistryService.cs`, add the field to the `LandTileEntry` class (after `VisitCount`):

```csharp
    public class LandTileEntry
    {
        public string OwnerId;
        public int    BuildLevel;
        public long   LastYieldClaimTs;
        public long   LastUpkeepTs;
        public int    VisitCount;
        public string[] Slots;   // slotIndex -> itemId (null/empty = empty slot). BuildLevel == filled count.
    }
```

- [ ] **Step 5: Add `PlotSlotCount` to `EconomyConfig` and derive `MaxBuildLevel` from it**

In `Assets/_Project/Scripts/Config/EconomyConfig.cs`, under the `[Header("Build")]` section, replace the `_maxBuildLevel` field with a `_plotSlotCount` field:

```csharp
        [Header("Build")]
        [SerializeField] private int   _plotSlotCount           = 8;   // placement slots per plot; also the max build level
```

Then replace the `MaxBuildLevel` property line with both accessors:

```csharp
        public int   PlotSlotCount            => _plotSlotCount;
        public int   MaxBuildLevel            => _plotSlotCount;   // a plot is "maxed" when every slot is filled
```

(Removing the `_maxBuildLevel` serialized field is intentional — the old value `4` was arbitrary; Unity drops the stale serialized value and `MaxBuildLevel` now spans the full slot range. `TileExtrusionView` and the yield formula reference `MaxBuildLevel` and keep working.)

- [ ] **Step 6: Run tests to verify they pass**

Run `LandBuildMathTests` — Expected: PASS (6 tests). Confirm the project still compiles (EconomyConfig / LandRegistryService changes).

- [ ] **Step 7: Commit**

```bash
git add Assets/_Project/Scripts/Economy/LandBuildMath.cs Assets/_Project/Scripts/Economy/LandBuildMath.cs.meta \
        Assets/_Project/Tests/EditMode/Economy/LandBuildMathTests.cs Assets/_Project/Tests/EditMode/Economy/LandBuildMathTests.cs.meta \
        Assets/_Project/Scripts/Economy/LandRegistryService.cs Assets/_Project/Scripts/Config/EconomyConfig.cs
git commit -m "feat(economy): slot-based plot layout foundation (LandBuildMath, LandTileEntry.Slots, PlotSlotCount)"
```

---

## Task 2: Server functions — slot-aware PlaceBuild + RemoveBuild + MoveBuild

Rework the existing `PlaceBuild` to write a specific slot and derive `buildLevel` from filled slots; add `RemoveBuild` and `MoveBuild`. These are UGS Cloud Code (JavaScript) — not covered by the Unity test runner. Deliverable is verified by code review against the spec and a documented manual UGS test the user runs.

**Files:**
- Modify: `ServerCode/PlaceBuild.js`
- Create: `ServerCode/RemoveBuild.js`
- Create: `ServerCode/MoveBuild.js`

**Interfaces:**
- Consumes: the `land_registry` Cloud Save Custom Data shape (existing), where each `registry[tileId]` entry now carries a `slots` array.
- Produces (return shapes the client DTOs in Task 3 must match):
  - `PlaceBuild` → `{ success: bool, reason?: string, newBalance?: number, buildLevel?: number }`
  - `RemoveBuild` → `{ success: bool, reason?: string, buildLevel?: number }`
  - `MoveBuild` → `{ success: bool, reason?: string }`

- [ ] **Step 1: Rework `PlaceBuild.js` to be slot-aware**

Replace the entire contents of `ServerCode/PlaceBuild.js`:

```javascript
// PlaceBuild — validates tile ownership, that the target slot is empty, and the
// player's balance, then deducts the item's coin cost, writes itemId into
// slots[slotIndex] in the planet's shared land registry, and sets buildLevel to
// the number of filled slots.
// NOTE: the validate -> deduct -> registry-write sequence is not transactional;
// same caveat as PurchaseLand.
const { CurrenciesApi } = require("@unity-services/economy-2.5");
const { DataApi }       = require("@unity-services/cloud-save-1.4");

const CURRENCY_ID  = "COINS";
const REGISTRY_KEY = "land_registry";
const SLOT_COUNT   = 8; // must match EconomyConfig.PlotSlotCount

function filledCount(slots) {
  if (!Array.isArray(slots)) return 0;
  return slots.filter(s => s !== null && s !== undefined && s !== "").length;
}

/**
 * @param {string} tileId
 * @param {string} planetId
 * @param {number} slotIndex - target slot, integer in [0, SLOT_COUNT).
 * @param {string} itemId - ItemDefinition id being placed.
 * @param {number} cost - coin cost, positive integer.
 */
module.exports = async ({ params, context, logger }) => {
  const { tileId, planetId, slotIndex, itemId, cost } = params;

  if (!tileId || !planetId || !itemId ||
      !Number.isInteger(slotIndex) || slotIndex < 0 || slotIndex >= SLOT_COUNT ||
      !Number.isInteger(cost) || cost <= 0) {
    throw new Error("Invalid params: tileId, planetId, slotIndex, itemId, cost required");
  }

  const { projectId, playerId, accessToken } = context;
  const econApi       = new CurrenciesApi({ accessToken });
  const customDataApi = new DataApi(context);
  const customId      = planetId.toLowerCase();

  try {
    let registry = {};
    try {
      const regRes = await customDataApi.getCustomItems(projectId, customId, [REGISTRY_KEY]);
      const item   = regRes.data.results.find(r => r.key === REGISTRY_KEY);
      if (item?.value) registry = item.value;
    } catch (_) { /* registry doesn't exist yet */ }

    const entry = registry[tileId];
    if (!entry || entry.ownerId !== playerId) {
      return { success: false, reason: "NOT_OWNER" };
    }

    if (!Array.isArray(entry.slots)) entry.slots = new Array(SLOT_COUNT).fill(null);
    if (entry.slots[slotIndex]) {
      return { success: false, reason: "SLOT_OCCUPIED" };
    }

    const balanceRes = await econApi.getPlayerCurrencyBalance({ projectId, playerId, currencyId: CURRENCY_ID });
    if (balanceRes.data.balance < cost) {
      return { success: false, reason: "INSUFFICIENT_FUNDS" };
    }

    const deductRes = await econApi.decrementPlayerCurrencyBalance({
      projectId, playerId, currencyId: CURRENCY_ID,
      currencyModifyBalanceRequest: { amount: cost }
    });
    const newBalance = deductRes.data.balance;

    entry.slots[slotIndex] = itemId;
    entry.buildLevel = filledCount(entry.slots);
    registry[tileId] = entry;
    await customDataApi.setCustomItem(projectId, customId, { key: REGISTRY_KEY, value: registry });

    logger.info(`PlaceBuild: ${playerId} placed ${itemId} in slot ${slotIndex} of ${tileId} (${planetId}) for ${cost} -> ${newBalance}, buildLevel ${entry.buildLevel}`);
    return { success: true, newBalance, buildLevel: entry.buildLevel };
  } catch (err) {
    logger.error("PlaceBuild failed", { "error.message": err.message });
    throw err;
  }
};
```

- [ ] **Step 2: Create `RemoveBuild.js`**

```javascript
// RemoveBuild — clears slots[slotIndex] for an owned tile and recomputes buildLevel.
// No coin refund (prevents place/remove refund-farming). Idempotent on an already-empty slot.
const { DataApi } = require("@unity-services/cloud-save-1.4");

const REGISTRY_KEY = "land_registry";
const SLOT_COUNT   = 8; // must match EconomyConfig.PlotSlotCount

function filledCount(slots) {
  if (!Array.isArray(slots)) return 0;
  return slots.filter(s => s !== null && s !== undefined && s !== "").length;
}

/**
 * @param {string} tileId
 * @param {string} planetId
 * @param {number} slotIndex - slot to clear, integer in [0, SLOT_COUNT).
 */
module.exports = async ({ params, context, logger }) => {
  const { tileId, planetId, slotIndex } = params;

  if (!tileId || !planetId ||
      !Number.isInteger(slotIndex) || slotIndex < 0 || slotIndex >= SLOT_COUNT) {
    throw new Error("Invalid params: tileId, planetId, slotIndex required");
  }

  const { projectId, playerId } = context;
  const customDataApi = new DataApi(context);
  const customId      = planetId.toLowerCase();

  try {
    let registry = {};
    try {
      const regRes = await customDataApi.getCustomItems(projectId, customId, [REGISTRY_KEY]);
      const item   = regRes.data.results.find(r => r.key === REGISTRY_KEY);
      if (item?.value) registry = item.value;
    } catch (_) { /* none yet */ }

    const entry = registry[tileId];
    if (!entry || entry.ownerId !== playerId) {
      return { success: false, reason: "NOT_OWNER" };
    }

    if (!Array.isArray(entry.slots) || !entry.slots[slotIndex]) {
      // already empty — nothing to do, report current level
      return { success: true, buildLevel: filledCount(entry.slots) };
    }

    entry.slots[slotIndex] = null;
    entry.buildLevel = filledCount(entry.slots);
    registry[tileId] = entry;
    await customDataApi.setCustomItem(projectId, customId, { key: REGISTRY_KEY, value: registry });

    logger.info(`RemoveBuild: ${playerId} cleared slot ${slotIndex} of ${tileId} (${planetId}), buildLevel ${entry.buildLevel}`);
    return { success: true, buildLevel: entry.buildLevel };
  } catch (err) {
    logger.error("RemoveBuild failed", { "error.message": err.message });
    throw err;
  }
};
```

- [ ] **Step 3: Create `MoveBuild.js`**

```javascript
// MoveBuild — moves an item from fromSlot to an empty toSlot on an owned tile.
// Free; buildLevel unchanged (filled count is invariant under a move).
const { DataApi } = require("@unity-services/cloud-save-1.4");

const REGISTRY_KEY = "land_registry";
const SLOT_COUNT   = 8; // must match EconomyConfig.PlotSlotCount

/**
 * @param {string} tileId
 * @param {string} planetId
 * @param {number} fromSlot
 * @param {number} toSlot
 */
module.exports = async ({ params, context, logger }) => {
  const { tileId, planetId, fromSlot, toSlot } = params;

  const inRange = i => Number.isInteger(i) && i >= 0 && i < SLOT_COUNT;
  if (!tileId || !planetId || !inRange(fromSlot) || !inRange(toSlot) || fromSlot === toSlot) {
    throw new Error("Invalid params: tileId, planetId, distinct in-range fromSlot/toSlot required");
  }

  const { projectId, playerId } = context;
  const customDataApi = new DataApi(context);
  const customId      = planetId.toLowerCase();

  try {
    let registry = {};
    try {
      const regRes = await customDataApi.getCustomItems(projectId, customId, [REGISTRY_KEY]);
      const item   = regRes.data.results.find(r => r.key === REGISTRY_KEY);
      if (item?.value) registry = item.value;
    } catch (_) { /* none yet */ }

    const entry = registry[tileId];
    if (!entry || entry.ownerId !== playerId) {
      return { success: false, reason: "NOT_OWNER" };
    }

    const slots = entry.slots;
    if (!Array.isArray(slots) || !slots[fromSlot] || slots[toSlot]) {
      return { success: false, reason: "INVALID_MOVE" };
    }

    slots[toSlot]   = slots[fromSlot];
    slots[fromSlot] = null;
    registry[tileId] = entry;
    await customDataApi.setCustomItem(projectId, customId, { key: REGISTRY_KEY, value: registry });

    logger.info(`MoveBuild: ${playerId} moved slot ${fromSlot}->${toSlot} on ${tileId} (${planetId})`);
    return { success: true };
  } catch (err) {
    logger.error("MoveBuild failed", { "error.message": err.message });
    throw err;
  }
};
```

- [ ] **Step 4: Review & manual-verification note**

Confirm each file against the spec §2: ownership check first, slot bounds `[0, SLOT_COUNT)`, `PlaceBuild` returns `SLOT_OCCUPIED`/`INSUFFICIENT_FUNDS`/`NOT_OWNER`, `RemoveBuild` no refund + idempotent, `MoveBuild` free + `INVALID_MOVE` guard. `SLOT_COUNT = 8` in all three matches `EconomyConfig.PlotSlotCount = 8` (Task 1).

**Manual verification (user, in UGS Dashboard → Cloud Code):** deploy the three functions; on a test tile you own, call `PlaceBuild {slotIndex:0,...}` and confirm balance drops and the registry entry gains `slots[0]` + `buildLevel:1`; call it again same slot → `SLOT_OCCUPIED`; `MoveBuild {fromSlot:0,toSlot:1}` → `slots[1]` set, `slots[0]` null, `buildLevel` still 1; `RemoveBuild {slotIndex:1}` → `slots[1]` null, `buildLevel:0`, balance unchanged.

- [ ] **Step 5: Commit**

```bash
git add ServerCode/PlaceBuild.js ServerCode/RemoveBuild.js ServerCode/MoveBuild.js
git commit -m "feat(server): slot-aware PlaceBuild + RemoveBuild + MoveBuild cloud functions"
```

---

## Task 3: LandBuildService client (Economy)

The client-side service that calls the three server functions through `IBackendClient` and maps their responses to typed results. Pure request/response — it performs no local state mutation (the controller applies slot changes to the handoff in Task 7), which keeps it trivially testable, mirroring `LandSaleService`.

**Files:**
- Create: `Assets/_Project/Scripts/Economy/LandBuildService.cs`
- Test: `Assets/_Project/Tests/EditMode/Economy/LandBuildServiceTests.cs`

**Interfaces:**
- Consumes: `IBackendClient` (Core), `SULog` (Core). Server return shapes from Task 2.
- Produces:
  - `class PlaceBuildResult { bool Success; string Reason; int NewBalance = -1; int BuildLevel = -1; }`
  - `class RemoveBuildResult { bool Success; string Reason; int BuildLevel = -1; }`
  - `class MoveBuildResult { bool Success; string Reason; }`
  - `LandBuildService(IBackendClient backend)` with:
    - `Task<PlaceBuildResult> PlaceAsync(string tileId, string planetId, int slotIndex, string itemId, int cost)`
    - `Task<RemoveBuildResult> RemoveAsync(string tileId, string planetId, int slotIndex)`
    - `Task<MoveBuildResult> MoveAsync(string tileId, string planetId, int fromSlot, int toSlot)`

- [ ] **Step 1: Write the failing test**

Create `Assets/_Project/Tests/EditMode/Economy/LandBuildServiceTests.cs`:

```csharp
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using SocialUniverse.Core;
using SocialUniverse.Economy;

namespace SocialUniverse.Tests
{
    public class LandBuildServiceTests
    {
        // Captures the last call and returns a pre-set object cast to the requested type.
        private class FakeBackendClient : IBackendClient
        {
            public string LastFunction;
            public Dictionary<string, object> LastArgs;
            public object NextResult;

            public Task<T> CallAsync<T>(string function, Dictionary<string, object> args = null)
            {
                LastFunction = function;
                LastArgs = args;
                return Task.FromResult((T)NextResult);
            }

            public Task CallAsync(string function, Dictionary<string, object> args = null) => Task.CompletedTask;
        }

        [Test]
        public async Task PlaceAsync_sends_expected_params_and_maps_result()
        {
            var backend = new FakeBackendClient
            {
                NextResult = new PlaceBuildResult { Success = true, NewBalance = 420, BuildLevel = 3 }
            };
            var service = new LandBuildService(backend);

            var result = await service.PlaceAsync("12", "earth", 2, "item_tree", 50);

            Assert.AreEqual("PlaceBuild", backend.LastFunction);
            Assert.AreEqual("12",        backend.LastArgs["tileId"]);
            Assert.AreEqual("earth",     backend.LastArgs["planetId"]);
            Assert.AreEqual(2,           backend.LastArgs["slotIndex"]);
            Assert.AreEqual("item_tree", backend.LastArgs["itemId"]);
            Assert.AreEqual(50,          backend.LastArgs["cost"]);
            Assert.IsTrue(result.Success);
            Assert.AreEqual(420, result.NewBalance);
            Assert.AreEqual(3,   result.BuildLevel);
        }

        [Test]
        public async Task PlaceAsync_returns_failure_on_null_response()
        {
            var service = new LandBuildService(new FakeBackendClient { NextResult = null });
            var result = await service.PlaceAsync("12", "earth", 0, "x", 10);
            Assert.IsFalse(result.Success);
        }

        [Test]
        public async Task RemoveAsync_sends_expected_params_and_maps_result()
        {
            var backend = new FakeBackendClient
            {
                NextResult = new RemoveBuildResult { Success = true, BuildLevel = 1 }
            };
            var service = new LandBuildService(backend);

            var result = await service.RemoveAsync("12", "earth", 1);

            Assert.AreEqual("RemoveBuild", backend.LastFunction);
            Assert.AreEqual(1, backend.LastArgs["slotIndex"]);
            Assert.IsTrue(result.Success);
            Assert.AreEqual(1, result.BuildLevel);
        }

        [Test]
        public async Task MoveAsync_sends_from_and_to_slots()
        {
            var backend = new FakeBackendClient
            {
                NextResult = new MoveBuildResult { Success = true }
            };
            var service = new LandBuildService(backend);

            var result = await service.MoveAsync("12", "earth", 0, 3);

            Assert.AreEqual("MoveBuild", backend.LastFunction);
            Assert.AreEqual(0, backend.LastArgs["fromSlot"]);
            Assert.AreEqual(3, backend.LastArgs["toSlot"]);
            Assert.IsTrue(result.Success);
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run `LandBuildServiceTests`. Expected: FAIL to compile — `LandBuildService` and its result types do not exist.

- [ ] **Step 3: Create `LandBuildService`**

Create `Assets/_Project/Scripts/Economy/LandBuildService.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SocialUniverse.Core;

namespace SocialUniverse.Economy
{
    // Result shapes are public so tests can construct them for a fake IBackendClient.
    public class PlaceBuildResult  { public bool Success; public string Reason; public int NewBalance = -1; public int BuildLevel = -1; }
    public class RemoveBuildResult { public bool Success; public string Reason; public int BuildLevel = -1; }
    public class MoveBuildResult   { public bool Success; public string Reason; }

    // Client wrapper over the PlaceBuild/RemoveBuild/MoveBuild cloud functions.
    // Pure request/response: performs no local state mutation. Callers apply the
    // resulting slot change to the LandBuildingHandoff (see LandBuildingController).
    public class LandBuildService
    {
        private readonly IBackendClient _backend;

        public LandBuildService(IBackendClient backend) => _backend = backend;

        public async Task<PlaceBuildResult> PlaceAsync(string tileId, string planetId, int slotIndex, string itemId, int cost)
        {
            try
            {
                var res = await _backend.CallAsync<PlaceBuildResult>("PlaceBuild",
                    new Dictionary<string, object>
                    {
                        { "tileId",    tileId    },
                        { "planetId",  planetId  },
                        { "slotIndex", slotIndex },
                        { "itemId",    itemId    },
                        { "cost",      cost      },
                    });
                return res ?? new PlaceBuildResult { Success = false, Reason = "No response" };
            }
            catch (Exception ex)
            {
                SULog.Error($"LandBuildService.Place failed — {ex.Message}", SULog.Channel.Economy);
                return new PlaceBuildResult { Success = false, Reason = "Network error" };
            }
        }

        public async Task<RemoveBuildResult> RemoveAsync(string tileId, string planetId, int slotIndex)
        {
            try
            {
                var res = await _backend.CallAsync<RemoveBuildResult>("RemoveBuild",
                    new Dictionary<string, object>
                    {
                        { "tileId",    tileId    },
                        { "planetId",  planetId  },
                        { "slotIndex", slotIndex },
                    });
                return res ?? new RemoveBuildResult { Success = false, Reason = "No response" };
            }
            catch (Exception ex)
            {
                SULog.Error($"LandBuildService.Remove failed — {ex.Message}", SULog.Channel.Economy);
                return new RemoveBuildResult { Success = false, Reason = "Network error" };
            }
        }

        public async Task<MoveBuildResult> MoveAsync(string tileId, string planetId, int fromSlot, int toSlot)
        {
            try
            {
                var res = await _backend.CallAsync<MoveBuildResult>("MoveBuild",
                    new Dictionary<string, object>
                    {
                        { "tileId",   tileId   },
                        { "planetId", planetId },
                        { "fromSlot", fromSlot },
                        { "toSlot",   toSlot   },
                    });
                return res ?? new MoveBuildResult { Success = false, Reason = "No response" };
            }
            catch (Exception ex)
            {
                SULog.Error($"LandBuildService.Move failed — {ex.Message}", SULog.Channel.Economy);
                return new MoveBuildResult { Success = false, Reason = "Network error" };
            }
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run `LandBuildServiceTests`. Expected: PASS (4 tests).

- [ ] **Step 5: Commit**

```bash
git add Assets/_Project/Scripts/Economy/LandBuildService.cs Assets/_Project/Scripts/Economy/LandBuildService.cs.meta \
        Assets/_Project/Tests/EditMode/Economy/LandBuildServiceTests.cs Assets/_Project/Tests/EditMode/Economy/LandBuildServiceTests.cs.meta
git commit -m "feat(economy): LandBuildService client for slot-aware build ops"
```

---

## Task 4: Rework BuildPaletteService + retire the old in-planet build path

Replace the linear per-level palette rule with a slot-model rule (any affordable item fits any empty slot on an owned tile), and remove the now-incompatible `BuildModeController` (it calls the old slotless `PlaceBuild` and uses `item.BuildLevel == tile.BuildLevel + 1` gating, which the Task 2 rework breaks).

**Files:**
- Modify: `Assets/_Project/Scripts/Economy/BuildPaletteService.cs`
- Modify: `Assets/_Project/Tests/EditMode/Economy/BuildPaletteServiceTests.cs`
- Delete: `Assets/_Project/Scripts/App/BuildModeController.cs` (+ `.meta`)
- Delete: `Assets/_Project/Scripts/*/BuildItemRequestedEvent.cs` (+ `.meta`) — locate with the grep in Step 4
- Modify: `Assets/_Project/Scripts/App/PlanetSceneScope.cs` (remove `builder.RegisterEntryPoint<BuildModeController>();`)

**Interfaces:**
- Consumes: `DatabaseRegistry.AllItems`, `EconomyConfig.MaxBuildLevel`, `ItemDefinition.Cost`, `TileData`/`TileState` (from Task 1's config change).
- Produces: `BuildPaletteService.GetAvailableItems(TileData tile, int availableCoins) : IEnumerable<ItemDefinition>`.

- [ ] **Step 1: Rewrite the palette tests to the slot model**

Replace the body of `Assets/_Project/Tests/EditMode/Economy/BuildPaletteServiceTests.cs` (keep the `using`s, `SetField` helper, and `TearDown`; change `MakeItem` to take a cost and the tests to the new rule):

```csharp
        private static ItemDefinition MakeItem(string itemId, int cost)
        {
            var item = ScriptableObject.CreateInstance<ItemDefinition>();
            SetField(item, "_itemId", itemId);
            SetField(item, "_cost", cost);
            return item;
        }

        [SetUp]
        public void SetUp()
        {
            _config   = ScriptableObject.CreateInstance<EconomyConfig>();
            _registry = ScriptableObject.CreateInstance<DatabaseRegistry>();
            SetField(_config, "_plotSlotCount", 8);

            _items = new[]
            {
                MakeItem("cheap_tree", 50),
                MakeItem("mid_statue", 200),
                MakeItem("pricey_house", 1000),
            };
            SetField(_registry, "_items", _items);

            _palette = new BuildPaletteService(_registry, _config);
        }

        [Test]
        public void Returns_all_affordable_items_for_owned_tile_with_free_slots()
        {
            var tile = new TileData("1") { State = TileState.OwnedByPlayer, BuildLevel = 2 };

            var available = _palette.GetAvailableItems(tile, 300).ToList();

            Assert.AreEqual(2, available.Count);
            Assert.IsTrue(available.Any(i => i.ItemId == "cheap_tree"));
            Assert.IsTrue(available.Any(i => i.ItemId == "mid_statue"));
            Assert.IsFalse(available.Any(i => i.ItemId == "pricey_house"));
        }

        [Test]
        public void Returns_empty_for_tile_not_owned_by_player()
        {
            var tile = new TileData("1") { State = TileState.OwnedByOther, BuildLevel = 0 };
            Assert.IsEmpty(_palette.GetAvailableItems(tile, int.MaxValue));
        }

        [Test]
        public void Returns_empty_for_available_tile()
        {
            var tile = new TileData("1") { State = TileState.Available, BuildLevel = 0 };
            Assert.IsEmpty(_palette.GetAvailableItems(tile, int.MaxValue));
        }

        [Test]
        public void Returns_empty_when_all_slots_full()
        {
            var tile = new TileData("1") { State = TileState.OwnedByPlayer, BuildLevel = _config.MaxBuildLevel };
            Assert.IsEmpty(_palette.GetAvailableItems(tile, int.MaxValue));
        }
```

- [ ] **Step 2: Run tests to verify they fail**

Run `BuildPaletteServiceTests`. Expected: FAIL to compile — `GetAvailableItems` has the old single-arg signature.

- [ ] **Step 3: Rewrite `BuildPaletteService`**

Replace the class body in `Assets/_Project/Scripts/Economy/BuildPaletteService.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using SocialUniverse.Config;
using SocialUniverse.World;

namespace SocialUniverse.Economy
{
    // Returns the buildable items a player can place on a given tile in the slot model:
    // any item the player can afford may go in any empty slot of a tile they own.
    // A tile is "full" when BuildLevel (== filled slot count) reaches MaxBuildLevel.
    // (Rarity / unlock-level gating is intentionally deferred — see the design spec.)
    public class BuildPaletteService
    {
        private readonly DatabaseRegistry _registry;
        private readonly EconomyConfig    _config;

        public BuildPaletteService(DatabaseRegistry registry, EconomyConfig config)
        {
            _registry = registry;
            _config   = config;
        }

        public IEnumerable<ItemDefinition> GetAvailableItems(TileData tile, int availableCoins)
        {
            if (tile.State != TileState.OwnedByPlayer) return Enumerable.Empty<ItemDefinition>();
            if (tile.BuildLevel >= _config.MaxBuildLevel) return Enumerable.Empty<ItemDefinition>();

            return _registry.AllItems.Where(i => i.Cost <= availableCoins);
        }
    }
}
```

- [ ] **Step 4: Retire the old in-planet build path**

Locate the old event and its usages:
```bash
grep -rn "BuildItemRequestedEvent\|BuildModeController" Assets/_Project/Scripts
```
Then:
- Delete `Assets/_Project/Scripts/App/BuildModeController.cs` and its `.meta`.
- Delete the `BuildItemRequestedEvent.cs` file the grep reveals and its `.meta`.
- In `Assets/_Project/Scripts/App/PlanetSceneScope.cs`, delete the line `builder.RegisterEntryPoint<BuildModeController>();`.
- If the grep shows any other publisher of `BuildItemRequestedEvent` (there should be none — no build UI is wired yet), stop and report it rather than deleting blindly.

- [ ] **Step 5: Run tests + compile to verify**

Run `BuildPaletteServiceTests` — Expected: PASS (4 tests). Confirm the project compiles with `BuildModeController` / `BuildItemRequestedEvent` gone and `PlanetSceneScope` updated (no missing-type errors).

- [ ] **Step 6: Commit**

```bash
git add Assets/_Project/Scripts/Economy/BuildPaletteService.cs \
        Assets/_Project/Tests/EditMode/Economy/BuildPaletteServiceTests.cs \
        Assets/_Project/Scripts/App/PlanetSceneScope.cs
git add -u   # stages the deleted BuildModeController + BuildItemRequestedEvent (+ .meta)
git commit -m "refactor(economy): slot-model BuildPaletteService; retire old in-planet BuildModeController"
```

---

## Task 5: Core plumbing — event, handoff, FSM state, entry handler

Wire the scene-swap machinery: the request event, the root-level layout handoff, the new FSM state (mirroring `ActiveMiningState`), the `PlanetState` entry method, the scene-name constant, DI registration, and the Planet-scene handler that turns the event into a transition.

**Files:**
- Create: `Assets/_Project/Scripts/Core/ViewLandRequestedEvent.cs`
- Create: `Assets/_Project/Scripts/Core/LandBuildingHandoff.cs`
- Create: `Assets/_Project/Scripts/Core/LandBuildingState.cs`
- Create: `Assets/_Project/Scripts/App/ViewLandRequestHandler.cs`
- Modify: `Assets/_Project/Scripts/Core/Constants.cs` (add `LandBuilding` scene name)
- Modify: `Assets/_Project/Scripts/Core/PlanetState.cs` (add `EnterLandBuilding()`)
- Modify: `Assets/_Project/Scripts/Core/ProjectLifetimeScope.cs` (register `LandBuildingState` + `LandBuildingHandoff`)
- Modify: `Assets/_Project/Scripts/App/PlanetSceneScope.cs` (register `ViewLandRequestHandler` in the production block)
- Test: `Assets/_Project/Tests/EditMode/Core/LandBuildingHandoffTests.cs`

**Interfaces:**
- Consumes: `IGameState`, `GameStateMachine`, `SceneLoader`, `IObjectResolver`, `EventBus`, `PlanetState` (Core).
- Produces:
  - `class ViewLandRequestedEvent { string TileId; string OwnerId; bool CanEdit; string[] Slots; int Coins; }`
  - `class LandBuildingHandoff` with `TileId/PlanetId/OwnerId/CanEdit/Coins` (get) + `string[] Slots` (get) and `void Begin(string tileId, string planetId, string ownerId, bool canEdit, string[] slots, int coins)`, `void Clear()`.
  - `PlanetState.EnterLandBuilding()`.
  - `LandBuildingState : IGameState` with `void Finish()` (returns to `PlanetState`).
  - `Constants.SceneNames.LandBuilding = "LandBuilding"`.

- [ ] **Step 1: Write the failing test**

Create `Assets/_Project/Tests/EditMode/Core/LandBuildingHandoffTests.cs`:

```csharp
using NUnit.Framework;
using SocialUniverse.Core;

namespace SocialUniverse.Tests
{
    public class LandBuildingHandoffTests
    {
        [Test]
        public void Begin_stores_all_fields()
        {
            var handoff = new LandBuildingHandoff();
            var slots = new[] { "a", null, "b" };

            handoff.Begin("12", "earth", "player_a", true, slots, 500);

            Assert.AreEqual("12",       handoff.TileId);
            Assert.AreEqual("earth",    handoff.PlanetId);
            Assert.AreEqual("player_a", handoff.OwnerId);
            Assert.IsTrue(handoff.CanEdit);
            Assert.AreEqual(500,        handoff.Coins);
            Assert.AreSame(slots,       handoff.Slots);
        }

        [Test]
        public void Clear_resets_reference_fields()
        {
            var handoff = new LandBuildingHandoff();
            handoff.Begin("12", "earth", "player_a", true, new[] { "a" }, 500);

            handoff.Clear();

            Assert.IsNull(handoff.TileId);
            Assert.IsNull(handoff.PlanetId);
            Assert.IsNull(handoff.Slots);
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run `LandBuildingHandoffTests`. Expected: FAIL to compile — `LandBuildingHandoff` does not exist.

- [ ] **Step 3: Create `LandBuildingHandoff`**

Create `Assets/_Project/Scripts/Core/LandBuildingHandoff.cs`:

```csharp
namespace SocialUniverse.Core
{
    // Carries a tile's plot layout across the Planet -> LandBuilding -> Planet scene swap.
    // LandRegistryService/Wallet live in PlanetSceneScope and are destroyed the moment Planet
    // unloads, so this Root-level singleton (registered in ProjectLifetimeScope) is the only
    // thing that survives the round trip — same pattern as ActiveMiningHandoff. Holds only
    // primitives/strings; Core must never depend on Economy/World types.
    public class LandBuildingHandoff
    {
        public string   TileId   { get; private set; }
        public string   PlanetId { get; private set; }
        public string   OwnerId  { get; private set; }
        public bool     CanEdit  { get; private set; }
        public int      Coins    { get; private set; }
        public string[] Slots    { get; private set; }

        public void Begin(string tileId, string planetId, string ownerId, bool canEdit, string[] slots, int coins)
        {
            TileId   = tileId;
            PlanetId = planetId;
            OwnerId  = ownerId;
            CanEdit  = canEdit;
            Slots    = slots;
            Coins    = coins;
        }

        public void Clear()
        {
            TileId   = null;
            PlanetId = null;
            OwnerId  = null;
            Slots    = null;
        }
    }
}
```

- [ ] **Step 4: Create `ViewLandRequestedEvent`**

Create `Assets/_Project/Scripts/Core/ViewLandRequestedEvent.cs`:

```csharp
namespace SocialUniverse.Core
{
    // Published by TileInfoModal's "View Land" button. Indirected through the event bus
    // (rather than the modal calling PlanetState directly) so Planet's standalone/no-Bootstrap
    // dev mode — which never registers PlanetState — doesn't break; same reasoning as
    // ActiveMiningRequestedEvent. Carries the layout snapshot because the handler (App scope)
    // cannot resolve the Planet-scoped LandRegistryService.
    public class ViewLandRequestedEvent
    {
        public string   TileId;
        public string   OwnerId;
        public bool     CanEdit;
        public string[] Slots;
        public int      Coins;
    }
}
```

- [ ] **Step 5: Create `LandBuildingState`**

Create `Assets/_Project/Scripts/Core/LandBuildingState.cs`:

```csharp
using System.Threading.Tasks;
using UnityEngine.SceneManagement;
using VContainer;

namespace SocialUniverse.Core
{
    // Owns the LandBuilding scene as the sole running gameplay scene — mirrors ActiveMiningState.
    // Entered from PlanetState.EnterLandBuilding() after LandBuildingHandoff is populated; Planet
    // is unloaded via PlanetState.Exit() before this state's Enter() runs.
    public class LandBuildingState : IGameState
    {
        private readonly SceneLoader        _sceneLoader;
        private readonly GameStateMachine   _fsm;
        private readonly IObjectResolver    _resolver;
        private readonly LandBuildingHandoff _handoff;

        public LandBuildingState(SceneLoader sceneLoader, GameStateMachine fsm, IObjectResolver resolver, LandBuildingHandoff handoff)
        {
            _sceneLoader = sceneLoader;
            _fsm         = fsm;
            _resolver    = resolver;
            _handoff     = handoff;
        }

        public void Enter() => _ = LoadAsync();
        public void Tick()  { }
        public void Exit()  => _ = UnloadAsync();

        private async Task LoadAsync()
        {
            SULog.Info($"LandBuilding: entering tile {_handoff.TileId} (canEdit={_handoff.CanEdit})");
            await _sceneLoader.LoadAsync(Constants.SceneNames.LoadingScreen);
            await _sceneLoader.LoadAsync(Constants.SceneNames.LandBuilding);
        }

        private async Task UnloadAsync()
        {
            await _sceneLoader.UnloadAsync(Constants.SceneNames.LandBuilding);
            var ls = SceneManager.GetSceneByName(Constants.SceneNames.LoadingScreen);
            if (ls.IsValid() && ls.isLoaded)
                await _sceneLoader.UnloadAsync(Constants.SceneNames.LoadingScreen);
        }

        // Called by the LandBuilding scene's Back button. Returns to the planet the player
        // came from; Planet re-hydrates the land registry + wallet from the server on entry
        // (PlanetSceneBootstrapper.HydrateServerStateAsync), so any builds made here are reflected.
        public void Finish()
        {
            var planetState = _resolver.Resolve<PlanetState>();
            planetState.TargetPlanetId = _handoff.PlanetId;
            _handoff.Clear();
            _fsm.TransitionTo(planetState);
        }
    }
}
```

- [ ] **Step 6: Add the scene-name constant**

In `Assets/_Project/Scripts/Core/Constants.cs`, add to `SceneNames`:

```csharp
            public const string LandBuilding  = "LandBuilding";
```

- [ ] **Step 7: Add `EnterLandBuilding` to `PlanetState`**

In `Assets/_Project/Scripts/Core/PlanetState.cs`, add next to `EnterActiveMining`:

```csharp
        // Called by ViewLandRequestHandler once LandBuildingHandoff is populated — transitions to
        // LandBuildingState, which loads the LandBuilding scene as the sole gameplay scene
        // (Exit() below unloads Planet).
        public void EnterLandBuilding() => _fsm.TransitionTo(_resolver.Resolve<LandBuildingState>());
```

- [ ] **Step 8: Register in `ProjectLifetimeScope`**

In `Assets/_Project/Scripts/Core/ProjectLifetimeScope.cs`, add after the `ActiveMiningState` / `ActiveMiningHandoff` registrations:

```csharp
            builder.Register<LandBuildingState>(Lifetime.Singleton);
            builder.Register<LandBuildingHandoff>(Lifetime.Singleton);
```

- [ ] **Step 9: Create `ViewLandRequestHandler`**

Create `Assets/_Project/Scripts/App/ViewLandRequestHandler.cs`:

```csharp
using System;
using VContainer.Unity;
using SocialUniverse.Core;

namespace SocialUniverse.App
{
    // Planet-scene handler: turns a ViewLandRequestedEvent into an FSM transition. Fills the
    // root-level LandBuildingHandoff (planetId comes from the current PlanetState). Mirrors
    // ActiveMiningRequestHandler; registered in PlanetSceneScope's production block.
    public class ViewLandRequestHandler : IStartable, IDisposable
    {
        private readonly PlanetState         _planetState;
        private readonly LandBuildingHandoff _handoff;

        public ViewLandRequestHandler(PlanetState planetState, LandBuildingHandoff handoff)
        {
            _planetState = planetState;
            _handoff     = handoff;
        }

        public void Start()   => EventBus.Subscribe<ViewLandRequestedEvent>(OnViewLandRequested);
        public void Dispose() => EventBus.Unsubscribe<ViewLandRequestedEvent>(OnViewLandRequested);

        private void OnViewLandRequested(ViewLandRequestedEvent e)
        {
            _handoff.Begin(e.TileId, _planetState.TargetPlanetId, e.OwnerId, e.CanEdit, e.Slots, e.Coins);
            _planetState.EnterLandBuilding();
        }
    }
}
```

- [ ] **Step 10: Register the handler in `PlanetSceneScope`**

In `Assets/_Project/Scripts/App/PlanetSceneScope.cs`, in the `if (parentPlanetState != null)` block (right after `builder.RegisterEntryPoint<ActiveMiningRequestHandler>();`), add:

```csharp
                builder.RegisterEntryPoint<ViewLandRequestHandler>();
```

`LandBuildingHandoff` resolves from the parent (root) scope; `PlanetState` is already registered in this block.

- [ ] **Step 11: Run tests + compile**

Run `LandBuildingHandoffTests` — Expected: PASS (2 tests). Confirm the whole project compiles (new Core/App types, scene constant, scope registrations).

- [ ] **Step 12: Commit**

```bash
git add Assets/_Project/Scripts/Core/ViewLandRequestedEvent.cs Assets/_Project/Scripts/Core/LandBuildingHandoff.cs \
        Assets/_Project/Scripts/Core/LandBuildingState.cs Assets/_Project/Scripts/App/ViewLandRequestHandler.cs \
        Assets/_Project/Scripts/Core/Constants.cs Assets/_Project/Scripts/Core/PlanetState.cs \
        Assets/_Project/Scripts/Core/ProjectLifetimeScope.cs Assets/_Project/Scripts/App/PlanetSceneScope.cs \
        Assets/_Project/Tests/EditMode/Core/LandBuildingHandoffTests.cs
git add -u
git commit -m "feat(core): LandBuilding FSM state, handoff, request event + Planet-scene handler"
```

---

## Task 6: LandBuilding scene, scene scope, and placeholder prefabs

Author the new scene (camera, light, plot ground, fixed slot anchors), its `LandBuildingSceneScope` (parented to the root scope, registering `LandBuildService` and the scene MonoBehaviours), and the placeholder buildable prefabs. This is Unity-editor work — verified by opening the scene and by later tasks; no unit test.

**Files:**
- Create: `Assets/Scenes/LandBuilding.unity` (+ `.meta`)
- Create: `Assets/_Project/Scripts/App/LandBuildingSceneScope.cs`
- Create: `Assets/_Project/Prefabs/Buildables/` placeholder prefabs (one per `ItemDefinition`)
- Modify: build settings (`EditorBuildSettings` — add the scene) — File → Build Settings

**Interfaces:**
- Consumes: `LandBuildingHandoff`, `IBackendClient`, `DatabaseRegistry`, `EconomyConfig` (from parent root scope); `LandBuildService` (Task 3); `LandBuildingController` + `LandBuildPaletteView` (Tasks 7–8, registered here now, wired later).
- Produces: a loadable `"LandBuilding"` scene whose scope resolves the scene MonoBehaviours.

- [ ] **Step 1: Create placeholder buildable prefabs**

For each existing `ItemDefinition` asset (find them: `Assets/_Project/ScriptableObjects/**` — search the Project for type `ItemDefinition`), create a simple placeholder prefab under `Assets/_Project/Prefabs/Buildables/`:
- Right-click in `Assets/_Project/Prefabs/Buildables/` → 3D Object primitive (Cube for buildings, Cylinder for trees, etc.), scale to ~1 unit, give it a distinct colored material (create a couple of URP Lit materials in the same folder), then drag it into the folder to make it a prefab and delete the scene instance.
- Assign the prefab to the matching `ItemDefinition`'s **Prefab** field in the Inspector.

If there are currently **no** `ItemDefinition` assets, create three (`SocialUniverse/Config/ItemDefinition` from the Create menu) — e.g. `Tree` (cost 50), `Statue` (cost 200), `House` (cost 1000) — set their `ItemId`/`DisplayName`/`Cost` and assign placeholder prefabs, and add them to `DatabaseRegistry._items`.

- [ ] **Step 2: Create the `LandBuilding` scene**

- File → New Scene (Basic URP). Save as `Assets/Scenes/LandBuilding.unity`.
- Ensure it has a **Main Camera** (positioned to look down at the plot at a slight angle) and a **Directional Light**.
- Add a ground: a plane or a flat cylinder as the "plot", centered at origin.
- Create an empty `SlotAnchors` GameObject; under it create `PlotSlotCount` (8) empty child transforms named `Slot0`…`Slot7`, arranged in a grid/ring on the plot surface. These are where placed prefabs spawn.
- Add a **UI Canvas** (Screen Space - Overlay) with a **Back** button (bottom-left) and an empty `PaletteRoot` panel (bottom bar) for Task 8.
- Add File → Build Settings → **Add Open Scenes** so `LandBuilding` is in the build list.

- [ ] **Step 3: Create `LandBuildingSceneScope`**

Create `Assets/_Project/Scripts/App/LandBuildingSceneScope.cs`:

```csharp
using UnityEngine;
using VContainer;
using VContainer.Unity;
using SocialUniverse.Config;
using SocialUniverse.Core;
using SocialUniverse.Economy;
using SocialUniverse.Net;

namespace SocialUniverse.App
{
    // Root LifetimeScope for the LandBuilding scene.
    // Production mode: set Parent = RootLifetimeScope so IBackendClient/DatabaseRegistry/
    // EconomyConfig/LandBuildingHandoff come from the parent. Standalone mode (opening
    // LandBuilding.unity directly) registers a mock backend + empty handoff so it doesn't crash.
    public class LandBuildingSceneScope : LifetimeScope
    {
        [SerializeField] private DatabaseRegistry _databaseRegistry;
        [SerializeField] private EconomyConfig    _economyConfig;

        protected override void Configure(IContainerBuilder builder)
        {
            bool standalone = parentReference.Type == null;
            if (standalone)
            {
                builder.RegisterInstance(_databaseRegistry);
                builder.RegisterInstance(_economyConfig);
                builder.Register<LandBuildingHandoff>(Lifetime.Singleton);
                builder.Register<NetworkBootstrap>(Lifetime.Singleton).AsImplementedInterfaces();
                builder.Register<LocalMockAuthService>(Lifetime.Singleton).As<IAuthService>();
                builder.Register<BackendClient>(Lifetime.Singleton).As<IBackendClient>();
            }

            builder.Register<LandBuildService>(Lifetime.Singleton);

            builder.RegisterComponentInHierarchy<SocialUniverse.UI.LandBuildingController>();
            builder.RegisterComponentInHierarchy<SocialUniverse.UI.LandBuildPaletteView>();
        }
    }
}
```

> Note: the two `RegisterComponentInHierarchy` lines reference types created in Tasks 7–8. If you are executing tasks strictly in order and the project won't compile without them, comment those two lines out now and uncomment them at the start of Task 7/8. (They are listed here so the scope's responsibility is complete in one place.)

- [ ] **Step 4: Add the scope to the scene**

- In the `LandBuilding` scene, create an empty GameObject `LandBuildingSceneScope`, add the `LandBuildingSceneScope` component, assign `DatabaseRegistry` + `EconomyConfig` in the Inspector (for standalone), and set its **Parent** field to reference the auto-injected root at runtime (leave Parent unset — VContainer's `LifetimeScope` finds the DontDestroyOnLoad root automatically when one exists, matching how `PlanetSceneScope` runs in production).

- [ ] **Step 5: Verify the scene loads**

Enter Play from Bootstrap is not wired yet (no entry button until Task 9). For now, open `LandBuilding.unity` directly and press Play: confirm no console errors, the camera shows the plot + 8 empty anchors, and the container builds (standalone branch). Report any DI resolution errors.

- [ ] **Step 6: Commit**

```bash
git add Assets/Scenes/LandBuilding.unity Assets/Scenes/LandBuilding.unity.meta \
        Assets/_Project/Scripts/App/LandBuildingSceneScope.cs \
        "Assets/_Project/Prefabs/Buildables" \
        Assets/_Project/ScriptableObjects ProjectSettings/EditorBuildSettings.asset
git add -u
git commit -m "feat(scene): LandBuilding scene, scene scope, placeholder buildable prefabs"
```

---

## Task 7: LandBuildingController — render the plot (view + edit)

Read the handoff, spawn each filled slot's prefab onto its anchor, and wire the Back button. Works in both view and edit mode (edit-only palette/interaction comes in Task 8). Extract the pure "which item goes in which slot" resolution so it's unit-testable.

**Files:**
- Create: `Assets/_Project/Scripts/UI/LandBuildingController.cs`
- Create: `Assets/_Project/Scripts/UI/LandSlotResolver.cs` (pure helper)
- Test: `Assets/_Project/Tests/EditMode/UI/LandSlotResolverTests.cs`

**Interfaces:**
- Consumes: `LandBuildingHandoff` (Core), `DatabaseRegistry.GetItem(itemId)` (Config), `ItemDefinition.Prefab` (Config), `LandBuildingState.Finish()` via resolved state, `EconomyConfig.PlotSlotCount`.
- Produces:
  - `static class LandSlotResolver` with `ItemDefinition Resolve(string itemId, DatabaseRegistry registry)` and `bool CanEdit(LandBuildingHandoff handoff)` helpers.
  - `LandBuildingController : MonoBehaviour` with `[SerializeField] Transform[] _slotAnchors; Button _backButton;` and public `void Render()` (called on scene start).

- [ ] **Step 1: Write the failing test**

Create `Assets/_Project/Tests/EditMode/UI/LandSlotResolverTests.cs`:

```csharp
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using SocialUniverse.Config;
using SocialUniverse.UI;

namespace SocialUniverse.Tests
{
    public class LandSlotResolverTests
    {
        private static void SetField(object t, string f, object v) =>
            t.GetType().GetField(f, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(t, v);

        [Test]
        public void Resolve_returns_matching_item_definition()
        {
            var item = ScriptableObject.CreateInstance<ItemDefinition>();
            SetField(item, "_itemId", "tree");
            var registry = ScriptableObject.CreateInstance<DatabaseRegistry>();
            SetField(registry, "_items", new[] { item });

            var resolved = LandSlotResolver.Resolve("tree", registry);

            Assert.AreSame(item, resolved);

            Object.DestroyImmediate(item);
            Object.DestroyImmediate(registry);
        }

        [Test]
        public void Resolve_returns_null_for_unknown_or_empty_id()
        {
            var registry = ScriptableObject.CreateInstance<DatabaseRegistry>();
            SetField(registry, "_items", new ItemDefinition[0]);

            Assert.IsNull(LandSlotResolver.Resolve("nope", registry));
            Assert.IsNull(LandSlotResolver.Resolve(null, registry));

            Object.DestroyImmediate(registry);
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run `LandSlotResolverTests`. Expected: FAIL to compile — `LandSlotResolver` does not exist. (If `Assets/_Project/Tests/EditMode/UI/` has no asmdef reference to `SocialUniverse.UI`, confirm the EditMode test asmdef references it — it already references `SocialUniverse.UI` because UI tests exist under that folder.)

- [ ] **Step 3: Create `LandSlotResolver`**

Create `Assets/_Project/Scripts/UI/LandSlotResolver.cs`:

```csharp
using SocialUniverse.Config;
using SocialUniverse.Core;

namespace SocialUniverse.UI
{
    // Pure helpers for LandBuildingController — kept out of the MonoBehaviour so they're unit-testable.
    public static class LandSlotResolver
    {
        public static ItemDefinition Resolve(string itemId, DatabaseRegistry registry)
        {
            if (string.IsNullOrEmpty(itemId) || registry == null) return null;
            return registry.GetItem(itemId);
        }

        public static bool CanEdit(LandBuildingHandoff handoff) => handoff != null && handoff.CanEdit;
    }
}
```

- [ ] **Step 4: Create `LandBuildingController`**

Create `Assets/_Project/Scripts/UI/LandBuildingController.cs`:

```csharp
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using VContainer.Unity;
using SocialUniverse.Config;
using SocialUniverse.Core;

namespace SocialUniverse.UI
{
    // Renders a plot's placed decorations from the LandBuildingHandoff and wires the Back button.
    // Edit-mode palette/slot interaction is added by LandBuildPaletteView (registered in the same
    // scene scope). Lives in the UI assembly because it reads Config/Core and must not create a
    // World->Economy cycle.
    public class LandBuildingController : MonoBehaviour, IStartable
    {
        [SerializeField] private Transform[] _slotAnchors;
        [SerializeField] private Button      _backButton;

        [Inject] private LandBuildingHandoff _handoff;
        [Inject] private DatabaseRegistry    _registry;
        [Inject] private IObjectResolver     _resolver;

        private readonly System.Collections.Generic.List<GameObject> _spawned = new();

        public void Start() // IStartable.Start (VContainer entry point)
        {
            _backButton.onClick.AddListener(OnBack);
            Render();
        }

        public void Render()
        {
            foreach (var go in _spawned) if (go != null) Destroy(go);
            _spawned.Clear();

            var slots = _handoff.Slots;
            if (slots == null) return;

            int count = Mathf.Min(slots.Length, _slotAnchors.Length);
            for (int i = 0; i < count; i++)
            {
                var item = LandSlotResolver.Resolve(slots[i], _registry);
                if (item == null || item.Prefab == null) continue;
                var go = Instantiate(item.Prefab, _slotAnchors[i].position, _slotAnchors[i].rotation, _slotAnchors[i]);
                _spawned.Add(go);
            }
        }

        // Public so LandBuildPaletteView can refresh a single anchor after an edit.
        public void SetSlotVisual(int slotIndex, ItemDefinition item)
        {
            if (slotIndex < 0 || slotIndex >= _slotAnchors.Length) return;
            // clear existing child under this anchor
            var anchor = _slotAnchors[slotIndex];
            for (int c = anchor.childCount - 1; c >= 0; c--) Destroy(anchor.GetChild(c).gameObject);
            if (item != null && item.Prefab != null)
                Instantiate(item.Prefab, anchor.position, anchor.rotation, anchor);
        }

        public Transform GetAnchor(int slotIndex) =>
            (slotIndex >= 0 && slotIndex < _slotAnchors.Length) ? _slotAnchors[slotIndex] : null;

        private void OnBack()
        {
            var state = _resolver.Resolve<LandBuildingState>();
            state.Finish();
        }
    }
}
```

- [ ] **Step 5: Register + wire in the scene**

- In `LandBuildingSceneScope.Configure`, ensure the `RegisterComponentInHierarchy<LandBuildingController>()` line is active, and register it as an entry point so `Start()` runs: change that line to also register the entry point. Concretely, use:
  ```csharp
  builder.RegisterComponentInHierarchy<LandBuildingController>();
  builder.RegisterEntryPoint<LandBuildingController>(component => component); // run IStartable.Start
  ```
  If that overload isn't available in this VContainer version, instead drop `IStartable` from the class and call `Render()` + button wiring from Unity's own `Start()` MonoBehaviour message (rename the method to `void Start()` MonoBehaviour message and keep `[Inject]` field injection via `RegisterComponentInHierarchy`, which injects before `Start`). Prefer this simpler MonoBehaviour-`Start` approach if unsure.
- Add the `LandBuildingController` component to a GameObject in the scene, assign the 8 `Slot0..7` transforms to `_slotAnchors` and the Back button to `_backButton`.

> Simplest reliable wiring: make `LandBuildingController` a plain `MonoBehaviour` (not `IStartable`), keep `[Inject]` fields, register only via `RegisterComponentInHierarchy`, and rename `public void Start()` to the Unity `void Start()` message. VContainer injects `[Inject]` fields at container build (before `Start`).

- [ ] **Step 6: Run tests + manual verify**

Run `LandSlotResolverTests` — Expected: PASS (2 tests). Then, to manually verify rendering before the entry button exists, temporarily populate the handoff: in `LandBuildingSceneScope` standalone branch add (temporarily) `handoff.Begin("t","earth","me",true,new[]{"tree",null,"house",null,null,null,null,null},500)` after resolving it — or just confirm empty render shows no errors. Open `LandBuilding.unity`, Play, confirm prefabs spawn on the right anchors and Back logs/attempts a transition. Remove any temporary handoff seeding.

- [ ] **Step 7: Commit**

```bash
git add Assets/_Project/Scripts/UI/LandBuildingController.cs Assets/_Project/Scripts/UI/LandSlotResolver.cs \
        Assets/_Project/Tests/EditMode/UI/LandSlotResolverTests.cs \
        Assets/_Project/Scripts/App/LandBuildingSceneScope.cs Assets/Scenes/LandBuilding.unity
git add -u
git commit -m "feat(ui): LandBuildingController renders plot layout + Back navigation"
```

---

## Task 8: LandBuildPaletteView — edit-mode place / remove / move

In edit mode, show the affordable-item palette and make slots interactive: place into an empty slot, remove a filled slot, move between slots. Each successful server op updates the handoff's working `Slots` (via `LandBuildMath`) and re-renders that anchor. View mode hides the palette entirely.

**Files:**
- Create: `Assets/_Project/Scripts/UI/LandBuildPaletteView.cs`
- Modify: `Assets/_Project/Scripts/App/LandBuildingSceneScope.cs` (ensure it's registered)

**Interfaces:**
- Consumes: `LandBuildingHandoff`, `LandBuildService` (Task 3), `BuildPaletteService` (Task 4), `DatabaseRegistry`, `EconomyConfig`, `LandBuildingController` (Task 7, for `SetSlotVisual`/`GetAnchor`), `LandBuildMath` (Task 1).
- Produces: `LandBuildPaletteView : MonoBehaviour` (scene component; no new public API consumed by later tasks).

- [ ] **Step 1: Create `LandBuildPaletteView`**

Create `Assets/_Project/Scripts/UI/LandBuildPaletteView.cs`:

```csharp
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using TMPro;
using SocialUniverse.Config;
using SocialUniverse.Core;
using SocialUniverse.Economy;
using SocialUniverse.World;

namespace SocialUniverse.UI
{
    // Edit-mode UI for a plot: an affordable-item palette plus slot tap-targets that place,
    // remove, or move decorations. Hidden entirely in view mode (visitor). All economy/slot
    // mutations go through LandBuildService (server-authoritative); the handoff's working Slots
    // are updated locally on success so the plot reflects the change immediately.
    public class LandBuildPaletteView : MonoBehaviour
    {
        [SerializeField] private GameObject   _paletteRoot;     // bottom bar; disabled in view mode
        [SerializeField] private Transform    _itemButtonParent;
        [SerializeField] private Button       _itemButtonPrefab; // a button with a child TMP_Text
        [SerializeField] private Button[]     _slotButtons;      // one per slot; screen-space hit targets
        [SerializeField] private TMP_Text     _statusText;

        [Inject] private LandBuildingHandoff  _handoff;
        [Inject] private LandBuildService     _buildService;
        [Inject] private BuildPaletteService  _palette;
        [Inject] private DatabaseRegistry     _registry;
        [Inject] private EconomyConfig        _config;
        [Inject] private LandBuildingController _controller;

        private ItemDefinition _selectedItem;
        private int            _localCoins;

        private void Start()
        {
            _localCoins = _handoff.Coins;

            bool canEdit = _handoff.CanEdit;
            _paletteRoot.SetActive(canEdit);
            if (!canEdit) return;

            BuildPalette();
            for (int i = 0; i < _slotButtons.Length; i++)
            {
                int index = i;
                _slotButtons[i].onClick.AddListener(() => OnSlotClicked(index));
            }
        }

        private void BuildPalette()
        {
            foreach (Transform c in _itemButtonParent) Destroy(c.gameObject);

            // Build a throwaway TileData describing this owned plot for the palette rule.
            var tile = new TileData(_handoff.TileId)
            {
                State      = TileState.OwnedByPlayer,
                BuildLevel = LandBuildMath.FilledCount(_handoff.Slots),
            };

            foreach (var item in _palette.GetAvailableItems(tile, _localCoins))
            {
                var btn = Instantiate(_itemButtonPrefab, _itemButtonParent);
                var label = btn.GetComponentInChildren<TMP_Text>();
                if (label != null) label.text = $"{item.DisplayName}\n{item.Cost}";
                var captured = item;
                btn.onClick.AddListener(() => _selectedItem = captured);
            }
        }

        private async void OnSlotClicked(int slotIndex)
        {
            var slots = _handoff.Slots;
            bool empty = LandBuildMath.IsEmpty(slots, slotIndex);

            if (empty)
            {
                if (_selectedItem == null) { _statusText.text = "Pick an item first"; return; }
                if (_selectedItem.Cost > _localCoins) { _statusText.text = "Not enough coins"; return; }

                var result = await _buildService.PlaceAsync(_handoff.TileId, _handoff.PlanetId, slotIndex, _selectedItem.ItemId, _selectedItem.Cost);
                if (!result.Success) { _statusText.text = $"Place failed: {result.Reason}"; return; }

                slots[slotIndex] = _selectedItem.ItemId;
                if (result.NewBalance >= 0) _localCoins = result.NewBalance;
                _controller.SetSlotVisual(slotIndex, _selectedItem);
                _statusText.text = "";
                BuildPalette(); // affordability may have changed
            }
            else
            {
                // Filled slot tapped → remove it. (Move is available via a long-press/drag in a
                // later pass; v1 exposes remove, then re-place, which is functionally complete.)
                var result = await _buildService.RemoveAsync(_handoff.TileId, _handoff.PlanetId, slotIndex);
                if (!result.Success) { _statusText.text = $"Remove failed: {result.Reason}"; return; }

                slots[slotIndex] = null;
                _controller.SetSlotVisual(slotIndex, null);
                _statusText.text = "";
                BuildPalette();
            }
        }
    }
}
```

> Scope note: `MoveBuild` / `LandBuildService.MoveAsync` are implemented (Tasks 2–3) but this v1 view exposes **place + remove** only; "move" is remove-then-replace from the player's point of view. A drag-to-move gesture is a deferred follow-up (spec §6). This keeps the first slice shippable without a drag system.

- [ ] **Step 2: Register + wire in the scene**

- Confirm `LandBuildingSceneScope` has `builder.RegisterComponentInHierarchy<LandBuildPaletteView>();` (from Task 6) active.
- Add the `LandBuildPaletteView` component to the Canvas. Create: a `PaletteRoot` panel (bottom bar), an `ItemButton` prefab (a Button with a child TMP_Text) assigned to `_itemButtonPrefab`, an empty horizontal-layout `_itemButtonParent` under `PaletteRoot`, a `_statusText` TMP label, and 8 transparent full-anchor `_slotButtons` (screen-space buttons positioned over each `Slot0..7` world anchor — simplest: place a small button near each anchor, or use one `Button` per anchor as an on-screen overlay). Assign all serialized fields.

- [ ] **Step 3: Manual verify (edit + view)**

Because this needs a populated handoff, verify end-to-end in Task 9. For an isolated check now: in `LandBuildingSceneScope`'s standalone branch, temporarily seed the handoff with `CanEdit=true` and an empty slots array + some coins, open `LandBuilding.unity`, Play, and confirm: the palette lists affordable items; tapping an item then an empty slot spawns a prefab (server call will fail in standalone mock — expect a "Place failed" status unless the mock backend returns success; that's fine for the UI wiring check); flipping `CanEdit=false` hides the palette. Remove the temporary seeding.

- [ ] **Step 4: Commit**

```bash
git add Assets/_Project/Scripts/UI/LandBuildPaletteView.cs Assets/_Project/Scripts/App/LandBuildingSceneScope.cs Assets/Scenes/LandBuilding.unity
git add -u
git commit -m "feat(ui): LandBuildPaletteView edit-mode place/remove; view-mode hides palette"
```

---

## Task 9: Entry point — "View Land" button + end-to-end verification

Add the "View Land" button to `TileInfoModal` (shown for owned tiles, yours or others'), publishing `ViewLandRequestedEvent` with the tile's current slots, the correct `CanEdit`, and the player's coins. Then verify the whole loop end-to-end.

**Files:**
- Modify: `Assets/_Project/Scripts/UI/TileInfoModal.cs`
- Modify: `Assets/Scenes/Planet.unity` (add the button to the TileInfoModal prefab/object + assign it)

**Interfaces:**
- Consumes: `LandRegistryService.GetEntry(tileId).Slots` (Economy), `Wallet.Coins` (Economy), `EventBus.Publish` + `ViewLandRequestedEvent` (Core), `TileData.State`/`OwnerId` (World).
- Produces: reachable LandBuilding flow.

- [ ] **Step 1: Inject `Wallet` and add the button field to `TileInfoModal`**

In `Assets/_Project/Scripts/UI/TileInfoModal.cs`:
- Add a serialized field near the other buttons:
  ```csharp
  [SerializeField] private Button _viewLandButton;
  ```
- Add an inject for the wallet near the other `[Inject]` fields:
  ```csharp
  [Inject] private Wallet _wallet;
  ```
  (`Wallet.Coins` is the verified getter — `public int Coins { get; private set; }`. `SocialUniverse.Economy` is already imported in this file.)

- [ ] **Step 2: Wire the button in `Awake` and `Open`**

- In `Awake()`, add the listener and default-hide:
  ```csharp
  _viewLandButton.onClick.AddListener(OnViewLandClicked);
  ```
- In `Open(TileData tile)`, after `bool ownedByPlayer = ...`, show the button for any owned tile (player or other), hide for landmark/available:
  ```csharp
  bool owned = tile.State == TileState.OwnedByPlayer || tile.State == TileState.OwnedByOther;
  _viewLandButton.gameObject.SetActive(owned);
  ```

- [ ] **Step 3: Add the click handler**

Add to `TileInfoModal`:

```csharp
        private void OnViewLandClicked()
        {
            if (_currentTile == null) return;
            _audio.PlaySfx(SfxId.OpenPanel);

            var entry = _landRegistryService.GetEntry(_currentTile.TileId);
            var slots = LandBuildMath.EnsureSize(entry?.Slots, _economyConfig.PlotSlotCount);

            EventBus.Publish(new ViewLandRequestedEvent
            {
                TileId  = _currentTile.TileId,
                OwnerId = _currentTile.OwnerId,
                CanEdit = _currentTile.State == TileState.OwnedByPlayer,
                Slots   = slots,
                Coins   = _wallet.Coins,
            });

            Close();
        }
```

(`_landRegistryService` and `_economyConfig` are already injected in `TileInfoModal`. `LandBuildMath` is in `SocialUniverse.Economy`, already imported. `SocialUniverse.Core` is already imported for `EventBus`.)

- [ ] **Step 4: Add + assign the button in the Planet scene**

Open `Assets/Scenes/Planet.unity`, find the TileInfoModal object, add a **View Land** `Button` to its layout, and assign it to the modal's `_viewLandButton` field. Save the scene.

- [ ] **Step 5: Compile + run the full EditMode suite**

Run the entire EditMode test suite. Expected: all green, including `LandBuildMathTests`, `LandBuildServiceTests`, `BuildPaletteServiceTests`, `LandBuildingHandoffTests`, `LandSlotResolverTests`. Confirm no compile errors.

- [ ] **Step 6: End-to-end manual verification (requires deployed server functions from Task 2)**

From Bootstrap, Play through to a planet. Then:
1. **Own a tile**, tap it → TileInfoModal → **View Land** → LandBuilding scene loads in **edit mode** (palette visible). Place an item into a slot → prefab appears, coins drop. Tap the filled slot → item removed. Tap **Back** → returns to the planet; re-open TileInfoModal on that tile → build level reflects the net placements (Planet re-hydrated the registry on entry).
2. **Another player's owned tile** (or a second test account's tile), tap it → **View Land** → LandBuilding scene loads in **view mode** (no palette); their placed items render. **Back** returns to the planet.
3. **An available (unowned) tile** shows the purchase flow and **no** View Land button.

Record the result (pass/fail per step) in the PR description.

- [ ] **Step 7: Commit**

```bash
git add Assets/_Project/Scripts/UI/TileInfoModal.cs Assets/Scenes/Planet.unity
git commit -m "feat(ui): View Land entry point on TileInfoModal -> LandBuilding scene"
```

---

## Self-Review

**Spec coverage:**
- §1 Data model (Slots, derived BuildLevel, PlotSlotCount, ItemDefinition.Prefab, BuildPaletteService rework) → Tasks 1, 4. ✓
- §2 Server functions (PlaceBuild rework, RemoveBuild, MoveBuild, GetLandRegistry carries slots) → Task 2. ✓ (GetLandRegistry needs no change — it returns entries verbatim, now with `slots`.)
- §3 Scene flow & FSM (LandBuilding scene, LandBuildingState, LandBuildingHandoff, entry via TileInfoModal + ViewLandRequestedEvent + handler) → Tasks 5, 6, 9. ✓
- §4 Scene behaviour (controller render, view vs edit, palette, optimistic update, placeholder art) → Tasks 6, 7, 8. ✓
- §5 Testing (LandBuildMath, LandBuildService w/ fake backend, palette rework, edit-vs-view gating via CanEdit, handoff, slot resolver; manual scene repro) → Tasks 1, 3, 4, 5, 7, 9. ✓
- §6 Deferred scope respected — no multi-layout, no unlock gating, no refunds, no drag-move gesture (MoveBuild exists but UI exposes place/remove). ✓

**Placeholder scan:** No "TBD"/"implement later"/vague-error steps. Scene-authoring steps give concrete editor actions. The one conditional (Task 7 IStartable vs MonoBehaviour-Start wiring) resolves to an explicit recommended path (plain MonoBehaviour `Start`). ✓

**Type consistency:**
- `PlaceBuildResult`/`RemoveBuildResult`/`MoveBuildResult` fields match between Task 3 (client) and Task 2 (server return JSON: `success`/`reason`/`newBalance`/`buildLevel`). ✓ (Serializer maps camelCase↔PascalCase as it already does for `OwnerId`/`BuildLevel`.)
- `LandBuildingHandoff.Begin(tileId, planetId, ownerId, canEdit, slots, coins)` signature identical in Tasks 5 (def), 5 handler, and consumed in 7/8. ✓
- `LandBuildMath.EnsureSize/FilledCount/IsEmpty` signatures consistent across Tasks 1, 7 (via resolver), 8, 9. ✓
- `BuildPaletteService.GetAvailableItems(TileData, int)` consistent between Task 4 (def/test) and Task 8 (call). ✓
- `LandBuildingController.SetSlotVisual(int, ItemDefinition)` / `GetAnchor(int)` defined in Task 7, consumed in Task 8. ✓
- `DatabaseRegistry.GetItem(itemId)` verified to exist. `ItemDefinition.Prefab`/`Cost`/`ItemId`/`DisplayName` verified. `Constants.SceneNames.LandBuilding` added Task 5, used Task 5 state. ✓

All referenced existing APIs verified against the codebase: `Wallet.Coins`, `DatabaseRegistry.GetItem`/`AllItems`, `ItemDefinition.Prefab`/`Cost`/`ItemId`/`DisplayName`, `LandRegistryService.GetEntry`, `ActiveMiningState`/`ActiveMiningHandoff`/`ActiveMiningRequestHandler` patterns, and the `PlanetSceneScope` production-block registration point.
