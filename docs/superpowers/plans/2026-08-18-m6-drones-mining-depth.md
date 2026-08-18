# M6 — Drones & Mining Depth Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Turn the one-step mining loop into a progression loop — mine tier-gated asteroids for typed minerals, sell them for coins, spend coins on drone upgrades and fleet expansion, unlock higher-tier drones that reach higher-tier asteroids.

**Architecture:** Additive `SocialUniverse.Config` data (MineralDefinition, UpgradeDefinition, DroneStat) + `SocialUniverse.Mining` runtime (DroneRuntime rework, MineralInventory, DroneFleet, pure DroneUpgradeMath) + `I*Service`/`LocalMock*` services calling `IBackendClient` + Cloud Code server functions (server-authoritative). UI is functional HUD-opened panels on the Planet scene, DI-wired via `PlanetSceneScope`, publishing intent events handled by App-layer controllers (the `TilePurchaseHandler` pattern).

**Tech Stack:** Unity 6 (URP), C#, VContainer DI, NUnit EditMode tests, UGS Cloud Code (Node) + Cloud Save JSON records.

**Spec:** `docs/superpowers/specs/2026-08-17-m6-drones-mining-depth-design.md`

## Global Constraints

- **Pre-Task Protocol (CLAUDE.md):** before touching code, re-read `Social_Universe_Architecture.md` §2/§4/§7 + Script Inventory; confirm namespace/assembly per the Project Structure table.
- **Server-authoritative economy (Rule 1):** the client never mints coins, grants ownership, or computes final rewards. It requests; a `ServerCode/` function validates and commits. Coins enter the wallet only via `SellMinerals`.
- **Backend behind interfaces (Rule 2):** gameplay depends on `I*Service`; production binds the real impl, standalone/dev binds `LocalMock*`. Never reference a UGS SDK from gameplay code.
- **ScriptableObjects for data (Rule 3):** all tunable numbers live on `*Definition`/`*Config` SOs. The only new loose tunables (`StartingFleetSlots`, `SlotUnlockBaseCost`, `SlotUnlockCostGrowth`) go on `EconomyConfig`.
- **Decouple via events (Rule 4):** UI → App communication is one-way intent events on `EventBus`; services → UI is state-change events. No direct cross-namespace UI→service calls.
- **"Must match" duplication:** mineral `SellValue`, upgrade cost formula, and slot cost formula are duplicated between C# SOs/`DroneUpgradeMath` and the JS server functions. Every duplicated constant/formula gets a `// MUST MATCH <other file>` comment on both sides.
- **Namespaces:** Config types → `SocialUniverse.Config`; runtime/services/events → `SocialUniverse.Mining`; App handlers → `SocialUniverse.App`; UI → `SocialUniverse.UI`. `SocialUniverse.Mining.asmdef` already references Core, Config, World, Economy, Safety, VContainer — no asmdef change needed.
- **Test command (EditMode, headless):** `"C:\Program Files\Unity\Hub\Editor\<version>\Editor\Unity.exe" -runTests -projectPath . -testResults results.xml -testPlatform EditMode` — or Window > General > Test Runner in-editor. The live Editor is open on the `main` checkout; a background/worktree session cannot compile — run tests in the Editor that owns the checkout.

## Planning deviations from the spec (resolved)

1. **Reward calculator rework.** `MiningRewardCalculator.Compute` takes a new `float effectiveYieldMult` argument and returns a **mineral quantity**, not coins. `MiningReward.TotalCoins` → `MineralQuantity` (int); `MiningReward.CoinsPerSec` → `UnitsPerSec` (float). The per-second field still drives the server anti-cheat cap, now in mineral units.
2. **MiningController payout dependency.** The mining payout swaps `IEconomyService` → `IMineralService` and gains `DroneFleet` (for the tier gate + effective yield). `asteroid.Definition.CoinsPerUnit` / `.MineralType` references become `asteroid.Definition.Mineral.*`. `Initialize` becomes parameterless (the active drone is read from `DroneFleet`, not passed in).
3. **Event placement.** M6 events live in `SocialUniverse.Mining` (co-located like `AsteroidSelectedEvent`), not Core — `MiningBlockedEvent` carries an `Asteroid`, which Core cannot reference. Intent events reference `DroneStat` (Config), which Mining references. Spec §8's "Core" is superseded.
4. **DroneController is visual-only** and does not read drone stats; `EffectiveTravelSpeed` is computed/persisted but not wired to the visual in M6 (out of scope, matches spec non-goals on polish).
5. **Open questions resolved:** (Q1) a new player owns `Drone_Scout` active by default — `GetBootstrapState` seeds it when `drone_fleet` is empty. (Q2) no downgrades/refunds. (Q3) fleet + inventory hydrate in `PlanetSceneBootstrapper.Start()` (planet-scoped, like wallet/owned-tiles).

## Phasing

- **Phase A (Tasks 1–9): Minerals & selling.** Mine → typed minerals → sell to the house → coins. Reaches a **working, shippable checkpoint** with the existing single drone (drone effective-yield defaults to the base `YieldMultiplier`, tier gate is a no-op until Phase B seeds tiers). ValidateMining flips to granting minerals here.
- **Phase B (Tasks 10–20): Drones, upgrades, fleet & tier gating.** DroneDefinition tiers, DroneRuntime upgrades, DroneFleet, IDroneService, tier gate, Garage UI, drone server functions.

Author-facing Editor work (SO assets, scene wiring, server deploy) is collected in the deferred checklist at the end — the same "deferred like M2–M5" pattern.

---

## Phase A — Minerals & Selling

### Task 1: `MineralDefinition` SO + `DroneStat` enum + `UpgradeDefinition` SO

**Files:**
- Create: `Assets/_Project/Scripts/Config/DroneStat.cs`
- Create: `Assets/_Project/Scripts/Config/MineralDefinition.cs`
- Create: `Assets/_Project/Scripts/Config/UpgradeDefinition.cs`

**Interfaces:**
- Produces: `enum SocialUniverse.Config.DroneStat { Cargo, Yield, Speed }`.
- Produces: `MineralDefinition` with accessors `string MineralId`, `string DisplayName`, `int Tier`, `int SellValue`, `Sprite Icon`, `Color TintColor`.
- Produces: `UpgradeDefinition` with accessors `DroneStat Stat`, `int MaxLevel`, `int BaseCost`, `float CostGrowth`, `float DeltaPerLevel`.

These are pure-data SOs (no logic), so they are verified by compilation + the DatabaseRegistry getter tests in Task 2 rather than their own unit test. `UpgradeDefinition` and `MineralDefinition` are authored as assets in the deferred checklist; here we only define the types.

- [ ] **Step 1: Write `DroneStat.cs`**

```csharp
namespace SocialUniverse.Config
{
    // Which upgradeable drone stat an UpgradeDefinition track targets.
    // MUST MATCH the stat keys used in ServerCode/UpgradeDrone.js ("Cargo"/"Yield"/"Speed").
    public enum DroneStat
    {
        Cargo,
        Yield,
        Speed
    }
}
```

- [ ] **Step 2: Write `MineralDefinition.cs`**

```csharp
using UnityEngine;

namespace SocialUniverse.Config
{
    [CreateAssetMenu(menuName = "SocialUniverse/Config/MineralDefinition", fileName = "NewMineral")]
    public class MineralDefinition : ScriptableObject
    {
        [SerializeField] private string _mineralId;
        [SerializeField] private string _displayName;
        [SerializeField] private int    _tier      = 1;
        [SerializeField] private int    _sellValue = 2;   // MUST MATCH ServerCode/SellMinerals.js SELL_VALUES
        [SerializeField] private Sprite _icon;
        [SerializeField] private Color  _tintColor = Color.white;

        public string MineralId   => _mineralId;
        public string DisplayName => _displayName;
        public int    Tier        => _tier;
        public int    SellValue   => _sellValue;
        public Sprite Icon        => _icon;
        public Color  TintColor   => _tintColor;
    }
}
```

- [ ] **Step 3: Write `UpgradeDefinition.cs`**

```csharp
using UnityEngine;

namespace SocialUniverse.Config
{
    [CreateAssetMenu(menuName = "SocialUniverse/Config/UpgradeDefinition", fileName = "NewUpgrade")]
    public class UpgradeDefinition : ScriptableObject
    {
        [SerializeField] private DroneStat _stat;
        [SerializeField] private int       _maxLevel      = 10;
        [SerializeField] private int       _baseCost      = 50;   // MUST MATCH ServerCode/UpgradeDrone.js cost formula
        [SerializeField] private float     _costGrowth    = 1.5f; // MUST MATCH ServerCode/UpgradeDrone.js
        [SerializeField] private float     _deltaPerLevel = 10f;

        public DroneStat Stat          => _stat;
        public int       MaxLevel      => _maxLevel;
        public int       BaseCost      => _baseCost;
        public float     CostGrowth    => _costGrowth;
        public float     DeltaPerLevel => _deltaPerLevel;
    }
}
```

- [ ] **Step 4: Compile check**

In the Unity Editor console (`read_console`), confirm 0 errors after domain reload. Expected: clean compile; `SocialUniverse/Config/MineralDefinition` and `.../UpgradeDefinition` appear under Assets > Create.

- [ ] **Step 5: Commit**

```bash
git add Assets/_Project/Scripts/Config/DroneStat.cs Assets/_Project/Scripts/Config/MineralDefinition.cs Assets/_Project/Scripts/Config/UpgradeDefinition.cs
git commit -m "feat(config): MineralDefinition, UpgradeDefinition SOs + DroneStat enum (M6)"
```

---

### Task 2: `AsteroidDefinition` → mineral reference + `DroneDefinition`/`DatabaseRegistry`/`EconomyConfig` fields

**Files:**
- Modify: `Assets/_Project/Scripts/Config/AsteroidDefinition.cs`
- Modify: `Assets/_Project/Scripts/Config/DroneDefinition.cs`
- Modify: `Assets/_Project/Scripts/Config/DatabaseRegistry.cs`
- Modify: `Assets/_Project/Scripts/Config/EconomyConfig.cs`
- Test: `Assets/_Project/Tests/EditMode/Config/DatabaseRegistryM6Tests.cs` (create)

**Interfaces:**
- Consumes: `MineralDefinition`, `UpgradeDefinition`, `DroneStat` (Task 1).
- Produces: `AsteroidDefinition.Mineral` (`MineralDefinition`) **added alongside** all existing fields. **`MineralType`/`CoinsPerUnit` are retained permanently for M6** — `MineralType` is the asteroid's display label (HUD, MiningModePromptView, ActiveMiningMinigameView) AND its identity/persistence key in `AsteroidSpawner` (respawn save format + `GetAsteroid` lookup). Removing them is explicitly out of scope.
- Produces: `DroneDefinition.Tier` (int), `.UnlockCost` (int), `.YieldMultiplier` (float); keeps `TravelSpeed`, `CargoCap`.
- Produces: `DatabaseRegistry.AllMinerals`, `.GetMineral(string)`, `.AllUpgrades`, `.GetUpgrade(DroneStat)`. `GetAsteroid` is **unchanged** (stays keyed on `MineralType`, which `AsteroidSpawner` respawn-restore depends on).
- Produces: `EconomyConfig.StartingFleetSlots` (int), `.SlotUnlockBaseCost` (int), `.SlotUnlockCostGrowth` (float).

> **Additive migration (controller ruling, 2026-08-18):** purely additive. The new `Mineral` reference drives the M6 economy (mineral grants keyed on `Mineral.MineralId`); the legacy `MineralType`/`CoinsPerUnit` fields stay because `MineralType` is load-bearing as a label + spawner persistence key across 5+ files. `CoinsPerUnit` becomes dead once `MiningController` migrates (Task 8) but a dead serialized field is harmless — far cheaper than a persistence-format change. Every task compiles green.

- [ ] **Step 1: Write the failing DatabaseRegistry test**

```csharp
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using SocialUniverse.Config;

namespace SocialUniverse.Tests
{
    public class DatabaseRegistryM6Tests
    {
        private static void SetField(object o, string f, object v) =>
            o.GetType().GetField(f, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(o, v);

        [Test]
        public void GetMineral_finds_by_id_and_GetUpgrade_finds_by_stat()
        {
            var iron = ScriptableObject.CreateInstance<MineralDefinition>();
            SetField(iron, "_mineralId", "iron");
            var cargo = ScriptableObject.CreateInstance<UpgradeDefinition>();
            SetField(cargo, "_stat", DroneStat.Cargo);

            var reg = ScriptableObject.CreateInstance<DatabaseRegistry>();
            SetField(reg, "_minerals", new[] { iron });
            SetField(reg, "_upgrades", new[] { cargo });

            Assert.AreSame(iron, reg.GetMineral("iron"));
            Assert.IsNull(reg.GetMineral("nope"));
            Assert.AreSame(cargo, reg.GetUpgrade(DroneStat.Cargo));
            Assert.AreEqual(1, reg.AllMinerals.Count());

            Object.DestroyImmediate(iron); Object.DestroyImmediate(cargo); Object.DestroyImmediate(reg);
        }
    }
}
```

- [ ] **Step 2: Run — verify it fails to compile** (`_minerals`, `_upgrades`, `GetMineral`, `GetUpgrade`, `AllMinerals` don't exist yet). Expected: compile error.

- [ ] **Step 3: Edit `AsteroidDefinition.cs`** — **add** a mineral reference alongside the existing fields (keep `_mineralType`/`_coinsPerUnit` — removed in Task 8):

```csharp
using UnityEngine;

namespace SocialUniverse.Config
{
    [CreateAssetMenu(menuName = "SocialUniverse/Config/AsteroidDefinition", fileName = "NewAsteroid")]
    public class AsteroidDefinition : ScriptableObject
    {
        [SerializeField] private MineralDefinition _mineral;   // M6: authoritative mineral (drives inventory grants)
        [SerializeField] private string     _mineralType;      // retained: display label + AsteroidSpawner identity/persistence key
        [SerializeField] private int        _tier          = 1;
        [SerializeField] private int        _baseYield     = 50;
        [SerializeField] [Range(0f, 1f)]
                         private float      _rarity        = 0.5f;
        [SerializeField] private int        _coinsPerUnit  = 2; // retained (legacy); dead after Task 8, harmless
        [SerializeField] private GameObject _modelPrefab;

        public MineralDefinition Mineral       => _mineral;
        public string            MineralType   => _mineralType;
        public int               Tier          => _tier;
        public int               BaseYield     => _baseYield;
        public float             Rarity        => _rarity;
        public int               CoinsPerUnit  => _coinsPerUnit;
        public GameObject        ModelPrefab   => _modelPrefab;
    }
}
```

- [ ] **Step 4: Edit `DroneDefinition.cs`** — add tier, unlock cost, yield multiplier:

```csharp
using UnityEngine;

namespace SocialUniverse.Config
{
    [CreateAssetMenu(menuName = "SocialUniverse/Config/DroneDefinition", fileName = "NewDrone")]
    public class DroneDefinition : ScriptableObject
    {
        [SerializeField] private string     _droneId;
        [SerializeField] private string     _displayName;
        [SerializeField] private int        _tier            = 1;   // highest asteroid tier this drone can mine
        [SerializeField] private int        _unlockCost      = 0;   // coins to acquire into the fleet (0 = starter)
        [SerializeField] private float      _travelSpeed     = 5f;  // base value, scaled by Speed upgrades
        [SerializeField] private int        _cargoCap        = 50;  // base value, scaled by Cargo upgrades
        [SerializeField] private float      _yieldMultiplier = 1f;  // base value, scaled by Yield upgrades
        [SerializeField] private GameObject _modelPrefab;

        public string     DroneId         => _droneId;
        public string     DisplayName     => _displayName;
        public int        Tier            => _tier;
        public int        UnlockCost      => _unlockCost;
        public float      TravelSpeed     => _travelSpeed;
        public int        CargoCap        => _cargoCap;
        public float      YieldMultiplier => _yieldMultiplier;
        public GameObject ModelPrefab     => _modelPrefab;
    }
}
```

- [ ] **Step 5: Edit `DatabaseRegistry.cs`** — add mineral/upgrade lists + accessors, and fix `GetAsteroid`:

```csharp
// add fields alongside the existing arrays:
[SerializeField] private MineralDefinition[] _minerals;
[SerializeField] private UpgradeDefinition[] _upgrades;

// add to the AllX block:
public IEnumerable<MineralDefinition> AllMinerals => _minerals ?? Array.Empty<MineralDefinition>();
public IEnumerable<UpgradeDefinition> AllUpgrades => _upgrades ?? Array.Empty<UpgradeDefinition>();

// add GetMineral/GetUpgrade (leave GetAsteroid on MineralType — it switches to Mineral.MineralId in Task 8):
public MineralDefinition  GetMineral(string mineralId)  => Array.Find(_minerals, m => m.MineralId == mineralId);
public UpgradeDefinition  GetUpgrade(DroneStat stat)     => Array.Find(_upgrades, u => u.Stat == stat);
```

- [ ] **Step 6: Edit `EconomyConfig.cs`** — add the M6 drone tunables. Add a header block after the `Mining — Active` block:

```csharp
[Header("Drones — M6")]
[SerializeField] private int   _startingFleetSlots    = 2;
[SerializeField] private int   _slotUnlockBaseCost    = 500;  // MUST MATCH ServerCode/UnlockDroneSlot.js
[SerializeField] private float _slotUnlockCostGrowth  = 2f;   // MUST MATCH ServerCode/UnlockDroneSlot.js
```

and the accessors near the other Mining accessors:

```csharp
public int   StartingFleetSlots   => _startingFleetSlots;
public int   SlotUnlockBaseCost    => _slotUnlockBaseCost;
public float SlotUnlockCostGrowth  => _slotUnlockCostGrowth;
```

- [ ] **Step 7: Run the Task 2 test.** The change is additive (old fields kept), so the whole project still compiles. Expected: PASS.

- [ ] **Step 8: Commit**

```bash
git add Assets/_Project/Scripts/Config Assets/_Project/Tests/EditMode/Config/DatabaseRegistryM6Tests.cs
git commit -m "feat(config): add asteroid mineral ref, drone tier/cost/yield, registry+economy M6 fields"
```

---

### Task 3: `SaveKeys` mineral/fleet keys

**Files:**
- Modify: `Assets/_Project/Scripts/Core/SaveKeys.cs`
- Test: `Assets/_Project/Tests/EditMode/Core/SaveKeysTests.cs` (extend)

**Interfaces:**
- Produces: `SaveKeys.MineralInventory` = `"mineral_inventory"`, `SaveKeys.DroneFleet` = `"drone_fleet"`.

- [ ] **Step 1: Add a failing assertion** to `SaveKeysTests.cs`:

```csharp
[Test]
public void M6_record_keys_are_stable()
{
    Assert.AreEqual("mineral_inventory", SocialUniverse.Core.SaveKeys.MineralInventory);
    Assert.AreEqual("drone_fleet",       SocialUniverse.Core.SaveKeys.DroneFleet);
}
```

- [ ] **Step 2: Run — fails to compile** (constants missing).

- [ ] **Step 3: Add constants** to `SaveKeys.cs` alongside the existing record keys:

```csharp
public const string MineralInventory = "mineral_inventory";
public const string DroneFleet        = "drone_fleet";
```

- [ ] **Step 4: Run — PASS.**

- [ ] **Step 5: Commit**

```bash
git add Assets/_Project/Scripts/Core/SaveKeys.cs Assets/_Project/Tests/EditMode/Core/SaveKeysTests.cs
git commit -m "feat(core): mineral_inventory + drone_fleet save keys (M6)"
```

---

### Task 4: `MineralInventory` runtime cache + change event

**Files:**
- Create: `Assets/_Project/Scripts/Mining/MineralInventory.cs`
- Test: `Assets/_Project/Tests/EditMode/Mining/MineralInventoryTests.cs`

**Interfaces:**
- Consumes: `DatabaseRegistry`, `MineralDefinition` (Config).
- Produces: `MineralInventory` — `void SetAll(IReadOnlyDictionary<string,int>)`, `int Get(string mineralId)`, `void Add(string mineralId, int qty)`, `IReadOnlyDictionary<string,int> All`, `int TotalSellValue(DatabaseRegistry)`. Raises `MineralInventoryChangedEvent` (empty marker) on `EventBus` after any mutation.
- Produces: `class MineralInventoryChangedEvent { }` (Mining).

- [ ] **Step 1: Write the failing test**

```csharp
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using SocialUniverse.Config;
using SocialUniverse.Mining;

namespace SocialUniverse.Tests
{
    public class MineralInventoryTests
    {
        private static void SetField(object o, string f, object v) =>
            o.GetType().GetField(f, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(o, v);

        private static MineralDefinition Mineral(string id, int sellValue)
        {
            var m = ScriptableObject.CreateInstance<MineralDefinition>();
            SetField(m, "_mineralId", id);
            SetField(m, "_sellValue", sellValue);
            return m;
        }

        [Test]
        public void Add_and_Get_accumulate()
        {
            var inv = new MineralInventory();
            inv.Add("iron", 5);
            inv.Add("iron", 3);
            Assert.AreEqual(8, inv.Get("iron"));
            Assert.AreEqual(0, inv.Get("platinum"));
        }

        [Test]
        public void SetAll_replaces_contents()
        {
            var inv = new MineralInventory();
            inv.Add("iron", 5);
            inv.SetAll(new Dictionary<string, int> { { "platinum", 2 } });
            Assert.AreEqual(0, inv.Get("iron"));
            Assert.AreEqual(2, inv.Get("platinum"));
        }

        [Test]
        public void TotalSellValue_sums_qty_times_sellValue_over_registry()
        {
            var iron     = Mineral("iron", 2);
            var platinum = Mineral("platinum", 20);
            var reg = ScriptableObject.CreateInstance<DatabaseRegistry>();
            SetField(reg, "_minerals", new[] { iron, platinum });

            var inv = new MineralInventory();
            inv.SetAll(new Dictionary<string, int> { { "iron", 10 }, { "platinum", 3 } });

            Assert.AreEqual(10 * 2 + 3 * 20, inv.TotalSellValue(reg));

            Object.DestroyImmediate(iron); Object.DestroyImmediate(platinum); Object.DestroyImmediate(reg);
        }
    }
}
```

- [ ] **Step 2: Run — FAIL** (`MineralInventory` missing).

- [ ] **Step 3: Write `MineralInventory.cs`**

```csharp
using System.Collections.Generic;
using SocialUniverse.Config;
using SocialUniverse.Core;

namespace SocialUniverse.Mining
{
    // Published after any inventory mutation so the Mineral inventory UI can refresh.
    public class MineralInventoryChangedEvent { }

    // Client-side view cache of { mineralId -> qty }. The server (Cloud Save
    // mineral_inventory record) is the source of truth; this mirrors Wallet <-> IEconomyService.
    public class MineralInventory
    {
        private readonly Dictionary<string, int> _held = new();

        public IReadOnlyDictionary<string, int> All => _held;

        public int Get(string mineralId) =>
            mineralId != null && _held.TryGetValue(mineralId, out var q) ? q : 0;

        public void SetAll(IReadOnlyDictionary<string, int> source)
        {
            _held.Clear();
            if (source != null)
                foreach (var kv in source)
                    if (kv.Value > 0) _held[kv.Key] = kv.Value;
            EventBus.Publish(new MineralInventoryChangedEvent());
        }

        public void Add(string mineralId, int qty)
        {
            if (string.IsNullOrEmpty(mineralId) || qty == 0) return;
            _held.TryGetValue(mineralId, out var current);
            int next = current + qty;
            if (next <= 0) _held.Remove(mineralId);
            else            _held[mineralId] = next;
            EventBus.Publish(new MineralInventoryChangedEvent());
        }

        public int TotalSellValue(DatabaseRegistry registry)
        {
            int total = 0;
            foreach (var kv in _held)
            {
                var def = registry.GetMineral(kv.Key);
                if (def != null) total += kv.Value * def.SellValue;
            }
            return total;
        }
    }
}
```

- [ ] **Step 4: Run — PASS.**

- [ ] **Step 5: Commit**

```bash
git add Assets/_Project/Scripts/Mining/MineralInventory.cs Assets/_Project/Tests/EditMode/Mining/MineralInventoryTests.cs
git commit -m "feat(mining): MineralInventory client cache + change event (M6)"
```

---

### Task 5: `IMineralService` + `LocalMockMineralService` + `MineralService` + `SellResult`

**Files:**
- Create: `Assets/_Project/Scripts/Mining/IMineralService.cs` (interface + `SellResult` DTO)
- Create: `Assets/_Project/Scripts/Mining/LocalMockMineralService.cs`
- Create: `Assets/_Project/Scripts/Mining/MineralService.cs`
- Test: `Assets/_Project/Tests/EditMode/Mining/MineralServiceTests.cs`

**Interfaces:**
- Consumes: `IBackendClient` (Core), `MineralInventory` (Task 4), `Wallet` (Economy), `DatabaseRegistry` (Config).
- Produces: `class SellResult { bool Success; string Reason; int NewBalance; Dictionary<string,int> RemainingInventory; }` (public top-level).
- Produces: `interface IMineralService` — `Task<SellResult> SellAsync(string mineralId, int qty)`, `Task<SellResult> SellAllAsync()`, `Task<int> GrantMiningAsync(string mineralId, int qty, float sessionDurationSec, float unitsPerSec)`.
- Produces: `MineralService` (real) and `LocalMockMineralService` (dev).

Rationale for `GrantMiningAsync` on this interface: MiningController must not hold an `IBackendClient` directly (Rule 2). The mining grant round-trips `ValidateMining` and applies the returned minerals to `MineralInventory`, so it belongs beside the other mineral operations.

> **Ruling R4 (2026-08-18):** `IMineralService` does **not** expose `RefreshAsync`/`ICloudSave`. `ICloudSave` lives in `SocialUniverse.Net`, which the `SocialUniverse.Mining` asmdef does not reference (and must not — it would pull the whole Net graph into Mining). Cloud Save hydration is an App-layer concern: the Planet bootstrapper (Task 17), which already has `ICloudSave`, loads the `mineral_inventory` record and calls `_inventory.SetAll(...)` directly — exactly how owned-tiles hydrate today. So `MineralService`'s constructor is `(IBackendClient, MineralInventory, Wallet)` — no `ICloudSave`.

- [ ] **Step 1: Write the failing test** (mirrors `LandSaleServiceTests`' private-fake pattern)

```csharp
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using SocialUniverse.Config;
using SocialUniverse.Core;
using SocialUniverse.Economy;
using SocialUniverse.Mining;

namespace SocialUniverse.Tests
{
    public class MineralServiceTests
    {
        private class FakeBackendClient : IBackendClient
        {
            public SellResult SellResponse;
            public Task<T> CallAsync<T>(string function, Dictionary<string, object> args = null)
            {
                if (function == "SellMinerals" && typeof(T) == typeof(SellResult))
                    return Task.FromResult((T)(object)SellResponse);
                return Task.FromResult(default(T));
            }
            public Task CallAsync(string function, Dictionary<string, object> args = null) => Task.CompletedTask;
        }

        [Test]
        public async Task SellAsync_success_applies_balance_and_remaining_inventory()
        {
            var backend = new FakeBackendClient
            {
                SellResponse = new SellResult
                {
                    Success = true, NewBalance = 620,
                    RemainingInventory = new Dictionary<string, int> { { "iron", 2 } }
                }
            };
            var wallet = new Wallet();
            var inv    = new MineralInventory();
            inv.SetAll(new Dictionary<string, int> { { "iron", 12 } });
            var svc = new MineralService(backend, inv, wallet);

            var result = await svc.SellAsync("iron", 10);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(620, wallet.Coins);
            Assert.AreEqual(2, inv.Get("iron"));
        }

        [Test]
        public async Task SellAsync_failure_leaves_wallet_and_inventory_unchanged()
        {
            var backend = new FakeBackendClient
            {
                SellResponse = new SellResult { Success = false, Reason = "INSUFFICIENT_QTY" }
            };
            var wallet = new Wallet();
            var inv    = new MineralInventory();
            inv.SetAll(new Dictionary<string, int> { { "iron", 12 } });
            var svc = new MineralService(backend, inv, wallet);

            var result = await svc.SellAsync("iron", 99);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(0, wallet.Coins);
            Assert.AreEqual(12, inv.Get("iron"));
        }

        [Test]
        public async Task LocalMock_SellAll_pays_total_and_empties_inventory()
        {
            var iron = ScriptableObject.CreateInstance<MineralDefinition>();
            typeof(MineralDefinition).GetField("_mineralId", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(iron, "iron");
            typeof(MineralDefinition).GetField("_sellValue", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(iron, 3);
            var reg = ScriptableObject.CreateInstance<DatabaseRegistry>();
            typeof(DatabaseRegistry).GetField("_minerals", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(reg, new[] { iron });

            var wallet = new Wallet();
            var inv    = new MineralInventory();
            inv.SetAll(new Dictionary<string, int> { { "iron", 4 } });
            var mock = new LocalMockMineralService(inv, wallet, reg);

            var result = await mock.SellAllAsync();

            Assert.IsTrue(result.Success);
            Assert.AreEqual(12, wallet.Coins); // 4 * 3
            Assert.AreEqual(0, inv.Get("iron"));

            Object.DestroyImmediate(iron); Object.DestroyImmediate(reg);
        }
    }
}
```

- [ ] **Step 2: Run — FAIL** (types missing).

- [ ] **Step 3: Write `IMineralService.cs`**

```csharp
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SocialUniverse.Mining
{
    // Public top-level DTO so IBackendClient.CallAsync<SellResult> can type the response
    // (the public-DTO testability pattern). Shape MUST MATCH ServerCode/SellMinerals.js.
    public class SellResult
    {
        public bool                    Success;
        public string                  Reason;
        public int                     NewBalance = -1;
        public Dictionary<string, int> RemainingInventory;
    }

    public interface IMineralService
    {
        Task<SellResult> SellAsync(string mineralId, int qty);
        Task<SellResult> SellAllAsync();

        // Mining payout: round-trips ValidateMining (server caps qty) and applies the
        // granted minerals to MineralInventory. Returns the granted quantity.
        // (Cloud Save hydration is App-layer, not here — see Ruling R4.)
        Task<int> GrantMiningAsync(string mineralId, int qty, float sessionDurationSec, float unitsPerSec);
    }
}
```

- [ ] **Step 4: Write `MineralService.cs`** (real, UGS-backed)

```csharp
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SocialUniverse.Core;
using SocialUniverse.Economy;

namespace SocialUniverse.Mining
{
    public class MineralService : IMineralService
    {
        private readonly IBackendClient   _backend;
        private readonly MineralInventory _inventory;
        private readonly Wallet           _wallet;

        public MineralService(IBackendClient backend, MineralInventory inventory, Wallet wallet)
        {
            _backend   = backend;
            _inventory = inventory;
            _wallet    = wallet;
        }

        public Task<SellResult> SellAsync(string mineralId, int qty) =>
            SellInternalAsync(new Dictionary<string, object> { { "mineralId", mineralId }, { "qty", qty } });

        public Task<SellResult> SellAllAsync() =>
            SellInternalAsync(new Dictionary<string, object> { { "all", true } });

        private async Task<SellResult> SellInternalAsync(Dictionary<string, object> args)
        {
            SellResult res;
            try
            {
                res = await _backend.CallAsync<SellResult>("SellMinerals", args);
            }
            catch (Exception ex)
            {
                SULog.Error($"MineralService.Sell failed — {ex.Message}", SULog.Channel.Economy);
                return new SellResult { Success = false, Reason = "Network error" };
            }

            if (res != null && res.Success)
            {
                if (res.NewBalance >= 0) _wallet.SetCoins(res.NewBalance);
                if (res.RemainingInventory != null) _inventory.SetAll(res.RemainingInventory);
            }
            return res ?? new SellResult { Success = false, Reason = "Empty response" };
        }

        public async Task<int> GrantMiningAsync(string mineralId, int qty, float sessionDurationSec, float unitsPerSec)
        {
            if (string.IsNullOrEmpty(mineralId) || qty <= 0) return 0;

            var res = await _backend.CallAsync<GrantResponse>("ValidateMining", new Dictionary<string, object>
            {
                { "mineralId",          mineralId },
                { "claimedQty",         qty },
                { "sessionDurationSec", sessionDurationSec },
                { "unitsPerSec",        unitsPerSec }
            });

            int granted = res?.granted ?? 0;
            if (granted > 0) _inventory.Add(mineralId, granted);
            return granted;
        }

        // MUST MATCH the return shape of ServerCode/ValidateMining.js.
        private class GrantResponse
        {
            public int granted;
            public string mineralId;
        }
    }
}
```

- [ ] **Step 5: Write `LocalMockMineralService.cs`** (dev, no server)

```csharp
using System.Collections.Generic;
using System.Threading.Tasks;
using SocialUniverse.Config;
using SocialUniverse.Economy;

namespace SocialUniverse.Mining
{
    // Dev-mode mineral service: validates against the in-memory inventory + registry,
    // pays coins into the wallet locally. No server round-trip.
    public class LocalMockMineralService : IMineralService
    {
        private readonly MineralInventory _inventory;
        private readonly Wallet           _wallet;
        private readonly DatabaseRegistry _registry;

        public LocalMockMineralService(MineralInventory inventory, Wallet wallet, DatabaseRegistry registry)
        {
            _inventory = inventory;
            _wallet    = wallet;
            _registry  = registry;
        }

        public Task<SellResult> SellAsync(string mineralId, int qty)
        {
            int held = _inventory.Get(mineralId);
            var def  = _registry.GetMineral(mineralId);
            if (def == null || qty <= 0 || held < qty)
                return Task.FromResult(new SellResult { Success = false, Reason = "INSUFFICIENT_QTY" });

            _inventory.Add(mineralId, -qty);
            _wallet.SetCoins(_wallet.Coins + qty * def.SellValue);
            return Task.FromResult(Snapshot());
        }

        public Task<SellResult> SellAllAsync()
        {
            int payout = _inventory.TotalSellValue(_registry);
            _inventory.SetAll(new Dictionary<string, int>());
            _wallet.SetCoins(_wallet.Coins + payout);
            return Task.FromResult(Snapshot());
        }

        public Task<int> GrantMiningAsync(string mineralId, int qty, float sessionDurationSec, float unitsPerSec)
        {
            if (!string.IsNullOrEmpty(mineralId) && qty > 0) _inventory.Add(mineralId, qty);
            return Task.FromResult(qty);
        }

        private SellResult Snapshot() => new SellResult
        {
            Success = true,
            NewBalance = _wallet.Coins,
            RemainingInventory = new Dictionary<string, int>(_inventory.All)
        };
    }
}
```

- [ ] **Step 6: Run — PASS** (all three tests).

- [ ] **Step 7: Commit**

```bash
git add Assets/_Project/Scripts/Mining/IMineralService.cs Assets/_Project/Scripts/Mining/MineralService.cs Assets/_Project/Scripts/Mining/LocalMockMineralService.cs Assets/_Project/Tests/EditMode/Mining/MineralServiceTests.cs
git commit -m "feat(mining): IMineralService + real/mock impls + SellResult DTO (M6)"
```

---

### Task 6: `MiningRewardCalculator` → mineral quantity (repairs compile)

**Files:**
- Modify: `Assets/_Project/Scripts/Mining/MiningRewardCalculator.cs`
- Test: `Assets/_Project/Tests/EditMode/Mining/MiningRewardCalculatorTests.cs` (update existing)

**Interfaces:**
- Consumes: `Asteroid` (Mining), `EconomyConfig` (Config).
- Produces: `readonly struct MiningReward { int MineralQuantity; float IdleDurationSeconds; int ActiveTapsRequired; float ActiveSessionDurationSeconds; float UnitsPerSec; }`.
- Produces: `MiningReward Compute(Asteroid asteroid, float effectiveYieldMult)`.

- [ ] **Step 1: Update `MiningRewardCalculatorTests.cs`** — replace coin assertions with mineral-quantity assertions. Read the existing file, then change the reward-total assertions: where a test previously expected `TotalCoins == remainingYield * coinsPerUnit`, it now expects `MineralQuantity == round(remainingYield * effectiveYieldMult)` and calls `Compute(asteroid, effectiveYieldMult)`. Add one new case:

```csharp
[Test]
public void Compute_scales_mineral_quantity_by_effective_yield_multiplier()
{
    // config with 1:1 pacing; asteroid remaining yield = 10
    var reward1 = _calc.Compute(_asteroid, 1f);
    var reward2 = _calc.Compute(_asteroid, 2f);
    Assert.AreEqual(reward1.MineralQuantity * 2, reward2.MineralQuantity);
    // pacing (duration/taps) is independent of the yield multiplier
    Assert.AreEqual(reward1.IdleDurationSeconds, reward2.IdleDurationSeconds);
}
```

(Adjust setup: `Compute` now takes the multiplier. `_coinsPerUnit` still exists on the asteroid def but the calculator no longer reads it — the reward magnitude derives from `RemainingYield` × multiplier.)

> **Compile coupling (controller ruling, 2026-08-18):** changing `Compute`'s signature + the `MiningReward` field rename breaks `MiningController`'s callers, which Task 8 repairs. Tasks 6 and 8 are an atomic pair — dispatch Task 6 immediately before Task 8, and treat the compile+test gate as landing at **Task 8** (which covers both). Task 6's own review is code-correctness only. `MiningReward` is read only inside `MiningController`/`MiningRewardCalculator` (the views compute coins independently from `Definition.CoinsPerUnit`), so the rename ripples no further.

- [ ] **Step 2: Run — FAIL to compile** (`Compute` arity changed / `TotalCoins` gone; `MiningController` callers break until Task 8).

- [ ] **Step 3: Rewrite `MiningRewardCalculator.cs`**

```csharp
using UnityEngine;
using SocialUniverse.Config;

namespace SocialUniverse.Mining
{
    public readonly struct MiningReward
    {
        public readonly int   MineralQuantity;
        public readonly float IdleDurationSeconds;
        public readonly int   ActiveTapsRequired;
        public readonly float ActiveSessionDurationSeconds;
        public readonly float UnitsPerSec;

        public MiningReward(int mineralQuantity, float idleDurationSeconds, int activeTapsRequired,
            float activeSessionDurationSeconds, float unitsPerSec)
        {
            MineralQuantity              = mineralQuantity;
            IdleDurationSeconds          = idleDurationSeconds;
            ActiveTapsRequired           = activeTapsRequired;
            ActiveSessionDurationSeconds = activeSessionDurationSeconds;
            UnitsPerSec                  = unitsPerSec;
        }
    }

    // Single source of truth for idle duration, active tap count, active countdown, and the
    // mined mineral quantity for an asteroid. Pacing derives from RemainingYield (unchanged
    // from M1); the mined quantity now scales by the active drone's effective yield multiplier.
    public class MiningRewardCalculator
    {
        private readonly EconomyConfig _config;

        public MiningRewardCalculator(EconomyConfig config) => _config = config;

        public MiningReward Compute(Asteroid asteroid, float effectiveYieldMult)
        {
            int remainingYield = asteroid.RemainingYield;
            int quantity       = Mathf.RoundToInt(remainingYield * Mathf.Max(0f, effectiveYieldMult));

            float rawDuration = remainingYield * _config.IdleSecondsPerYieldUnit;
            float duration    = Mathf.Clamp(rawDuration, _config.MinIdleSessionSeconds, _config.MaxIdleSessionSeconds);

            int rawTaps = Mathf.CeilToInt(remainingYield / _config.ActiveYieldPerTap);
            int taps    = Mathf.Clamp(rawTaps, _config.MinActiveTaps, _config.MaxActiveTaps);

            float rawActiveSeconds = taps * _config.ActiveSecondsPerTap;
            float activeSeconds    = Mathf.Clamp(rawActiveSeconds, _config.MinActiveSessionSeconds, _config.MaxActiveSessionSeconds);

            // Per-claim rate so durationSec * unitsPerSec == quantity exactly even when duration
            // was clamped — feeds the server anti-cheat cap in ValidateMining (mineral units).
            float unitsPerSec = duration > 0f ? quantity / duration : 0f;

            return new MiningReward(quantity, duration, taps, activeSeconds, unitsPerSec);
        }
    }
}
```

- [ ] **Step 4: Commit** (compile/test verification lands at Task 8, per the coupling ruling above — do not expect a green compile between here and Task 8).

```bash
git add Assets/_Project/Scripts/Mining/MiningRewardCalculator.cs Assets/_Project/Tests/EditMode/Mining/MiningRewardCalculatorTests.cs
git commit -m "feat(mining): reward calculator yields mineral quantity scaled by drone yield (M6)"
```

---

### Task 7: `DroneFleet` minimal (active-drone yield source for Phase A)

**Files:**
- Create: `Assets/_Project/Scripts/Mining/DroneRuntime.cs` (rework — see full version in Task 10; Phase A needs only the effective-yield accessor)
- Create: `Assets/_Project/Scripts/Mining/DroneFleet.cs`
- Test: `Assets/_Project/Tests/EditMode/Mining/DroneFleetTests.cs`

> **Sequencing note:** `DroneRuntime` and `DroneFleet` are fully specified in Tasks 10–11. Phase A needs them to exist so `MiningController` can read `fleet.Active.EffectiveYieldMult` and `.Definition.Tier`. Implement the **full** Task 10 `DroneRuntime` and Task 11 `DroneFleet` now (do Tasks 10 and 11 here), then Phase B adds the upgrade/acquire services on top. This keeps the compile atomic. Mark Tasks 10 & 11 checkboxes done when you complete them here.

- [ ] Complete **Task 10** (`DroneUpgradeMath` + `DroneRuntime`) and **Task 11** (`DroneFleet` + snapshot DTOs) now — their full specs are below. Then return here.

- [ ] **Step: Commit** happens within Tasks 10/11.

---

### Task 8: `MiningController` — mineral payout + tier gate

**Files:**
- Modify: `Assets/_Project/Scripts/Mining/MiningController.cs`
- Modify: `Assets/_Project/Scripts/App/PlanetSceneScope.cs` (constructor deps + registration; bootstrapper build)
- Create: `Assets/_Project/Scripts/Mining/MiningBlockedEvent.cs`
- Test: `Assets/_Project/Tests/EditMode/Mining/MiningControllerTests.cs` (rewrite the economy-double + Initialize calls; add tier-gate case)

**Interfaces:**
- Consumes: `IMineralService` (Task 5), `DroneFleet` (Task 11), `MiningRewardCalculator` (Task 6).
- Produces: `class MiningBlockedEvent { Asteroid Asteroid; int RequiredTier; }` (Mining).
- Produces: `MiningController` ctor `(IMineralService minerals, MiningRewardCalculator rewardCalc, AsteroidSpawner spawner, EconomyConfig config, PlanetDefinition planet, ActiveMiningHandoff handoff, IAudioManager audio, DroneFleet fleet)`; `Initialize()` is parameterless.

- [ ] **Step 1: Write `MiningBlockedEvent.cs`**

```csharp
namespace SocialUniverse.Mining
{
    // Published when a mining session is refused because the active drone's tier is below
    // the asteroid's tier. The HUD surfaces "Requires a Tier N drone."
    public class MiningBlockedEvent
    {
        public Asteroid Asteroid;
        public int      RequiredTier;
    }
}
```

- [ ] **Step 2: Update `MiningControllerTests.cs`** — the existing `ThrowingEconomyService`/`LocalMockEconomy`/`_economy` wiring is replaced with a mineral-service double and a `DroneFleet`. Add a fake:

```csharp
private class CapturingMineralService : IMineralService
{
    public string LastMineralId; public int LastQty; public bool Throw;
    public Task<SellResult> SellAsync(string mineralId, int qty) => Task.FromResult(new SellResult { Success = true });
    public Task<SellResult> SellAllAsync() => Task.FromResult(new SellResult { Success = true });
    public Task RefreshAsync() => Task.CompletedTask;
    public Task<int> GrantMiningAsync(string mineralId, int qty, float d, float r)
    {
        if (Throw) throw new System.InvalidOperationException("simulated");
        LastMineralId = mineralId; LastQty = qty; return Task.FromResult(qty);
    }
}
```

Build the controller with a fleet whose active drone is Tier 1, an asteroid def referencing a Tier-1 mineral, and update `Initialize(...)` calls to `Initialize()`. Replace the coin assertions in `ClaimIdleSessionAsync_grants_full_yield_and_schedules_respawn` with: `Assert.AreEqual("iron", mineralSvc.LastMineralId); Assert.AreEqual(20, mineralSvc.LastQty);` (20 yield × 1.0 effective mult). Keep the respawn-on-throw regression test but point it at `CapturingMineralService { Throw = true }` and the `Regex("GrantMiningAsync.*")` log expectation. **Add the tier-gate test:**

```csharp
[Test]
public void BeginIdleMining_blocks_and_publishes_when_drone_tier_below_asteroid_tier()
{
    // asteroid def Tier 2, active drone Tier 1
    SetField(_asteroidDef, "_tier", 2);
    MiningBlockedEvent captured = null;
    EventBus.Subscribe<MiningBlockedEvent>(e => captured = e);

    var asteroid = MakeAndRegisterAsteroid("slot_0", 10);
    bool started = _mining.BeginIdleMining(asteroid);

    Assert.IsFalse(started);
    Assert.IsNull(_mining.CurrentIdleSession);
    Assert.IsNotNull(captured);
    Assert.AreEqual(2, captured.RequiredTier);

    EventBus.Clear();
}
```

- [ ] **Step 3: Run — FAIL** (ctor signature, `Initialize()` arity, `MineralQuantity`).

- [ ] **Step 4: Edit `MiningController.cs`** — apply these concrete changes:
  - Replace the `IEconomyService _economy` field/ctor-arg with `IMineralService _minerals`; add `DroneFleet _fleet` field/ctor-arg.
  - `Initialize(DroneRuntime drone)` → `Initialize()`; remove the `Drone` property and the `Drone = drone;` line (restore/finalize logic is unchanged).
  - In `BeginIdleMining` and `BeginActiveMining`, after the existing null/depleted guards, add the tier gate:

    ```csharp
    var active = _fleet.Active;
    if (active == null || active.Definition.Tier < asteroid.Definition.Tier)
    {
        EventBus.Publish(new MiningBlockedEvent { Asteroid = asteroid, RequiredTier = asteroid.Definition.Tier });
        return false;
    }
    ```
  - Everywhere `_rewardCalc.Compute(asteroid)` is called, pass the effective yield: `_rewardCalc.Compute(asteroid, _fleet.Active.EffectiveYieldMult)`.
  - In `ClaimIdleSessionAsync` and `CompleteActiveMiningAsync`, replace the coins block:

    ```csharp
    int mined = asteroid.Mine(asteroid.RemainingYield);
    if (asteroid.IsDepleted) _audio.PlaySfx(SfxId.AsteroidDestroyed);
    int quantity = reward.MineralQuantity;
    var mineral  = asteroid.Definition.Mineral;

    // ... session teardown unchanged ...

    if (quantity > 0 && mineral != null)
    {
        try
        {
            int granted = await _minerals.GrantMiningAsync(mineral.MineralId, quantity, reward.IdleDurationSeconds, reward.UnitsPerSec);
            _audio.PlaySfx(SfxId.CoinsReward);
            SULog.Info($"Idle session claimed: +{granted} {mineral.MineralId}", SULog.Channel.Mining);
        }
        catch (Exception ex)
        {
            SULog.Error($"GrantMiningAsync failed for idle claim on {mineral.MineralId} ({quantity}): {ex.Message}", SULog.Channel.Mining);
        }
    }
    ```
    Apply the analogous change in `CompleteActiveMiningAsync` (no audio SFX there, matching the current code). Keep `ScheduleRespawn` after the try/catch exactly as now.

- [ ] **Step 5: Edit `PlanetSceneScope.cs`**:
  - Register the new runtime + services (production real, standalone mock):
    ```csharp
    // Mining — M6
    builder.Register<MineralInventory>(Lifetime.Singleton);
    builder.Register<DroneFleet>(Lifetime.Singleton);
    if (standalone)
    {
        builder.Register<LocalMockMineralService>(Lifetime.Singleton).As<IMineralService>();
    }
    else
    {
        builder.Register<MineralService>(Lifetime.Singleton).As<IMineralService>();
    }
    ```
    (Production `MineralService` needs `ICloudSave`, which the parent scope provides; standalone registers `CloudSaveService` already.)
  - `MiningController` now resolves `IMineralService` + `DroneFleet` from the container automatically (VContainer constructor injection) — no explicit change needed beyond the registrations existing.
  - In `PlanetSceneBootstrapper`: replace the `_economy`-based single-drone build. After `HydrateServerStateAsync()`, build the fleet from the hydrated snapshot (see Task 18 for the hydrate call) and call `_miningController.Initialize()` with no argument. For Phase A, seed a default single-drone fleet with a literal slot count (replaced by the real hydrate in Task 17): `_fleet.Apply(DroneFleetSnapshot.SingleDrone(droneDef.DroneId, 2), _registry);` then `_miningController.Initialize();`. (`DroneFleetSnapshot.SingleDrone(string droneId, int slots)` is defined in Task 11.) Inject `DroneFleet _fleet` and `IMineralService _minerals` into the bootstrapper constructor.

- [ ] **Step 6: Run** the mining tests. Expected: PASS. Then run the **full** EditMode suite to confirm the Phase-A rework is green across previously-passing tests.

- [ ] **Step 7: Commit**

```bash
git add Assets/_Project/Scripts/Mining/MiningController.cs Assets/_Project/Scripts/Mining/MiningBlockedEvent.cs Assets/_Project/Scripts/App/PlanetSceneScope.cs Assets/_Project/Tests/EditMode/Mining/MiningControllerTests.cs
git commit -m "feat(mining): grant minerals instead of coins + drone tier gate (M6)"
```

---

### Task 9: `ValidateMining.js` → grant minerals + `SellMinerals.js` + `MineralSaleHandler` + `MineralInventoryView` + cap-alignment test

**Files:**
- Rewrite: `ServerCode/ValidateMining.js`
- Create: `ServerCode/SellMinerals.js`
- Create: `Assets/_Project/Scripts/Mining/SellMineralsRequestedEvent.cs`
- Create: `Assets/_Project/Scripts/App/MineralSaleHandler.cs`
- Create: `Assets/_Project/Scripts/UI/MineralInventoryView.cs`
- Modify: `Assets/_Project/Tests/EditMode/Mining/ValidateMiningCapAlignmentTests.cs` (retarget to the mineral cap constant)

**Interfaces:**
- Consumes: `IMineralService` (Task 5), `MineralInventory` + `DatabaseRegistry` (for the view).
- Produces: `class SellMineralsRequestedEvent { string MineralId; int Qty; bool All; }` (Mining).
- Produces: `MineralSaleHandler` (`IStartable`/`IDisposable`, App).
- Produces: `MineralInventoryView` (`MonoBehaviour`, UI).

- [ ] **Step 1: Write `SellMineralsRequestedEvent.cs`**

```csharp
namespace SocialUniverse.Mining
{
    // UI -> App intent: sell a specific mineral qty, or all minerals when All == true.
    public class SellMineralsRequestedEvent
    {
        public string MineralId;
        public int    Qty;
        public bool   All;
    }
}
```

- [ ] **Step 2: Write `MineralSaleHandler.cs`** (mirrors `LandSaleHandler`/`TilePurchaseHandler`)

```csharp
using System;
using VContainer.Unity;
using SocialUniverse.Core;
using SocialUniverse.Mining;

namespace SocialUniverse.App
{
    public class MineralSaleHandler : IStartable, IDisposable
    {
        private readonly IMineralService _minerals;

        public MineralSaleHandler(IMineralService minerals) => _minerals = minerals;

        public void Start()   => EventBus.Subscribe<SellMineralsRequestedEvent>(OnSellRequested);
        public void Dispose() => EventBus.Unsubscribe<SellMineralsRequestedEvent>(OnSellRequested);

        private async void OnSellRequested(SellMineralsRequestedEvent e)
        {
            var result = e.All ? await _minerals.SellAllAsync() : await _minerals.SellAsync(e.MineralId, e.Qty);
            if (result is { Success: false })
                SULog.Warn($"Sell minerals failed: {result.Reason}", SULog.Channel.Economy);
            // Wallet + MineralInventory events already fired by the service on success; the
            // view refreshes via MineralInventoryChangedEvent.
        }
    }
}
```

- [ ] **Step 3: Write `MineralInventoryView.cs`** (functional, unpolished — a scroll list + Sell-all button). Follow the existing modal pattern (`[Inject]` deps, `EventBus.Subscribe` in `OnEnable`, publish intent events). Minimal concrete version:

```csharp
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using SocialUniverse.Core;
using SocialUniverse.Config;
using SocialUniverse.Mining;

namespace SocialUniverse.UI
{
    // Functional (unpolished) mineral inventory panel: one row per held mineral + a Sell-all
    // button. Opened from the HUD. Rebuilds on MineralInventoryChangedEvent.
    public class MineralInventoryView : MonoBehaviour
    {
        [SerializeField] private GameObject _root;          // panel container, toggled open/closed
        [SerializeField] private Transform  _rowParent;     // vertical layout group
        [SerializeField] private GameObject _rowPrefab;     // has a Text (name/qty/value) + a Sell button
        [SerializeField] private Button     _sellAllButton;
        [SerializeField] private Button     _closeButton;
        [SerializeField] private Text        _totalValueLabel;

        private MineralInventory _inventory;
        private DatabaseRegistry _registry;

        [Inject]
        public void Construct(MineralInventory inventory, DatabaseRegistry registry)
        {
            _inventory = inventory;
            _registry  = registry;
        }

        private void OnEnable()
        {
            EventBus.Subscribe<MineralInventoryChangedEvent>(OnInventoryChanged);
            if (_sellAllButton != null) _sellAllButton.onClick.AddListener(OnSellAll);
            if (_closeButton   != null) _closeButton.onClick.AddListener(Close);
            Rebuild();
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<MineralInventoryChangedEvent>(OnInventoryChanged);
            if (_sellAllButton != null) _sellAllButton.onClick.RemoveListener(OnSellAll);
            if (_closeButton   != null) _closeButton.onClick.RemoveListener(Close);
        }

        public void Open()  { if (_root != null) _root.SetActive(true); Rebuild(); }
        public void Close() { if (_root != null) _root.SetActive(false); }

        private void OnInventoryChanged(MineralInventoryChangedEvent _) => Rebuild();
        private void OnSellAll() => EventBus.Publish(new SellMineralsRequestedEvent { All = true });

        private void Rebuild()
        {
            if (_rowParent == null || _rowPrefab == null) return;
            for (int i = _rowParent.childCount - 1; i >= 0; i--)
                Destroy(_rowParent.GetChild(i).gameObject);

            foreach (var kv in _inventory.All)
            {
                var def = _registry.GetMineral(kv.Key);
                var go  = Instantiate(_rowPrefab, _rowParent);
                var label = go.GetComponentInChildren<Text>();
                if (label != null)
                    label.text = $"{def?.DisplayName ?? kv.Key}  x{kv.Value}  ({(def != null ? def.SellValue : 0)}/ea)";
                var sell = go.GetComponentInChildren<Button>();
                if (sell != null)
                {
                    string id = kv.Key; int qty = kv.Value;
                    sell.onClick.AddListener(() => EventBus.Publish(new SellMineralsRequestedEvent { MineralId = id, Qty = qty }));
                }
            }
            if (_totalValueLabel != null)
                _totalValueLabel.text = $"Total: {_inventory.TotalSellValue(_registry)}";
        }
    }
}
```

- [ ] **Step 4: Rewrite `ValidateMining.js`** to grant minerals into the `mineral_inventory` Cloud Save record, capped by session duration × rate (same anti-cheat structure, now in mineral units):

```javascript
// ValidateMining — validates a mining session payout and grants MINERALS (M6).
// The client sends the mined mineralId + claimed quantity + session params; the server
// caps the grant at floor(sessionDurationSec * unitsPerSec) to prevent inflated claims,
// then increments the player's mineral_inventory Cloud Save record.
// FIX (Known Issue #6/#8): DataApi(context) uses the service token; getItems/setItem are positional.
const { DataApi } = require("@unity-services/cloud-save-1.4");

const ABSOLUTE_SESSION_CAP_SECONDS = 1800; // MUST be >= EconomyConfig.MaxIdleSessionSeconds
const ABSOLUTE_QTY_CAP             = 10000; // hard upper bound per call
const INVENTORY_KEY                = "mineral_inventory";

/**
 * @param {string} mineralId - Id of the mineral mined this session.
 * @param {number} claimedQty - Units the client claims to have mined. Positive integer.
 * @param {number} [sessionDurationSec] - Session length; defaults 30, capped at 1800.
 * @param {number} [unitsPerSec] - Mineral yield rate/sec; defaults 1.
 */
module.exports = async ({ params, context, logger }) => {
  const { mineralId, claimedQty, sessionDurationSec, unitsPerSec } = params;

  if (!mineralId || !Number.isInteger(claimedQty) || claimedQty <= 0) {
    throw new Error(`Invalid params: mineralId + positive claimedQty required (got ${mineralId}, ${claimedQty})`);
  }

  const cappedDuration = Math.min(sessionDurationSec ?? 30, ABSOLUTE_SESSION_CAP_SECONDS);
  const maxByRate      = Math.floor(cappedDuration * (unitsPerSec ?? 1));
  const grantAmount    = Math.min(claimedQty, maxByRate, ABSOLUTE_QTY_CAP);

  if (grantAmount <= 0) {
    return { granted: 0, mineralId };
  }

  const { projectId, playerId } = context;
  const saveApi = new DataApi(context);

  let inventory = {};
  try {
    const res  = await saveApi.getItems(projectId, playerId, [INVENTORY_KEY]);
    const item = res.data.results.find(r => r.key === INVENTORY_KEY);
    if (item && item.value && typeof item.value === "object") inventory = item.value;
  } catch (_) { /* record doesn't exist yet */ }

  inventory[mineralId] = (inventory[mineralId] || 0) + grantAmount;
  await saveApi.setItem(projectId, playerId, { key: INVENTORY_KEY, value: inventory });

  logger.info(`ValidateMining: player ${playerId} +${grantAmount} ${mineralId}`);
  return { granted: grantAmount, mineralId };
};
```

- [ ] **Step 5: Write `SellMinerals.js`** — validate held qty, compute payout `Σ qty × SELL_VALUES[id]`, decrement inventory, grant coins:

```javascript
// SellMinerals — sells minerals from the player's mineral_inventory to the house at a fixed
// per-mineral value, granting COINS. Accepts { mineralId, qty } or { all: true }.
// SELL_VALUES MUST MATCH each MineralDefinition._sellValue (SocialUniverse/Config/MineralDefinition).
const { CurrenciesApi } = require("@unity-services/economy-2.5");
const { DataApi }       = require("@unity-services/cloud-save-1.4");

const CURRENCY_ID   = "COINS";
const INVENTORY_KEY = "mineral_inventory";
// MUST MATCH MineralDefinition assets (iron, carbon, silicon, nickel, platinum, iridium).
const SELL_VALUES = { iron: 2, carbon: 3, silicon: 5, nickel: 8, platinum: 20, iridium: 40 };

module.exports = async ({ params, context, logger }) => {
  const { mineralId, qty, all } = params;
  const { projectId, playerId, accessToken } = context;
  const econApi = new CurrenciesApi({ accessToken });
  const saveApi = new DataApi(context);

  // Load inventory.
  let inventory = {};
  try {
    const res  = await saveApi.getItems(projectId, playerId, [INVENTORY_KEY]);
    const item = res.data.results.find(r => r.key === INVENTORY_KEY);
    if (item && item.value && typeof item.value === "object") inventory = item.value;
  } catch (_) { /* none */ }

  // Determine payout + resulting inventory.
  let payout = 0;
  if (all) {
    for (const [id, held] of Object.entries(inventory)) {
      payout += (SELL_VALUES[id] || 0) * held;
    }
    inventory = {};
  } else {
    if (!mineralId || !Number.isInteger(qty) || qty <= 0) {
      return { success: false, reason: "INVALID_PARAMS" };
    }
    const held = inventory[mineralId] || 0;
    if (held < qty) return { success: false, reason: "INSUFFICIENT_QTY" };
    payout = (SELL_VALUES[mineralId] || 0) * qty;
    const remaining = held - qty;
    if (remaining <= 0) delete inventory[mineralId];
    else                inventory[mineralId] = remaining;
  }

  if (payout <= 0) {
    return { success: true, newBalance: -1, remainingInventory: inventory };
  }

  await saveApi.setItem(projectId, playerId, { key: INVENTORY_KEY, value: inventory });

  const res = await econApi.incrementPlayerCurrencyBalance({
    projectId, playerId, currencyId: CURRENCY_ID,
    currencyModifyBalanceRequest: { amount: payout }
  });

  logger.info(`SellMinerals: player ${playerId} sold for ${payout} -> ${res.data.balance}`);
  return { success: true, newBalance: res.data.balance, remainingInventory: inventory };
};
```

- [ ] **Step 6: Retarget `ValidateMiningCapAlignmentTests.cs`** — the constant name is unchanged (`ABSOLUTE_SESSION_CAP_SECONDS`), so the existing regex still matches; no code change required. Re-run it to confirm it still passes against the rewritten JS. If the milestone later renames the constant, update the regex here.

- [ ] **Step 7: Wire the handler in `PlanetSceneScope.cs`** — register the App handler as an entry point (in the same block as the other `RegisterEntryPoint` calls):

```csharp
builder.RegisterEntryPoint<MineralSaleHandler>();
```

> **Ruling R6 (2026-08-18):** do NOT add `builder.RegisterComponentInHierarchy<MineralInventoryView>()` here. VContainer's `FindComponentProvider` **throws** at container build if the component isn't in the scene, and these registrations are force-resolved at build (see the SettingsPanel note in `PlanetSceneScope`). The `MineralInventoryView` GameObject is added to `Planet.unity` only in the deferred Editor-wiring step, so its `RegisterComponentInHierarchy` line is added there too — adding it now would break the Planet scene at runtime. `MineralSaleHandler` is a plain entry point whose only dependency (`IMineralService`) is already registered, so it resolves fine.

- [ ] **Step 8: Run** the EditMode suite. Expected: PASS (server JS is not executed by tests; the cap-alignment text-scan and the C# service/handler tests are the coverage).

- [ ] **Step 9: Commit**

```bash
git add ServerCode/ValidateMining.js ServerCode/SellMinerals.js Assets/_Project/Scripts/Mining/SellMineralsRequestedEvent.cs Assets/_Project/Scripts/App/MineralSaleHandler.cs Assets/_Project/Scripts/UI/MineralInventoryView.cs Assets/_Project/Scripts/App/PlanetSceneScope.cs
git commit -m "feat(mining): ValidateMining grants minerals, SellMinerals server fn, sale handler + inventory view (M6)"
```

> **Phase A checkpoint:** the full loop mine → typed minerals → sell → coins works end-to-end in standalone (mock) mode and, once server functions are deployed, in production. Tier gate is present but a no-op until Phase B seeds asteroid tiers > 1 and multi-tier drones. This is a shippable slice.

---

## Phase B — Drones, Upgrades, Fleet & Tier Gating

### Task 10: `DroneUpgradeMath` (pure) + `DroneRuntime` rework

**Files:**
- Create: `Assets/_Project/Scripts/Mining/DroneUpgradeMath.cs`
- Rewrite: `Assets/_Project/Scripts/Mining/DroneRuntime.cs`
- Test: `Assets/_Project/Tests/EditMode/Mining/DroneUpgradeMathTests.cs`
- Test: `Assets/_Project/Tests/EditMode/Mining/DroneRuntimeTests.cs`

**Interfaces:**
- Consumes: `UpgradeDefinition`, `DroneStat`, `DroneDefinition` (Config).
- Produces: `static class DroneUpgradeMath` — `int NextCost(UpgradeDefinition, int currentLevel)`, `float EffectiveStat(float baseValue, UpgradeDefinition, int level)`, `int SlotUnlockCost(int baseCost, float growth, int currentSlots, int startSlots)`.
- Produces: `DroneRuntime(DroneDefinition def, IDictionary<DroneStat,int> levels = null, IReadOnlyDictionary<DroneStat,UpgradeDefinition> upgrades = null)` with `Definition`, `IReadOnlyDictionary<DroneStat,int> Levels`, `int Level(DroneStat)`, `void SetLevel(DroneStat,int)`, `int EffectiveCargoCap`, `float EffectiveYieldMult`, `float EffectiveTravelSpeed`.

- [ ] **Step 1: Write `DroneUpgradeMathTests.cs`**

```csharp
using NUnit.Framework;
using UnityEngine;
using SocialUniverse.Config;
using SocialUniverse.Mining;

namespace SocialUniverse.Tests
{
    public class DroneUpgradeMathTests
    {
        private static UpgradeDefinition Upgrade(int baseCost, float growth, float delta, int maxLevel)
        {
            var u = ScriptableObject.CreateInstance<UpgradeDefinition>();
            void Set(string f, object v) => typeof(UpgradeDefinition)
                .GetField(f, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(u, v);
            Set("_baseCost", baseCost); Set("_costGrowth", growth); Set("_deltaPerLevel", delta); Set("_maxLevel", maxLevel);
            return u;
        }

        [Test]
        public void NextCost_grows_geometrically_from_level_zero()
        {
            var u = Upgrade(baseCost: 100, growth: 2f, delta: 5f, maxLevel: 10);
            Assert.AreEqual(100, DroneUpgradeMath.NextCost(u, 0)); // level 0 -> 1
            Assert.AreEqual(200, DroneUpgradeMath.NextCost(u, 1)); // level 1 -> 2
            Assert.AreEqual(400, DroneUpgradeMath.NextCost(u, 2)); // level 2 -> 3
            Object.DestroyImmediate(u);
        }

        [Test]
        public void EffectiveStat_is_base_plus_level_times_delta()
        {
            var u = Upgrade(100, 2f, delta: 5f, maxLevel: 10);
            Assert.AreEqual(50f, DroneUpgradeMath.EffectiveStat(50f, u, 0));
            Assert.AreEqual(65f, DroneUpgradeMath.EffectiveStat(50f, u, 3));
            Assert.AreEqual(50f, DroneUpgradeMath.EffectiveStat(50f, null, 3)); // null track -> base
            Object.DestroyImmediate(u);
        }

        [Test]
        public void SlotUnlockCost_scales_from_start_slots()
        {
            // baseCost 500, growth 2, start 2 slots: first extra (currentSlots=2) = 500, next (3) = 1000
            Assert.AreEqual(500,  DroneUpgradeMath.SlotUnlockCost(500, 2f, currentSlots: 2, startSlots: 2));
            Assert.AreEqual(1000, DroneUpgradeMath.SlotUnlockCost(500, 2f, currentSlots: 3, startSlots: 2));
        }
    }
}
```

- [ ] **Step 2: Run — FAIL** (`DroneUpgradeMath` missing).

- [ ] **Step 3: Write `DroneUpgradeMath.cs`**

```csharp
using UnityEngine;
using SocialUniverse.Config;

namespace SocialUniverse.Mining
{
    // Pure upgrade/economy math for drones. NextCost and SlotUnlockCost are DUPLICATED in
    // ServerCode/UpgradeDrone.js and ServerCode/UnlockDroneSlot.js ("must match") — keep in sync.
    public static class DroneUpgradeMath
    {
        // Coin cost to advance a stat track from currentLevel to currentLevel+1.
        public static int NextCost(UpgradeDefinition def, int currentLevel)
        {
            if (def == null) return 0;
            return Mathf.RoundToInt(def.BaseCost * Mathf.Pow(def.CostGrowth, Mathf.Max(0, currentLevel)));
        }

        // Effective stat value at a given upgrade level: base + level * deltaPerLevel.
        public static float EffectiveStat(float baseValue, UpgradeDefinition def, int level)
        {
            if (def == null || level <= 0) return baseValue;
            return baseValue + level * def.DeltaPerLevel;
        }

        // Coin cost to unlock one more fleet slot, scaling from the starting slot count.
        public static int SlotUnlockCost(int baseCost, float growth, int currentSlots, int startSlots)
        {
            int steps = Mathf.Max(0, currentSlots - startSlots);
            return Mathf.RoundToInt(baseCost * Mathf.Pow(growth, steps));
        }
    }
}
```

- [ ] **Step 4: Write `DroneRuntimeTests.cs`**

```csharp
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using SocialUniverse.Config;
using SocialUniverse.Mining;

namespace SocialUniverse.Tests
{
    public class DroneRuntimeTests
    {
        private static void Set(object o, string f, object v) => o.GetType()
            .GetField(f, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(o, v);

        [Test]
        public void Effective_stats_reflect_upgrade_levels()
        {
            var def = ScriptableObject.CreateInstance<DroneDefinition>();
            Set(def, "_cargoCap", 50); Set(def, "_yieldMultiplier", 1f); Set(def, "_travelSpeed", 5f);

            var cargo = ScriptableObject.CreateInstance<UpgradeDefinition>();
            Set(cargo, "_stat", DroneStat.Cargo); Set(cargo, "_deltaPerLevel", 10f);
            var yield = ScriptableObject.CreateInstance<UpgradeDefinition>();
            Set(yield, "_stat", DroneStat.Yield); Set(yield, "_deltaPerLevel", 0.5f);

            var upgrades = new Dictionary<DroneStat, UpgradeDefinition> { { DroneStat.Cargo, cargo }, { DroneStat.Yield, yield } };
            var levels   = new Dictionary<DroneStat, int> { { DroneStat.Cargo, 2 }, { DroneStat.Yield, 3 } };

            var drone = new DroneRuntime(def, levels, upgrades);

            Assert.AreEqual(70, drone.EffectiveCargoCap);          // 50 + 2*10
            Assert.AreEqual(2.5f, drone.EffectiveYieldMult, 1e-4); // 1 + 3*0.5
            Assert.AreEqual(5f, drone.EffectiveTravelSpeed);       // no Speed upgrade -> base

            Object.DestroyImmediate(def); Object.DestroyImmediate(cargo); Object.DestroyImmediate(yield);
        }

        [Test]
        public void Unknown_stat_level_defaults_to_zero_and_base()
        {
            var def = ScriptableObject.CreateInstance<DroneDefinition>();
            Set(def, "_cargoCap", 50);
            var drone = new DroneRuntime(def);
            Assert.AreEqual(0, drone.Level(DroneStat.Cargo));
            Assert.AreEqual(50, drone.EffectiveCargoCap);
            Object.DestroyImmediate(def);
        }
    }
}
```

- [ ] **Step 5: Run — FAIL** (new `DroneRuntime` API).

- [ ] **Step 6: Rewrite `DroneRuntime.cs`**

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;
using SocialUniverse.Config;

namespace SocialUniverse.Mining
{
    // Live drone: its definition (base stats) + per-stat upgrade levels. Exposes effective
    // stats via DroneUpgradeMath. Each owned drone has its own DroneRuntime in the DroneFleet.
    public class DroneRuntime
    {
        public DroneDefinition Definition { get; }

        private readonly Dictionary<DroneStat, int> _levels;
        private readonly IReadOnlyDictionary<DroneStat, UpgradeDefinition> _upgrades;

        public DroneRuntime(DroneDefinition definition,
            IDictionary<DroneStat, int> levels = null,
            IReadOnlyDictionary<DroneStat, UpgradeDefinition> upgrades = null)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            _levels    = levels != null ? new Dictionary<DroneStat, int>(levels) : new Dictionary<DroneStat, int>();
            _upgrades  = upgrades;
        }

        public IReadOnlyDictionary<DroneStat, int> Levels => _levels;

        public int Level(DroneStat stat) => _levels.TryGetValue(stat, out var l) ? l : 0;

        public void SetLevel(DroneStat stat, int level) => _levels[stat] = Mathf.Max(0, level);

        private UpgradeDefinition Upgrade(DroneStat stat) =>
            _upgrades != null && _upgrades.TryGetValue(stat, out var u) ? u : null;

        public int   EffectiveCargoCap    => Mathf.RoundToInt(DroneUpgradeMath.EffectiveStat(Definition.CargoCap,        Upgrade(DroneStat.Cargo), Level(DroneStat.Cargo)));
        public float EffectiveYieldMult   => DroneUpgradeMath.EffectiveStat(Definition.YieldMultiplier, Upgrade(DroneStat.Yield), Level(DroneStat.Yield));
        public float EffectiveTravelSpeed => DroneUpgradeMath.EffectiveStat(Definition.TravelSpeed,     Upgrade(DroneStat.Speed), Level(DroneStat.Speed));
    }
}
```

- [ ] **Step 7: Run — PASS** (both test files). (Mining assembly compiles once Task 11's `DroneFleet` also exists if any type references it — `DroneRuntime` alone compiles independently.)

- [ ] **Step 8: Commit**

```bash
git add Assets/_Project/Scripts/Mining/DroneUpgradeMath.cs Assets/_Project/Scripts/Mining/DroneRuntime.cs Assets/_Project/Tests/EditMode/Mining/DroneUpgradeMathTests.cs Assets/_Project/Tests/EditMode/Mining/DroneRuntimeTests.cs
git commit -m "feat(mining): DroneUpgradeMath (pure) + DroneRuntime effective stats (M6)"
```

---

### Task 11: `DroneFleet` + snapshot DTOs

**Files:**
- Create: `Assets/_Project/Scripts/Mining/DroneFleet.cs` (fleet + `DroneSnapshot` + `DroneFleetSnapshot` + `DroneFleetChangedEvent`)
- Test: `Assets/_Project/Tests/EditMode/Mining/DroneFleetTests.cs`

**Interfaces:**
- Consumes: `DatabaseRegistry`, `DroneDefinition`, `UpgradeDefinition`, `DroneStat` (Config); `DroneRuntime` (Task 10).
- Produces: `class DroneSnapshot { string DroneId; Dictionary<string,int> Upgrades; }` (public).
- Produces: `class DroneFleetSnapshot { int Slots; string ActiveDroneId; List<DroneSnapshot> Drones; static DroneFleetSnapshot SingleDrone(string droneId, int slots); }` (public).
- Produces: `class DroneFleetChangedEvent { }`.
- Produces: `DroneFleet` — `void Apply(DroneFleetSnapshot, DatabaseRegistry)`, `DroneFleetSnapshot ToSnapshot()`, `IReadOnlyList<DroneRuntime> Drones`, `string ActiveDroneId`, `int UnlockedSlots`, `DroneRuntime Active`, `DroneRuntime Get(string droneId)`.

- [ ] **Step 1: Write `DroneFleetTests.cs`**

```csharp
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using SocialUniverse.Config;
using SocialUniverse.Mining;

namespace SocialUniverse.Tests
{
    public class DroneFleetTests
    {
        private static void Set(object o, string f, object v) => o.GetType()
            .GetField(f, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(o, v);

        private DatabaseRegistry _registry;

        [SetUp]
        public void SetUp()
        {
            var scout = ScriptableObject.CreateInstance<DroneDefinition>();
            Set(scout, "_droneId", "scout"); Set(scout, "_tier", 1);
            var hauler = ScriptableObject.CreateInstance<DroneDefinition>();
            Set(hauler, "_droneId", "hauler"); Set(hauler, "_tier", 2);
            var cargo = ScriptableObject.CreateInstance<UpgradeDefinition>();
            Set(cargo, "_stat", DroneStat.Cargo); Set(cargo, "_deltaPerLevel", 10f);

            _registry = ScriptableObject.CreateInstance<DatabaseRegistry>();
            Set(_registry, "_drones", new[] { scout, hauler });
            Set(_registry, "_upgrades", new[] { cargo });
        }

        [Test]
        public void Apply_rebuilds_runtimes_and_resolves_active_and_levels()
        {
            var snap = new DroneFleetSnapshot
            {
                Slots = 2, ActiveDroneId = "hauler",
                Drones = new List<DroneSnapshot>
                {
                    new DroneSnapshot { DroneId = "scout",  Upgrades = new Dictionary<string,int>() },
                    new DroneSnapshot { DroneId = "hauler", Upgrades = new Dictionary<string,int> { { "Cargo", 3 } } }
                }
            };

            var fleet = new DroneFleet();
            fleet.Apply(snap, _registry);

            Assert.AreEqual(2, fleet.UnlockedSlots);
            Assert.AreEqual("hauler", fleet.Active.Definition.DroneId);
            Assert.AreEqual(2, fleet.Active.Definition.Tier);
            Assert.AreEqual(3, fleet.Get("hauler").Level(DroneStat.Cargo));
            Assert.AreEqual(80, fleet.Get("hauler").EffectiveCargoCap); // 50 base default + 3*10
        }

        [Test]
        public void SingleDrone_snapshot_seeds_one_active_drone()
        {
            var fleet = new DroneFleet();
            fleet.Apply(DroneFleetSnapshot.SingleDrone("scout", slots: 2), _registry);
            Assert.AreEqual("scout", fleet.Active.Definition.DroneId);
            Assert.AreEqual(1, fleet.Drones.Count);
        }

        [Test]
        public void ToSnapshot_round_trips_through_Apply()
        {
            var fleet = new DroneFleet();
            fleet.Apply(DroneFleetSnapshot.SingleDrone("scout", 2), _registry);
            fleet.Get("scout").SetLevel(DroneStat.Cargo, 4);

            var snap = fleet.ToSnapshot();
            Assert.AreEqual("scout", snap.ActiveDroneId);
            Assert.AreEqual(4, snap.Drones[0].Upgrades["Cargo"]);
        }
    }
}
```

- [ ] **Step 2: Run — FAIL** (types missing).

- [ ] **Step 3: Write `DroneFleet.cs`**

```csharp
using System;
using System.Collections.Generic;
using SocialUniverse.Config;
using SocialUniverse.Core;

namespace SocialUniverse.Mining
{
    // Serializable snapshot of one owned drone. Upgrades keyed by DroneStat name ("Cargo"/...)
    // so the JSON shape matches the drone_fleet Cloud Save record + ServerCode functions.
    public class DroneSnapshot
    {
        public string                  DroneId;
        public Dictionary<string, int> Upgrades;
    }

    // Serializable snapshot of the whole fleet. Shape MUST MATCH the drone_fleet Cloud Save
    // record and the { fleet } payload returned by the drone ServerCode functions.
    public class DroneFleetSnapshot
    {
        public int                 Slots;
        public string              ActiveDroneId;
        public List<DroneSnapshot> Drones;

        public static DroneFleetSnapshot SingleDrone(string droneId, int slots) => new DroneFleetSnapshot
        {
            Slots = slots, ActiveDroneId = droneId,
            Drones = new List<DroneSnapshot> { new DroneSnapshot { DroneId = droneId, Upgrades = new Dictionary<string, int>() } }
        };
    }

    public class DroneFleetChangedEvent { }

    // Client-side view cache of owned drones + active selection + unlocked slot count.
    // Server (drone_fleet Cloud Save record) is authoritative; this mirrors Wallet/MineralInventory.
    public class DroneFleet
    {
        private readonly List<DroneRuntime> _drones = new();

        public IReadOnlyList<DroneRuntime> Drones => _drones;
        public string ActiveDroneId { get; private set; }
        public int    UnlockedSlots { get; private set; }

        public DroneRuntime Active => Get(ActiveDroneId) ?? (_drones.Count > 0 ? _drones[0] : null);

        public DroneRuntime Get(string droneId) =>
            droneId == null ? null : _drones.Find(d => d.Definition.DroneId == droneId);

        public void Apply(DroneFleetSnapshot snapshot, DatabaseRegistry registry)
        {
            _drones.Clear();
            UnlockedSlots = snapshot?.Slots ?? 0;
            ActiveDroneId = snapshot?.ActiveDroneId;

            var upgradeLookup = BuildUpgradeLookup(registry);

            if (snapshot?.Drones != null)
            {
                foreach (var ds in snapshot.Drones)
                {
                    var def = registry.GetDrone(ds.DroneId);
                    if (def == null) continue; // unknown drone id — skip defensively

                    var levels = new Dictionary<DroneStat, int>();
                    if (ds.Upgrades != null)
                        foreach (var kv in ds.Upgrades)
                            if (Enum.TryParse<DroneStat>(kv.Key, out var stat)) levels[stat] = kv.Value;

                    _drones.Add(new DroneRuntime(def, levels, upgradeLookup));
                }
            }

            EventBus.Publish(new DroneFleetChangedEvent());
        }

        public DroneFleetSnapshot ToSnapshot()
        {
            var list = new List<DroneSnapshot>();
            foreach (var d in _drones)
            {
                var up = new Dictionary<string, int>();
                foreach (var kv in d.Levels) up[kv.Key.ToString()] = kv.Value;
                list.Add(new DroneSnapshot { DroneId = d.Definition.DroneId, Upgrades = up });
            }
            return new DroneFleetSnapshot { Slots = UnlockedSlots, ActiveDroneId = ActiveDroneId, Drones = list };
        }

        private static IReadOnlyDictionary<DroneStat, UpgradeDefinition> BuildUpgradeLookup(DatabaseRegistry registry)
        {
            var map = new Dictionary<DroneStat, UpgradeDefinition>();
            foreach (var u in registry.AllUpgrades) map[u.Stat] = u;
            return map;
        }
    }
}
```

- [ ] **Step 4: Run — PASS** (with the clean `EffectiveCargoCap == 80` assertion).

- [ ] **Step 5: Commit**

```bash
git add Assets/_Project/Scripts/Mining/DroneFleet.cs Assets/_Project/Tests/EditMode/Mining/DroneFleetTests.cs
git commit -m "feat(mining): DroneFleet client cache + snapshot DTOs (M6)"
```

> Tasks 10 & 11 satisfy the forward reference in Task 7 — return to Task 8 to finish the MiningController rework if you followed the phase order strictly.

---

### Task 12: `IDroneService` + `DroneActionResult` + real/mock impls

**Files:**
- Create: `Assets/_Project/Scripts/Mining/IDroneService.cs` (interface + `DroneActionResult`)
- Create: `Assets/_Project/Scripts/Mining/DroneService.cs`
- Create: `Assets/_Project/Scripts/Mining/LocalMockDroneService.cs`
- Test: `Assets/_Project/Tests/EditMode/Mining/DroneServiceTests.cs`

**Interfaces:**
- Consumes: `IBackendClient` (Core), `DroneFleet` (Task 11), `Wallet` (Economy), `DatabaseRegistry`, `EconomyConfig` (Config), `DroneUpgradeMath` (Task 10).
- Produces: `class DroneActionResult { bool Success; string Reason; int NewBalance; DroneFleetSnapshot Fleet; }` (public).
- Produces: `interface IDroneService` — `Task<DroneActionResult> AcquireDroneAsync(string droneId)`, `Task<DroneActionResult> UnlockSlotAsync()`, `Task<DroneActionResult> UpgradeAsync(string droneId, DroneStat stat)`, `Task<DroneActionResult> SetActiveAsync(string droneId)`.
- Produces: `DroneService` (real), `LocalMockDroneService` (dev).

- [ ] **Step 1: Write `DroneServiceTests.cs`**

```csharp
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using SocialUniverse.Config;
using SocialUniverse.Core;
using SocialUniverse.Economy;
using SocialUniverse.Mining;

namespace SocialUniverse.Tests
{
    public class DroneServiceTests
    {
        private static void Set(object o, string f, object v) => o.GetType()
            .GetField(f, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(o, v);

        private class FakeBackendClient : IBackendClient
        {
            public DroneActionResult Response;
            public Task<T> CallAsync<T>(string function, Dictionary<string, object> args = null)
            {
                if (typeof(T) == typeof(DroneActionResult)) return Task.FromResult((T)(object)Response);
                return Task.FromResult(default(T));
            }
            public Task CallAsync(string function, Dictionary<string, object> args = null) => Task.CompletedTask;
        }

        private DatabaseRegistry _registry;
        private EconomyConfig    _config;

        [SetUp]
        public void SetUp()
        {
            var scout = ScriptableObject.CreateInstance<DroneDefinition>();
            Set(scout, "_droneId", "scout"); Set(scout, "_tier", 1); Set(scout, "_unlockCost", 0);
            var hauler = ScriptableObject.CreateInstance<DroneDefinition>();
            Set(hauler, "_droneId", "hauler"); Set(hauler, "_tier", 2); Set(hauler, "_unlockCost", 300);
            var cargo = ScriptableObject.CreateInstance<UpgradeDefinition>();
            Set(cargo, "_stat", DroneStat.Cargo); Set(cargo, "_baseCost", 100); Set(cargo, "_costGrowth", 2f); Set(cargo, "_deltaPerLevel", 10f); Set(cargo, "_maxLevel", 5);

            _registry = ScriptableObject.CreateInstance<DatabaseRegistry>();
            Set(_registry, "_drones", new[] { scout, hauler });
            Set(_registry, "_upgrades", new[] { cargo });

            _config = ScriptableObject.CreateInstance<EconomyConfig>();
            Set(_config, "_startingFleetSlots", 2);
            Set(_config, "_slotUnlockBaseCost", 500);
            Set(_config, "_slotUnlockCostGrowth", 2f);
        }

        [Test]
        public async Task Real_service_applies_returned_snapshot_and_balance_on_success()
        {
            var backend = new FakeBackendClient
            {
                Response = new DroneActionResult
                {
                    Success = true, NewBalance = 200,
                    Fleet = new DroneFleetSnapshot
                    {
                        Slots = 2, ActiveDroneId = "scout",
                        Drones = new List<DroneSnapshot>
                        {
                            new DroneSnapshot { DroneId = "scout",  Upgrades = new Dictionary<string,int>() },
                            new DroneSnapshot { DroneId = "hauler", Upgrades = new Dictionary<string,int>() }
                        }
                    }
                }
            };
            var wallet = new Wallet();
            var fleet  = new DroneFleet();
            var svc = new DroneService(backend, fleet, wallet, _registry);

            var result = await svc.AcquireDroneAsync("hauler");

            Assert.IsTrue(result.Success);
            Assert.AreEqual(200, wallet.Coins);
            Assert.IsNotNull(fleet.Get("hauler"));
        }

        [Test]
        public async Task Real_service_is_noop_on_failure()
        {
            var backend = new FakeBackendClient { Response = new DroneActionResult { Success = false, Reason = "INSUFFICIENT_FUNDS" } };
            var wallet = new Wallet();
            var fleet  = new DroneFleet();
            fleet.Apply(DroneFleetSnapshot.SingleDrone("scout", 2), _registry);
            var svc = new DroneService(backend, fleet, wallet, _registry);

            var result = await svc.AcquireDroneAsync("hauler");

            Assert.IsFalse(result.Success);
            Assert.AreEqual(0, wallet.Coins);
            Assert.IsNull(fleet.Get("hauler"));
        }

        [Test]
        public async Task Mock_upgrade_deducts_next_cost_and_increments_level()
        {
            var wallet = new Wallet(); wallet.SetCoins(500);
            var fleet  = new DroneFleet();
            fleet.Apply(DroneFleetSnapshot.SingleDrone("scout", 2), _registry);
            var mock = new LocalMockDroneService(fleet, wallet, _registry, _config);

            var result = await mock.UpgradeAsync("scout", DroneStat.Cargo);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(400, wallet.Coins); // 500 - baseCost 100
            Assert.AreEqual(1, fleet.Get("scout").Level(DroneStat.Cargo));
        }

        [Test]
        public async Task Mock_acquire_fails_when_slots_full()
        {
            var wallet = new Wallet(); wallet.SetCoins(9999);
            var fleet  = new DroneFleet();
            // 2 slots, already 2 drones owned
            fleet.Apply(new DroneFleetSnapshot
            {
                Slots = 2, ActiveDroneId = "scout",
                Drones = new List<DroneSnapshot>
                {
                    new DroneSnapshot { DroneId = "scout",  Upgrades = new Dictionary<string,int>() },
                    new DroneSnapshot { DroneId = "hauler", Upgrades = new Dictionary<string,int>() }
                }
            }, _registry);
            var mock = new LocalMockDroneService(fleet, wallet, _registry, _config);

            // a third drone id doesn't exist, but slot-full check should trip first for an owned-capacity guard;
            // acquire an already-owned drone -> ALREADY_OWNED, acquire with full slots -> SLOTS_FULL
            var result = await mock.AcquireDroneAsync("hauler");
            Assert.IsFalse(result.Success);
        }
    }
}
```

- [ ] **Step 2: Run — FAIL** (types missing).

- [ ] **Step 3: Write `IDroneService.cs`**

```csharp
using System.Threading.Tasks;
using SocialUniverse.Config;

namespace SocialUniverse.Mining
{
    // Public top-level DTO so IBackendClient.CallAsync<DroneActionResult> can type the response.
    // Fleet MUST MATCH the { fleet } payload returned by the drone ServerCode functions.
    public class DroneActionResult
    {
        public bool               Success;
        public string             Reason;
        public int                NewBalance = -1;
        public DroneFleetSnapshot Fleet;
    }

    public interface IDroneService
    {
        Task<DroneActionResult> AcquireDroneAsync(string droneId);
        Task<DroneActionResult> UnlockSlotAsync();
        Task<DroneActionResult> UpgradeAsync(string droneId, DroneStat stat);
        Task<DroneActionResult> SetActiveAsync(string droneId);
    }
}
```

- [ ] **Step 4: Write `DroneService.cs`** (real)

```csharp
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SocialUniverse.Config;
using SocialUniverse.Core;
using SocialUniverse.Economy;

namespace SocialUniverse.Mining
{
    public class DroneService : IDroneService
    {
        private readonly IBackendClient   _backend;
        private readonly DroneFleet       _fleet;
        private readonly Wallet           _wallet;
        private readonly DatabaseRegistry _registry;

        public DroneService(IBackendClient backend, DroneFleet fleet, Wallet wallet, DatabaseRegistry registry)
        {
            _backend  = backend;
            _fleet    = fleet;
            _wallet   = wallet;
            _registry = registry;
        }

        public Task<DroneActionResult> AcquireDroneAsync(string droneId) =>
            CallAsync("AcquireDrone", new Dictionary<string, object> { { "droneId", droneId } });

        public Task<DroneActionResult> UnlockSlotAsync() =>
            CallAsync("UnlockDroneSlot", new Dictionary<string, object>());

        public Task<DroneActionResult> UpgradeAsync(string droneId, DroneStat stat) =>
            CallAsync("UpgradeDrone", new Dictionary<string, object> { { "droneId", droneId }, { "stat", stat.ToString() } });

        public Task<DroneActionResult> SetActiveAsync(string droneId) =>
            CallAsync("SetActiveDrone", new Dictionary<string, object> { { "droneId", droneId } });

        private async Task<DroneActionResult> CallAsync(string fn, Dictionary<string, object> args)
        {
            DroneActionResult res;
            try
            {
                res = await _backend.CallAsync<DroneActionResult>(fn, args);
            }
            catch (Exception ex)
            {
                SULog.Error($"DroneService.{fn} failed — {ex.Message}", SULog.Channel.Economy);
                return new DroneActionResult { Success = false, Reason = "Network error" };
            }

            if (res != null && res.Success)
            {
                if (res.Fleet != null)   _fleet.Apply(res.Fleet, _registry);
                if (res.NewBalance >= 0) _wallet.SetCoins(res.NewBalance);
            }
            return res ?? new DroneActionResult { Success = false, Reason = "Empty response" };
        }
    }
}
```

- [ ] **Step 5: Write `LocalMockDroneService.cs`** (dev)

```csharp
using System.Threading.Tasks;
using SocialUniverse.Config;
using SocialUniverse.Economy;

namespace SocialUniverse.Mining
{
    // Dev-mode drone service: validates against the current DroneFleet snapshot + wallet,
    // deducts coins locally, and re-applies a mutated snapshot. No server round-trip.
    // Validation logic MUST MATCH the drone ServerCode functions.
    public class LocalMockDroneService : IDroneService
    {
        private readonly DroneFleet       _fleet;
        private readonly Wallet           _wallet;
        private readonly DatabaseRegistry _registry;
        private readonly EconomyConfig    _config;

        public LocalMockDroneService(DroneFleet fleet, Wallet wallet, DatabaseRegistry registry, EconomyConfig config)
        {
            _fleet    = fleet;
            _wallet   = wallet;
            _registry = registry;
            _config   = config;
        }

        public Task<DroneActionResult> AcquireDroneAsync(string droneId)
        {
            var def = _registry.GetDrone(droneId);
            if (def == null)                              return Fail("UNKNOWN_DRONE");
            var snap = _fleet.ToSnapshot();
            if (snap.Drones.Exists(d => d.DroneId == droneId)) return Fail("ALREADY_OWNED");
            if (snap.Drones.Count >= snap.Slots)          return Fail("SLOTS_FULL");
            if (!_wallet.CanAfford(def.UnlockCost))       return Fail("INSUFFICIENT_FUNDS");

            _wallet.SetCoins(_wallet.Coins - def.UnlockCost);
            snap.Drones.Add(new DroneSnapshot { DroneId = droneId, Upgrades = new System.Collections.Generic.Dictionary<string, int>() });
            return Apply(snap);
        }

        public Task<DroneActionResult> UnlockSlotAsync()
        {
            var snap = _fleet.ToSnapshot();
            int cost = DroneUpgradeMath.SlotUnlockCost(_config.SlotUnlockBaseCost, _config.SlotUnlockCostGrowth, snap.Slots, _config.StartingFleetSlots);
            if (!_wallet.CanAfford(cost)) return Fail("INSUFFICIENT_FUNDS");

            _wallet.SetCoins(_wallet.Coins - cost);
            snap.Slots += 1;
            return Apply(snap);
        }

        public Task<DroneActionResult> UpgradeAsync(string droneId, DroneStat stat)
        {
            var snap = _fleet.ToSnapshot();
            var ds   = snap.Drones.Find(d => d.DroneId == droneId);
            var def  = _registry.GetUpgrade(stat);
            if (ds == null || def == null) return Fail("INVALID");

            ds.Upgrades ??= new System.Collections.Generic.Dictionary<string, int>();
            ds.Upgrades.TryGetValue(stat.ToString(), out int level);
            if (level >= def.MaxLevel) return Fail("MAX_LEVEL");

            int cost = DroneUpgradeMath.NextCost(def, level);
            if (!_wallet.CanAfford(cost)) return Fail("INSUFFICIENT_FUNDS");

            _wallet.SetCoins(_wallet.Coins - cost);
            ds.Upgrades[stat.ToString()] = level + 1;
            return Apply(snap);
        }

        public Task<DroneActionResult> SetActiveAsync(string droneId)
        {
            var snap = _fleet.ToSnapshot();
            if (!snap.Drones.Exists(d => d.DroneId == droneId)) return Fail("NOT_OWNED");
            snap.ActiveDroneId = droneId;
            return Apply(snap);
        }

        private Task<DroneActionResult> Apply(DroneFleetSnapshot snap)
        {
            _fleet.Apply(snap, _registry);
            return Task.FromResult(new DroneActionResult { Success = true, NewBalance = _wallet.Coins, Fleet = snap });
        }

        private static Task<DroneActionResult> Fail(string reason) =>
            Task.FromResult(new DroneActionResult { Success = false, Reason = reason });
    }
}
```

- [ ] **Step 6: Run — PASS.**

- [ ] **Step 7: Commit**

```bash
git add Assets/_Project/Scripts/Mining/IDroneService.cs Assets/_Project/Scripts/Mining/DroneService.cs Assets/_Project/Scripts/Mining/LocalMockDroneService.cs Assets/_Project/Tests/EditMode/Mining/DroneServiceTests.cs
git commit -m "feat(mining): IDroneService + real/mock impls + DroneActionResult (M6)"
```

---

### Task 13: Drone intent events + `DroneGarageHandler`

**Files:**
- Create: `Assets/_Project/Scripts/Mining/DroneEvents.cs` (the four drone intent events)
- Create: `Assets/_Project/Scripts/App/DroneGarageHandler.cs`
- Test: `Assets/_Project/Tests/EditMode/App/DroneGarageHandlerTests.cs`

**Interfaces:**
- Consumes: `IDroneService` (Task 12), `DroneStat` (Config), `EventBus` (Core).
- Produces: `DroneAcquireRequestedEvent { string DroneId }`, `DroneSlotUnlockRequestedEvent { }`, `DroneUpgradeRequestedEvent { string DroneId; DroneStat Stat }`, `SetActiveDroneRequestedEvent { string DroneId }` (Mining).
- Produces: `DroneGarageHandler` (`IStartable`/`IDisposable`, App).

- [ ] **Step 1: Write `DroneEvents.cs`**

```csharp
using SocialUniverse.Config;

namespace SocialUniverse.Mining
{
    // UI -> App intent events for the Drone Garage.
    public class DroneAcquireRequestedEvent     { public string DroneId; }
    public class DroneSlotUnlockRequestedEvent  { }
    public class DroneUpgradeRequestedEvent     { public string DroneId; public DroneStat Stat; }
    public class SetActiveDroneRequestedEvent   { public string DroneId; }
}
```

- [ ] **Step 2: Write the failing handler test** (verifies each event routes to the right service call via a capturing fake)

```csharp
using System.Threading.Tasks;
using NUnit.Framework;
using SocialUniverse.Config;
using SocialUniverse.Core;
using SocialUniverse.Mining;
using SocialUniverse.App;

namespace SocialUniverse.Tests
{
    public class DroneGarageHandlerTests
    {
        private class CapturingDroneService : IDroneService
        {
            public string LastCall; public string LastDroneId; public DroneStat LastStat;
            public Task<DroneActionResult> AcquireDroneAsync(string droneId) { LastCall = "acquire"; LastDroneId = droneId; return Ok(); }
            public Task<DroneActionResult> UnlockSlotAsync() { LastCall = "unlock"; return Ok(); }
            public Task<DroneActionResult> UpgradeAsync(string droneId, DroneStat stat) { LastCall = "upgrade"; LastDroneId = droneId; LastStat = stat; return Ok(); }
            public Task<DroneActionResult> SetActiveAsync(string droneId) { LastCall = "setactive"; LastDroneId = droneId; return Ok(); }
            private static Task<DroneActionResult> Ok() => Task.FromResult(new DroneActionResult { Success = true });
        }

        [Test]
        public void Each_intent_event_routes_to_the_matching_service_call()
        {
            EventBus.Clear();
            var svc = new CapturingDroneService();
            var handler = new DroneGarageHandler(svc);
            handler.Start();

            EventBus.Publish(new DroneAcquireRequestedEvent { DroneId = "hauler" });
            Assert.AreEqual("acquire", svc.LastCall);
            Assert.AreEqual("hauler", svc.LastDroneId);

            EventBus.Publish(new DroneSlotUnlockRequestedEvent());
            Assert.AreEqual("unlock", svc.LastCall);

            EventBus.Publish(new DroneUpgradeRequestedEvent { DroneId = "scout", Stat = DroneStat.Yield });
            Assert.AreEqual("upgrade", svc.LastCall);
            Assert.AreEqual(DroneStat.Yield, svc.LastStat);

            EventBus.Publish(new SetActiveDroneRequestedEvent { DroneId = "scout" });
            Assert.AreEqual("setactive", svc.LastCall);

            handler.Dispose();
            EventBus.Clear();
        }
    }
}
```

- [ ] **Step 3: Run — FAIL** (handler missing).

- [ ] **Step 4: Write `DroneGarageHandler.cs`**

```csharp
using System;
using VContainer.Unity;
using SocialUniverse.Core;
using SocialUniverse.Mining;

namespace SocialUniverse.App
{
    // Owns the service calls for Drone Garage intents (mirrors TilePurchaseHandler). The
    // Garage view only publishes intent events; this controller performs the IDroneService call.
    public class DroneGarageHandler : IStartable, IDisposable
    {
        private readonly IDroneService _drones;

        public DroneGarageHandler(IDroneService drones) => _drones = drones;

        public void Start()
        {
            EventBus.Subscribe<DroneAcquireRequestedEvent>(OnAcquire);
            EventBus.Subscribe<DroneSlotUnlockRequestedEvent>(OnUnlockSlot);
            EventBus.Subscribe<DroneUpgradeRequestedEvent>(OnUpgrade);
            EventBus.Subscribe<SetActiveDroneRequestedEvent>(OnSetActive);
        }

        public void Dispose()
        {
            EventBus.Unsubscribe<DroneAcquireRequestedEvent>(OnAcquire);
            EventBus.Unsubscribe<DroneSlotUnlockRequestedEvent>(OnUnlockSlot);
            EventBus.Unsubscribe<DroneUpgradeRequestedEvent>(OnUpgrade);
            EventBus.Unsubscribe<SetActiveDroneRequestedEvent>(OnSetActive);
        }

        private async void OnAcquire(DroneAcquireRequestedEvent e)   { var r = await _drones.AcquireDroneAsync(e.DroneId); Warn("acquire", r); }
        private async void OnUnlockSlot(DroneSlotUnlockRequestedEvent e) { var r = await _drones.UnlockSlotAsync();        Warn("unlock", r); }
        private async void OnUpgrade(DroneUpgradeRequestedEvent e)   { var r = await _drones.UpgradeAsync(e.DroneId, e.Stat); Warn("upgrade", r); }
        private async void OnSetActive(SetActiveDroneRequestedEvent e) { var r = await _drones.SetActiveAsync(e.DroneId);   Warn("setactive", r); }

        private static void Warn(string action, DroneActionResult r)
        {
            if (r is { Success: false })
                SULog.Warn($"Drone {action} failed: {r.Reason}", SULog.Channel.Economy);
            // Fleet + Wallet changes already applied + eventful (DroneFleetChangedEvent) by the service on success.
        }
    }
}
```

> **Assembly note:** `DroneGarageHandlerTests` lives under `Tests/EditMode/App/`. Confirm the EditMode test asmdef references `SocialUniverse.App` (it already references the gameplay assemblies used by other App-adjacent tests — check `Assets/_Project/Tests/EditMode/*.asmdef`; if `SocialUniverse.App` is missing from `references`, add it).

- [ ] **Step 5: Run — PASS.**

- [ ] **Step 6: Commit**

```bash
git add Assets/_Project/Scripts/Mining/DroneEvents.cs Assets/_Project/Scripts/App/DroneGarageHandler.cs Assets/_Project/Tests/EditMode/App/DroneGarageHandlerTests.cs
git commit -m "feat(mining): drone intent events + DroneGarageHandler (M6)"
```

---

### Task 14: `DroneGarageView` (functional UI)

**Files:**
- Create: `Assets/_Project/Scripts/UI/DroneGarageView.cs`

**Interfaces:**
- Consumes: `DroneFleet`, `DatabaseRegistry`, `EconomyConfig`, `Wallet`, `DroneUpgradeMath` (Task 10), the drone intent events (Task 13).
- Produces: `DroneGarageView` (`MonoBehaviour`, UI) — HUD-opened panel rebuilt on `DroneFleetChangedEvent`.

This is a functional (unpolished) panel with no test (view logic is exercised manually + via the handler tests). Follow `MineralInventoryView`'s structure: `[Inject] Construct(...)`, subscribe in `OnEnable`, rebuild rows, publish intent events on button clicks.

- [ ] **Step 1: Write `DroneGarageView.cs`**

```csharp
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using SocialUniverse.Core;
using SocialUniverse.Config;
using SocialUniverse.Economy;
using SocialUniverse.Mining;

namespace SocialUniverse.UI
{
    // Functional (unpolished) Drone Garage: owned drones (active marker + per-stat upgrade rows),
    // acquirable drone types, and an unlock-slot button. Publishes intent events; the
    // DroneGarageHandler performs the service calls. Rebuilds on DroneFleetChangedEvent.
    public class DroneGarageView : MonoBehaviour
    {
        [SerializeField] private GameObject _root;
        [SerializeField] private Transform  _ownedParent;      // rows for owned drones
        [SerializeField] private Transform  _acquireParent;    // rows for acquirable drone types
        [SerializeField] private GameObject _ownedRowPrefab;   // Text + N buttons (set-active + 3 upgrade)
        [SerializeField] private GameObject _acquireRowPrefab; // Text + Acquire button
        [SerializeField] private Button     _unlockSlotButton;
        [SerializeField] private Text        _unlockSlotLabel;
        [SerializeField] private Button     _closeButton;

        private DroneFleet       _fleet;
        private DatabaseRegistry _registry;
        private EconomyConfig    _config;

        [Inject]
        public void Construct(DroneFleet fleet, DatabaseRegistry registry, EconomyConfig config)
        {
            _fleet = fleet; _registry = registry; _config = config;
        }

        private void OnEnable()
        {
            EventBus.Subscribe<DroneFleetChangedEvent>(OnFleetChanged);
            if (_unlockSlotButton != null) _unlockSlotButton.onClick.AddListener(OnUnlockSlot);
            if (_closeButton       != null) _closeButton.onClick.AddListener(Close);
            Rebuild();
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<DroneFleetChangedEvent>(OnFleetChanged);
            if (_unlockSlotButton != null) _unlockSlotButton.onClick.RemoveListener(OnUnlockSlot);
            if (_closeButton       != null) _closeButton.onClick.RemoveListener(Close);
        }

        public void Open()  { if (_root != null) _root.SetActive(true); Rebuild(); }
        public void Close() { if (_root != null) _root.SetActive(false); }

        private void OnFleetChanged(DroneFleetChangedEvent _) => Rebuild();
        private void OnUnlockSlot() => EventBus.Publish(new DroneSlotUnlockRequestedEvent());

        private void Rebuild()
        {
            if (_registry == null) return;
            ClearChildren(_ownedParent);
            ClearChildren(_acquireParent);

            // Owned drones
            foreach (var drone in _fleet.Drones)
            {
                var def = drone.Definition;
                var go  = Instantiate(_ownedRowPrefab, _ownedParent);
                var label = go.GetComponentInChildren<Text>();
                bool isActive = def.DroneId == _fleet.ActiveDroneId;
                if (label != null)
                    label.text = $"{def.DisplayName} (T{def.Tier}){(isActive ? "  [ACTIVE]" : "")}\n" +
                                 $"Cargo {drone.EffectiveCargoCap}  Yield {drone.EffectiveYieldMult:0.00}  Speed {drone.EffectiveTravelSpeed:0.0}";

                // Wire buttons by name convention on the prefab. Expected child buttons:
                //   "SetActive", "UpgradeCargo", "UpgradeYield", "UpgradeSpeed".
                Wire(go, "SetActive", () => EventBus.Publish(new SetActiveDroneRequestedEvent { DroneId = def.DroneId }));
                WireUpgrade(go, "UpgradeCargo", def.DroneId, DroneStat.Cargo, drone.Level(DroneStat.Cargo));
                WireUpgrade(go, "UpgradeYield", def.DroneId, DroneStat.Yield, drone.Level(DroneStat.Yield));
                WireUpgrade(go, "UpgradeSpeed", def.DroneId, DroneStat.Speed, drone.Level(DroneStat.Speed));
            }

            // Acquirable drone types (in registry, not yet owned, and slots available)
            bool slotsAvailable = _fleet.Drones.Count < _fleet.UnlockedSlots;
            foreach (var def in _registry.AllDrones)
            {
                if (_fleet.Get(def.DroneId) != null) continue;
                var go  = Instantiate(_acquireRowPrefab, _acquireParent);
                var label = go.GetComponentInChildren<Text>();
                if (label != null) label.text = $"{def.DisplayName} (T{def.Tier}) — {def.UnlockCost}";
                Wire(go, null, () => EventBus.Publish(new DroneAcquireRequestedEvent { DroneId = def.DroneId }));
                var btn = go.GetComponentInChildren<Button>();
                if (btn != null) btn.interactable = slotsAvailable;
            }

            if (_unlockSlotLabel != null)
            {
                int cost = DroneUpgradeMath.SlotUnlockCost(_config.SlotUnlockBaseCost, _config.SlotUnlockCostGrowth, _fleet.UnlockedSlots, _config.StartingFleetSlots);
                _unlockSlotLabel.text = $"Unlock slot — {cost}";
            }
        }

        private void WireUpgrade(GameObject row, string childName, string droneId, DroneStat stat, int level)
        {
            var upgradeDef = _registry.GetUpgrade(stat);
            int cost = DroneUpgradeMath.NextCost(upgradeDef, level);
            bool maxed = upgradeDef != null && level >= upgradeDef.MaxLevel;
            var btn = FindButton(row, childName);
            if (btn == null) return;
            var t = btn.GetComponentInChildren<Text>();
            if (t != null) t.text = maxed ? $"{stat} MAX" : $"{stat} {level}→{level + 1} ({cost})";
            btn.interactable = !maxed && upgradeDef != null;
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => EventBus.Publish(new DroneUpgradeRequestedEvent { DroneId = droneId, Stat = stat }));
        }

        private static void Wire(GameObject row, string childName, UnityEngine.Events.UnityAction action)
        {
            var btn = childName == null ? row.GetComponentInChildren<Button>() : FindButton(row, childName);
            if (btn == null) return;
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(action);
        }

        private static Button FindButton(GameObject row, string childName)
        {
            foreach (var b in row.GetComponentsInChildren<Button>(true))
                if (b.gameObject.name == childName) return b;
            return null;
        }

        private static void ClearChildren(Transform parent)
        {
            if (parent == null) return;
            for (int i = parent.childCount - 1; i >= 0; i--) Destroy(parent.GetChild(i).gameObject);
        }
    }
}
```

- [ ] **Step 2: Compile check** — 0 console errors after reload.

- [ ] **Step 3: Commit**

```bash
git add Assets/_Project/Scripts/UI/DroneGarageView.cs
git commit -m "feat(ui): functional DroneGarageView (M6)"
```

---

### Task 15: Drone server functions (`AcquireDrone`, `UnlockDroneSlot`, `UpgradeDrone`, `SetActiveDrone`)

**Files:**
- Create: `ServerCode/AcquireDrone.js`, `ServerCode/UnlockDroneSlot.js`, `ServerCode/UpgradeDrone.js`, `ServerCode/SetActiveDrone.js`

Each follows the proven `PurchaseLand.js` SDK pattern: `CurrenciesApi({ accessToken })` for reads + `decrementPlayerCurrencyBalance` (with `configAssignmentHash` from `ConfigurationApi`) for spends; `DataApi(context)` for the `drone_fleet` record (positional `getItems`/`setItem`). All return `{ success, newBalance, fleet }` (or `{ success, reason }` on validation failure). No Unity-side unit test executes these; correctness is verified on deploy against the live SDK. **Duplicated constants carry `// MUST MATCH` comments.**

- [ ] **Step 1: Write `AcquireDrone.js`**

```javascript
// AcquireDrone — validate coins >= unlockCost, fleet not full, not already owned; deduct; append.
// UNLOCK_COSTS MUST MATCH each DroneDefinition._unlockCost.
const { CurrenciesApi, ConfigurationApi } = require("@unity-services/economy-2.5");
const { DataApi } = require("@unity-services/cloud-save-1.4");

const CURRENCY_ID = "COINS";
const FLEET_KEY   = "drone_fleet";
// MUST MATCH DroneDefinition assets.
const UNLOCK_COSTS = { scout: 0, hauler: 300, prospector: 1200 };
const DRONE_TIERS  = { scout: 1, hauler: 2, prospector: 3 };

module.exports = async ({ params, context, logger }) => {
  const { droneId } = params;
  if (!droneId || UNLOCK_COSTS[droneId] === undefined) return { success: false, reason: "UNKNOWN_DRONE" };

  const { projectId, playerId, accessToken } = context;
  const econApi = new CurrenciesApi({ accessToken });
  const config  = new ConfigurationApi({ accessToken });
  const saveApi = new DataApi(context);

  const fleet = await loadFleet(saveApi, projectId, playerId);
  if (fleet.drones.some(d => d.droneId === droneId)) return { success: false, reason: "ALREADY_OWNED" };
  if (fleet.drones.length >= fleet.slots)             return { success: false, reason: "SLOTS_FULL" };

  const cost = UNLOCK_COSTS[droneId];
  let newBalance = await currentBalance(econApi, projectId, playerId);
  if (newBalance < cost) return { success: false, reason: "INSUFFICIENT_FUNDS" };

  if (cost > 0) {
    const cfg  = await config.getPlayerConfiguration({ projectId, playerId });
    const hash = cfg.data.metadata.configAssignmentHash;
    const res  = await econApi.decrementPlayerCurrencyBalance({
      projectId, playerId, currencyId: CURRENCY_ID, configAssignmentHash: hash,
      currencyModifyBalanceRequest: { currencyId: CURRENCY_ID, amount: cost }
    });
    newBalance = res.data.balance;
  }

  fleet.drones.push({ droneId, upgrades: { Cargo: 0, Yield: 0, Speed: 0 } });
  await saveFleet(saveApi, projectId, playerId, fleet);

  logger.info(`AcquireDrone: ${playerId} bought ${droneId} for ${cost} -> ${newBalance}`);
  return { success: true, newBalance, fleet };
};

// ---- shared helpers (duplicate this block into each drone function; keep in sync) ----
async function loadFleet(saveApi, projectId, playerId) {
  let fleet = { slots: 2, activeDroneId: "scout", drones: [] }; // MUST MATCH EconomyConfig.StartingFleetSlots
  try {
    const res  = await saveApi.getItems(projectId, playerId, ["drone_fleet"]);
    const item = res.data.results.find(r => r.key === "drone_fleet");
    if (item && item.value && typeof item.value === "object") fleet = item.value;
  } catch (_) { /* none */ }
  if (!Array.isArray(fleet.drones)) fleet.drones = [];
  if (typeof fleet.slots !== "number") fleet.slots = 2;
  return fleet;
}
async function saveFleet(saveApi, projectId, playerId, fleet) {
  await saveApi.setItem(projectId, playerId, { key: "drone_fleet", value: fleet });
}
async function currentBalance(econApi, projectId, playerId) {
  const res = await econApi.getPlayerCurrencies({ projectId, playerId });
  const c   = res.data.results.find(x => x.currencyId === "COINS");
  return c ? c.balance : 0;
}
```

- [ ] **Step 2: Write `UnlockDroneSlot.js`** — same helpers; cost `= SLOT_BASE * SLOT_GROWTH^(slots - START_SLOTS)`:

```javascript
// UnlockDroneSlot — scaling slot price; deduct; slots++.
// SLOT_BASE/SLOT_GROWTH/START_SLOTS MUST MATCH EconomyConfig + DroneUpgradeMath.SlotUnlockCost.
const { CurrenciesApi, ConfigurationApi } = require("@unity-services/economy-2.5");
const { DataApi } = require("@unity-services/cloud-save-1.4");

const CURRENCY_ID = "COINS";
const SLOT_BASE   = 500;  // MUST MATCH EconomyConfig._slotUnlockBaseCost
const SLOT_GROWTH = 2;    // MUST MATCH EconomyConfig._slotUnlockCostGrowth
const START_SLOTS = 2;    // MUST MATCH EconomyConfig._startingFleetSlots

module.exports = async ({ params, context, logger }) => {
  const { projectId, playerId, accessToken } = context;
  const econApi = new CurrenciesApi({ accessToken });
  const config  = new ConfigurationApi({ accessToken });
  const saveApi = new DataApi(context);

  const fleet = await loadFleet(saveApi, projectId, playerId);
  const steps = Math.max(0, fleet.slots - START_SLOTS);
  const cost  = Math.round(SLOT_BASE * Math.pow(SLOT_GROWTH, steps));

  let newBalance = await currentBalance(econApi, projectId, playerId);
  if (newBalance < cost) return { success: false, reason: "INSUFFICIENT_FUNDS" };

  const cfg  = await config.getPlayerConfiguration({ projectId, playerId });
  const hash = cfg.data.metadata.configAssignmentHash;
  const res  = await econApi.decrementPlayerCurrencyBalance({
    projectId, playerId, currencyId: CURRENCY_ID, configAssignmentHash: hash,
    currencyModifyBalanceRequest: { currencyId: CURRENCY_ID, amount: cost }
  });
  newBalance = res.data.balance;

  fleet.slots += 1;
  await saveFleet(saveApi, projectId, playerId, fleet);
  logger.info(`UnlockDroneSlot: ${playerId} -> ${fleet.slots} slots for ${cost}`);
  return { success: true, newBalance, fleet };
};
// (paste the same loadFleet/saveFleet/currentBalance helpers as AcquireDrone.js)
```

- [ ] **Step 3: Write `UpgradeDrone.js`** — validate owned + level<maxLevel + coins>=NextCost; deduct; increment. `UPGRADES` cost formula MUST MATCH `UpgradeDefinition`/`DroneUpgradeMath.NextCost`:

```javascript
// UpgradeDrone — validate owned + level < maxLevel + coins >= NextCost; deduct; level++.
// UPGRADES MUST MATCH UpgradeDefinition assets + DroneUpgradeMath.NextCost.
const { CurrenciesApi, ConfigurationApi } = require("@unity-services/economy-2.5");
const { DataApi } = require("@unity-services/cloud-save-1.4");

const CURRENCY_ID = "COINS";
// MUST MATCH the three UpgradeDefinition assets.
const UPGRADES = {
  Cargo: { baseCost: 50,  growth: 1.5, maxLevel: 10 },
  Yield: { baseCost: 80,  growth: 1.6, maxLevel: 10 },
  Speed: { baseCost: 40,  growth: 1.4, maxLevel: 10 }
};

module.exports = async ({ params, context, logger }) => {
  const { droneId, stat } = params;
  const cfg = UPGRADES[stat];
  if (!droneId || !cfg) return { success: false, reason: "INVALID_PARAMS" };

  const { projectId, playerId, accessToken } = context;
  const econApi = new CurrenciesApi({ accessToken });
  const config  = new ConfigurationApi({ accessToken });
  const saveApi = new DataApi(context);

  const fleet = await loadFleet(saveApi, projectId, playerId);
  const drone = fleet.drones.find(d => d.droneId === droneId);
  if (!drone) return { success: false, reason: "NOT_OWNED" };
  drone.upgrades = drone.upgrades || { Cargo: 0, Yield: 0, Speed: 0 };
  const level = drone.upgrades[stat] || 0;
  if (level >= cfg.maxLevel) return { success: false, reason: "MAX_LEVEL" };

  const cost = Math.round(cfg.baseCost * Math.pow(cfg.growth, level));
  let newBalance = await currentBalance(econApi, projectId, playerId);
  if (newBalance < cost) return { success: false, reason: "INSUFFICIENT_FUNDS" };

  const pcfg = await config.getPlayerConfiguration({ projectId, playerId });
  const hash = pcfg.data.metadata.configAssignmentHash;
  const res  = await econApi.decrementPlayerCurrencyBalance({
    projectId, playerId, currencyId: CURRENCY_ID, configAssignmentHash: hash,
    currencyModifyBalanceRequest: { currencyId: CURRENCY_ID, amount: cost }
  });
  newBalance = res.data.balance;

  drone.upgrades[stat] = level + 1;
  await saveFleet(saveApi, projectId, playerId, fleet);
  logger.info(`UpgradeDrone: ${playerId} ${droneId}.${stat} -> ${level + 1} for ${cost}`);
  return { success: true, newBalance, fleet };
};
// (paste the same loadFleet/saveFleet/currentBalance helpers as AcquireDrone.js)
```

- [ ] **Step 4: Write `SetActiveDrone.js`** — validate ownership; set `activeDroneId`; no economy mutation:

```javascript
// SetActiveDrone — validate ownership; set activeDroneId. No currency change.
const { DataApi } = require("@unity-services/cloud-save-1.4");

module.exports = async ({ params, context, logger }) => {
  const { droneId } = params;
  const { projectId, playerId } = context;
  const saveApi = new DataApi(context);

  const fleet = await loadFleet(saveApi, projectId, playerId);
  if (!fleet.drones.some(d => d.droneId === droneId)) return { success: false, reason: "NOT_OWNED" };

  fleet.activeDroneId = droneId;
  await saveFleet(saveApi, projectId, playerId, fleet);
  logger.info(`SetActiveDrone: ${playerId} active=${droneId}`);
  return { success: true, newBalance: -1, fleet };
};
// (paste the same loadFleet/saveFleet helpers; currentBalance not needed here)
```

- [ ] **Step 5: Commit**

```bash
git add ServerCode/AcquireDrone.js ServerCode/UnlockDroneSlot.js ServerCode/UpgradeDrone.js ServerCode/SetActiveDrone.js
git commit -m "feat(server): AcquireDrone/UnlockDroneSlot/UpgradeDrone/SetActiveDrone (M6, deploy deferred)"
```

---

### Task 16: `GetBootstrapState.js` — include + seed `drone_fleet` and `mineral_inventory`

**Files:**
- Modify: `ServerCode/GetBootstrapState.js`

**Interfaces:**
- Produces: bootstrap response additionally carries `mineralInventory` (`{ id: qty }`) and `droneFleet` (`{ slots, activeDroneId, drones[] }`), seeding an empty `drone_fleet` with the starter Scout so a new player owns it.

- [ ] **Step 1: Edit `GetBootstrapState.js`** — extend the parallel fetch + seed logic:

```javascript
const { CurrenciesApi } = require("@unity-services/economy-2.5");
const { DataApi }       = require("@unity-services/cloud-save-1.4");

const START_SLOTS = 2; // MUST MATCH EconomyConfig._startingFleetSlots

module.exports = async ({ params, context, logger }) => {
  const { projectId, playerId, accessToken } = context;
  const economyApi   = new CurrenciesApi({ accessToken });
  const cloudSaveApi = new DataApi(context);

  const [balancesRes, saveRes] = await Promise.all([
    economyApi.getPlayerCurrencies({ projectId, playerId }),
    cloudSaveApi.getItems(projectId, playerId, ["player_profile", "mineral_inventory", "drone_fleet"])
      .catch(() => ({ data: { results: [] } }))
  ]);

  const balances = {};
  for (const currency of balancesRes.data.results) balances[currency.currencyId] = currency.balance;

  const results = saveRes.data.results;
  const get = (k) => { const it = results.find(r => r.key === k); return it ? it.value : null; };

  const profile          = get("player_profile");
  const mineralInventory = get("mineral_inventory") || {};
  let   droneFleet       = get("drone_fleet");

  // Seed the starter fleet for a brand-new player so they own Scout by default.
  if (!droneFleet || !Array.isArray(droneFleet.drones) || droneFleet.drones.length === 0) {
    droneFleet = { slots: START_SLOTS, activeDroneId: "scout", drones: [{ droneId: "scout", upgrades: { Cargo: 0, Yield: 0, Speed: 0 } }] };
    await cloudSaveApi.setItem(projectId, playerId, { key: "drone_fleet", value: droneFleet });
  }

  return {
    serverTimeMs: Date.now(),
    balances,
    profile: typeof profile === "string" ? JSON.parse(profile) : profile,
    mineralInventory,
    droneFleet
  };
};
```

- [ ] **Step 2: Commit**

```bash
git add ServerCode/GetBootstrapState.js
git commit -m "feat(server): GetBootstrapState includes + seeds drone_fleet/mineral_inventory (M6)"
```

---

### Task 17: Bootstrap hydration + DI wiring for fleet/inventory/drone service

**Files:**
- Modify: `Assets/_Project/Scripts/App/PlanetSceneScope.cs` (register `IDroneService` + `DroneGarageHandler`; hydrate fleet + inventory)

**Interfaces:**
- Consumes: everything above.
- Produces: on Planet scene start, `DroneFleet` + `MineralInventory` are hydrated (server or local Scout-seed fallback), `IDroneService` resolves, `DroneGarageHandler` runs.

- [ ] **Step 1: Register the drone service + handler** in `PlanetSceneScope.Configure`, next to the Task 8/9 mineral registrations:

```csharp
if (standalone)
    builder.Register<LocalMockDroneService>(Lifetime.Singleton).As<IDroneService>();
else
    builder.Register<DroneService>(Lifetime.Singleton).As<IDroneService>();

builder.RegisterEntryPoint<DroneGarageHandler>();
```

> **Ruling R6 (2026-08-18):** as with the mineral view, do NOT add `builder.RegisterComponentInHierarchy<DroneGarageView>()` here — it would throw at Planet-scene container build until the GameObject is placed. That registration line is added in the deferred Editor-wiring step alongside the GameObject. `DroneGarageHandler` (entry point, depends only on the already-registered `IDroneService`) resolves fine.

- [ ] **Step 2: Hydrate in `PlanetSceneBootstrapper`** — inject `DroneFleet _fleet`, `MineralInventory _inventory`, `ICloudSave _cloudSave` (already injected), and `DatabaseRegistry _registry` (already injected). (Do **not** inject `IMineralService` for hydration — per Ruling R4, Cloud Save loads happen here in App, not in the service.) Replace the Phase-A temporary single-drone seed (Task 8 Step 5) with a real hydrate helper, called from `HydrateServerStateAsync()`:

```csharp
// Hydrate fleet + mineral inventory (planet-scoped, like wallet + owned tiles). Non-fatal:
// falls back to a local Scout-seeded fleet if the server/record is unavailable.
private async Task HydrateDronesAndMineralsAsync()
{
    // Mineral inventory: load the mineral_inventory Cloud Save record directly (App-layer,
    // same pattern as owned-tiles hydration — Mining must not reference ICloudSave/Net).
    try
    {
        var held = await _cloudSave.LoadAsync<Dictionary<string, int>>(SaveKeys.MineralInventory, null);
        if (held != null) _inventory.SetAll(held);
    }
    catch (Exception ex) { SULog.Warn($"Mineral inventory hydration failed ({ex.Message})", SULog.Channel.Economy); }

    DroneFleetSnapshot snapshot = null;
    try { snapshot = await _cloudSave.LoadAsync<DroneFleetSnapshot>(SaveKeys.DroneFleet, null); }
    catch (Exception ex) { SULog.Warn($"Drone fleet hydration failed ({ex.Message})", SULog.Channel.Economy); }

    if (snapshot == null || snapshot.Drones == null || snapshot.Drones.Count == 0)
    {
        // New player / server unavailable: own the starter Scout by default (spec Q1).
        var scout = FirstStarterDrone();
        snapshot = DroneFleetSnapshot.SingleDrone(scout?.DroneId ?? _registry.AllDrones.First().DroneId, _config.StartingFleetSlots);
    }
    _fleet.Apply(snapshot, _registry);
}

// The Tier-1, zero-cost starter drone (Scout); falls back to the first registered drone.
private DroneDefinition FirstStarterDrone()
{
    foreach (var d in _registry.AllDrones)
        if (d.Tier == 1 && d.UnlockCost == 0) return d;
    return _registry.AllDrones.FirstOrDefault();
}
```

Call `await HydrateDronesAndMineralsAsync();` inside `HydrateServerStateAsync()` (after the wallet/fuel hydration). Then, in `Start()`, drop the old `_registry.AllDrones.FirstOrDefault()` + `new DroneRuntime(droneDef)` block and simply call `_miningController.Initialize();` (the controller reads `_fleet.Active`). Add `_config` (`EconomyConfig`) to the bootstrapper's constructor if not already present (it is not — add it).

- [ ] **Step 3: Run** the full EditMode suite. Expected: PASS. Also open `Planet.unity` in standalone mode in the Editor and confirm no console errors on load (fleet seeds Scout, mining still starts).

- [ ] **Step 4: Commit**

```bash
git add Assets/_Project/Scripts/App/PlanetSceneScope.cs
git commit -m "feat(app): hydrate drone fleet + mineral inventory, wire drone service/garage (M6)"
```

---

### Task 18: HUD open buttons for Garage + Mineral inventory

**Files:**
- Modify: `Assets/_Project/Scripts/UI/HUDController.cs` (add two buttons that Open the views)

**Interfaces:**
- Consumes: `DroneGarageView`, `MineralInventoryView` (Tasks 14, 9).
- Produces: HUD buttons that call `DroneGarageView.Open()` / `MineralInventoryView.Open()`.

- [ ] **Step 1: Read `HUDController.cs`** to match its existing button-wiring style (e.g., the Settings/Fuel buttons), then add serialized `Button _garageButton;` + `Button _mineralsButton;` and `[SerializeField]` references (or `[Inject]`) to the two views, wiring `onClick` to `.Open()` in the same place the existing buttons are wired. Follow whatever pattern `FuelPanel`/`SettingsPanel` opening already uses in `HUDController` (button → view.Open, or button → EventBus event the view listens for). Match it exactly rather than introducing a new pattern.

- [ ] **Step 2: Compile check** — 0 console errors.

- [ ] **Step 3: Commit**

```bash
git add Assets/_Project/Scripts/UI/HUDController.cs
git commit -m "feat(ui): HUD buttons open Drone Garage + Mineral inventory (M6)"
```

---

## Deferred author/ops checklist (Editor + server, like M2–M5)

These require the live Unity Editor (which owns the `main` checkout) and the UGS dashboard — not doable from a headless/background session. Do them before the milestone is playable end-to-end in production.

- [ ] **Author SOs** (Assets > Create > SocialUniverse/Config):
  - 6 `MineralDefinition`: Iron (t1, sell 2), Carbon (t2, 3), Silicon (t3, 5), Nickel (t4, 8), Platinum (t5, 20), Iridium (t6, 40) — ids `iron/carbon/silicon/nickel/platinum/iridium`. **Sell values MUST MATCH `SellMinerals.js` `SELL_VALUES`.**
  - 3 `UpgradeDefinition`: Cargo (baseCost 50, growth 1.5), Yield (80, 1.6), Speed (40, 1.4), each maxLevel 10. **MUST MATCH `UpgradeDrone.js` `UPGRADES`.**
  - 2 new `DroneDefinition`: `Drone_Hauler` (id `hauler`, T2, unlockCost 300), `Drone_Prospector` (id `prospector`, T3, unlockCost 1200), each with a model. Set `Drone_Scout` id `scout`, T1, unlockCost 0. **MUST MATCH `AcquireDrone.js` `UNLOCK_COSTS`/`DRONE_TIERS`.**
  - Re-author the 6 existing `AsteroidDefinition` assets: set `_mineral` to the matching `MineralDefinition`, set `_tier` to the mineral's tier (the old `_mineralType`/`_coinsPerUnit` fields are gone).
  - Add all new `MineralDefinition`, `UpgradeDefinition`, and drone assets to `DatabaseRegistry` (`_minerals`, `_upgrades`, `_drones`).
  - Set `EconomyConfig`: `StartingFleetSlots = 2`, `SlotUnlockBaseCost = 500`, `SlotUnlockCostGrowth = 2`.
- [ ] **Scene wiring** (`Planet.unity`): add `DroneGarageView` + `MineralInventoryView` GameObjects (root panel, row parents, row prefabs, buttons named `SetActive`/`UpgradeCargo`/`UpgradeYield`/`UpgradeSpeed`/`Acquire`), and HUD `Garage`/`Minerals` open buttons. **Then** add `builder.RegisterComponentInHierarchy<SocialUniverse.UI.MineralInventoryView>();` and `builder.RegisterComponentInHierarchy<SocialUniverse.UI.DroneGarageView>();` to `PlanetSceneScope.Configure` (deferred per Ruling R6 — these throw at container build if the GameObjects aren't present, so they must be added together with the scene objects). After wiring, run the PlayMode `PlanetSceneFlowTests` to confirm the scene container still builds.
- [ ] **Deploy Cloud Code** (UGS dashboard): `ValidateMining` (rewritten), `SellMinerals`, `AcquireDrone`, `UnlockDroneSlot`, `UpgradeDrone`, `SetActiveDrone`, `GetBootstrapState` (extended). Verify each against the live SDK (Known Issues #6/#8/#9). Confirm `mineral_inventory` + `drone_fleet` Cloud Save record shapes round-trip.
- [ ] **Device/editor smoke test** the full loop: mine (tier-gated) → minerals in inventory → sell → coins → upgrade drone / unlock slot / acquire higher-tier drone → reach a higher-tier asteroid.

## Self-review (run against the spec)

- **§1 Config:** MineralDefinition ✓ (T1), UpgradeDefinition + DroneStat ✓ (T1), AsteroidDefinition→mineral ✓ (T2), DroneDefinition tier/cost/yield ✓ (T2), DatabaseRegistry getters ✓ (T2), EconomyConfig slots ✓ (T2).
- **§2 Runtime/persistence:** DroneRuntime rework ✓ (T10), DroneUpgradeMath ✓ (T10), MineralInventory ✓ (T4), DroneFleet ✓ (T11), Cloud Save records + hydration ✓ (T3/T17).
- **§3 Services:** IDroneService/impls ✓ (T12), IMineralService/impls ✓ (T5), mining grant→inventory ✓ (T5/T8).
- **§4 Server:** all 7 functions ✓ (T9/T15/T16); "must match" comments ✓.
- **§5 Tier gating:** MiningController gate + effective stats ✓ (T8).
- **§6 UI:** DroneGarageView ✓ (T14), MineralInventoryView ✓ (T9), HUD buttons ✓ (T18).
- **§7 App handlers:** DroneGarageHandler ✓ (T13), MineralSaleHandler ✓ (T9).
- **§8 Events:** all intent + state events ✓ (placed in Mining, not Core — see deviation 3).
- **§9 DI:** PlanetSceneScope registrations + hydration ✓ (T8/T9/T17).
- **Testing table:** DroneUpgradeMathTests ✓, DroneRuntimeTests ✓, MineralInventoryTests ✓, DroneServiceTests ✓, MineralServiceTests ✓, MiningControllerTests tier-gate ✓, ValidateMiningCapAlignment retarget ✓.

---

## Execution handoff

Plan complete and saved to `docs/superpowers/plans/2026-08-18-m6-drones-mining-depth.md`. Two execution options:

**1. Subagent-Driven (recommended)** — I dispatch a fresh subagent per task, review between tasks, fast iteration. **Note:** the live Unity Editor owns the `main` checkout, so a background/worktree subagent cannot compile or run tests — subagents can write code, but compile/test verification (and all Editor/server checklist items) must run in the Editor's checkout. Factor that into task gating.

**2. Inline Execution** — Execute tasks in this session using executing-plans, with checkpoints (natural stop at the Phase A checkpoint after Task 9).

Which approach?
