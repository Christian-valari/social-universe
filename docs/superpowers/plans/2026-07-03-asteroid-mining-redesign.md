# Asteroid Mining Redesign Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Rework asteroid mining into two explicit, asteroid-scoped modes — a drone-based Idle Mining flow that survives the app being closed, and a standalone tap-timing Active Mining minigame — that pay out identical, server-validated rewards for a given asteroid.

**Architecture:** A new `MiningRewardCalculator` is the single source of truth deriving idle duration, active tap count, total coin payout, and the `coinsPerSec` cap basis from an asteroid's remaining yield. `MiningController` orchestrates both modes (idle sessions gated by a single drone; active sessions standalone) and is the only thing that talks to `IEconomyService`/`AsteroidSpawner` for payout and respawn. Idle-session state persists to `PlayerPrefs` keyed by a stable per-asteroid `SlotId` so it survives an app restart without needing frame-by-frame ticking while closed (`IdleMiningSession` derives its stage from `DateTime.UtcNow - StartUtc`, not accumulated deltaTime).

**Tech Stack:** Unity 6, C#, VContainer (DI), NUnit (EditMode/PlayMode tests), Unity Gaming Services Cloud Code (ServerCode/*.js).

## Global Constraints

- Server-authoritative economy: mining payouts must go through a server-validated call, never a client-computed grant with no server check (Architecture Rule 1, CLAUDE.md).
- Backend behind interfaces: gameplay code depends on `IEconomyService`, never a backend SDK directly (Architecture Rule 2).
- Tunable numbers live in `EconomyConfig`/`PlanetDefinition` ScriptableObjects, not hardcoded (Architecture Rule 3).
- One responsibility per script; namespaces mirror folders (`SocialUniverse.Mining`, `SocialUniverse.Economy`, `SocialUniverse.UI`).
- No backwards-compatibility shims: this is a pre-launch prototype project, so breaking changes to `PlayerPrefs` save formats are acceptable and should not be migrated — just changed cleanly.
- Spec: `docs/superpowers/specs/2026-07-03-asteroid-mining-redesign-design.md`.

---

### Task 1: Config & data model changes

**Files:**
- Modify: `Assets/_Project/Scripts/Config/EconomyConfig.cs`
- Modify: `Assets/_Project/Scripts/Config/PlanetDefinition.cs`

**Interfaces:**
- Produces: `EconomyConfig.IdleSecondsPerYieldUnit`, `.MinIdleSessionSeconds`, `.MaxIdleSessionSeconds`, `.ActiveYieldPerTap`, `.MinActiveTaps`, `.MaxActiveTaps`, `.ActiveTapWindowSeconds`, `.ActiveMaxErrors` (all `float`/`int` public getters); `PlanetDefinition.AsteroidFieldSize` (`int` public getter). Removes `EconomyConfig.IdleMiningRate`, `.MaxOfflineHours`, `.IdleSessionClaimTaps`, `.ActiveTapYield`, `.CritChance`, `.CritMultiplier`.

This is a data-only change (ScriptableObject fields); no unit test — later tasks exercise these fields through consuming logic.

- [ ] **Step 1: Replace the mining config fields in `EconomyConfig.cs`**

Replace the three `[Header("Mining — ...")]` blocks (lines 37–49 in the current file) with:

```csharp
        [Header("Mining — Shared")]
        [SerializeField] private float _asteroidRespawnHours    = 4f;   // claimed asteroid is destroyed and respawns after this many real-world hours

        [Header("Mining — Idle")]
        [SerializeField] private float _idleSecondsPerYieldUnit = 3f;    // idle duration scales with the asteroid's remaining yield
        [SerializeField] private float _minIdleSessionSeconds   = 30f;   // clamp: smallest asteroids still take at least this long
        [SerializeField] private float _maxIdleSessionSeconds   = 1800f; // clamp: largest asteroids cap out at this long (30 min)

        [Header("Mining — Active")]
        [SerializeField] private float _activeYieldPerTap       = 8f;    // how much RemainingYield one successful tap represents
        [SerializeField] private int   _minActiveTaps           = 5;     // clamp: smallest asteroids still take at least this many taps
        [SerializeField] private int   _maxActiveTaps            = 20;    // clamp: largest asteroids cap out at this many taps
        [SerializeField] private float _activeTapWindowSeconds  = 1.2f;  // time allowed to hit each spawned target point
        [SerializeField] private int   _activeMaxErrors         = 3;     // misses/timeouts before the asteroid is lost
```

Replace the matching public getters (which currently read `IdleMiningRate`, `MaxOfflineHours`, `IdleSessionDuration`, `IdleSessionClaimTaps`, `AsteroidRespawnHours`, `ActiveTapYield`, `CritChance`, `CritMultiplier`) with:

```csharp
        public float AsteroidRespawnHours  => _asteroidRespawnHours;

        public float IdleSecondsPerYieldUnit => _idleSecondsPerYieldUnit;
        public float MinIdleSessionSeconds   => _minIdleSessionSeconds;
        public float MaxIdleSessionSeconds   => _maxIdleSessionSeconds;

        public float ActiveYieldPerTap      => _activeYieldPerTap;
        public int   MinActiveTaps          => _minActiveTaps;
        public int   MaxActiveTaps          => _maxActiveTaps;
        public float ActiveTapWindowSeconds => _activeTapWindowSeconds;
        public int   ActiveMaxErrors        => _activeMaxErrors;
```

- [ ] **Step 2: Add `AsteroidFieldSize` to `PlanetDefinition.cs`**

Add a field alongside `_asteroidTier`:

```csharp
        [SerializeField] private int _asteroidFieldSize = 6; // total asteroids simultaneously present on this planet
```

Add the getter alongside `AsteroidTier`:

```csharp
        public int AsteroidFieldSize => _asteroidFieldSize;
```

- [ ] **Step 3: Verify the project still compiles**

Open Unity (or run a headless compile check) and confirm no compile errors yet — other scripts still reference the old `EconomyConfig` members and will fail to compile until later tasks update them. Skip fixing those now; just confirm this file's own syntax is valid via `mcp__UnityMCP__read_console` if Unity is open, otherwise proceed (later tasks fix all call sites and Task 18 does the final full-project compile check).

- [ ] **Step 4: Commit**

```bash
git add Assets/_Project/Scripts/Config/EconomyConfig.cs Assets/_Project/Scripts/Config/PlanetDefinition.cs
git commit -m "config: replace mining tunables for idle/active parity, add per-planet asteroid field size"
```

---

### Task 2: `Asteroid.SlotId` + `AsteroidSpawner` field-size distribution and slot lookup

**Files:**
- Modify: `Assets/_Project/Scripts/Mining/Asteroid.cs`
- Modify: `Assets/_Project/Scripts/Mining/AsteroidSpawner.cs`
- Test: `Assets/_Project/Tests/EditMode/Mining/AsteroidSpawnerDistributionTests.cs`

**Interfaces:**
- Consumes: `PlanetDefinition.AsteroidFieldSize` (Task 1); `AsteroidDefinition.Rarity` (existing).
- Produces: `Asteroid.SlotId` (`string` public getter); `Asteroid.Initialize(AsteroidDefinition definition, string slotId)` (signature change from the current single-arg `Initialize`); `AsteroidSpawner.FindBySlotId(string slotId) → Asteroid` (public); `AsteroidSpawner.DistributeFieldSize(AsteroidDefinition[] types, int fieldSize) → int[]` (public static, pure). Consumed by Task 3 (test helper), Task 10 (`MiningController` persistence).

- [ ] **Step 1: Write the failing distribution test**

Create `Assets/_Project/Tests/EditMode/Mining/AsteroidSpawnerDistributionTests.cs`:

```csharp
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using SocialUniverse.Config;
using SocialUniverse.Mining;
using UnityEngine;

namespace SocialUniverse.Tests
{
    public class AsteroidSpawnerDistributionTests
    {
        private static AsteroidDefinition MakeDef(float rarity)
        {
            var def = ScriptableObject.CreateInstance<AsteroidDefinition>();
            typeof(AsteroidDefinition).GetField("_rarity", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(def, rarity);
            return def;
        }

        [Test]
        public void Counts_always_sum_to_field_size()
        {
            var types = new[] { MakeDef(0.7f), MakeDef(0.5f), MakeDef(0.1f) };

            foreach (int fieldSize in new[] { 1, 3, 6, 10, 25 })
            {
                var counts = AsteroidSpawner.DistributeFieldSize(types, fieldSize);
                Assert.AreEqual(fieldSize, counts.Sum(), $"fieldSize={fieldSize}");
            }
        }

        [Test]
        public void Rarer_types_get_fewer_slots_than_common_types()
        {
            var types  = new[] { MakeDef(0.8f), MakeDef(0.1f) }; // [0]=rare, [1]=common
            var counts = AsteroidSpawner.DistributeFieldSize(types, 20);

            Assert.Less(counts[0], counts[1]);
        }

        [Test]
        public void Zero_field_size_yields_all_zero_counts()
        {
            var types  = new[] { MakeDef(0.5f), MakeDef(0.5f) };
            var counts = AsteroidSpawner.DistributeFieldSize(types, 0);

            Assert.AreEqual(new[] { 0, 0 }, counts);
        }

        [Test]
        public void Single_type_gets_the_full_field_size()
        {
            var types  = new[] { MakeDef(0.9f) };
            var counts = AsteroidSpawner.DistributeFieldSize(types, 7);

            Assert.AreEqual(new[] { 7 }, counts);
        }
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `"C:\Program Files\Unity\Hub\Editor\<version>\Editor\Unity.exe" -runTests -projectPath . -testResults results.xml -testPlatform EditMode -assemblyNames SocialUniverse.Tests`
Expected: compile error — `AsteroidSpawner.DistributeFieldSize` does not exist yet.

- [ ] **Step 3: Add `SlotId` to `Asteroid.cs`**

In `Assets/_Project/Scripts/Mining/Asteroid.cs`, change:

```csharp
        public AsteroidDefinition Definition     { get; private set; }
        public int                RemainingYield { get; private set; }
        public bool                IsDepleted     => RemainingYield <= 0;
```

to:

```csharp
        public AsteroidDefinition Definition     { get; private set; }
        public string             SlotId         { get; private set; }
        public int                RemainingYield { get; private set; }
        public bool                IsDepleted     => RemainingYield <= 0;
```

and change:

```csharp
        public void Initialize(AsteroidDefinition definition)
        {
            Definition     = definition;
            RemainingYield = Mathf.RoundToInt(definition.BaseYield * Random.Range(0.8f, 1.2f));
```

to:

```csharp
        public void Initialize(AsteroidDefinition definition, string slotId)
        {
            Definition     = definition;
            SlotId         = slotId;
            RemainingYield = Mathf.RoundToInt(definition.BaseYield * Random.Range(0.8f, 1.2f));
```

(the rest of the method body is unchanged).

- [ ] **Step 4: Implement `DistributeFieldSize` in `AsteroidSpawner.cs`**

Add this public static method to `AsteroidSpawner` (e.g., right after the `NextRespawnUtc` property):

```csharp
        // Distributes `fieldSize` slots across `types`, weighted by (1 - Rarity) per type —
        // rarer types get fewer slots. Uses largest-remainder rounding so the returned counts
        // always sum to exactly `fieldSize` (each type gets at least 1 slot when fieldSize
        // allows it). Pure and static so it's directly unit-testable without a scene.
        public static int[] DistributeFieldSize(AsteroidDefinition[] types, int fieldSize)
        {
            int n = types?.Length ?? 0;
            var counts = new int[n];
            if (n == 0 || fieldSize <= 0) return counts;

            var weights = new float[n];
            float totalWeight = 0f;
            for (int i = 0; i < n; i++)
            {
                weights[i]   = Mathf.Max(0.01f, 1f - types[i].Rarity);
                totalWeight += weights[i];
            }

            var raw       = new float[n];
            var remainder = new float[n];
            int assigned  = 0;

            for (int i = 0; i < n; i++)
            {
                raw[i]       = fieldSize * weights[i] / totalWeight;
                counts[i]    = Mathf.Max(1, Mathf.FloorToInt(raw[i]));
                remainder[i] = raw[i] - Mathf.Floor(raw[i]);
                assigned    += counts[i];
            }

            var byRemainderDesc = Enumerable.Range(0, n).OrderByDescending(i => remainder[i]).ToArray();

            int diff = fieldSize - assigned;
            int cursor = 0;
            while (diff > 0)
            {
                counts[byRemainderDesc[cursor % n]]++;
                diff--;
                cursor++;
            }

            cursor = 0;
            int guard = 0;
            while (diff < 0 && guard < n * 64)
            {
                int i = byRemainderDesc[n - 1 - (cursor % n)];
                if (counts[i] > 1) { counts[i]--; diff++; }
                cursor++;
                guard++;
            }

            return counts;
        }
```

- [ ] **Step 5: Run the distribution tests to verify they pass**

Run: `"C:\Program Files\Unity\Hub\Editor\<version>\Editor\Unity.exe" -runTests -projectPath . -testResults results.xml -testPlatform EditMode -assemblyNames SocialUniverse.Tests`
Expected: PASS for all `AsteroidSpawnerDistributionTests`.

- [ ] **Step 6: Wire the distribution + slot IDs into `SpawnForPlanet`, `ScheduleRespawn`, `SpawnOne`, and add `FindBySlotId`**

Replace the `PendingRespawn` struct and the body of `SpawnForPlanet`, `ScheduleRespawn`, `Update`, `SpawnOne`, `LoadPendingRespawns`, `SavePendingRespawns` in `AsteroidSpawner.cs` as follows (the `PendingRespawn` struct gains a `SlotId` field, and every method that touches slot identity is updated to carry it through):

```csharp
        private struct PendingRespawn
        {
            public AsteroidDefinition Definition;
            public string             SlotId;
            public DateTime           RespawnAtUtc;
        }

        public void SpawnForPlanet(PlanetDefinition planet)
        {
            ClearAll();
            LoadPendingRespawns();

            if (planet.AsteroidTypes == null || planet.AsteroidTypes.Length == 0)
            {
                SULog.Warn($"Planet '{planet.DisplayName}' has no asteroid types defined", SULog.Channel.Mining);
                return;
            }

            var counts = DistributeFieldSize(planet.AsteroidTypes, planet.AsteroidFieldSize);

            for (int t = 0; t < planet.AsteroidTypes.Length; t++)
            {
                var def          = planet.AsteroidTypes[t];
                int targetCount  = counts[t];
                int pendingCount = _pending.Count(p => p.Definition == def);
                int toSpawn      = Mathf.Max(0, targetCount - pendingCount);

                for (int i = 0; i < toSpawn; i++)
                    SpawnOne(def, $"{def.MineralType}#{pendingCount + i}");
            }

            SULog.Info($"AsteroidSpawner: spawned {_active.Count} asteroids ({_pending.Count} pending respawn)", SULog.Channel.Mining);
        }

        public void ClearAll()
        {
            foreach (var a in _active)
                if (a != null) Destroy(a.gameObject);
            _active.Clear();
        }

        // Destroys a claimed asteroid and schedules a same-type, same-slot replacement to
        // spawn after the cooldown.
        public void ScheduleRespawn(Asteroid asteroid, float respawnHours)
        {
            if (asteroid == null) return;

            var definition = asteroid.Definition;
            var slotId      = asteroid.SlotId;
            _active.Remove(asteroid);
            Destroy(asteroid.gameObject);

            _pending.Add(new PendingRespawn
            {
                Definition   = definition,
                SlotId       = slotId,
                RespawnAtUtc = DateTime.UtcNow.AddHours(respawnHours)
            });
            SavePendingRespawns();

            SULog.Info($"Asteroid '{definition.MineralType}' claimed — respawns in {respawnHours:0.#}h", SULog.Channel.Mining);
        }

        // Returns the currently-active asteroid occupying the given slot, or null if it's
        // been claimed/is pending respawn. Used to reconcile a persisted idle-mining session
        // against the freshly spawned field after an app restart.
        public Asteroid FindBySlotId(string slotId)
        {
            foreach (var a in _active)
                if (a.SlotId == slotId) return a;
            return null;
        }

        private void Update()
        {
            if (_pending.Count == 0) return;

            var now     = DateTime.UtcNow;
            bool changed = false;

            for (int i = _pending.Count - 1; i >= 0; i--)
            {
                if (now < _pending[i].RespawnAtUtc) continue;

                SpawnOne(_pending[i].Definition, _pending[i].SlotId);
                _pending.RemoveAt(i);
                changed = true;
            }

            if (changed) SavePendingRespawns();
        }

        private void SpawnOne(AsteroidDefinition def, string slotId)
        {
            GameObject go;
            if (def.ModelPrefab != null)
            {
                go = Instantiate(def.ModelPrefab, RandomOrbitPoint(), UnityEngine.Random.rotation, transform);
            }
            else
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.transform.SetParent(transform);
                go.transform.position   = RandomOrbitPoint();
                go.transform.rotation   = UnityEngine.Random.rotation;
                go.transform.localScale = Vector3.one * 0.5f;
            }

            go.name = $"Asteroid_{def.MineralType}";
            var asteroid = go.AddComponent<Asteroid>();
            asteroid.Initialize(def, slotId);
            _active.Add(asteroid);
        }

        private Vector3 RandomOrbitPoint() => UnityEngine.Random.onUnitSphere * _orbitRadius;

        private void LoadPendingRespawns()
        {
            _pending.Clear();

            var raw = PlayerPrefs.GetString(SaveKeys.AsteroidRespawns, "");
            if (string.IsNullOrEmpty(raw)) return;

            foreach (var entry in raw.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = entry.Split('|');
                if (parts.Length != 3 || !long.TryParse(parts[2], out var unixSeconds)) continue;

                var definition = _registry.GetAsteroid(parts[0]);
                if (definition == null) continue;

                _pending.Add(new PendingRespawn
                {
                    Definition   = definition,
                    SlotId       = parts[1],
                    RespawnAtUtc = DateTimeOffset.FromUnixTimeSeconds(unixSeconds).UtcDateTime
                });
            }
        }

        private void SavePendingRespawns()
        {
            var serialized = string.Join(";", _pending.Select(p =>
                $"{p.Definition.MineralType}|{p.SlotId}|{new DateTimeOffset(p.RespawnAtUtc).ToUnixTimeSeconds()}"));

            PlayerPrefs.SetString(SaveKeys.AsteroidRespawns, serialized);
            PlayerPrefs.Save();
        }
```

Note: this changes the persisted `AsteroidRespawns` format from `"{mineral}|{unixSeconds}"` to `"{mineral}|{slotId}|{unixSeconds}"`. Old-format entries fail the `parts.Length != 3` check and are silently dropped — acceptable per this project's no-migration-shims policy (Global Constraints) since it's a pre-launch prototype.

- [ ] **Step 7: Re-run the full EditMode Mining suite**

Run: `"C:\Program Files\Unity\Hub\Editor\<version>\Editor\Unity.exe" -runTests -projectPath . -testResults results.xml -testPlatform EditMode -assemblyNames SocialUniverse.Tests`
Expected: `AsteroidSpawnerDistributionTests` still PASS; no new compile errors from the `Asteroid.Initialize`/`AsteroidSpawner` signature changes (both call sites — `SpawnOne`'s two callers — were updated in this same step).

- [ ] **Step 8: Commit**

```bash
git add Assets/_Project/Scripts/Mining/Asteroid.cs Assets/_Project/Scripts/Mining/AsteroidSpawner.cs Assets/_Project/Tests/EditMode/Mining/AsteroidSpawnerDistributionTests.cs Assets/_Project/Tests/EditMode/Mining/AsteroidSpawnerDistributionTests.cs.meta
git commit -m "mining: explicit per-planet asteroid field size + stable per-asteroid SlotId"
```

---

### Task 3: `MiningRewardCalculator`

**Files:**
- Create: `Assets/_Project/Scripts/Mining/MiningRewardCalculator.cs`
- Test: `Assets/_Project/Tests/EditMode/Mining/MiningRewardCalculatorTests.cs`

**Interfaces:**
- Consumes: `EconomyConfig.IdleSecondsPerYieldUnit/MinIdleSessionSeconds/MaxIdleSessionSeconds/ActiveYieldPerTap/MinActiveTaps/MaxActiveTaps` (Task 1); `Asteroid.RemainingYield` (`int`, existing), `Asteroid.Definition.CoinsPerUnit` (`int`, existing); `Asteroid.Initialize(AsteroidDefinition, string slotId)` (Task 2 — this task's test helper needs the two-argument signature).
- Produces: `MiningReward` struct (`TotalCoins: int`, `IdleDurationSeconds: float`, `ActiveTapsRequired: int`, `CoinsPerSec: float`); `MiningRewardCalculator(EconomyConfig config)` constructor; `MiningReward Compute(Asteroid asteroid)`. Consumed by Task 7 (`ActiveMiningMinigame`), Task 10 (`MiningController`).

- [ ] **Step 1: Write the failing tests**

Create `Assets/_Project/Tests/EditMode/Mining/MiningRewardCalculatorTests.cs`:

```csharp
using System.Reflection;
using NUnit.Framework;
using SocialUniverse.Config;
using SocialUniverse.Mining;
using UnityEngine;

namespace SocialUniverse.Tests
{
    public class MiningRewardCalculatorTests
    {
        private EconomyConfig          _config;
        private AsteroidDefinition     _def;
        private MiningRewardCalculator _calc;

        [SetUp]
        public void SetUp()
        {
            _config = ScriptableObject.CreateInstance<EconomyConfig>();
            SetField(_config, "_idleSecondsPerYieldUnit", 3f);
            SetField(_config, "_minIdleSessionSeconds", 30f);
            SetField(_config, "_maxIdleSessionSeconds", 1800f);
            SetField(_config, "_activeYieldPerTap", 8f);
            SetField(_config, "_minActiveTaps", 5);
            SetField(_config, "_maxActiveTaps", 20);

            _def = ScriptableObject.CreateInstance<AsteroidDefinition>();
            SetField(_def, "_coinsPerUnit", 2);

            _calc = new MiningRewardCalculator(_config);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_config);
            Object.DestroyImmediate(_def);
        }

        private static void SetField(Object target, string field, object value) =>
            target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(target, value);

        // RemainingYield is a { get; private set; } auto-property set inside Initialize() from
        // BaseYield * Random.Range(0.8f, 1.2f) — not directly settable. Tests need exact,
        // reproducible values, so this reaches through the auto-property's backing field
        // directly rather than trying to control the randomized Initialize() path.
        private Asteroid MakeAsteroid(int remainingYield)
        {
            var go = new GameObject("TestAsteroid");
            var asteroid = go.AddComponent<Asteroid>();
            asteroid.Initialize(_def, "slot_0");

            typeof(Asteroid).GetField("<RemainingYield>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(asteroid, remainingYield);

            return asteroid;
        }

        [Test]
        public void Mid_range_yield_is_not_clamped_and_coinsPerSec_reproduces_totalCoins_exactly()
        {
            var asteroid = MakeAsteroid(100); // duration = 100*3 = 300s, within [30,1800]

            var reward = _calc.Compute(asteroid);

            Assert.AreEqual(200, reward.TotalCoins);          // 100 * 2 coins/unit
            Assert.AreEqual(300f, reward.IdleDurationSeconds, 0.001f);
            Assert.AreEqual(200f / 300f, reward.CoinsPerSec, 0.0001f);
            Assert.AreEqual(reward.TotalCoins, reward.IdleDurationSeconds * reward.CoinsPerSec, 0.01f,
                "sessionDurationSec * coinsPerSec must reproduce totalCoins exactly so the server cap never under-grants");
        }

        [Test]
        public void Tiny_yield_clamps_duration_to_minimum_and_still_reproduces_totalCoins()
        {
            var asteroid = MakeAsteroid(1); // raw duration = 3s, clamped up to 30s

            var reward = _calc.Compute(asteroid);

            Assert.AreEqual(30f, reward.IdleDurationSeconds, 0.001f);
            Assert.AreEqual(reward.TotalCoins, reward.IdleDurationSeconds * reward.CoinsPerSec, 0.01f);
        }

        [Test]
        public void Huge_yield_clamps_duration_to_maximum_and_still_reproduces_totalCoins()
        {
            var asteroid = MakeAsteroid(10000); // raw duration = 30000s, clamped down to 1800s

            var reward = _calc.Compute(asteroid);

            Assert.AreEqual(1800f, reward.IdleDurationSeconds, 0.001f);
            Assert.AreEqual(20000, reward.TotalCoins); // 10000 * 2
            Assert.AreEqual(reward.TotalCoins, reward.IdleDurationSeconds * reward.CoinsPerSec, 0.5f,
                "even when duration is clamped down, coinsPerSec must be recomputed so the cap still equals totalCoins");
        }

        [Test]
        public void Active_taps_scale_with_yield_and_clamp_at_bounds()
        {
            Assert.AreEqual(5, _calc.Compute(MakeAsteroid(1)).ActiveTapsRequired);     // ceil(1/8)=1, clamped up to min 5
            Assert.AreEqual(13, _calc.Compute(MakeAsteroid(100)).ActiveTapsRequired);  // ceil(100/8)=13
            Assert.AreEqual(20, _calc.Compute(MakeAsteroid(10000)).ActiveTapsRequired); // clamped down to max 20
        }
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `"C:\Program Files\Unity\Hub\Editor\<version>\Editor\Unity.exe" -runTests -projectPath . -testResults results.xml -testPlatform EditMode -assemblyNames SocialUniverse.Tests`
Expected: compile error — `MiningRewardCalculator` does not exist yet.

- [ ] **Step 3: Implement `MiningRewardCalculator`**

Create `Assets/_Project/Scripts/Mining/MiningRewardCalculator.cs`:

```csharp
using UnityEngine;
using SocialUniverse.Config;

namespace SocialUniverse.Mining
{
    public readonly struct MiningReward
    {
        public readonly int   TotalCoins;
        public readonly float IdleDurationSeconds;
        public readonly int   ActiveTapsRequired;
        public readonly float CoinsPerSec;

        public MiningReward(int totalCoins, float idleDurationSeconds, int activeTapsRequired, float coinsPerSec)
        {
            TotalCoins          = totalCoins;
            IdleDurationSeconds = idleDurationSeconds;
            ActiveTapsRequired  = activeTapsRequired;
            CoinsPerSec         = coinsPerSec;
        }
    }

    // Single source of truth for idle-mining duration, active-mining tap count, and total
    // coin payout for a given asteroid — both mining modes derive their pacing from the
    // same RemainingYield so they pay out identical totals (see MiningRewardCalculatorTests).
    public class MiningRewardCalculator
    {
        private readonly EconomyConfig _config;

        public MiningRewardCalculator(EconomyConfig config) => _config = config;

        public MiningReward Compute(Asteroid asteroid)
        {
            int remainingYield = asteroid.RemainingYield;
            int totalCoins     = remainingYield * asteroid.Definition.CoinsPerUnit;

            float rawDuration = remainingYield * _config.IdleSecondsPerYieldUnit;
            float duration    = Mathf.Clamp(rawDuration, _config.MinIdleSessionSeconds, _config.MaxIdleSessionSeconds);

            int rawTaps = Mathf.CeilToInt(remainingYield / _config.ActiveYieldPerTap);
            int taps    = Mathf.Clamp(rawTaps, _config.MinActiveTaps, _config.MaxActiveTaps);

            // Computed per-claim from this asteroid's actual totalCoins/duration (not a fixed
            // per-type constant) so sessionDurationSec * coinsPerSec always equals totalCoins
            // exactly, even when duration was clamped — see EconomyService.GrantMiningRewardAsync.
            float coinsPerSec = duration > 0f ? totalCoins / duration : 0f;

            return new MiningReward(totalCoins, duration, taps, coinsPerSec);
        }
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `"C:\Program Files\Unity\Hub\Editor\<version>\Editor\Unity.exe" -runTests -projectPath . -testResults results.xml -testPlatform EditMode -assemblyNames SocialUniverse.Tests`
Expected: PASS for all `MiningRewardCalculatorTests`.

- [ ] **Step 5: Commit**

```bash
git add Assets/_Project/Scripts/Mining/MiningRewardCalculator.cs Assets/_Project/Tests/EditMode/Mining/MiningRewardCalculatorTests.cs Assets/_Project/Tests/EditMode/Mining/MiningRewardCalculatorTests.cs.meta
git commit -m "mining: add MiningRewardCalculator as shared idle/active/payout formula"
```

---

### Task 4: Simplify `DroneRuntime` (remove cargo)

**Files:**
- Modify: `Assets/_Project/Scripts/Mining/DroneRuntime.cs`

**Interfaces:**
- Produces: `DroneRuntime` retains only `Definition` (`DroneDefinition`, public getter) and its constructor. Removes `CargoAmount`, `IsCargoFull`, `AddCargo(int)`, `EmptyCargo()`.

- [ ] **Step 1: Verify no other consumers depend on the cargo API before removing it**

Run: `grep -rn "CargoAmount\|IsCargoFull\|AddCargo\|EmptyCargo" Assets/_Project/Scripts Assets/_Project/Tests`
Expected output at this point in the plan: only `MiningController.cs`, `MiningInputHandler.cs`, `IdleMiningCalculatorTests.cs`, and `HUDController.cs` — all of which are rewritten or deleted in Tasks 9–15 of this plan. If anything else shows up, stop and re-scope before proceeding.

- [ ] **Step 2: Simplify `DroneRuntime.cs`**

Replace the entire file with:

```csharp
using System;
using SocialUniverse.Config;

namespace SocialUniverse.Mining
{
    public class DroneRuntime
    {
        public DroneDefinition Definition { get; }

        public DroneRuntime(DroneDefinition definition)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        }
    }
}
```

(No test — this is a pure data holder now; its only remaining behavior, the null-check, is exercised implicitly by every consumer that constructs one. Compilation will be broken by this change until Tasks 9–15 update the remaining call sites — that's expected and resolved by the end of this plan.)

- [ ] **Step 3: Commit**

```bash
git add Assets/_Project/Scripts/Mining/DroneRuntime.cs
git commit -m "mining: drop DroneRuntime cargo API, replaced by direct per-asteroid payout"
```

---

### Task 5: `IdleMiningSession` rewrite — wall-clock-based, single-tap claim

**Files:**
- Modify: `Assets/_Project/Scripts/Mining/IdleMiningSession.cs`
- Test: `Assets/_Project/Tests/EditMode/Mining/IdleMiningSessionTests.cs`

**Interfaces:**
- Produces: `IdleMiningSession(Asteroid asteroid, DateTime startUtc, float durationSeconds)` constructor (replaces the old `(Asteroid, float miningDuration, int claimTapsRequired)` signature); `StartUtc` (`DateTime`, public getter), `DurationSeconds` (`float`, public getter, was `_miningDuration` private field); `MiningProgress01` (now computed live from wall-clock elapsed vs `DurationSeconds`, not accumulated `Tick` deltas); `Claim()` (replaces `RegisterClaimTap()`); removes `ClaimTapsRequired`/`ClaimTapsRemaining`. `Stage`/`OnStageChanged`/`BeginMining()`/`Tick(float deltaTime)` keep their existing names and purpose. Consumed by Task 10 (`MiningController` and its `IdleMiningSessionController` update), Task 14 (`HUDController`).

- [ ] **Step 1: Write the failing tests**

Create `Assets/_Project/Tests/EditMode/Mining/IdleMiningSessionTests.cs`:

```csharp
using System;
using System.Threading.Tasks;
using NUnit.Framework;
using SocialUniverse.Mining;
using UnityEngine;

namespace SocialUniverse.Tests
{
    public class IdleMiningSessionTests
    {
        private Asteroid MakeAsteroid()
        {
            var def = ScriptableObject.CreateInstance<Config.AsteroidDefinition>();
            var go  = new GameObject("TestAsteroid");
            var a   = go.AddComponent<Asteroid>();
            a.Initialize(def, "slot_0");
            return a;
        }

        [Test]
        public void New_session_starts_in_Traveling_when_duration_has_not_elapsed()
        {
            var session = new IdleMiningSession(MakeAsteroid(), DateTime.UtcNow, 60f);
            Assert.AreEqual(IdleMiningStage.Traveling, session.Stage);
        }

        [Test]
        public void Reconstructing_with_a_past_startUtc_that_exceeds_duration_starts_ReadyToClaim()
        {
            // Simulates restoring a persisted session after the app was closed long enough
            // for the duration to have fully elapsed while it was closed.
            var startUtc = DateTime.UtcNow.AddSeconds(-120);
            var session  = new IdleMiningSession(MakeAsteroid(), startUtc, 60f);

            Assert.AreEqual(IdleMiningStage.ReadyToClaim, session.Stage);
            Assert.AreEqual(1f, session.MiningProgress01, 0.001f);
        }

        [Test]
        public void BeginMining_only_transitions_from_Traveling()
        {
            var session = new IdleMiningSession(MakeAsteroid(), DateTime.UtcNow, 60f);
            session.BeginMining();
            Assert.AreEqual(IdleMiningStage.Mining, session.Stage);

            session.BeginMining(); // no-op, already past Traveling
            Assert.AreEqual(IdleMiningStage.Mining, session.Stage);
        }

        [Test]
        public async Task Tick_flips_to_ReadyToClaim_once_real_time_reaches_duration()
        {
            var session = new IdleMiningSession(MakeAsteroid(), DateTime.UtcNow, 0.05f);
            Assert.AreEqual(IdleMiningStage.Traveling, session.Stage);

            await Task.Delay(100);
            session.Tick(0f); // deltaTime is unused for the ready check — real elapsed time drives it

            Assert.AreEqual(IdleMiningStage.ReadyToClaim, session.Stage);
        }

        [Test]
        public void Claim_only_succeeds_from_ReadyToClaim()
        {
            var session = new IdleMiningSession(MakeAsteroid(), DateTime.UtcNow.AddSeconds(-120), 60f);
            Assert.AreEqual(IdleMiningStage.ReadyToClaim, session.Stage);

            session.Claim();

            Assert.AreEqual(IdleMiningStage.Complete, session.Stage);
        }

        [Test]
        public void Claim_is_a_no_op_when_not_ReadyToClaim()
        {
            var session = new IdleMiningSession(MakeAsteroid(), DateTime.UtcNow, 60f);
            session.Claim();
            Assert.AreEqual(IdleMiningStage.Traveling, session.Stage);
        }

        [Test]
        public void OnStageChanged_fires_when_stage_transitions()
        {
            var session = new IdleMiningSession(MakeAsteroid(), DateTime.UtcNow, 60f);
            IdleMiningStage? seen = null;
            session.OnStageChanged += s => seen = s;

            session.BeginMining();

            Assert.AreEqual(IdleMiningStage.Mining, seen);
        }
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `"C:\Program Files\Unity\Hub\Editor\<version>\Editor\Unity.exe" -runTests -projectPath . -testResults results.xml -testPlatform EditMode -assemblyNames SocialUniverse.Tests`
Expected: compile errors — `IdleMiningSession`'s constructor and `Claim()` don't exist with these signatures yet.

- [ ] **Step 3: Rewrite `IdleMiningSession.cs`**

Replace the entire file:

```csharp
using System;
using UnityEngine;

namespace SocialUniverse.Mining
{
    public enum IdleMiningStage { Traveling, Mining, ReadyToClaim, Complete }

    // Tracks one player-directed idle-mining run against a single asteroid. Timing is driven
    // by real wall-clock elapsed time (DateTime.UtcNow - StartUtc), not accumulated per-frame
    // deltaTime — this is what lets a session resume correctly after the app was closed and
    // reopened: reconstructing with the persisted StartUtc/DurationSeconds is enough to derive
    // the correct current stage with no additional bookkeeping.
    public class IdleMiningSession
    {
        public Asteroid        Asteroid        { get; }
        public DateTime        StartUtc        { get; }
        public float           DurationSeconds { get; }
        public IdleMiningStage Stage           { get; private set; }

        public float MiningProgress01 =>
            Mathf.Clamp01((float)(DateTime.UtcNow - StartUtc).TotalSeconds / DurationSeconds);

        public event Action<IdleMiningStage> OnStageChanged;

        public IdleMiningSession(Asteroid asteroid, DateTime startUtc, float durationSeconds)
        {
            Asteroid        = asteroid;
            StartUtc        = startUtc;
            DurationSeconds = Mathf.Max(0.01f, durationSeconds);
            Stage           = HasDurationElapsed() ? IdleMiningStage.ReadyToClaim : IdleMiningStage.Traveling;
        }

        // Drone has visually arrived at the asteroid. Flavor-only transition for HUD text —
        // does not affect the ReadyToClaim timing, which is purely wall-clock based.
        public void BeginMining()
        {
            if (Stage != IdleMiningStage.Traveling) return;
            SetStage(IdleMiningStage.Mining);
        }

        // Call every frame while the session is active. deltaTime is intentionally unused for
        // the readiness check (wall-clock driven) — it exists so callers can call this
        // uniformly alongside other per-frame Tick methods.
        public void Tick(float deltaTime)
        {
            if (Stage == IdleMiningStage.ReadyToClaim || Stage == IdleMiningStage.Complete) return;
            if (HasDurationElapsed())
                SetStage(IdleMiningStage.ReadyToClaim);
        }

        // Completes the session. No-op unless the session is ReadyToClaim.
        public void Claim()
        {
            if (Stage != IdleMiningStage.ReadyToClaim) return;
            SetStage(IdleMiningStage.Complete);
        }

        private bool HasDurationElapsed() =>
            (DateTime.UtcNow - StartUtc).TotalSeconds >= DurationSeconds;

        private void SetStage(IdleMiningStage stage)
        {
            Stage = stage;
            OnStageChanged?.Invoke(stage);
        }
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `"C:\Program Files\Unity\Hub\Editor\<version>\Editor\Unity.exe" -runTests -projectPath . -testResults results.xml -testPlatform EditMode -assemblyNames SocialUniverse.Tests`
Expected: PASS for all `IdleMiningSessionTests`. (`IdleMiningSessionController.cs` and `MiningController.cs` will fail to compile against the old API until Tasks 11–12 — expected at this point.)

- [ ] **Step 5: Commit**

```bash
git add Assets/_Project/Scripts/Mining/IdleMiningSession.cs Assets/_Project/Tests/EditMode/Mining/IdleMiningSessionTests.cs Assets/_Project/Tests/EditMode/Mining/IdleMiningSessionTests.cs.meta
git commit -m "mining: IdleMiningSession is wall-clock driven with single-tap claim, survives app restart"
```

---

### Task 6: `ActiveMiningSession` (new minigame state machine)

**Files:**
- Create: `Assets/_Project/Scripts/Mining/ActiveMiningSession.cs`
- Test: `Assets/_Project/Tests/EditMode/Mining/ActiveMiningSessionTests.cs`

**Interfaces:**
- Produces: `ActiveMiningStage` enum (`InProgress`, `Success`, `Failed`); `ActiveMiningSession(Asteroid asteroid, int tapsRequired, int maxErrors, float tapWindowSeconds)`; `Asteroid` (getter), `TapsRequired`/`SuccessfulTaps`/`ErrorCount`/`MaxErrors` (`int`, getters), `Stage` (getter), `OnStageChanged` (`event Action<ActiveMiningStage>`), `Tick(float deltaTime)`, `RegisterHit()`, `RegisterMiss()`. Consumed by Task 7 (`ActiveMiningMinigame`), Task 10 (`MiningController`), Task 15 (`ActiveMiningMinigameView`).

- [ ] **Step 1: Write the failing tests**

Create `Assets/_Project/Tests/EditMode/Mining/ActiveMiningSessionTests.cs`:

```csharp
using NUnit.Framework;
using SocialUniverse.Config;
using SocialUniverse.Mining;
using UnityEngine;

namespace SocialUniverse.Tests
{
    public class ActiveMiningSessionTests
    {
        private Asteroid MakeAsteroid()
        {
            var def = ScriptableObject.CreateInstance<AsteroidDefinition>();
            var go  = new GameObject("TestAsteroid");
            var a   = go.AddComponent<Asteroid>();
            a.Initialize(def, "slot_0");
            return a;
        }

        [Test]
        public void Reaching_required_taps_succeeds()
        {
            var session = new ActiveMiningSession(MakeAsteroid(), tapsRequired: 3, maxErrors: 3, tapWindowSeconds: 1f);

            session.RegisterHit();
            session.RegisterHit();
            Assert.AreEqual(ActiveMiningStage.InProgress, session.Stage);
            session.RegisterHit();

            Assert.AreEqual(ActiveMiningStage.Success, session.Stage);
            Assert.AreEqual(3, session.SuccessfulTaps);
        }

        [Test]
        public void Reaching_max_errors_fails()
        {
            var session = new ActiveMiningSession(MakeAsteroid(), tapsRequired: 10, maxErrors: 3, tapWindowSeconds: 1f);

            session.RegisterMiss();
            session.RegisterMiss();
            Assert.AreEqual(ActiveMiningStage.InProgress, session.Stage);
            session.RegisterMiss();

            Assert.AreEqual(ActiveMiningStage.Failed, session.Stage);
            Assert.AreEqual(3, session.ErrorCount);
        }

        [Test]
        public void Tick_past_the_tap_window_counts_as_a_miss()
        {
            var session = new ActiveMiningSession(MakeAsteroid(), tapsRequired: 10, maxErrors: 3, tapWindowSeconds: 1f);

            session.Tick(0.5f);
            Assert.AreEqual(0, session.ErrorCount);
            session.Tick(0.6f); // total 1.1s > 1s window

            Assert.AreEqual(1, session.ErrorCount);
        }

        [Test]
        public void Hit_resets_the_window_timer()
        {
            var session = new ActiveMiningSession(MakeAsteroid(), tapsRequired: 10, maxErrors: 3, tapWindowSeconds: 1f);

            session.Tick(0.9f);
            session.RegisterHit();
            session.Tick(0.9f); // would have missed at 1.8s total if the timer hadn't reset

            Assert.AreEqual(0, session.ErrorCount);
        }

        [Test]
        public void Terminal_stages_ignore_further_hits_misses_and_ticks()
        {
            var session = new ActiveMiningSession(MakeAsteroid(), tapsRequired: 1, maxErrors: 3, tapWindowSeconds: 1f);
            session.RegisterHit(); // -> Success

            session.RegisterMiss();
            session.Tick(10f);

            Assert.AreEqual(ActiveMiningStage.Success, session.Stage);
            Assert.AreEqual(0, session.ErrorCount);
        }

        [Test]
        public void OnStageChanged_fires_on_terminal_transition_only()
        {
            var session = new ActiveMiningSession(MakeAsteroid(), tapsRequired: 2, maxErrors: 3, tapWindowSeconds: 1f);
            int fireCount = 0;
            session.OnStageChanged += _ => fireCount++;

            session.RegisterHit(); // 1/2, no transition
            Assert.AreEqual(0, fireCount);

            session.RegisterHit(); // 2/2 -> Success
            Assert.AreEqual(1, fireCount);
        }
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `"C:\Program Files\Unity\Hub\Editor\<version>\Editor\Unity.exe" -runTests -projectPath . -testResults results.xml -testPlatform EditMode -assemblyNames SocialUniverse.Tests`
Expected: compile error — `ActiveMiningSession` doesn't exist yet.

- [ ] **Step 3: Implement `ActiveMiningSession.cs`**

Create `Assets/_Project/Scripts/Mining/ActiveMiningSession.cs`:

```csharp
using System;
using UnityEngine;

namespace SocialUniverse.Mining
{
    public enum ActiveMiningStage { InProgress, Success, Failed }

    // Player-vs-asteroid tap-timing minigame: one target point is "live" at a time, must be
    // hit within TapWindowSeconds or it counts as a miss. MaxErrors misses fails the asteroid;
    // TapsRequired hits succeeds it. Does not reference DroneRuntime — active mining never
    // occupies the drone.
    public class ActiveMiningSession
    {
        public Asteroid Asteroid         { get; }
        public int      TapsRequired     { get; }
        public int      SuccessfulTaps   { get; private set; }
        public int      MaxErrors        { get; }
        public int      ErrorCount       { get; private set; }
        public float    TapWindowSeconds { get; }

        public ActiveMiningStage Stage { get; private set; } = ActiveMiningStage.InProgress;

        public event Action<ActiveMiningStage> OnStageChanged;

        private float _windowElapsed;

        public ActiveMiningSession(Asteroid asteroid, int tapsRequired, int maxErrors, float tapWindowSeconds)
        {
            Asteroid         = asteroid;
            TapsRequired     = Mathf.Max(1, tapsRequired);
            MaxErrors        = Mathf.Max(1, maxErrors);
            TapWindowSeconds = Mathf.Max(0.05f, tapWindowSeconds);
        }

        // Call every frame while Stage == InProgress; a target point that isn't hit within
        // TapWindowSeconds counts as a miss.
        public void Tick(float deltaTime)
        {
            if (Stage != ActiveMiningStage.InProgress) return;

            _windowElapsed += deltaTime;
            if (_windowElapsed >= TapWindowSeconds)
                RegisterMiss();
        }

        // The live target point was tapped within its window.
        public void RegisterHit()
        {
            if (Stage != ActiveMiningStage.InProgress) return;

            SuccessfulTaps++;
            _windowElapsed = 0f;

            if (SuccessfulTaps >= TapsRequired)
                SetStage(ActiveMiningStage.Success);
        }

        // The player tapped the wrong spot, or the window expired via Tick.
        public void RegisterMiss()
        {
            if (Stage != ActiveMiningStage.InProgress) return;

            ErrorCount++;
            _windowElapsed = 0f;

            if (ErrorCount >= MaxErrors)
                SetStage(ActiveMiningStage.Failed);
        }

        private void SetStage(ActiveMiningStage stage)
        {
            Stage = stage;
            OnStageChanged?.Invoke(stage);
        }
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `"C:\Program Files\Unity\Hub\Editor\<version>\Editor\Unity.exe" -runTests -projectPath . -testResults results.xml -testPlatform EditMode -assemblyNames SocialUniverse.Tests`
Expected: PASS for all `ActiveMiningSessionTests`.

- [ ] **Step 5: Commit**

```bash
git add Assets/_Project/Scripts/Mining/ActiveMiningSession.cs Assets/_Project/Tests/EditMode/Mining/ActiveMiningSessionTests.cs Assets/_Project/Tests/EditMode/Mining/ActiveMiningSessionTests.cs.meta
git commit -m "mining: add ActiveMiningSession tap-timing minigame state machine"
```

---

### Task 7: Rewrite `ActiveMiningMinigame` as the session factory/dispatcher

**Files:**
- Modify: `Assets/_Project/Scripts/Mining/ActiveMiningMinigame.cs`
- Test: `Assets/_Project/Tests/EditMode/Mining/ActiveMiningMinigameTests.cs`

**Interfaces:**
- Consumes: `MiningRewardCalculator.Compute(Asteroid) → MiningReward` (Task 3); `EconomyConfig.ActiveMaxErrors/ActiveTapWindowSeconds` (Task 1); `ActiveMiningSession` (Task 6).
- Produces: `ActiveMiningMinigame(EconomyConfig config, MiningRewardCalculator rewardCalc)`; `CurrentSession` (`ActiveMiningSession`, getter); `event Action<ActiveMiningSession> OnSessionChanged`; `bool Begin(Asteroid asteroid)`; `void Tick(float deltaTime)`; `void RegisterTap(bool hitTarget)`; `void Clear()`. Consumed by Task 10 (`MiningController`).

- [ ] **Step 1: Write the failing tests**

Create `Assets/_Project/Tests/EditMode/Mining/ActiveMiningMinigameTests.cs`:

```csharp
using System.Reflection;
using NUnit.Framework;
using SocialUniverse.Config;
using SocialUniverse.Mining;
using UnityEngine;

namespace SocialUniverse.Tests
{
    public class ActiveMiningMinigameTests
    {
        private EconomyConfig          _config;
        private MiningRewardCalculator _rewardCalc;
        private ActiveMiningMinigame   _minigame;

        [SetUp]
        public void SetUp()
        {
            _config = ScriptableObject.CreateInstance<EconomyConfig>();
            SetField(_config, "_activeMaxErrors", 3);
            SetField(_config, "_activeTapWindowSeconds", 1f);
            SetField(_config, "_activeYieldPerTap", 8f);
            SetField(_config, "_minActiveTaps", 1);
            SetField(_config, "_maxActiveTaps", 99);

            _rewardCalc = new MiningRewardCalculator(_config);
            _minigame   = new ActiveMiningMinigame(_config, _rewardCalc);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_config);

        private static void SetField(Object target, string field, object value) =>
            target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);

        private Asteroid MakeAsteroid(int remainingYield = 8)
        {
            var def = ScriptableObject.CreateInstance<AsteroidDefinition>();
            var go  = new GameObject("TestAsteroid");
            var a   = go.AddComponent<Asteroid>();
            a.Initialize(def, "slot_0");
            typeof(Asteroid).GetField("<RemainingYield>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(a, remainingYield);
            return a;
        }

        [Test]
        public void Begin_creates_a_session_sized_from_the_reward_calculator()
        {
            bool started = _minigame.Begin(MakeAsteroid(remainingYield: 8)); // ceil(8/8)=1 tap, clamped up? min=1 so 1

            Assert.IsTrue(started);
            Assert.IsNotNull(_minigame.CurrentSession);
            Assert.AreEqual(1, _minigame.CurrentSession.TapsRequired);
        }

        [Test]
        public void Begin_fails_while_a_session_is_already_in_progress()
        {
            _minigame.Begin(MakeAsteroid());
            bool startedAgain = _minigame.Begin(MakeAsteroid());

            Assert.IsFalse(startedAgain);
        }

        [Test]
        public void RegisterTap_true_forwards_to_RegisterHit()
        {
            _minigame.Begin(MakeAsteroid(remainingYield: 8)); // 1 tap required
            _minigame.RegisterTap(true);

            Assert.AreEqual(ActiveMiningStage.Success, _minigame.CurrentSession.Stage);
        }

        [Test]
        public void Clear_releases_the_current_session()
        {
            _minigame.Begin(MakeAsteroid());
            _minigame.Clear();

            Assert.IsNull(_minigame.CurrentSession);
        }

        [Test]
        public void OnSessionChanged_fires_when_Begin_starts_a_session()
        {
            ActiveMiningSession seen = null;
            _minigame.OnSessionChanged += s => seen = s;

            _minigame.Begin(MakeAsteroid());

            Assert.IsNotNull(seen);
        }
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `"C:\Program Files\Unity\Hub\Editor\<version>\Editor\Unity.exe" -runTests -projectPath . -testResults results.xml -testPlatform EditMode -assemblyNames SocialUniverse.Tests`
Expected: compile error — `ActiveMiningMinigame`'s constructor/API don't match yet (current file has the old `Tap(Asteroid, DroneRuntime)` free-tap API).

- [ ] **Step 3: Rewrite `ActiveMiningMinigame.cs`**

Replace the entire file:

```csharp
using System;
using SocialUniverse.Config;

namespace SocialUniverse.Mining
{
    // Owns the currently-running active-mining minigame session (if any). Standalone from
    // the drone — active mining never travels and can run concurrently with an idle-mining
    // session on a different asteroid.
    public class ActiveMiningMinigame
    {
        private readonly EconomyConfig           _config;
        private readonly MiningRewardCalculator  _rewardCalc;

        public ActiveMiningSession CurrentSession { get; private set; }

        public event Action<ActiveMiningSession> OnSessionChanged;

        public ActiveMiningMinigame(EconomyConfig config, MiningRewardCalculator rewardCalc)
        {
            _config     = config;
            _rewardCalc = rewardCalc;
        }

        public bool Begin(Asteroid asteroid)
        {
            if (asteroid == null || asteroid.IsDepleted || CurrentSession != null)
                return false;

            var reward = _rewardCalc.Compute(asteroid);
            CurrentSession = new ActiveMiningSession(asteroid, reward.ActiveTapsRequired,
                _config.ActiveMaxErrors, _config.ActiveTapWindowSeconds);
            CurrentSession.OnStageChanged += _ => OnSessionChanged?.Invoke(CurrentSession);

            OnSessionChanged?.Invoke(CurrentSession);
            return true;
        }

        public void Tick(float deltaTime) => CurrentSession?.Tick(deltaTime);

        public void RegisterTap(bool hitTarget)
        {
            if (CurrentSession == null) return;
            if (hitTarget) CurrentSession.RegisterHit();
            else           CurrentSession.RegisterMiss();
        }

        public void Clear() => CurrentSession = null;
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `"C:\Program Files\Unity\Hub\Editor\<version>\Editor\Unity.exe" -runTests -projectPath . -testResults results.xml -testPlatform EditMode -assemblyNames SocialUniverse.Tests`
Expected: PASS for all `ActiveMiningMinigameTests`. (`MiningController.cs` and `PlanetSceneScope.cs` still reference the old constructor/DI — fixed in Tasks 11/13.)

- [ ] **Step 5: Commit**

```bash
git add Assets/_Project/Scripts/Mining/ActiveMiningMinigame.cs Assets/_Project/Tests/EditMode/Mining/ActiveMiningMinigameTests.cs Assets/_Project/Tests/EditMode/Mining/ActiveMiningMinigameTests.cs.meta
git commit -m "mining: ActiveMiningMinigame owns session lifecycle instead of the old free-tap stub"
```

---

### Task 8: `IEconomyService.GrantMiningRewardAsync` + `LocalMockEconomy`

**Files:**
- Modify: `Assets/_Project/Scripts/Economy/IEconomyService.cs`
- Modify: `Assets/_Project/Scripts/Economy/LocalMockEconomy.cs`
- Test: `Assets/_Project/Tests/EditMode/Economy/LocalMockEconomyTests.cs` (extend existing file)

**Interfaces:**
- Produces: `IEconomyService.GrantMiningRewardAsync(int claimedCoins, float sessionDurationSec, float coinsPerSec) → Task<int>` (granted amount). Consumed by Task 10 (`MiningController`).

- [ ] **Step 1: Write the failing test (extend the existing `LocalMockEconomyTests.cs`)**

Add this test method to the existing `LocalMockEconomyTests` class in `Assets/_Project/Tests/EditMode/Economy/LocalMockEconomyTests.cs` (after `GrantCoinsAsync_adds_to_balance`):

```csharp
        [Test]
        public async Task GrantMiningRewardAsync_grants_claimedCoins_directly_with_no_validation()
        {
            int granted = await _economy.GrantMiningRewardAsync(claimedCoins: 75, sessionDurationSec: 30f, coinsPerSec: 2.5f);

            Assert.AreEqual(75, granted);
            Assert.AreEqual(_config.StartingCoins + 75, _wallet.Coins);
        }
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `"C:\Program Files\Unity\Hub\Editor\<version>\Editor\Unity.exe" -runTests -projectPath . -testResults results.xml -testPlatform EditMode -assemblyNames SocialUniverse.Tests`
Expected: compile error — `GrantMiningRewardAsync` doesn't exist on `IEconomyService`/`LocalMockEconomy` yet.

- [ ] **Step 3: Add the method to `IEconomyService.cs`**

In `Assets/_Project/Scripts/Economy/IEconomyService.cs`, add to the interface (after `GrantCoinsAsync`):

```csharp
        // Idle-claim and active-mining-success payouts go through here instead of
        // GrantCoinsAsync, so the server can validate the amount against the session's
        // duration/rate rather than trusting a bare client-supplied amount.
        Task<int> GrantMiningRewardAsync(int claimedCoins, float sessionDurationSec, float coinsPerSec);
```

- [ ] **Step 4: Implement it in `LocalMockEconomy.cs`**

Add to `LocalMockEconomy` (after `GrantCoinsAsync`):

```csharp
        public Task<int> GrantMiningRewardAsync(int claimedCoins, float sessionDurationSec, float coinsPerSec)
        {
            _wallet.SetCoins(_wallet.Coins + claimedCoins);
            return Task.FromResult(claimedCoins);
        }
```

- [ ] **Step 5: Run the test to verify it passes**

Run: `"C:\Program Files\Unity\Hub\Editor\<version>\Editor\Unity.exe" -runTests -projectPath . -testResults results.xml -testPlatform EditMode -assemblyNames SocialUniverse.Tests`
Expected: PASS. (`EconomyService.cs` will fail to compile until Task 9 implements the interface member there too — expected at this point since `EconomyService : IEconomyService` now has a missing member.)

- [ ] **Step 6: Commit**

```bash
git add Assets/_Project/Scripts/Economy/IEconomyService.cs Assets/_Project/Scripts/Economy/LocalMockEconomy.cs Assets/_Project/Tests/EditMode/Economy/LocalMockEconomyTests.cs
git commit -m "economy: add GrantMiningRewardAsync to IEconomyService, implement in LocalMockEconomy"
```

---

### Task 9: `EconomyService.GrantMiningRewardAsync` — wire through `ValidateMining` for real

**Files:**
- Modify: `Assets/_Project/Scripts/Economy/EconomyService.cs`
- Test: `Assets/_Project/Tests/EditMode/Economy/EconomyServiceMiningTests.cs`

**Interfaces:**
- Consumes: `IBackendClient.CallAsync<T>(string function, Dictionary<string, object> args) → Task<T>` (existing); `ServerCode/ValidateMining.js`'s existing param names `claimedCoins`/`sessionDurationSec`/`coinsPerSec` and response shape `{ granted, newBalance }` (existing, unmodified).
- Produces: `EconomyService.GrantMiningRewardAsync` implementation, satisfying Task 8's interface addition.

- [ ] **Step 1: Write the failing test**

Create `Assets/_Project/Tests/EditMode/Economy/EconomyServiceMiningTests.cs`:

```csharp
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using SocialUniverse.Config;
using SocialUniverse.Economy;
using SocialUniverse.Net;
using UnityEngine;

namespace SocialUniverse.Tests
{
    public class EconomyServiceMiningTests
    {
        private class FakeBackendClient : IBackendClient
        {
            public string LastFunction;
            public Dictionary<string, object> LastArgs;
            public int  GrantedToReturn = 100;
            public int? NewBalanceToReturn = 600;

            public Task<T> CallAsync<T>(string function, Dictionary<string, object> args = null)
            {
                LastFunction = function;
                LastArgs     = args;

                if (function == "ValidateMining" && typeof(T) == typeof(MiningGrantResult))
                {
                    object response = new MiningGrantResult { Granted = GrantedToReturn, NewBalance = NewBalanceToReturn };
                    return Task.FromResult((T)response);
                }
                return Task.FromResult(default(T));
            }

            public Task CallAsync(string function, Dictionary<string, object> args = null) => Task.CompletedTask;
        }

        private EconomyConfig      _config;
        private Wallet             _wallet;
        private FakeBackendClient  _backend;
        private EconomyService     _economy;

        [SetUp]
        public void SetUp()
        {
            _config  = ScriptableObject.CreateInstance<EconomyConfig>();
            _wallet  = new Wallet();
            _backend = new FakeBackendClient();
            _economy = new EconomyService(_wallet, _config, _backend);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_config);

        [Test]
        public async Task GrantMiningRewardAsync_calls_ValidateMining_with_the_given_params()
        {
            await _economy.GrantMiningRewardAsync(claimedCoins: 200, sessionDurationSec: 300f, coinsPerSec: 0.667f);

            Assert.AreEqual("ValidateMining", _backend.LastFunction);
            Assert.AreEqual(200, _backend.LastArgs["claimedCoins"]);
            Assert.AreEqual(300f, _backend.LastArgs["sessionDurationSec"]);
            Assert.AreEqual(0.667f, _backend.LastArgs["coinsPerSec"]);
        }

        [Test]
        public async Task GrantMiningRewardAsync_returns_granted_amount_and_updates_wallet_from_newBalance()
        {
            _backend.GrantedToReturn    = 150;
            _backend.NewBalanceToReturn = 650;

            int granted = await _economy.GrantMiningRewardAsync(200, 300f, 0.667f);

            Assert.AreEqual(150, granted);
            Assert.AreEqual(650, _wallet.Coins);
        }

        [Test]
        public async Task GrantMiningRewardAsync_does_not_touch_wallet_when_newBalance_is_null()
        {
            _wallet.SetCoins(500);
            _backend.GrantedToReturn    = 0;
            _backend.NewBalanceToReturn = null; // ValidateMining.js returns this when grantAmount <= 0

            int granted = await _economy.GrantMiningRewardAsync(0, 300f, 0.667f);

            Assert.AreEqual(0, granted);
            Assert.AreEqual(500, _wallet.Coins, "wallet must not be zeroed out when the server reports no new balance");
        }
    }
}
```

Note: this test references `MiningGrantResult` as a type visible to the test assembly — Step 3 below makes it `internal` on `EconomyService` won't be visible; make it a top-level `internal` type is still assembly-private. Since the test lives in a different assembly (`SocialUniverse.Tests`), declare `MiningGrantResult` as a **public** nested struct on `EconomyService` (matching the test's `typeof(T) == typeof(MiningGrantResult)` check, which needs the type to be resolvable from the test assembly). Adjust Step 3 accordingly.

- [ ] **Step 2: Run the test to verify it fails**

Run: `"C:\Program Files\Unity\Hub\Editor\<version>\Editor\Unity.exe" -runTests -projectPath . -testResults results.xml -testPlatform EditMode -assemblyNames SocialUniverse.Tests`
Expected: compile error — `EconomyService.GrantMiningRewardAsync`/`EconomyService.MiningGrantResult` don't exist yet.

- [ ] **Step 3: Implement in `EconomyService.cs`**

Add to `EconomyService` (after `GrantCoinsAsync`):

```csharp
        public async Task<int> GrantMiningRewardAsync(int claimedCoins, float sessionDurationSec, float coinsPerSec)
        {
            var result = await _backend.CallAsync<MiningGrantResult>(
                "ValidateMining",
                new Dictionary<string, object>
                {
                    { "claimedCoins", claimedCoins },
                    { "sessionDurationSec", sessionDurationSec },
                    { "coinsPerSec", coinsPerSec }
                });

            if (result.NewBalance.HasValue)
                _wallet.SetCoins(result.NewBalance.Value);

            return result.Granted;
        }
```

Change the private `// Thin DTOs for Cloud Code responses.` struct block at the bottom of the class from `private struct` to include a new public struct (public because the test above needs to reference it by type from another assembly):

```csharp
        // Thin DTOs for Cloud Code responses.
        private struct SpendResult  { public bool Success; public int NewBalance; }
        private struct GrantResult  { public int NewBalance; }
        public struct MiningGrantResult { public int Granted; public int? NewBalance; }
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `"C:\Program Files\Unity\Hub\Editor\<version>\Editor\Unity.exe" -runTests -projectPath . -testResults results.xml -testPlatform EditMode -assemblyNames SocialUniverse.Tests`
Expected: PASS for all `EconomyServiceMiningTests`.

- [ ] **Step 5: Commit**

```bash
git add Assets/_Project/Scripts/Economy/EconomyService.cs Assets/_Project/Tests/EditMode/Economy/EconomyServiceMiningTests.cs Assets/_Project/Tests/EditMode/Economy/EconomyServiceMiningTests.cs.meta
git commit -m "economy: EconomyService.GrantMiningRewardAsync wires mining payouts through ValidateMining"
```

---

### Task 10: `MiningController` rewrite — idle persistence + active orchestration

**Files:**
- Modify: `Assets/_Project/Scripts/Mining/MiningController.cs`
- Test: `Assets/_Project/Tests/EditMode/Mining/MiningControllerTests.cs`

**Interfaces:**
- Consumes: `MiningRewardCalculator.Compute` (Task 3), `AsteroidSpawner.FindBySlotId`/`ScheduleRespawn` (Task 2), `IdleMiningSession` new ctor/`Claim` (Task 5), `ActiveMiningMinigame.Begin/Tick/RegisterTap/Clear/CurrentSession/OnSessionChanged` (Task 7), `IEconomyService.GrantMiningRewardAsync` (Task 8/9), `SaveKeys` (extended in this task).
- Produces: `MiningController.Initialize(DroneRuntime drone)` (replaces `StartSession(DroneRuntime, DateTime)`); `Drone` (getter, unchanged); `CurrentIdleSession` (getter, unchanged name); `CurrentActiveSession` (new getter); `ClaimingAsteroid` (getter, unchanged); `event Action<IdleMiningSession> OnIdleSessionChanged` (unchanged); `event Action<ActiveMiningSession> OnActiveSessionChanged` (new); `bool BeginIdleMining(Asteroid)` (unchanged signature); `Task ClaimIdleSessionAsync(Asteroid)` (replaces `RegisterIdleClaimTapAsync`); `bool BeginActiveMining(Asteroid)` (new); `void TickActiveSession(float deltaTime)` (new, consumed by Task 11); `void RegisterActiveTap(bool hitTarget)` (new). Removes `MiningPhase`, `Phase`, `OnPhaseChanged`, `Tap()`, `CommitCargoAsync()`, `PickNextTarget()`, `NotifyIdleSessionStageChanged()`, `CurrentTarget`.
- Also modifies `SaveKeys.cs`: adds `IdleMiningSession` key, removes `LastSessionEnd` (no longer read/written by anything after this task — verified in Task 12).

- [ ] **Step 1: Add the new `SaveKeys` entry**

In `Assets/_Project/Scripts/Core/SaveKeys.cs`, add:

```csharp
        public const string IdleMiningSession = "idle_mining_session";
```

(Leave `LastSessionEnd` in place for now — Task 12 removes it once `PlanetSceneScope`'s write side is also deleted, to keep this task focused on `MiningController`.)

- [ ] **Step 2: Write the failing tests**

Create `Assets/_Project/Tests/EditMode/Mining/MiningControllerTests.cs`:

```csharp
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
using SocialUniverse.Config;
using SocialUniverse.Economy;
using SocialUniverse.Mining;
using UnityEngine;

namespace SocialUniverse.Tests
{
    public class MiningControllerTests
    {
        private EconomyConfig          _config;
        private AsteroidDefinition     _asteroidDef;
        private PlanetDefinition       _planet;
        private Wallet                 _wallet;
        private LocalMockEconomy       _economy;
        private MiningRewardCalculator _rewardCalc;
        private ActiveMiningMinigame   _activeMinigame;
        private AsteroidSpawner        _spawner;
        private MiningController       _mining;

        private static void SetField(object target, string field, object value) =>
            target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);

        [SetUp]
        public void SetUp()
        {
            PlayerPrefs.DeleteKey(SocialUniverse.Core.SaveKeys.IdleMiningSession);

            _config = ScriptableObject.CreateInstance<EconomyConfig>();
            SetField(_config, "_idleSecondsPerYieldUnit", 0f);
            SetField(_config, "_minIdleSessionSeconds", 0.05f);
            SetField(_config, "_maxIdleSessionSeconds", 0.05f);
            SetField(_config, "_activeYieldPerTap", 1f);
            SetField(_config, "_minActiveTaps", 1);
            SetField(_config, "_maxActiveTaps", 99);
            SetField(_config, "_activeMaxErrors", 3);
            SetField(_config, "_activeTapWindowSeconds", 5f);
            SetField(_config, "_asteroidRespawnHours", 4f);

            _asteroidDef = ScriptableObject.CreateInstance<AsteroidDefinition>();
            SetField(_asteroidDef, "_coinsPerUnit", 2);

            _planet = ScriptableObject.CreateInstance<PlanetDefinition>();
            SetField(_planet, "_planetId", "test_planet");

            _wallet     = new Wallet();
            _economy    = new LocalMockEconomy(_wallet, _config);
            _rewardCalc = new MiningRewardCalculator(_config);

            var spawnerGo = new GameObject("TestSpawner");
            _spawner = spawnerGo.AddComponent<AsteroidSpawner>();

            _activeMinigame = new ActiveMiningMinigame(_config, _rewardCalc);
            _mining = new MiningController(_economy, _rewardCalc, _activeMinigame, _spawner, _config, _planet);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_config);
            Object.DestroyImmediate(_asteroidDef);
            Object.DestroyImmediate(_planet);
            PlayerPrefs.DeleteKey(SocialUniverse.Core.SaveKeys.IdleMiningSession);
        }

        private Asteroid MakeAndRegisterAsteroid(string slotId, int remainingYield)
        {
            var go = new GameObject("TestAsteroid");
            var asteroid = go.AddComponent<Asteroid>();
            asteroid.Initialize(_asteroidDef, slotId);
            typeof(Asteroid).GetField("<RemainingYield>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(asteroid, remainingYield);

            var active = (List<Asteroid>)typeof(AsteroidSpawner)
                .GetField("_active", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(_spawner);
            active.Add(asteroid);

            return asteroid;
        }

        [Test]
        public async Task ClaimIdleSessionAsync_grants_full_yield_and_schedules_respawn()
        {
            var asteroid = MakeAndRegisterAsteroid("slot_0", remainingYield: 20);
            Assert.IsTrue(_mining.BeginIdleMining(asteroid));

            await Task.Delay(100);
            _mining.CurrentIdleSession.Tick(0f);
            Assert.AreEqual(IdleMiningStage.ReadyToClaim, _mining.CurrentIdleSession.Stage);

            int coinsBefore = _wallet.Coins;

            await _mining.ClaimIdleSessionAsync(asteroid);

            Assert.IsNull(_mining.CurrentIdleSession);
            Assert.AreEqual(coinsBefore + 40, _wallet.Coins); // 20 yield * 2 coins/unit
            Assert.IsTrue(asteroid.IsDepleted);
        }

        [Test]
        public void BeginIdleMining_fails_while_a_session_is_already_running()
        {
            var a1 = MakeAndRegisterAsteroid("slot_0", 10);
            var a2 = MakeAndRegisterAsteroid("slot_1", 10);

            Assert.IsTrue(_mining.BeginIdleMining(a1));
            Assert.IsFalse(_mining.BeginIdleMining(a2));
        }

        [Test]
        public void BeginActiveMining_does_not_require_the_drone_and_can_run_alongside_an_idle_session()
        {
            var idleAsteroid   = MakeAndRegisterAsteroid("slot_0", 10);
            var activeAsteroid = MakeAndRegisterAsteroid("slot_1", 10);

            Assert.IsTrue(_mining.BeginIdleMining(idleAsteroid));
            Assert.IsTrue(_mining.BeginActiveMining(activeAsteroid));

            Assert.IsNotNull(_mining.CurrentIdleSession);
            Assert.IsNotNull(_mining.CurrentActiveSession);
        }

        [Test]
        public async Task Active_mining_success_grants_full_yield()
        {
            var asteroid = MakeAndRegisterAsteroid("slot_0", remainingYield: 10);
            Assert.IsTrue(_mining.BeginActiveMining(asteroid));
            int tapsRequired = _mining.CurrentActiveSession.TapsRequired;
            int coinsBefore  = _wallet.Coins;

            for (int i = 0; i < tapsRequired; i++)
                _mining.RegisterActiveTap(true);

            await Task.Yield(); // let the fire-and-forget payout Task complete

            Assert.IsNull(_mining.CurrentActiveSession);
            Assert.AreEqual(coinsBefore + 20, _wallet.Coins); // 10 yield * 2 coins/unit
            Assert.IsTrue(asteroid.IsDepleted);
        }

        [Test]
        public void Active_mining_failure_grants_nothing_and_clears_the_session()
        {
            var asteroid = MakeAndRegisterAsteroid("slot_0", remainingYield: 10);
            Assert.IsTrue(_mining.BeginActiveMining(asteroid));
            int coinsBefore = _wallet.Coins;

            _mining.RegisterActiveTap(false);
            _mining.RegisterActiveTap(false);
            _mining.RegisterActiveTap(false); // 3rd miss -> Failed

            Assert.IsNull(_mining.CurrentActiveSession);
            Assert.AreEqual(coinsBefore, _wallet.Coins);
            Assert.IsTrue(asteroid.IsDepleted, "a failed asteroid is still consumed with zero payout");
        }

        [Test]
        public void Initialize_restores_a_persisted_idle_session_for_the_current_planet()
        {
            var asteroid = MakeAndRegisterAsteroid("slot_0", remainingYield: 20);
            string value = $"test_planet|slot_0|{System.DateTime.UtcNow.AddMinutes(-10):O}|60";
            PlayerPrefs.SetString(SocialUniverse.Core.SaveKeys.IdleMiningSession, value);

            var droneDef = ScriptableObject.CreateInstance<DroneDefinition>();
            _mining.Initialize(new DroneRuntime(droneDef));

            Assert.IsNotNull(_mining.CurrentIdleSession);
            Assert.AreEqual(asteroid, _mining.CurrentIdleSession.Asteroid);
            Assert.AreEqual(IdleMiningStage.ReadyToClaim, _mining.CurrentIdleSession.Stage,
                "10 minutes elapsed against a 60s duration should already be ready to claim");

            Object.DestroyImmediate(droneDef);
        }

        [Test]
        public void Initialize_discards_a_persisted_session_for_a_different_planet()
        {
            MakeAndRegisterAsteroid("slot_0", remainingYield: 20);
            string value = $"other_planet|slot_0|{System.DateTime.UtcNow:O}|60";
            PlayerPrefs.SetString(SocialUniverse.Core.SaveKeys.IdleMiningSession, value);

            var droneDef = ScriptableObject.CreateInstance<DroneDefinition>();
            _mining.Initialize(new DroneRuntime(droneDef));

            Assert.IsNull(_mining.CurrentIdleSession);

            Object.DestroyImmediate(droneDef);
        }
    }
}
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `"C:\Program Files\Unity\Hub\Editor\<version>\Editor\Unity.exe" -runTests -projectPath . -testResults results.xml -testPlatform EditMode -assemblyNames SocialUniverse.Tests`
Expected: compile errors — `MiningController`'s constructor and API don't match yet.

- [ ] **Step 4: Rewrite `MiningController.cs`**

Replace the entire file:

```csharp
using System;
using System.Globalization;
using System.Threading.Tasks;
using UnityEngine;
using SocialUniverse.Config;
using SocialUniverse.Economy;
using SocialUniverse.Core;

namespace SocialUniverse.Mining
{
    public class MiningController
    {
        private readonly IEconomyService        _economy;
        private readonly MiningRewardCalculator  _rewardCalc;
        private readonly ActiveMiningMinigame    _activeMinigame;
        private readonly AsteroidSpawner         _spawner;
        private readonly EconomyConfig           _config;
        private readonly PlanetDefinition        _planet;

        public DroneRuntime Drone { get; private set; }

        public IdleMiningSession   CurrentIdleSession   { get; private set; }
        public ActiveMiningSession CurrentActiveSession => _activeMinigame.CurrentSession;
        public Asteroid            ClaimingAsteroid     { get; private set; }

        public event Action<IdleMiningSession>   OnIdleSessionChanged;
        public event Action<ActiveMiningSession> OnActiveSessionChanged;

        public MiningController(IEconomyService economy, MiningRewardCalculator rewardCalc,
            ActiveMiningMinigame activeMinigame, AsteroidSpawner spawner, EconomyConfig config, PlanetDefinition planet)
        {
            _economy        = economy;
            _rewardCalc     = rewardCalc;
            _activeMinigame = activeMinigame;
            _spawner        = spawner;
            _config         = config;
            _planet         = planet;

            _activeMinigame.OnSessionChanged += OnActiveMinigameSessionChanged;
        }

        public void Initialize(DroneRuntime drone)
        {
            Drone = drone;
            TryRestoreIdleSession();
        }

        // ---- Idle mining ----

        public bool BeginIdleMining(Asteroid asteroid)
        {
            if (asteroid == null || asteroid.IsDepleted || CurrentIdleSession != null)
                return false;

            var reward = _rewardCalc.Compute(asteroid);
            CurrentIdleSession = new IdleMiningSession(asteroid, DateTime.UtcNow, reward.IdleDurationSeconds);
            CurrentIdleSession.OnStageChanged += _ => OnIdleSessionChanged?.Invoke(CurrentIdleSession);

            PersistIdleSession(CurrentIdleSession);
            SULog.Info($"Idle session started on {asteroid.name} ({reward.IdleDurationSeconds:0}s)", SULog.Channel.Mining);
            OnIdleSessionChanged?.Invoke(CurrentIdleSession);
            return true;
        }

        // Player tapped the asteroid while it's ready to claim. Completes and pays out.
        public async Task ClaimIdleSessionAsync(Asteroid asteroid)
        {
            var session = CurrentIdleSession;
            if (session == null || session.Asteroid != asteroid || session.Stage != IdleMiningStage.ReadyToClaim)
                return;

            var reward = _rewardCalc.Compute(asteroid);
            session.Claim();

            int mined = asteroid.Mine(asteroid.RemainingYield);
            int coins = mined * asteroid.Definition.CoinsPerUnit;

            CurrentIdleSession = null;
            ClaimingAsteroid   = asteroid;
            ClearPersistedIdleSession();
            OnIdleSessionChanged?.Invoke(null);

            if (coins > 0)
            {
                int granted = await _economy.GrantMiningRewardAsync(coins, reward.IdleDurationSeconds, reward.CoinsPerSec);
                SULog.Info($"Idle session claimed: +{mined} {asteroid.Definition.MineralType} -> {granted} coins", SULog.Channel.Mining);
            }

            ClaimingAsteroid = null;
            _spawner.ScheduleRespawn(asteroid, _config.AsteroidRespawnHours);
        }

        // ---- Active mining ----

        public bool BeginActiveMining(Asteroid asteroid) => _activeMinigame.Begin(asteroid);

        public void TickActiveSession(float deltaTime) => _activeMinigame.Tick(deltaTime);

        public void RegisterActiveTap(bool hitTarget) => _activeMinigame.RegisterTap(hitTarget);

        private void OnActiveMinigameSessionChanged(ActiveMiningSession session)
        {
            OnActiveSessionChanged?.Invoke(session);

            if (session == null) return;
            if (session.Stage == ActiveMiningStage.Success) _ = CompleteActiveMiningAsync(session);
            else if (session.Stage == ActiveMiningStage.Failed) FailActiveMining(session);
        }

        private async Task CompleteActiveMiningAsync(ActiveMiningSession session)
        {
            var asteroid = session.Asteroid;
            var reward   = _rewardCalc.Compute(asteroid);

            int mined = asteroid.Mine(asteroid.RemainingYield);
            int coins = mined * asteroid.Definition.CoinsPerUnit;

            _activeMinigame.Clear();
            OnActiveSessionChanged?.Invoke(null);

            if (coins > 0)
            {
                int granted = await _economy.GrantMiningRewardAsync(coins, reward.IdleDurationSeconds, reward.CoinsPerSec);
                SULog.Info($"Active mining success: +{mined} {asteroid.Definition.MineralType} -> {granted} coins", SULog.Channel.Mining);
            }

            _spawner.ScheduleRespawn(asteroid, _config.AsteroidRespawnHours);
        }

        private void FailActiveMining(ActiveMiningSession session)
        {
            var asteroid = session.Asteroid;
            asteroid.Mine(asteroid.RemainingYield);

            _activeMinigame.Clear();
            OnActiveSessionChanged?.Invoke(null);

            SULog.Info($"Active mining failed on {asteroid.name} — asteroid lost", SULog.Channel.Mining);
            _spawner.ScheduleRespawn(asteroid, _config.AsteroidRespawnHours);
        }

        // ---- Idle session persistence (survives the app being closed) ----

        private void TryRestoreIdleSession()
        {
            var raw = PlayerPrefs.GetString(SaveKeys.IdleMiningSession, "");
            if (string.IsNullOrEmpty(raw)) return;

            var parts = raw.Split('|');
            if (parts.Length != 4)
            {
                ClearPersistedIdleSession();
                return;
            }

            string planetId = parts[0];
            string slotId   = parts[1];

            bool validTimestamp = DateTime.TryParse(parts[2], null, DateTimeStyles.RoundtripKind, out var startUtc);
            bool validDuration  = float.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var duration);

            if (planetId != _planet.PlanetId || !validTimestamp || !validDuration)
            {
                ClearPersistedIdleSession();
                return;
            }

            var asteroid = _spawner.FindBySlotId(slotId);
            if (asteroid == null || asteroid.IsDepleted)
            {
                ClearPersistedIdleSession();
                return;
            }

            CurrentIdleSession = new IdleMiningSession(asteroid, startUtc, duration);
            CurrentIdleSession.OnStageChanged += _ => OnIdleSessionChanged?.Invoke(CurrentIdleSession);

            SULog.Info($"Idle session restored on {asteroid.name} (stage={CurrentIdleSession.Stage})", SULog.Channel.Mining);
            OnIdleSessionChanged?.Invoke(CurrentIdleSession);
        }

        private void PersistIdleSession(IdleMiningSession session)
        {
            string duration = session.DurationSeconds.ToString(CultureInfo.InvariantCulture);
            string value    = $"{_planet.PlanetId}|{session.Asteroid.SlotId}|{session.StartUtc:O}|{duration}";
            PlayerPrefs.SetString(SaveKeys.IdleMiningSession, value);
            PlayerPrefs.Save();
        }

        private static void ClearPersistedIdleSession()
        {
            PlayerPrefs.DeleteKey(SaveKeys.IdleMiningSession);
            PlayerPrefs.Save();
        }
    }
}
```

- [ ] **Step 5: Run the `MiningControllerTests` and the whole EditMode `Mining`/`Economy` suites**

Run: `"C:\Program Files\Unity\Hub\Editor\<version>\Editor\Unity.exe" -runTests -projectPath . -testResults results.xml -testPlatform EditMode -assemblyNames SocialUniverse.Tests`
Expected: PASS for `MiningControllerTests` and all previously-passing tests from Tasks 2–9. `IdleMiningSessionController.cs` still references the old session API at this point — fixed in Step 6 below, within this same task. `MiningInputHandler.cs`, `MiningModePromptView.cs`, `HUDController.cs`, `PlanetSceneScope.cs`, `PlanetSceneBootstrapper` also still reference removed members — expected, fixed in Tasks 12–15.

- [ ] **Step 6: Update `IdleMiningSessionController.cs` to match the simplified session API**

Replace the file's `Tick()` method and remove the now-unused `NotifyIdleSessionStageChanged`-driven flow (the session's own `OnStageChanged`, wired in `MiningController`, replaces it). Replace the whole file:

```csharp
using System;
using UnityEngine;
using VContainer.Unity;
using SocialUniverse.Core;

namespace SocialUniverse.Mining
{
    // Drives a player-directed idle mining session end to end:
    // travel to the asteroid -> wall-clock mining wait -> single-tap claim.
    public class IdleMiningSessionController : ITickable, IStartable, IDisposable
    {
        private readonly MiningController _mining;
        private readonly DroneController  _drone;

        private IdleMiningSession _trackedSession;

        public IdleMiningSessionController(MiningController mining, DroneController drone)
        {
            _mining = mining;
            _drone  = drone;
        }

        public void Start() => EventBus.Subscribe<AsteroidSelectedEvent>(OnAsteroidSelected);

        public void Dispose() => EventBus.Unsubscribe<AsteroidSelectedEvent>(OnAsteroidSelected);

        public void Tick()
        {
            var session = _mining.CurrentIdleSession;

            if (session != _trackedSession)
            {
                _trackedSession = session;
                if (session != null)
                    _drone.SetTarget(session.Asteroid.transform);
                else
                    _drone.ReturnToBase(); // asteroid claimed — head back to base
            }

            if (session == null) return;

            session.Tick(Time.deltaTime);

            if (session.Stage == IdleMiningStage.Traveling && _drone.IsAtTarget)
                session.BeginMining();
        }

        private void OnAsteroidSelected(AsteroidSelectedEvent e)
        {
            var session = _mining.CurrentIdleSession;
            if (session != null && session.Asteroid == e.Asteroid && session.Stage == IdleMiningStage.ReadyToClaim)
                _ = _mining.ClaimIdleSessionAsync(e.Asteroid);
        }
    }
}
```

(This drops the dead `_vfx`/`SpawnVfx`/`DespawnVfx` members — they were already commented out and unused in the pre-existing code, and this file's `Dispose()` no longer needs to clean up VFX that's never spawned.)

- [ ] **Step 7: Re-run the full EditMode suite and commit**

Run: `"C:\Program Files\Unity\Hub\Editor\<version>\Editor\Unity.exe" -runTests -projectPath . -testResults results.xml -testPlatform EditMode -assemblyNames SocialUniverse.Tests`
Expected: PASS for all tests written so far.

```bash
git add Assets/_Project/Scripts/Mining/MiningController.cs Assets/_Project/Scripts/Mining/IdleMiningSessionController.cs Assets/_Project/Scripts/Core/SaveKeys.cs Assets/_Project/Tests/EditMode/Mining/MiningControllerTests.cs Assets/_Project/Tests/EditMode/Mining/MiningControllerTests.cs.meta
git commit -m "mining: MiningController orchestrates idle persistence + active mining, drops cargo/free-tap"
```

---

### Task 11: `ActiveMiningSessionController` (new ITickable)

**Files:**
- Create: `Assets/_Project/Scripts/Mining/ActiveMiningSessionController.cs`

**Interfaces:**
- Consumes: `MiningController.TickActiveSession(float deltaTime)` (Task 10, already exists by this point).

- [ ] **Step 1: Implement `ActiveMiningSessionController.cs`**

Create `Assets/_Project/Scripts/Mining/ActiveMiningSessionController.cs`:

```csharp
using UnityEngine;
using VContainer.Unity;

namespace SocialUniverse.Mining
{
    // Advances the active-mining minigame's tap-window timer every frame, so a target point
    // that's never tapped still counts as a miss once its window expires. Active mining has
    // no travel/arrival phase, so unlike IdleMiningSessionController this only drives Tick.
    public class ActiveMiningSessionController : ITickable
    {
        private readonly MiningController _mining;

        public ActiveMiningSessionController(MiningController mining) => _mining = mining;

        public void Tick() => _mining.TickActiveSession(Time.deltaTime);
    }
}
```

- [ ] **Step 2: Verify the project compiles**

Run `mcp__UnityMCP__read_console` (or open Unity) and confirm no compile errors reference `ActiveMiningSessionController`. It is not yet registered in DI (that happens in Task 12 alongside the other Mining DI cleanup), so it isn't ticking yet — this task only needs to compile cleanly against `MiningController.TickActiveSession`, which Task 10 already produced.

- [ ] **Step 3: Commit**

```bash
git add Assets/_Project/Scripts/Mining/ActiveMiningSessionController.cs
git commit -m "mining: add ActiveMiningSessionController to drive active-mining tap-window timeouts"
```

---

### Task 12: Delete `MiningInputHandler` and `IdleMiningCalculator` (+ its test), wire DI

**Files:**
- Delete: `Assets/_Project/Scripts/Mining/MiningInputHandler.cs`, `.meta`
- Delete: `Assets/_Project/Scripts/Mining/IdleMiningCalculator.cs`, `.meta`
- Delete: `Assets/_Project/Tests/EditMode/Mining/IdleMiningCalculatorTests.cs`, `.meta`
- Modify: `Assets/_Project/Scripts/App/PlanetSceneScope.cs`
- Modify: `Assets/_Project/Scripts/Core/SaveKeys.cs`

**Interfaces:**
- Produces: DI registrations updated so `MiningRewardCalculator`, `ActiveMiningSessionController` are registered and `IdleMiningCalculator`, `MiningInputHandler` are not.

- [ ] **Step 1: Delete the obsolete files**

```bash
git rm Assets/_Project/Scripts/Mining/MiningInputHandler.cs Assets/_Project/Scripts/Mining/MiningInputHandler.cs.meta
git rm Assets/_Project/Scripts/Mining/IdleMiningCalculator.cs Assets/_Project/Scripts/Mining/IdleMiningCalculator.cs.meta
git rm Assets/_Project/Tests/EditMode/Mining/IdleMiningCalculatorTests.cs Assets/_Project/Tests/EditMode/Mining/IdleMiningCalculatorTests.cs.meta
```

- [ ] **Step 2: Update DI registrations in `PlanetSceneScope.cs`**

Change:

```csharp
            // Mining
            builder.Register<IdleMiningCalculator>(Lifetime.Singleton);
            builder.Register<ActiveMiningMinigame>(Lifetime.Singleton);
            builder.Register<MiningController>(Lifetime.Singleton);
            builder.RegisterComponentInHierarchy<AsteroidSpawner>();
            builder.RegisterComponentInHierarchy<DroneController>();
            builder.RegisterComponentInHierarchy<AsteroidSelectionController>();
```

to:

```csharp
            // Mining
            builder.Register<MiningRewardCalculator>(Lifetime.Singleton);
            builder.Register<ActiveMiningMinigame>(Lifetime.Singleton);
            builder.Register<MiningController>(Lifetime.Singleton);
            builder.RegisterComponentInHierarchy<AsteroidSpawner>();
            builder.RegisterComponentInHierarchy<DroneController>();
            builder.RegisterComponentInHierarchy<AsteroidSelectionController>();
```

and change:

```csharp
            builder.RegisterEntryPoint<PlanetSceneBootstrapper>();
            builder.RegisterEntryPoint<MiningInputHandler>();
            builder.RegisterEntryPoint<IdleMiningSessionController>();
```

to:

```csharp
            builder.RegisterEntryPoint<PlanetSceneBootstrapper>();
            builder.RegisterEntryPoint<IdleMiningSessionController>();
            builder.RegisterEntryPoint<ActiveMiningSessionController>();
```

- [ ] **Step 3: Remove the offline-session-end persistence (now dead) from `PlanetSceneScope.cs`**

Delete these two methods and the `OnApplicationPause` handler entirely:

```csharp
        private void OnApplicationPause(bool pausing)
        {
            if (pausing) SaveSessionEnd();
        }

        private void OnApplicationQuit() => SaveSessionEnd();

        private static void SaveSessionEnd()
        {
            PlayerPrefs.SetString(SaveKeys.LastSessionEnd, DateTime.UtcNow.ToString("O"));
            PlayerPrefs.Save();
        }
```

(If the `using System;` at the top of the file becomes unused after this removal, check the rest of the file before removing the import — `PlanetSceneBootstrapper` in the same file uses `DateTime`/`Exception` elsewhere, so `using System;` stays.)

- [ ] **Step 4: Update `PlanetSceneBootstrapper.Start()` to call `Initialize` instead of `StartSession`**

Change:

```csharp
            EventBus.Publish(new LoadingStatusEvent(0.90f));
            var saved       = PlayerPrefs.GetString(SaveKeys.LastSessionEnd, "");
            var lastSession = DateTime.TryParse(saved, out var dt) ? dt : DateTime.UtcNow;
            var drone       = new DroneRuntime(droneDef);
            _miningController.StartSession(drone, lastSession);
```

to:

```csharp
            EventBus.Publish(new LoadingStatusEvent(0.90f));
            var drone = new DroneRuntime(droneDef);
            _miningController.Initialize(drone);
```

- [ ] **Step 5: Remove `SaveKeys.LastSessionEnd`**

Run: `grep -rn "LastSessionEnd" Assets/_Project/Scripts` to confirm no remaining references after Steps 3–4.
Expected: no matches.

In `Assets/_Project/Scripts/Core/SaveKeys.cs`, delete the line:

```csharp
        public const string LastSessionEnd = "last_session_end";
```

- [ ] **Step 6: Run the full EditMode suite**

Run: `"C:\Program Files\Unity\Hub\Editor\<version>\Editor\Unity.exe" -runTests -projectPath . -testResults results.xml -testPlatform EditMode -assemblyNames SocialUniverse.Tests`
Expected: PASS for all tests. No references to `IdleMiningCalculator`/`MiningInputHandler` remain anywhere (`grep -rn "IdleMiningCalculator\|MiningInputHandler" Assets/_Project` returns nothing).

- [ ] **Step 7: Commit**

```bash
git add -A Assets/_Project/Scripts/Mining Assets/_Project/Scripts/App/PlanetSceneScope.cs Assets/_Project/Scripts/Core/SaveKeys.cs Assets/_Project/Tests/EditMode/Mining
git commit -m "mining: remove IdleMiningCalculator and MiningInputHandler, rewire scene DI"
```

---

### Task 13: `MiningModePromptView` — wire the Active Mine button

**Files:**
- Modify: `Assets/_Project/Scripts/UI/MiningModePromptView.cs`

**Interfaces:**
- Consumes: `MiningController.BeginIdleMining(Asteroid)` (unchanged), `MiningController.BeginActiveMining(Asteroid)` (Task 10).

- [ ] **Step 1: Wire `OnActiveMineClicked`**

Change:

```csharp
        private void OnActiveMineClicked()
        {
            // Active mining mini-game arrives in a later milestone — no-op for now.
            SULog.Info("Active mining mode chosen — mini-game coming in a later milestone", SULog.Channel.Mining);
            ClosePrompt();
        }
```

to:

```csharp
        private void OnActiveMineClicked()
        {
            if (_pendingAsteroid != null)
                _mining.BeginActiveMining(_pendingAsteroid);

            ClosePrompt();
        }
```

- [ ] **Step 2: Verify compile**

Run `mcp__UnityMCP__read_console` (or open Unity) and confirm no compile errors reference `MiningModePromptView`.

- [ ] **Step 3: Commit**

```bash
git add Assets/_Project/Scripts/UI/MiningModePromptView.cs
git commit -m "ui: wire Active Mine button to MiningController.BeginActiveMining"
```

---

### Task 14: `HUDController` — drop cargo readout and `OnPhaseChanged`

**Files:**
- Modify: `Assets/_Project/Scripts/UI/HUDController.cs`

**Interfaces:**
- Consumes: `MiningController.CurrentIdleSession` (unchanged getter name; underlying type's stage/progress API changed in Task 5 but the switch expression's cases are unchanged: `Traveling`/`Mining`/`ReadyToClaim`).

- [ ] **Step 1: Remove the `OnPhaseChanged` subscription**

Change:

```csharp
            _playerState.OnLevelChanged       += SetLevel;
            _playerState.OnFuelChanged        += SetFuel;
            _playerState.OnDisplayNameChanged += SetUsername;
            _mining.OnPhaseChanged            += _ => RefreshMiningStatus();
            _presence.PresenceChanged         += RefreshExplorerCount;
```

to:

```csharp
            _playerState.OnLevelChanged       += SetLevel;
            _playerState.OnFuelChanged        += SetFuel;
            _playerState.OnDisplayNameChanged += SetUsername;
            _presence.PresenceChanged         += RefreshExplorerCount;
```

- [ ] **Step 2: Rewrite `RefreshMiningStatus` to drop the cargo fallback and the multi-tap claim text**

Change:

```csharp
        private void RefreshMiningStatus()
        {
            if (_miningStatusText == null) return;
            var session = _mining.CurrentIdleSession;
            if (session != null)
            {
                _miningStatusText.text = session.Stage switch
                {
                    IdleMiningStage.Traveling    => $"Heading to {session.Asteroid.Definition.MineralType} asteroid...",
                    IdleMiningStage.Mining       => $"Mining {session.Asteroid.Definition.MineralType}: {Mathf.RoundToInt(session.MiningProgress01 * 100f)}%",
                    IdleMiningStage.ReadyToClaim => $"Tap the asteroid to claim! ({session.ClaimTapsRemaining} left)",
                    _                            => "Mining: —"
                };
                return;
            }

            var drone  = _mining.Drone;
            var target = _mining.CurrentTarget;

            if (drone == null)
            {
                _miningStatusText.text = "Mining: —";
                return;
            }

            string mineral = target?.Definition != null ? target.Definition.MineralType : "—";
            _miningStatusText.text = $"Mining {mineral}: {drone.CargoAmount}/{drone.Definition.CargoCap}";
        }
```

to:

```csharp
        private void RefreshMiningStatus()
        {
            if (_miningStatusText == null) return;
            var session = _mining.CurrentIdleSession;
            if (session != null)
            {
                _miningStatusText.text = session.Stage switch
                {
                    IdleMiningStage.Traveling    => $"Heading to {session.Asteroid.Definition.MineralType} asteroid...",
                    IdleMiningStage.Mining       => $"Mining {session.Asteroid.Definition.MineralType}: {Mathf.RoundToInt(session.MiningProgress01 * 100f)}%",
                    IdleMiningStage.ReadyToClaim => "Tap the asteroid to claim!",
                    _                            => "Mining: —"
                };
                return;
            }

            _miningStatusText.text = "Mining: —";
        }
```

- [ ] **Step 3: Verify compile**

Run `mcp__UnityMCP__read_console` (or open Unity) and confirm no compile errors reference `HUDController`.

- [ ] **Step 4: Commit**

```bash
git add Assets/_Project/Scripts/UI/HUDController.cs
git commit -m "ui: HUDController drops cargo readout and OnPhaseChanged, idle status only"
```

---

### Task 15: `ActiveMiningMinigameView` (new UI overlay)

**Files:**
- Create: `Assets/_Project/Scripts/UI/ActiveMiningMinigameView.cs`

**Interfaces:**
- Consumes: `MiningController.OnActiveSessionChanged` (Task 10), `MiningController.CurrentActiveSession` (Task 10), `MiningController.RegisterActiveTap(bool)` (Task 10).

- [ ] **Step 1: Implement the view**

Create `Assets/_Project/Scripts/UI/ActiveMiningMinigameView.cs`:

```csharp
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using SocialUniverse.Mining;

namespace SocialUniverse.UI
{
    // Overlay shown while an active-mining session is running: renders the current target
    // point, an error counter, and forwards player taps to MiningController. _targetButton
    // must be a UI child rendered above _missAreaButton in the hierarchy so a tap on the
    // point hits the target first and any other tap in the asteroid area falls through to
    // the miss button (standard Unity UI raycast ordering).
    public class ActiveMiningMinigameView : MonoBehaviour
    {
        [SerializeField] private GameObject    _root;
        [SerializeField] private RectTransform _asteroidArea;  // bounds for random point placement
        [SerializeField] private RectTransform _targetPoint;
        [SerializeField] private Button        _targetButton;
        [SerializeField] private Button        _missAreaButton; // full-bleed background behind the target point
        [SerializeField] private Text          _progressText;
        [SerializeField] private Text          _errorText;

        [Inject] private MiningController _mining;

        private void Awake()
        {
            if (_root != null) _root.SetActive(false);
            if (_targetButton   != null) _targetButton.onClick.AddListener(() => OnTapped(hitTarget: true));
            if (_missAreaButton != null) _missAreaButton.onClick.AddListener(() => OnTapped(hitTarget: false));
        }

        private void Start() => _mining.OnActiveSessionChanged += OnSessionChanged;

        private void OnDestroy() => _mining.OnActiveSessionChanged -= OnSessionChanged;

        private void Update()
        {
            var session = _mining.CurrentActiveSession;
            if (session != null) Refresh(session);
        }

        private void OnSessionChanged(ActiveMiningSession session)
        {
            if (session == null)
            {
                if (_root != null) _root.SetActive(false);
                return;
            }

            if (_root != null) _root.SetActive(true);
            Refresh(session);
            PlaceTargetPoint();
        }

        private void Refresh(ActiveMiningSession session)
        {
            if (_progressText != null) _progressText.text = $"{session.SuccessfulTaps}/{session.TapsRequired}";
            if (_errorText    != null) _errorText.text    = $"Misses: {session.ErrorCount}/{session.MaxErrors}";
        }

        private void PlaceTargetPoint()
        {
            if (_targetPoint == null || _asteroidArea == null) return;

            float x = Random.Range(-_asteroidArea.rect.width  * 0.5f, _asteroidArea.rect.width  * 0.5f);
            float y = Random.Range(-_asteroidArea.rect.height * 0.5f, _asteroidArea.rect.height * 0.5f);
            _targetPoint.anchoredPosition = new Vector2(x, y);
        }

        private void OnTapped(bool hitTarget)
        {
            if (_mining.CurrentActiveSession == null) return;

            _mining.RegisterActiveTap(hitTarget);

            if (_mining.CurrentActiveSession != null) // session still in progress -> next point
                PlaceTargetPoint();
        }
    }
}
```

- [ ] **Step 2: Register it in `PlanetSceneScope.cs`**

Add alongside the other UI registrations:

```csharp
            builder.RegisterComponentInHierarchy<SocialUniverse.UI.ActiveMiningMinigameView>();
```

- [ ] **Step 3: Verify compile**

Run `mcp__UnityMCP__read_console` (or open Unity) and confirm no compile errors.

- [ ] **Step 4: Commit**

```bash
git add Assets/_Project/Scripts/UI/ActiveMiningMinigameView.cs Assets/_Project/Scripts/App/PlanetSceneScope.cs
git commit -m "ui: add ActiveMiningMinigameView overlay for the tap-timing minigame"
```

---

### Task 16: Manual Editor wiring — Active Mining overlay panel

**Files:**
- Modify (Editor-only, not script code): `Assets/Scenes/Planet.unity`

This task has no code and therefore no automated test — its verification is a manual Play Mode smoke test in the Unity Editor, consistent with how the existing `MiningModePromptView` panel was wired per `PROGRESS.md`.

- [ ] **Step 1: Add the overlay GameObject to the Planet scene's Canvas**

In the Unity Editor, open `Assets/Scenes/Planet.unity`. Under the same Canvas that hosts `MiningPrompt` (the `MiningModePromptView` panel), add a new child GameObject `ActiveMiningOverlay` with an `Image` (background) and the `ActiveMiningMinigameView` component. Under it, add:
- `MissArea` — a full-rect `Image` + `Button` (this is `_missAreaButton`) sized to cover the whole overlay.
- `AsteroidArea` — a `RectTransform` sized to the area within which the target point should appear (this is `_asteroidArea`).
- `TargetPoint` — a small `Image` + `Button` (this is `_targetPoint`/`_targetButton`) parented under `AsteroidArea`, rendered **after** (below, i.e. later in hierarchy order so it draws on top of) `MissArea` in the hierarchy so it receives taps first.
- `ProgressText` / `ErrorText` — `Text` elements for tap progress and miss count.

Set the `ActiveMiningOverlay` GameObject inactive by default (the view's `Awake()` also does this defensively, matching `MiningModePromptView`'s pattern).

- [ ] **Step 2: Assign the serialized fields**

Select `ActiveMiningOverlay`, and in the `ActiveMiningMinigameView` component inspector, assign `_root` (the overlay GameObject itself), `_asteroidArea`, `_targetPoint`, `_targetButton`, `_missAreaButton`, `_progressText`, `_errorText` to the GameObjects created in Step 1.

- [ ] **Step 3: Manual smoke test in Play Mode**

Enter Play Mode on the Planet scene. Tap an asteroid, choose "Active Mine" in the prompt. Confirm: the overlay appears, a target point is visible, tapping it increments the progress counter and moves the point, missing (tapping elsewhere, or waiting out the window) increments the miss counter, reaching the required taps closes the overlay and grants coins (visible in the HUD currency view), and 3 misses closes the overlay with no coin change and the asteroid disappears from the field.

- [ ] **Step 4: Commit the scene change**

```bash
git add Assets/Scenes/Planet.unity
git commit -m "scene: wire ActiveMiningMinigameView overlay panel into the Planet scene Canvas"
```

---

### Task 17: `PlanetSceneFlowTests` — replace the cargo-based PlayMode test

**Files:**
- Modify: `Assets/_Project/Tests/PlayMode/PlanetSceneFlowTests.cs`

**Interfaces:**
- Consumes: `MiningController.Initialize` (Task 10, called by `PlanetSceneBootstrapper` — already wired by Task 12), `MiningController.BeginIdleMining`/`ClaimIdleSessionAsync` (Task 10), `AsteroidSpawner.ActiveAsteroids` (existing).

- [ ] **Step 1: Replace the `SetUp` mining-readiness wait and the cargo test**

Replace the whole file:

```csharp
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using SocialUniverse.App;
using SocialUniverse.Core;
using SocialUniverse.Config;
using SocialUniverse.Economy;
using SocialUniverse.Mining;
using SocialUniverse.World;

namespace SocialUniverse.Tests
{
    // Covers the M1 exit-criteria loop end to end against LocalMock services:
    // idle mining a claimed asteroid pays out coins, and buying a tile transfers ownership.
    public class PlanetSceneFlowTests
    {
        private const string PlanetScenePath = "Assets/Scenes/Planet.unity";

        private PlanetSceneScope     _scope;
        private MiningController     _mining;
        private AsteroidSpawner      _spawner;
        private Wallet               _wallet;
        private HexasphereManager    _hex;
        private LandPurchaseService  _purchaseService;
        private EconomyConfig        _economyConfig;
        private PlanetDefinition     _planet;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return SceneManager.LoadSceneAsync(PlanetScenePath, LoadSceneMode.Single);

            _scope = UnityEngine.Object.FindFirstObjectByType<PlanetSceneScope>();
            Assert.IsNotNull(_scope, "PlanetSceneScope not found in Planet scene");
            Assert.IsNotNull(_scope.Container, "PlanetSceneScope.Container not initialized");

            _mining          = (MiningController)_scope.Container.Resolve(typeof(MiningController));
            _spawner         = (AsteroidSpawner)_scope.Container.Resolve(typeof(AsteroidSpawner));
            _wallet          = (Wallet)_scope.Container.Resolve(typeof(Wallet));
            _hex             = (HexasphereManager)_scope.Container.Resolve(typeof(HexasphereManager));
            _purchaseService = (LandPurchaseService)_scope.Container.Resolve(typeof(LandPurchaseService));
            _economyConfig   = (EconomyConfig)_scope.Container.Resolve(typeof(EconomyConfig));
            _planet          = (PlanetDefinition)_scope.Container.Resolve(typeof(PlanetDefinition));

            // _economyConfig is the actual project asset (tuned for real play — durations can
            // run into minutes for higher-yield asteroids). Force every idle session in this
            // test run to a fixed 1-second duration by mutating the resolved in-memory instance's
            // private fields directly, the same reflection pattern the EditMode tests in this
            // plan already use. This only changes the runtime object held by this test session —
            // it is not saved back to the .asset file on disk (no AssetDatabase.SaveAssets call).
            SetField(_economyConfig, "_idleSecondsPerYieldUnit", 0f);
            SetField(_economyConfig, "_minIdleSessionSeconds", 1f);
            SetField(_economyConfig, "_maxIdleSessionSeconds", 1f);

            // Asteroids spawn synchronously during PlanetSceneBootstrapper.Start(); wait for the field to populate.
            float timeout = Time.realtimeSinceStartup + 5f;
            while (_spawner.ActiveAsteroids.Count == 0 && Time.realtimeSinceStartup < timeout)
                yield return null;
        }

        private static void SetField(object target, string field, object value) =>
            target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);

        [UnityTest]
        public IEnumerator Idle_mining_a_claimed_asteroid_grants_coins_and_schedules_respawn()
        {
            var asteroid = _spawner.ActiveAsteroids.FirstOrDefault(a => !a.IsDepleted);
            Assert.IsNotNull(asteroid, "Expected at least one active asteroid after scene boot");

            int expectedCoins = asteroid.RemainingYield * asteroid.Definition.CoinsPerUnit;
            int coinsBefore   = _wallet.Coins;

            Assert.IsTrue(_mining.BeginIdleMining(asteroid));

            // SetUp forced a fixed 1-second duration, so this only ever waits ~1 real second
            // regardless of the asteroid's actual yield or this scene's production EconomyConfig.
            float timeout = Time.realtimeSinceStartup + 5f;
            while (_mining.CurrentIdleSession != null
                   && _mining.CurrentIdleSession.Stage != IdleMiningStage.ReadyToClaim
                   && Time.realtimeSinceStartup < timeout)
            {
                yield return null;
            }

            Assert.AreEqual(IdleMiningStage.ReadyToClaim, _mining.CurrentIdleSession?.Stage,
                "Idle session should reach ReadyToClaim well within the 5s timeout given the 1s forced duration");

            var claimTask = _mining.ClaimIdleSessionAsync(asteroid);
            while (!claimTask.IsCompleted) yield return null;
            if (claimTask.Exception != null) throw claimTask.Exception;

            Assert.IsNull(_mining.CurrentIdleSession);
            Assert.AreEqual(coinsBefore + expectedCoins, _wallet.Coins,
                "Wallet should increase by the asteroid's full yield * coins-per-unit after claiming");
        }

        [UnityTest]
        public IEnumerator Selecting_an_available_tile_purchases_it_and_transfers_ownership()
        {
            TileData tile = null;
            foreach (var kv in _hex.Tiles)
            {
                if (kv.Value.State == TileState.Available) { tile = kv.Value; break; }
            }
            Assert.IsNotNull(tile, "Expected at least one Available tile on the planet");

            int price = (int)Math.Round(_economyConfig.BaseLandPrice * _planet.LandPriceMultiplier);
            Assert.GreaterOrEqual(_wallet.Coins, price, "Test setup expects enough coins to afford the tile");
            int coinsBefore = _wallet.Coins;

            EventBus.Publish(new TileSelectedEvent { Tile = tile });

            // TilePurchaseHandler.OnTileSelected runs PurchaseAsync — wait for the state transition.
            float timeout = Time.realtimeSinceStartup + 5f;
            while (tile.State == TileState.Available && Time.realtimeSinceStartup < timeout)
                yield return null;

            Assert.AreEqual(TileState.OwnedByPlayer, tile.State, "Tile should become OwnedByPlayer after purchase");
            Assert.AreEqual("local_player", tile.OwnerId);
            Assert.AreEqual(coinsBefore - price, _wallet.Coins, "Wallet should be debited the tile price");
        }
    }
}
```

- [ ] **Step 2: Run the PlayMode suite**

Run: `"C:\Program Files\Unity\Hub\Editor\<version>\Editor\Unity.exe" -runTests -projectPath . -testResults results.xml -testPlatform PlayMode`
Expected: PASS for both tests in `PlanetSceneFlowTests`.

- [ ] **Step 3: Commit**

```bash
git add Assets/_Project/Tests/PlayMode/PlanetSceneFlowTests.cs
git commit -m "test: replace cargo-based mining PlayMode test with idle-claim flow"
```

---

### Task 18: Final verification

**Files:** none (verification only)

- [ ] **Step 1: Full-project compile check**

Run: `"C:\Program Files\Unity\Hub\Editor\<version>\Editor\Unity.exe" -batchmode -quit -projectPath . -logFile compile.log`, then check `compile.log` for `error CS` — expect none. Alternatively, if Unity is already open via UnityMCP, call `mcp__UnityMCP__read_console` and confirm zero compile errors.

- [ ] **Step 2: Run the full EditMode suite**

Run: `"C:\Program Files\Unity\Hub\Editor\<version>\Editor\Unity.exe" -runTests -projectPath . -testResults editmode-results.xml -testPlatform EditMode`
Expected: all tests PASS, including every test file added/modified in Tasks 2, 3, 5, 6, 7, 8, 9, 11.

- [ ] **Step 3: Run the full PlayMode suite**

Run: `"C:\Program Files\Unity\Hub\Editor\<version>\Editor\Unity.exe" -runTests -projectPath . -testResults playmode-results.xml -testPlatform PlayMode`
Expected: all tests PASS, including `PlanetSceneFlowTests`.

- [ ] **Step 4: Grep-verify no dangling references to removed members**

Run: `grep -rn "MiningPhase\|OnPhaseChanged\|CommitCargoAsync\|CargoAmount\|IsCargoFull\|IdleMiningCalculator\|MiningInputHandler\|ClaimTapsRequired\|ClaimTapsRemaining\|RegisterIdleClaimTapAsync\|LastSessionEnd" Assets/_Project/Scripts Assets/_Project/Tests`
Expected: no matches.

- [ ] **Step 5: Manual Play Mode smoke test of the full idle + active flow**

Per the project's UI-verification convention, open the Planet scene in the Editor and Play Mode test both flows end to end: (a) idle-mine an asteroid, stop Play Mode (or background the Editor briefly) and resume, confirm the session is still tracked and reaches ReadyToClaim at the right time, claim it and see coins increase; (b) active-mine a different asteroid via the new overlay (Task 16), both a full-success run and a 3-miss failure run, confirming payout/no-payout and respawn scheduling in both cases (visible via the HUD's "Next asteroid" countdown).

- [ ] **Step 6: Update `PROGRESS.md`'s Mining section to reflect the completed rework**

Locate the Mining section (`### Mining`, lines ~220–235 per the pre-change file) and the `ActiveMiningMinigame` row currently marked `⚠️ Stubbed`. Update it to `✅` with a note describing the tap-timing minigame, and update the `IdleMiningSession`/`IdleMiningCalculator`/`MiningController` rows to reflect: wall-clock persistence, single-tap claim, `IdleMiningCalculator` removed, `MiningRewardCalculator` added. This is a documentation-only edit — no code changes, so it's exempt from the Pre-Task Protocol per `CLAUDE.md`.

- [ ] **Step 7: Commit**

```bash
git add PROGRESS.md
git commit -m "docs: update PROGRESS.md mining section for the idle/active rework"
```
