# Mining Claim Budget Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make `ValidateMining` server-authoritative — it checks planet, mineral, drone tier and a per-planet claim budget, and computes the grant cap itself instead of trusting client-supplied duration/rate.

**Architecture:** `ServerCode/ValidateMining.js` gains a catalog (planets → minerals, mineral yields/tiers, drones) mirrored from the SO assets and guarded by an EditMode drift test, plus a `mining_claim_log` Cloud Save record that limits each planet to 6 claims per rolling 4 hours. The client sends `{ planetId, mineralId, claimedQty }`, receives a `MiningGrantResult` with an optional rejection reason, and surfaces rejections through the existing toast (`ServerActionFailedEvent`).

**Tech Stack:** Unity 6000.3.12f1 (C#, VContainer, NUnit EditMode/PlayMode tests), UGS Cloud Code JavaScript (`@unity-services/cloud-save-1.4`), Node (ad-hoc verification only).

**Spec:** `docs/superpowers/specs/2026-09-29-mining-claim-budget-design.md`

## Global Constraints

- Work on `main`. **Do not commit** — the user commits and pushes manually. Where a task would commit, stop at "all tests green" instead.
- Namespaces mirror folders: `Mining/` → `SocialUniverse.Mining`, `App/` → `SocialUniverse.App`, `Config/` → `SocialUniverse.Config`, tests → `SocialUniverse.Tests`. One public type per file, file named after the type.
- New `.cs` files need a `.meta`. The Unity Editor is usually open on the project and generates them; if it is not, copy the `.meta` the headless run generates in `C:\su` back into the repo (check the repo first — never overwrite an existing `.meta`).
- Write files with LF line endings (existing files are LF).
- `ServerCode/*.js` must pass `node --check`.
- Server catalog values (verbatim from the assets):
  - `PLANET_MINERALS`: earth `["iron","carbon","silicon"]`, jupiter `["silicon","nickel","platinum"]`, mars `["silicon","nickel"]`, mercury `["iron","carbon"]`, moon `["iron","carbon"]`, neptune `["platinum","iridium"]`, pluto `["platinum","iridium"]`, saturn `["silicon","nickel","platinum"]`, uranus `["platinum","iridium"]`, venus `["iron","carbon"]`
  - `MINERALS`: iron 80/T1, carbon 65/T1, silicon 50/T2, nickel 40/T2, platinum 25/T3, iridium 15/T3 (baseYield/tier)
  - `DRONES`: scout T1 ×1, extractor T2 ×1.5, excavator T4 ×1.8, surveyor T4 ×2.5, titan T5 ×3
  - `FIELD_SIZE = 6`, `RESPAWN_HOURS = 4`, `YIELD_ROLL_MAX = 1.2`, `YIELD_DELTA_PER_LEVEL = 0.15`, `STARTER_DRONE_ID = "scout"`
- Rejection reasons (exact strings): `INVALID_PARAMS`, `WRONG_PLANET`, `MINERAL_NOT_ON_PLANET`, `TIER_TOO_LOW`, `BUDGET_EXHAUSTED`.
- Toast text (exact): `BUDGET_EXHAUSTED` → "No more asteroids here for now. Try again later."; `TIER_TOO_LOW` → "Requires a higher-tier drone"; `WRONG_PLANET` / `MINERAL_NOT_ON_PLANET` → "Mining isn't available here right now".

### How to run Unity tests (used by every task)

The Editor holds a lock on the project, so tests run headless on a mirror at `C:\su` (short path — some asset paths exceed 260 chars). In PowerShell:

```powershell
$src = "C:\Users\chris\UnityProjects\social-universe"
foreach ($d in "Assets","Packages","ProjectSettings","ServerCode") { robocopy "$src\$d" "C:\su\$d" /MIR /NFL /NDL /NJH /NJS /NP /R:1 /W:1 | Out-Null }
$r = "C:\su\results.xml"; Remove-Item $r -ErrorAction SilentlyContinue
& "C:\Program Files\Unity\Hub\Editor\6000.3.12f1\Editor\Unity.exe" -runTests -batchmode -projectPath C:\su -testPlatform EditMode -testFilter "<FILTER>" -testResults $r -logFile C:\su\log.txt | Out-Null
if (Test-Path $r) { $x = ([xml](Get-Content $r)).'test-run'; "total $($x.total) passed $($x.passed) failed $($x.failed)"; ([xml](Get-Content $r)).SelectNodes("//test-case[@result='Failed']") | % { "FAIL $($_.fullname): $($_.failure.message.'#cdata-section')" } } else { Select-String C:\su\log.txt -Pattern "error CS" | Select -First 10 | % Line }
```

`<FILTER>` is a regex on test full names (e.g. `MiningCatalogAlignmentTests`). Omit `-testFilter` to run the whole suite; use `-testPlatform PlayMode` for PlayMode. No `results.xml` means a compile error — the last line prints it.

---

## File Map

| File | Change | Responsibility |
|---|---|---|
| `Assets/_Project/Scripts/Config/EconomyConfig.cs` | Modify | Add `AsteroidYieldRollMin/Max` |
| `Assets/_Project/ScriptableObjects/EconomyConfig.asset` | Modify | Serialize the two new fields |
| `Assets/_Project/Scripts/Mining/Asteroid.cs` | Modify | `RollYield` + configurable `Initialize` |
| `Assets/_Project/Scripts/Mining/AsteroidSpawner.cs` | Modify | Inject `EconomyConfig`, pass roll range |
| `ServerCode/ValidateMining.js` | Rewrite | Catalog, checks, claim budget |
| `Assets/_Project/Scripts/Mining/MiningGrantResult.cs` | Create | Public DTO for the `ValidateMining` response |
| `Assets/_Project/Scripts/Mining/IMineralService.cs` | Modify | New `GrantMiningAsync` signature |
| `Assets/_Project/Scripts/Mining/MineralService.cs` | Modify | New request shape, returns `MiningGrantResult` |
| `Assets/_Project/Scripts/Mining/LocalMockMineralService.cs` | Modify | New signature |
| `Assets/_Project/Scripts/Mining/MiningClaimRejectedEvent.cs` | Create | Event for a rejected claim |
| `Assets/_Project/Scripts/Mining/MiningController.cs` | Modify | Pass `planetId`, publish rejection |
| `Assets/_Project/Scripts/Mining/MiningRewardCalculator.cs` | Modify | Remove `UnitsPerSec` |
| `Assets/_Project/Scripts/App/MiningClaimFeedbackHandler.cs` | Create | Rejection → toast |
| `Assets/_Project/Scripts/App/ServerFailureMessages.cs` | Modify | New reason texts |
| `Assets/_Project/Scripts/App/PlanetSceneScope.cs` | Modify | Register the handler |
| Tests (see tasks) | Create/Modify | — |
| `PROGRESS.md` | Modify | Known Issue #16 status |

---

### Task 1: Configurable asteroid yield roll

**Files:**
- Modify: `Assets/_Project/Scripts/Config/EconomyConfig.cs` (Mining — Shared header, ~line 41; getters ~line 89)
- Modify: `Assets/_Project/ScriptableObjects/EconomyConfig.asset` (after `_asteroidRespawnHours: 4`)
- Modify: `Assets/_Project/Scripts/Mining/Asteroid.cs` (`Initialize`)
- Modify: `Assets/_Project/Scripts/Mining/AsteroidSpawner.cs` (fields ~line 20; `SpawnOne` ~line 224)
- Modify: `Assets/_Project/Tests/EditMode/Mining/IdleMiningSessionTests.cs:16`, `MiningControllerTests.cs:134`, `MiningRewardCalculatorTests.cs:57` (Initialize callers)
- Create: `Assets/_Project/Tests/EditMode/Mining/AsteroidYieldRollTests.cs`

**Interfaces:**
- Produces: `EconomyConfig.AsteroidYieldRollMin` / `AsteroidYieldRollMax` (float, defaults 0.8 / 1.2); `Asteroid.RollYield(int baseYield, float min, float max, float random01) → int`; `Asteroid.Initialize(AsteroidDefinition definition, string slotId, float yieldRollMin, float yieldRollMax)`.

- [ ] **Step 1: Write the failing test** — `Assets/_Project/Tests/EditMode/Mining/AsteroidYieldRollTests.cs`

```csharp
using NUnit.Framework;
using SocialUniverse.Config;
using SocialUniverse.Mining;
using UnityEditor;

namespace SocialUniverse.Tests
{
    // The server caps a mining grant at ceil(ceil(baseYield × YIELD_ROLL_MAX) × mult)
    // (ServerCode/ValidateMining.js). That cap is only safe if a spawned asteroid can never roll
    // above EconomyConfig.AsteroidYieldRollMax.
    public class AsteroidYieldRollTests
    {
        [Test]
        public void Roll_endpoints_hit_the_configured_min_and_max()
        {
            Assert.AreEqual(64, Asteroid.RollYield(80, 0.8f, 1.2f, 0f));
            Assert.AreEqual(96, Asteroid.RollYield(80, 0.8f, 1.2f, 1f));
        }

        [Test]
        public void Roll_never_exceeds_base_times_max()
        {
            for (int i = 0; i <= 100; i++)
                Assert.LessOrEqual(Asteroid.RollYield(15, 0.8f, 1.2f, i / 100f), 18);
        }

        [Test]
        public void Project_config_uses_the_documented_roll_range()
        {
            var config = AssetDatabase.LoadAssetAtPath<EconomyConfig>("Assets/_Project/ScriptableObjects/EconomyConfig.asset");
            Assert.IsNotNull(config);
            Assert.AreEqual(0.8f, config.AsteroidYieldRollMin, 1e-6f);
            Assert.AreEqual(1.2f, config.AsteroidYieldRollMax, 1e-6f);
        }
    }
}
```

- [ ] **Step 2: Run it to verify it fails**

Run the Unity test command with `-testFilter "AsteroidYieldRollTests"`.
Expected: no results file; log shows `error CS0117: 'Asteroid' does not contain a definition for 'RollYield'`.

- [ ] **Step 3: Implement**

`EconomyConfig.cs` — directly under `_asteroidRespawnHours`:

```csharp
        [SerializeField] private float _asteroidYieldRollMin    = 0.8f; // spawned asteroid yield = BaseYield × a roll in [min, max]
        [SerializeField] private float _asteroidYieldRollMax    = 1.2f; // MUST MATCH ServerCode/ValidateMining.js YIELD_ROLL_MAX
```

and next to `public float AsteroidRespawnHours => _asteroidRespawnHours;`:

```csharp
        public float AsteroidYieldRollMin  => _asteroidYieldRollMin;
        public float AsteroidYieldRollMax  => _asteroidYieldRollMax;
```

`EconomyConfig.asset` — after the line `  _asteroidRespawnHours: 4`:

```yaml
  _asteroidYieldRollMin: 0.8
  _asteroidYieldRollMax: 1.2
```

`Asteroid.cs` — replace the `Initialize` signature and yield line, and add `RollYield`:

```csharp
        public void Initialize(AsteroidDefinition definition, string slotId, float yieldRollMin, float yieldRollMax)
        {
            Definition     = definition;
            SlotId         = slotId;
            RemainingYield = RollYield(definition.BaseYield, yieldRollMin, yieldRollMax, Random.value);
```

```csharp
        // Spawn yield: BaseYield scaled by a roll in [min, max]. random01 is injected so tests can
        // pin it. ServerCode/ValidateMining.js caps grants assuming max is the ceiling.
        public static int RollYield(int baseYield, float min, float max, float random01) =>
            Mathf.RoundToInt(baseYield * Mathf.Lerp(min, max, random01));
```

`AsteroidSpawner.cs` — next to `[Inject] private DatabaseRegistry _registry;`:

```csharp
        [Inject] private EconomyConfig    _config;
```

and in `SpawnOne`:

```csharp
            asteroid.Initialize(def, slotId, _config.AsteroidYieldRollMin, _config.AsteroidYieldRollMax);
```

Test callers — change each `Initialize(x, "…")` / `Initialize(_asteroidDef, slotId)` to pass `, 1f, 1f` (these tests overwrite or ignore `RemainingYield`):
- `IdleMiningSessionTests.cs:16` → `a.Initialize(def, "slot_0", 1f, 1f);`
- `MiningControllerTests.cs:134` → `asteroid.Initialize(_asteroidDef, slotId, 1f, 1f);`
- `MiningRewardCalculatorTests.cs:57` → `asteroid.Initialize(_def, "slot_0", 1f, 1f);`

Also update the comment above `MakeAsteroid` in `MiningRewardCalculatorTests.cs` ("BaseYield * Random.Range(0.8f, 1.2f)") to "BaseYield × a random roll (Asteroid.RollYield)".

- [ ] **Step 4: Run tests to verify they pass**

Run with `-testFilter "AsteroidYieldRollTests|IdleMiningSessionTests|MiningControllerTests|MiningRewardCalculatorTests"`.
Expected: all pass.

---

### Task 2: Server — `ValidateMining.js` with catalog and claim budget

**Files:**
- Rewrite: `ServerCode/ValidateMining.js`
- Create: `Assets/_Project/Tests/EditMode/Mining/MiningCatalogAlignmentTests.cs`
- Delete: `Assets/_Project/Tests/EditMode/Mining/ValidateMiningCapAlignmentTests.cs` and its `.meta`

**Interfaces:**
- Consumes: `EconomyConfig.AsteroidYieldRollMax` (Task 1).
- Produces: Cloud Code `ValidateMining` — request `{ planetId, mineralId, claimedQty }`; response `{ granted, mineralId }` or `{ granted: 0, mineralId, reason }`. Cloud Save key `mining_claim_log` = `{ [planetId]: number[] }` (ms timestamps).

- [ ] **Step 1: Write the failing drift test** — `Assets/_Project/Tests/EditMode/Mining/MiningCatalogAlignmentTests.cs`

```csharp
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using SocialUniverse.Config;
using UnityEditor;
using UnityEngine;

namespace SocialUniverse.Tests
{
    // ServerCode/ValidateMining.js mirrors planet/asteroid/drone data to validate mining claims
    // server-side (docs/superpowers/specs/2026-09-29-mining-claim-budget-design.md). This reads the
    // JS as text (no Node harness) and checks every mirrored value against the real assets.
    public class MiningCatalogAlignmentTests
    {
        private const string SoRoot = "Assets/_Project/ScriptableObjects/";
        private string _js;
        private DatabaseRegistry _registry;
        private EconomyConfig _config;

        [SetUp]
        public void SetUp()
        {
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "ServerCode", "ValidateMining.js"));
            Assert.IsTrue(File.Exists(path), $"Expected to find ValidateMining.js at {path}");
            _js = File.ReadAllText(path);
            _registry = AssetDatabase.LoadAssetAtPath<DatabaseRegistry>(SoRoot + "DatabaseRegistry.asset");
            _config   = AssetDatabase.LoadAssetAtPath<EconomyConfig>(SoRoot + "EconomyConfig.asset");
            Assert.IsNotNull(_registry);
            Assert.IsNotNull(_config);
        }

        private string Block(string name)
        {
            var m = Regex.Match(_js, @"const\s+" + name + @"\s*=\s*\{(?<body>.*?)\n\};", RegexOptions.Singleline);
            Assert.IsTrue(m.Success, $"Could not find the {name} block in ValidateMining.js");
            return m.Groups["body"].Value;
        }

        private float Number(string name)
        {
            var m = Regex.Match(_js, @"const\s+" + name + @"\s*=\s*(?<v>[0-9]+(\.[0-9]+)?)");
            Assert.IsTrue(m.Success, $"Could not find {name} in ValidateMining.js");
            return float.Parse(m.Groups["v"].Value, CultureInfo.InvariantCulture);
        }

        [Test]
        public void Planet_minerals_match_each_planets_asteroid_types()
        {
            var server = new Dictionary<string, string[]>();
            foreach (Match m in Regex.Matches(Block("PLANET_MINERALS"), @"(?<id>\w+)\s*:\s*\[(?<list>[^\]]*)\]"))
                server[m.Groups["id"].Value] = Regex.Matches(m.Groups["list"].Value, "\"(\\w+)\"")
                    .Cast<Match>().Select(x => x.Groups[1].Value).OrderBy(x => x).ToArray();

            var planets = _registry.AllPlanets.ToList();
            CollectionAssert.AreEquivalent(planets.Select(p => p.PlanetId), server.Keys, "PLANET_MINERALS must list every registered planet");
            foreach (var p in planets)
            {
                var expected = p.AsteroidTypes.Select(a => a.Mineral.MineralId).Distinct().OrderBy(x => x).ToArray();
                CollectionAssert.AreEqual(expected, server[p.PlanetId], $"PLANET_MINERALS.{p.PlanetId}");
            }
        }

        [Test]
        public void Minerals_match_asteroid_base_yield_and_tier()
        {
            var server = Regex.Matches(Block("MINERALS"), @"(?<id>\w+)\s*:\s*\{\s*baseYield:\s*(?<y>\d+),\s*tier:\s*(?<t>\d+)\s*\}")
                .Cast<Match>().ToDictionary(m => m.Groups["id"].Value, m => (int.Parse(m.Groups["y"].Value), int.Parse(m.Groups["t"].Value)));

            var asteroids = _registry.AllPlanets.SelectMany(p => p.AsteroidTypes).Distinct().ToList();
            CollectionAssert.AreEquivalent(asteroids.Select(a => a.Mineral.MineralId), server.Keys);
            foreach (var a in asteroids)
                Assert.AreEqual((a.BaseYield, a.Tier), server[a.Mineral.MineralId], $"MINERALS.{a.Mineral.MineralId}");
        }

        [Test]
        public void Drones_match_tier_and_yield_multiplier()
        {
            var server = Regex.Matches(Block("DRONES"), @"(?<id>\w+)\s*:\s*\{\s*tier:\s*(?<t>\d+),\s*yieldMult:\s*(?<m>[0-9.]+)\s*\}")
                .Cast<Match>().ToDictionary(m => m.Groups["id"].Value,
                    m => (int.Parse(m.Groups["t"].Value), float.Parse(m.Groups["m"].Value, CultureInfo.InvariantCulture)));

            CollectionAssert.AreEquivalent(_registry.AllDrones.Select(d => d.DroneId), server.Keys);
            foreach (var d in _registry.AllDrones)
            {
                Assert.AreEqual(d.Tier, server[d.DroneId].Item1, $"DRONES.{d.DroneId}.tier");
                Assert.AreEqual(d.YieldMultiplier, server[d.DroneId].Item2, 1e-6f, $"DRONES.{d.DroneId}.yieldMult");
            }
        }

        [Test]
        public void Scalar_constants_match_config_and_upgrades()
        {
            var yieldUpgrade = _registry.GetUpgrade(DroneStat.Yield);
            Assert.IsNotNull(yieldUpgrade, "DatabaseRegistry has no Yield upgrade");
            Assert.AreEqual(yieldUpgrade.DeltaPerLevel, Number("YIELD_DELTA_PER_LEVEL"), 1e-6f);
            Assert.AreEqual(_config.AsteroidRespawnHours, Number("RESPAWN_HOURS"), 1e-6f);
            Assert.AreEqual(_config.AsteroidYieldRollMax, Number("YIELD_ROLL_MAX"), 1e-6f);

            float fieldSize = Number("FIELD_SIZE");
            foreach (var p in _registry.AllPlanets)
                Assert.LessOrEqual(p.AsteroidFieldSize, fieldSize, $"{p.PlanetId} field is larger than FIELD_SIZE");
        }
    }
}
```

Members used (verified to exist): `DatabaseRegistry.GetUpgrade(DroneStat)`, `AllPlanets`, `AllDrones`; `PlanetDefinition.PlanetId`, `AsteroidTypes`, `AsteroidFieldSize`; `AsteroidDefinition.Mineral`, `BaseYield`, `Tier`; `DroneDefinition.DroneId`, `Tier`, `YieldMultiplier`; `UpgradeDefinition.DeltaPerLevel`.

- [ ] **Step 2: Delete the superseded test**

```bash
git -c safe.directory='*' rm -q Assets/_Project/Tests/EditMode/Mining/ValidateMiningCapAlignmentTests.cs Assets/_Project/Tests/EditMode/Mining/ValidateMiningCapAlignmentTests.cs.meta
```

- [ ] **Step 3: Run the drift test to verify it fails**

Run with `-testFilter "MiningCatalogAlignmentTests"`.
Expected: 4 failures, "Could not find the PLANET_MINERALS block" (etc.).

- [ ] **Step 4: Rewrite `ServerCode/ValidateMining.js`**

```js
// ValidateMining — grants MINERALS for an idle or active mining claim (M6), server-authoritatively.
// Design: docs/superpowers/specs/2026-09-29-mining-claim-budget-design.md (Known Issue #16).
//
// Request { planetId, mineralId, claimedQty }. Checks, in order:
//   1. planetId == the server's current_planet (a missing/null record accepts the claimed planet)
//   2. mineralId is one of that planet's asteroid minerals
//   3. the saved fleet's active drone tier >= the mineral's asteroid tier
//   4. grant = min(claimedQty, ceil(ceil(baseYield × YIELD_ROLL_MAX) × effectiveYieldMult))
//   5. fewer than FIELD_SIZE claims on this planet in the last RESPAWN_HOURS (mining_claim_log)
// Rejections return { granted: 0, mineralId, reason } so the client can show why.
// The budget slot is reserved before minerals are granted and released if the grant fails.
const { DataApi } = require("@unity-services/cloud-save-1.4");

const INVENTORY_KEY      = "mineral_inventory";
const CLAIM_LOG_KEY      = "mining_claim_log";
const PLANET_KEY         = "current_planet";
const FLEET_KEY          = "drone_fleet";
const MAX_WRITE_ATTEMPTS = 3;
const MS_PER_HOUR        = 3600000;

// ---- Catalog: MUST MATCH the SO assets (MiningCatalogAlignmentTests) ----
const FIELD_SIZE            = 6;    // >= every PlanetDefinition._asteroidFieldSize
const RESPAWN_HOURS         = 4;    // EconomyConfig._asteroidRespawnHours
const YIELD_ROLL_MAX        = 1.2;  // EconomyConfig._asteroidYieldRollMax
const YIELD_DELTA_PER_LEVEL = 0.15; // Upgrade_Yield._deltaPerLevel
const STARTER_DRONE_ID      = "scout"; // MUST MATCH the Tier-1, zero-cost DroneDefinition

// PlanetDefinition._planetId -> its _asteroidTypes' mineral ids
const PLANET_MINERALS = {
  earth:   ["iron", "carbon", "silicon"],
  jupiter: ["silicon", "nickel", "platinum"],
  mars:    ["silicon", "nickel"],
  mercury: ["iron", "carbon"],
  moon:    ["iron", "carbon"],
  neptune: ["platinum", "iridium"],
  pluto:   ["platinum", "iridium"],
  saturn:  ["silicon", "nickel", "platinum"],
  uranus:  ["platinum", "iridium"],
  venus:   ["iron", "carbon"],
};

// Mineral id -> its AsteroidDefinition's _baseYield and _tier
const MINERALS = {
  iron:     { baseYield: 80, tier: 1 },
  carbon:   { baseYield: 65, tier: 1 },
  silicon:  { baseYield: 50, tier: 2 },
  nickel:   { baseYield: 40, tier: 2 },
  platinum: { baseYield: 25, tier: 3 },
  iridium:  { baseYield: 15, tier: 3 },
};

// DroneDefinition._droneId -> _tier and _yieldMultiplier
const DRONES = {
  scout:     { tier: 1, yieldMult: 1 },
  extractor: { tier: 2, yieldMult: 1.5 },
  excavator: { tier: 4, yieldMult: 1.8 },
  surveyor:  { tier: 4, yieldMult: 2.5 },
  titan:     { tier: 5, yieldMult: 3 },
};

/**
 * @param {string} planetId - Planet the claim was mined on (PlanetDefinition._planetId).
 * @param {string} mineralId - Mineral mined (MineralDefinition._mineralId).
 * @param {number} claimedQty - Units the client computed. Positive integer; clamped server-side.
 */
module.exports = async ({ params, context, logger }) => {
  const { planetId, mineralId, claimedQty } = params;
  const reject = reason => ({ granted: 0, mineralId: mineralId ?? null, reason });

  if (!planetId || !mineralId || !Number.isInteger(claimedQty) || claimedQty <= 0) return reject("INVALID_PARAMS");

  const { projectId, playerId } = context;
  const saveApi = new DataApi(context);

  try {
    const records = await loadRecords(saveApi, projectId, playerId, [PLANET_KEY, FLEET_KEY]);

    const serverPlanet = records[PLANET_KEY]?.value?.planetId;
    if (serverPlanet && serverPlanet !== planetId) return reject("WRONG_PLANET");

    const mineral = MINERALS[mineralId];
    if (!mineral || !(PLANET_MINERALS[planetId] ?? []).includes(mineralId)) return reject("MINERAL_NOT_ON_PLANET");

    const drone = activeDrone(records[FLEET_KEY]?.value);
    if (mineral.tier > drone.tier) return reject("TIER_TOO_LOW");

    const cap         = Math.ceil(Math.ceil(mineral.baseYield * YIELD_ROLL_MAX) * drone.yieldMult);
    const grantAmount = Math.min(claimedQty, cap);

    const claimTs = Date.now();
    if (!(await reserveBudget(saveApi, projectId, playerId, planetId, claimTs))) return reject("BUDGET_EXHAUSTED");

    try {
      await addToInventory(saveApi, projectId, playerId, mineralId, grantAmount);
    } catch (err) {
      try { await releaseBudget(saveApi, projectId, playerId, planetId, claimTs); }
      catch (releaseErr) { logger.error(`ValidateMining: budget release FAILED for ${playerId}: ${releaseErr?.message}`); }
      throw err;
    }

    logger.info(`ValidateMining: player ${playerId} +${grantAmount} ${mineralId} on ${planetId} (claimed ${claimedQty}, cap ${cap})`);
    return { granted: grantAmount, mineralId };
  } catch (err) {
    logger.error("ValidateMining failed", { "error.message": err.message });
    throw err;
  }
};

// ---- helpers ----

async function loadRecords(saveApi, projectId, playerId, keys) {
  const out = {};
  try {
    const res = await saveApi.getItems(projectId, playerId, keys);
    for (const r of res.data.results) out[r.key] = { value: r.value, writeLock: r.writeLock };
  } catch (_) { /* none yet */ }
  return out;
}

// The fleet's active drone, if owned; otherwise the starter drone (empty fleet, as in Known Issue #11).
function activeDrone(fleet) {
  const owned = Array.isArray(fleet?.drones) ? fleet.drones : [];
  const entry = owned.find(d => d.droneId === fleet?.activeDroneId) ?? { droneId: STARTER_DRONE_ID, upgrades: {} };
  const def   = DRONES[entry.droneId] ?? DRONES[STARTER_DRONE_ID];
  const level = Number.isInteger(entry.upgrades?.Yield) ? entry.upgrades.Yield : 0;
  return { tier: def.tier, yieldMult: def.yieldMult + level * YIELD_DELTA_PER_LEVEL };
}

function prune(log, now) {
  const cutoff = now - RESPAWN_HOURS * MS_PER_HOUR;
  for (const id of Object.keys(log)) {
    log[id] = (Array.isArray(log[id]) ? log[id] : []).filter(ts => ts > cutoff);
    if (log[id].length === 0) delete log[id];
  }
  return log;
}

// Appends claimTs to the planet's log under the record's writeLock. False = budget exhausted.
async function reserveBudget(saveApi, projectId, playerId, planetId, claimTs) {
  for (let attempt = 0; attempt < MAX_WRITE_ATTEMPTS; attempt++) {
    const rec = (await loadRecords(saveApi, projectId, playerId, [CLAIM_LOG_KEY]))[CLAIM_LOG_KEY];
    const log = prune(typeof rec?.value === "object" && rec.value ? rec.value : {}, claimTs);
    const list = log[planetId] ?? [];
    if (list.length >= FIELD_SIZE) return false;
    log[planetId] = [...list, claimTs];
    try {
      await saveApi.setItem(projectId, playerId, withLock(CLAIM_LOG_KEY, log, rec?.writeLock));
      return true;
    } catch (err) {
      if (isConflict(err) && attempt < MAX_WRITE_ATTEMPTS - 1) continue;
      throw err;
    }
  }
}

async function releaseBudget(saveApi, projectId, playerId, planetId, claimTs) {
  for (let attempt = 0; attempt < MAX_WRITE_ATTEMPTS; attempt++) {
    const rec = (await loadRecords(saveApi, projectId, playerId, [CLAIM_LOG_KEY]))[CLAIM_LOG_KEY];
    const log = typeof rec?.value === "object" && rec.value ? rec.value : {};
    log[planetId] = (log[planetId] ?? []).filter(ts => ts !== claimTs);
    if (log[planetId].length === 0) delete log[planetId];
    try {
      await saveApi.setItem(projectId, playerId, withLock(CLAIM_LOG_KEY, log, rec?.writeLock));
      return;
    } catch (err) {
      if (isConflict(err) && attempt < MAX_WRITE_ATTEMPTS - 1) continue;
      throw err;
    }
  }
}

// Read-modify-write under the record's writeLock: SellMinerals writes the same record, and an
// unlocked write here could undo a sale that already paid out (Known Issue #16).
async function addToInventory(saveApi, projectId, playerId, mineralId, qty) {
  for (let attempt = 0; attempt < MAX_WRITE_ATTEMPTS; attempt++) {
    const rec = (await loadRecords(saveApi, projectId, playerId, [INVENTORY_KEY]))[INVENTORY_KEY];
    const inventory = typeof rec?.value === "object" && rec.value ? rec.value : {};
    inventory[mineralId] = (inventory[mineralId] || 0) + qty;
    try {
      await saveApi.setItem(projectId, playerId, withLock(INVENTORY_KEY, inventory, rec?.writeLock));
      return;
    } catch (err) {
      if (isConflict(err) && attempt < MAX_WRITE_ATTEMPTS - 1) continue;
      throw err;
    }
  }
}

function withLock(key, value, writeLock) {
  return writeLock ? { key, value, writeLock } : { key, value };
}

function isConflict(err) {
  const status = err?.response?.status ?? err?.status;
  return status === 409;
}
```

Note: **do not** add `ValidateMining` to `DroneCatalogAlignmentTests.FleetFunctions` — that test requires `START_SLOTS`, and `ValidateMining` never creates or saves a fleet; it only reads the active drone.

- [ ] **Step 5: Syntax-check and run the drift test**

```bash
node --check ServerCode/ValidateMining.js && echo ok
```

Then run the Unity test command with `-testFilter "MiningCatalogAlignmentTests|DroneCatalogAlignmentTests|ServerCodeEconomyApiTests"`.
Expected: all pass.

- [ ] **Step 6: Verify server behaviour in Node (ad hoc, not committed)**

Write this to the session scratchpad (not the repo) as `mining-harness.js` and run `node mining-harness.js` from `ServerCode/`:

```js
const Module = require("module");
let store, clock;
const tick = () => new Promise(r => setImmediate(r));
let failInventory = false;
const save = { DataApi: class {
  async getItems(p, pl, keys) { await tick(); return { data: { results: keys.filter(k => store[k]).map(k => ({ key: k, value: JSON.parse(JSON.stringify(store[k].value)), writeLock: String(store[k].v) })) } }; }
  async setItem(p, pl, body) { await tick();
    if (body.key === "mineral_inventory" && failInventory) throw new Error("save down");
    const cur = store[body.key];
    if (cur && body.writeLock !== undefined && body.writeLock !== String(cur.v)) { const e = new Error("conflict"); e.response = { status: 409 }; throw e; }
    store[body.key] = { value: body.value, v: (cur?.v ?? 0) + 1 }; return { data: { writeLock: String(store[body.key].v) } }; } } };
const orig = Module._load; Module._load = (r, ...a) => r.includes("cloud-save") ? save : orig(r, ...a);
const realNow = Date.now; Date.now = () => clock;
const fn = require(process.cwd() + "/ValidateMining.js");
const ctx = { projectId: "p", playerId: "pl" }, logger = { info() {}, warn() {}, error() {} };
const call = p => fn({ params: p, context: ctx, logger });
const reset = (extra = {}) => { store = { ...extra }; clock = 1e12; failInventory = false; };
const fleet = (id, yieldLvl = 0) => ({ drone_fleet: { value: { slots: 2, activeDroneId: id, drones: [{ droneId: id, upgrades: { Yield: yieldLvl } }] }, v: 1 } });
const planet = id => ({ current_planet: { value: { planetId: id }, v: 1 } });
const expect = (label, got, want) => console.log((JSON.stringify(got) === JSON.stringify(want) ? "PASS " : "FAIL ") + label, JSON.stringify(got));
(async () => {
  reset(planet("mars"));               expect("wrong planet", (await call({ planetId: "earth", mineralId: "iron", claimedQty: 10 })).reason, "WRONG_PLANET");
  reset();                             expect("null planet accepts claim", (await call({ planetId: "earth", mineralId: "iron", claimedQty: 10 })).granted, 10);
  reset();                             expect("mineral not on planet", (await call({ planetId: "earth", mineralId: "iridium", claimedQty: 10 })).reason, "MINERAL_NOT_ON_PLANET");
  reset();                             expect("tier too low (starter vs T2)", (await call({ planetId: "earth", mineralId: "silicon", claimedQty: 10 })).reason, "TIER_TOO_LOW");
  reset(fleet("extractor"));           expect("T2 drone may mine T2", (await call({ planetId: "earth", mineralId: "silicon", claimedQty: 10 })).granted, 10);
  reset();                             expect("clamp iron scout = ceil(96 × 1) = 96", (await call({ planetId: "earth", mineralId: "iron", claimedQty: 5000 })).granted, 96);
  reset(fleet("titan", 10));           expect("clamp iron titan+10 = ceil(96 × 4.5) = 432", (await call({ planetId: "earth", mineralId: "iron", claimedQty: 5000 })).granted, 432);
  reset(); for (let i = 0; i < 6; i++) await call({ planetId: "earth", mineralId: "iron", claimedQty: 1 });
  expect("7th claim in window", (await call({ planetId: "earth", mineralId: "iron", claimedQty: 1 })).reason, "BUDGET_EXHAUSTED");
  expect("other planet has its own budget", (await call({ planetId: "mars", mineralId: "silicon", claimedQty: 1 })).reason, "TIER_TOO_LOW");
  clock += 4 * 3600000 + 1;            expect("accepted after window", (await call({ planetId: "earth", mineralId: "iron", claimedQty: 1 })).granted, 1);
  reset(); for (let i = 0; i < 5; i++) await call({ planetId: "earth", mineralId: "iron", claimedQty: 1 });
  const race = await Promise.all([call({ planetId: "earth", mineralId: "iron", claimedQty: 1 }), call({ planetId: "earth", mineralId: "iron", claimedQty: 1 })]);
  expect("race for last slot: exactly one wins", race.filter(r => r.granted === 1).length, 1);
  expect("inventory after race", store.mineral_inventory.value.iron, 6);
  reset(); failInventory = true; let threw = false;
  try { await call({ planetId: "earth", mineralId: "iron", claimedQty: 1 }); } catch { threw = true; }
  expect("grant failure throws", threw, true);
  expect("budget released", store.mining_claim_log?.value?.earth ?? [], []);
  expect("missing params", (await call({ planetId: "earth", mineralId: "iron" })).reason, "INVALID_PARAMS");
  Date.now = realNow;
})();
```

Expected: every line starts with `PASS`. (The "other planet has its own budget" case uses mars + silicon with the starter drone, so the expected rejection is the tier check, proving earth's full budget did not block mars.)

---

### Task 3: Client grant contract — `MiningGrantResult`

**Files:**
- Create: `Assets/_Project/Scripts/Mining/MiningGrantResult.cs`
- Modify: `Assets/_Project/Scripts/Mining/IMineralService.cs:21-24`
- Modify: `Assets/_Project/Scripts/Mining/MineralService.cs:49-71`
- Modify: `Assets/_Project/Scripts/Mining/LocalMockMineralService.cs:43-47`
- Modify: `Assets/_Project/Scripts/Mining/MiningController.cs` (the two `GrantMiningAsync` calls, ~lines 97 and 179)
- Modify test doubles: `Assets/_Project/Tests/EditMode/Mining/MiningControllerTests.cs:15-33`, `Assets/_Project/Tests/EditMode/App/MineralSaleHandlerTests.cs:18`, `Assets/_Project/Tests/EditMode/App/ServerFailureFeedbackTests.cs:30`
- Test: `Assets/_Project/Tests/EditMode/Mining/MineralServiceTests.cs`

**Interfaces:**
- Produces: `public class MiningGrantResult { public int Granted; public string MineralId; public string Reason; }`; `IMineralService.GrantMiningAsync(string planetId, string mineralId, int qty) → Task<MiningGrantResult>`. Backend call: `"ValidateMining"` with args `planetId`, `mineralId`, `claimedQty`.

- [ ] **Step 1: Write the failing tests** — add to `MineralServiceTests.cs`

Extend its private `FakeBackendClient` (inside `MineralServiceTests`) so it records calls and can answer `ValidateMining`:

```csharp
        private class FakeBackendClient : IBackendClient
        {
            public SellResult        SellResponse;
            public MiningGrantResult GrantResponse;
            public string                     LastFunction;
            public Dictionary<string, object> LastArgs;

            public Task<T> CallAsync<T>(string function, Dictionary<string, object> args = null)
            {
                LastFunction = function;
                LastArgs     = args;
                if (function == "SellMinerals" && typeof(T) == typeof(SellResult))
                    return Task.FromResult((T)(object)SellResponse);
                if (function == "ValidateMining" && typeof(T) == typeof(MiningGrantResult))
                    return Task.FromResult((T)(object)GrantResponse);
                return Task.FromResult(default(T));
            }
            public Task CallAsync(string function, Dictionary<string, object> args = null) => Task.CompletedTask;
        }
```

New tests:

```csharp
        [Test]
        public async Task GrantMiningAsync_sends_planet_mineral_and_quantity_only()
        {
            var backend = new FakeBackendClient { GrantResponse = new MiningGrantResult { Granted = 12, MineralId = "iron" } };
            var svc = new MineralService(backend, new MineralInventory(), new Wallet());

            await svc.GrantMiningAsync("earth", "iron", 12);

            Assert.AreEqual("ValidateMining", backend.LastFunction);
            CollectionAssert.AreEquivalent(new[] { "planetId", "mineralId", "claimedQty" }, backend.LastArgs.Keys);
            Assert.AreEqual("earth", backend.LastArgs["planetId"]);
            Assert.AreEqual("iron",  backend.LastArgs["mineralId"]);
            Assert.AreEqual(12,      backend.LastArgs["claimedQty"]);
        }

        [Test]
        public async Task GrantMiningAsync_adds_the_granted_amount_to_the_inventory()
        {
            var backend = new FakeBackendClient { GrantResponse = new MiningGrantResult { Granted = 9, MineralId = "iron" } };
            var inv = new MineralInventory();
            var svc = new MineralService(backend, inv, new Wallet());

            var result = await svc.GrantMiningAsync("earth", "iron", 12);

            Assert.AreEqual(9, result.Granted);
            Assert.AreEqual(9, inv.Get("iron"), "the server's clamped amount, not the claimed one");
        }

        [Test]
        public async Task GrantMiningAsync_rejection_returns_the_reason_and_leaves_inventory_unchanged()
        {
            var backend = new FakeBackendClient { GrantResponse = new MiningGrantResult { Granted = 0, MineralId = "iron", Reason = "BUDGET_EXHAUSTED" } };
            var inv = new MineralInventory();
            var svc = new MineralService(backend, inv, new Wallet());

            var result = await svc.GrantMiningAsync("earth", "iron", 12);

            Assert.AreEqual(0, result.Granted);
            Assert.AreEqual("BUDGET_EXHAUSTED", result.Reason);
            Assert.AreEqual(0, inv.Get("iron"));
        }

        [Test]
        public async Task GrantMiningAsync_null_response_is_an_empty_response_rejection()
        {
            var svc = new MineralService(new FakeBackendClient(), new MineralInventory(), new Wallet());

            var result = await svc.GrantMiningAsync("earth", "iron", 12);

            Assert.AreEqual(0, result.Granted);
            Assert.AreEqual("Empty response", result.Reason);
        }
```

- [ ] **Step 2: Run to verify they fail**

Run with `-testFilter "MineralServiceTests"`.
Expected: compile error `The type or namespace name 'MiningGrantResult' could not be found`.

- [ ] **Step 3: Implement**

`Assets/_Project/Scripts/Mining/MiningGrantResult.cs`:

```csharp
namespace SocialUniverse.Mining
{
    // Public DTO so IBackendClient.CallAsync<MiningGrantResult> can type the response (the
    // public-DTO testability pattern, like SellResult). Shape MUST MATCH ServerCode/ValidateMining.js:
    // { granted, mineralId } on success, { granted: 0, mineralId, reason } on rejection.
    public class MiningGrantResult
    {
        public int    Granted;
        public string MineralId;
        public string Reason;
    }
}
```

`IMineralService.cs` — replace the grant member and its comment:

```csharp
        // Mining payout: round-trips ValidateMining, which checks planet, mineral, drone tier and
        // the per-planet claim budget, and clamps qty. Applies Granted to MineralInventory.
        // Granted == 0 with a Reason means the server rejected the claim.
        // (Cloud Save hydration is App-layer, not here — see Ruling R4.)
        Task<MiningGrantResult> GrantMiningAsync(string planetId, string mineralId, int qty);
```

`MineralService.cs` — replace `GrantMiningAsync` and delete the private `GrantResponse` class:

```csharp
        public async Task<MiningGrantResult> GrantMiningAsync(string planetId, string mineralId, int qty)
        {
            if (string.IsNullOrEmpty(mineralId) || qty <= 0) return new MiningGrantResult { MineralId = mineralId };

            var res = await _backend.CallAsync<MiningGrantResult>("ValidateMining", new Dictionary<string, object>
            {
                { "planetId",   planetId },
                { "mineralId",  mineralId },
                { "claimedQty", qty }
            }) ?? new MiningGrantResult { MineralId = mineralId, Reason = "Empty response" };

            if (res.Granted > 0) _inventory.Add(mineralId, res.Granted);
            return res;
        }
```

`LocalMockMineralService.cs`:

```csharp
        public Task<MiningGrantResult> GrantMiningAsync(string planetId, string mineralId, int qty)
        {
            if (!string.IsNullOrEmpty(mineralId) && qty > 0) _inventory.Add(mineralId, qty);
            return Task.FromResult(new MiningGrantResult { Granted = qty, MineralId = mineralId });
        }
```

`MiningController.cs` — minimal compile fix for both call sites (Task 4 adds the rejection path). Idle claim:

```csharp
                    var result  = await _minerals.GrantMiningAsync(_planet.PlanetId, mineral.MineralId, quantity);
                    int granted = result.Granted;
```

Active claim (`CompleteActiveMiningAsync`):

```csharp
                    int granted = (await _minerals.GrantMiningAsync(_planet.PlanetId, mineral.MineralId, quantity)).Granted;
```

Test doubles — `MiningControllerTests.cs` `CapturingMineralService`:

```csharp
    public class CapturingMineralService : IMineralService
    {
        public string LastPlanetId;
        public string LastMineralId;
        public int    LastQty;
        public bool   Throw;
        public string RejectReason; // when set, the grant is rejected with this reason

        public Task<SellResult> SellAsync(string mineralId, int qty) => Task.FromResult(new SellResult { Success = true });
        public Task<SellResult> SellAllAsync() => Task.FromResult(new SellResult { Success = true });

        public Task<MiningGrantResult> GrantMiningAsync(string planetId, string mineralId, int qty)
        {
            if (Throw) throw new System.InvalidOperationException("simulated");
            LastPlanetId  = planetId;
            LastMineralId = mineralId;
            LastQty       = qty;
            return Task.FromResult(RejectReason != null
                ? new MiningGrantResult { Granted = 0, MineralId = mineralId, Reason = RejectReason }
                : new MiningGrantResult { Granted = qty, MineralId = mineralId });
        }
    }
```

`MineralSaleHandlerTests.cs:18` and `ServerFailureFeedbackTests.cs:30`:

```csharp
            public Task<MiningGrantResult> GrantMiningAsync(string planetId, string mineralId, int qty) => Task.FromResult(new MiningGrantResult());
```

- [ ] **Step 4: Run tests to verify they pass**

Run with `-testFilter "MineralServiceTests|MiningControllerTests|MineralSaleHandlerTests|ServerFailureFeedbackTests"`.
Expected: all pass.

---

### Task 4: `MiningController` publishes rejected claims; drop `UnitsPerSec`

**Files:**
- Create: `Assets/_Project/Scripts/Mining/MiningClaimRejectedEvent.cs`
- Modify: `Assets/_Project/Scripts/Mining/MiningController.cs` (`ClaimIdleSessionAsync` ~lines 91-106, `CompleteActiveMiningAsync` ~lines 175-190)
- Modify: `Assets/_Project/Scripts/Mining/MiningRewardCalculator.cs`
- Modify: `Assets/_Project/Tests/EditMode/Mining/MiningRewardCalculatorTests.cs` (the three `UnitsPerSec` tests, ~lines 66-101)
- Test: `Assets/_Project/Tests/EditMode/Mining/MiningControllerTests.cs`

**Interfaces:**
- Consumes: `MiningGrantResult`, `CapturingMineralService.RejectReason` / `LastPlanetId` (Task 3).
- Produces: `public class MiningClaimRejectedEvent { public string MineralId; public string Reason; }`. `MiningReward` constructor becomes `(int mineralQuantity, float idleDurationSeconds, int activeTapsRequired, float activeSessionDurationSeconds)`.

- [ ] **Step 1: Write the failing tests** — add to `MiningControllerTests`

```csharp
        [Test]
        public async Task ClaimIdleSessionAsync_sends_the_scene_planet_id()
        {
            var asteroid = MakeAndRegisterAsteroid("slot_0", remainingYield: 20);
            Assert.IsTrue(_mining.BeginIdleMining(asteroid));
            await Task.Delay(100);
            _mining.CurrentIdleSession.Tick(0f);

            await _mining.ClaimIdleSessionAsync(asteroid);

            Assert.AreEqual("test_planet", _minerals.LastPlanetId);
        }

        [Test]
        public async Task ClaimIdleSessionAsync_rejection_publishes_rejected_event_not_claim_completed_and_still_respawns()
        {
            var rejecting = new CapturingMineralService { RejectReason = "BUDGET_EXHAUSTED" };
            var mining = new MiningController(rejecting, _rewardCalc, _spawner, _config, _planet, _handoff, new FakeAudioManager(), _fleet);

            var asteroid = MakeAndRegisterAsteroid("slot_0", remainingYield: 20);
            Assert.IsTrue(mining.BeginIdleMining(asteroid));
            await Task.Delay(100);
            mining.CurrentIdleSession.Tick(0f);

            IdleClaimCompletedEvent completed = null;
            MiningClaimRejectedEvent rejected = null;
            EventBus.Subscribe<IdleClaimCompletedEvent>(e => completed = e);
            EventBus.Subscribe<MiningClaimRejectedEvent>(e => rejected = e);

            await mining.ClaimIdleSessionAsync(asteroid);

            Assert.IsNull(completed, "no reward modal for a rejected claim");
            Assert.IsNotNull(rejected);
            Assert.AreEqual("iron", rejected.MineralId);
            Assert.AreEqual("BUDGET_EXHAUSTED", rejected.Reason);
            Assert.IsTrue(_spawner.NextRespawnUtc.HasValue, "the asteroid is consumed and respawns as usual");

            EventBus.Clear();
        }

        [Test]
        public async Task Active_mining_rejection_publishes_rejected_event()
        {
            var rejecting = new CapturingMineralService { RejectReason = "TIER_TOO_LOW" };
            var mining = new MiningController(rejecting, _rewardCalc, _spawner, _config, _planet, _handoff, new FakeAudioManager(), _fleet);

            var asteroid = MakeAndRegisterAsteroid("slot_0", remainingYield: 10);
            Assert.IsTrue(mining.BeginActiveMining(asteroid));
            _handoff.SetResult(succeeded: true);

            MiningClaimRejectedEvent rejected = null;
            EventBus.Subscribe<MiningClaimRejectedEvent>(e => rejected = e);

            mining.Initialize();
            await Task.Yield();

            Assert.IsNotNull(rejected);
            Assert.AreEqual("TIER_TOO_LOW", rejected.Reason);

            EventBus.Clear();
        }
```

- [ ] **Step 2: Run to verify they fail**

Run with `-testFilter "MiningControllerTests"`.
Expected: compile error `The type or namespace name 'MiningClaimRejectedEvent' could not be found`.

- [ ] **Step 3: Implement**

`Assets/_Project/Scripts/Mining/MiningClaimRejectedEvent.cs`:

```csharp
namespace SocialUniverse.Mining
{
    // Published when the server rejects a mining claim (ValidateMining returned granted: 0 with a
    // reason such as BUDGET_EXHAUSTED or TIER_TOO_LOW). The asteroid is still consumed.
    // App's MiningClaimFeedbackHandler turns it into a player-facing toast.
    public class MiningClaimRejectedEvent
    {
        public string MineralId;
        public string Reason;
    }
}
```

`MiningController.ClaimIdleSessionAsync` — replace the body of the `try` block:

```csharp
                try
                {
                    var result = await _minerals.GrantMiningAsync(_planet.PlanetId, mineral.MineralId, quantity);
                    if (result.Granted > 0)
                    {
                        _audio.PlaySfx(SfxId.CoinsReward);
                        EventBus.Publish(new IdleClaimCompletedEvent { MineralId = mineral.MineralId, Quantity = result.Granted });
                        SULog.Info($"Idle session claimed: +{result.Granted} {mineral.MineralId}", SULog.Channel.Mining);
                    }
                    else
                    {
                        SULog.Warn($"Idle claim rejected for {mineral.MineralId}: {result.Reason}", SULog.Channel.Mining);
                        EventBus.Publish(new MiningClaimRejectedEvent { MineralId = mineral.MineralId, Reason = result.Reason });
                    }
                }
```

`MiningController.CompleteActiveMiningAsync` — replace the body of the `try` block:

```csharp
                try
                {
                    var result = await _minerals.GrantMiningAsync(_planet.PlanetId, mineral.MineralId, quantity);
                    if (result.Granted > 0)
                    {
                        SULog.Info($"Active mining success: +{result.Granted} {mineral.MineralId}", SULog.Channel.Mining);
                    }
                    else
                    {
                        SULog.Warn($"Active mining claim rejected for {mineral.MineralId}: {result.Reason}", SULog.Channel.Mining);
                        EventBus.Publish(new MiningClaimRejectedEvent { MineralId = mineral.MineralId, Reason = result.Reason });
                    }
                }
```

`MiningRewardCalculator.cs` — remove `UnitsPerSec`:
- delete the field `public readonly float UnitsPerSec;`, the constructor parameter `float unitsPerSec` and the assignment `UnitsPerSec = unitsPerSec;`;
- in `Compute`, delete the comment "Per-claim rate so durationSec * unitsPerSec == quantity …" and the line `float unitsPerSec = …;`;
- change the return to `return new MiningReward(quantity, duration, taps, activeSeconds);`.

`MiningRewardCalculatorTests.cs` — the three tests asserted the removed rate. Rewrite them to keep their duration/quantity assertions:

```csharp
        [Test]
        public void Mid_range_yield_is_not_clamped()
        {
            var asteroid = MakeAsteroid(100); // duration = 100*3 = 300s, within [30,1800]

            var reward = _calc.Compute(asteroid, 1f);

            Assert.AreEqual(100, reward.MineralQuantity);          // 100 remaining yield * 1.0 mult
            Assert.AreEqual(300f, reward.IdleDurationSeconds, 0.001f);
        }

        [Test]
        public void Tiny_yield_clamps_duration_to_minimum()
        {
            var asteroid = MakeAsteroid(1); // raw duration = 3s, clamped up to 30s

            var reward = _calc.Compute(asteroid, 1f);

            Assert.AreEqual(30f, reward.IdleDurationSeconds, 0.001f);
        }

        [Test]
        public void Huge_yield_clamps_duration_to_maximum()
        {
            var asteroid = MakeAsteroid(10000); // raw duration = 30000s, clamped down to 1800s

            var reward = _calc.Compute(asteroid, 1f);

            Assert.AreEqual(1800f, reward.IdleDurationSeconds, 0.001f);
            Assert.AreEqual(10000, reward.MineralQuantity); // 10000 * 1.0
        }
```

Then `grep -rn "UnitsPerSec" Assets/_Project` must return nothing.

- [ ] **Step 4: Run tests to verify they pass**

Run with `-testFilter "MiningControllerTests|MiningRewardCalculatorTests"`.
Expected: all pass.

---

### Task 5: Rejection toast — `MiningClaimFeedbackHandler`

**Files:**
- Create: `Assets/_Project/Scripts/App/MiningClaimFeedbackHandler.cs`
- Modify: `Assets/_Project/Scripts/App/ServerFailureMessages.cs`
- Modify: `Assets/_Project/Scripts/App/PlanetSceneScope.cs` (after `builder.RegisterEntryPoint<MineralSaleHandler>();`)
- Test: `Assets/_Project/Tests/EditMode/App/ServerFailureFeedbackTests.cs`

**Interfaces:**
- Consumes: `MiningClaimRejectedEvent` (Task 4); `ServerActionFailedEvent(string message)` and `ServerFailureMessages.For(string action, string reason)` (existing).

- [ ] **Step 1: Write the failing tests** — add to `ServerFailureFeedbackTests`

```csharp
        [Test]
        public void A_rejected_mining_claim_publishes_a_readable_message()
        {
            var handler = new MiningClaimFeedbackHandler();
            handler.Start();

            EventBus.Publish(new MiningClaimRejectedEvent { MineralId = "iron", Reason = "BUDGET_EXHAUSTED" });

            handler.Dispose();
            CollectionAssert.AreEqual(new[] { "No more asteroids here for now. Try again later." }, _messages);
        }

        [TestCase("BUDGET_EXHAUSTED",      "No more asteroids here for now. Try again later.")]
        [TestCase("TIER_TOO_LOW",          "Requires a higher-tier drone")]
        [TestCase("WRONG_PLANET",          "Mining isn't available here right now")]
        [TestCase("MINERAL_NOT_ON_PLANET", "Mining isn't available here right now")]
        public void Mining_reason_codes_map_to_player_facing_text(string reason, string expected) =>
            Assert.AreEqual(expected, ServerFailureMessages.For("Mining", reason));
```

- [ ] **Step 2: Run to verify they fail**

Run with `-testFilter "ServerFailureFeedbackTests"`.
Expected: compile error `The type or namespace name 'MiningClaimFeedbackHandler' could not be found`.

- [ ] **Step 3: Implement**

`Assets/_Project/Scripts/App/MiningClaimFeedbackHandler.cs`:

```csharp
using System;
using VContainer.Unity;
using SocialUniverse.Core;
using SocialUniverse.Mining;

namespace SocialUniverse.App
{
    // Turns a server-rejected mining claim into the failure toast (ServerActionFailedEvent), so
    // Mining stays free of UI text. MiningController already logs the rejection.
    public class MiningClaimFeedbackHandler : IStartable, IDisposable
    {
        public void Start()   => EventBus.Subscribe<MiningClaimRejectedEvent>(OnRejected);
        public void Dispose() => EventBus.Unsubscribe<MiningClaimRejectedEvent>(OnRejected);

        private static void OnRejected(MiningClaimRejectedEvent e) =>
            EventBus.Publish(new ServerActionFailedEvent(ServerFailureMessages.For("Mining", e.Reason)));
    }
}
```

`ServerFailureMessages.cs` — add before the `"Network error" or "Empty response"` arm:

```csharp
            "BUDGET_EXHAUSTED"                   => "No more asteroids here for now. Try again later.",
            "TIER_TOO_LOW"                       => "Requires a higher-tier drone",
            "WRONG_PLANET" or "MINERAL_NOT_ON_PLANET" => "Mining isn't available here right now",
```

`PlanetSceneScope.cs` — after `builder.RegisterEntryPoint<MineralSaleHandler>();`:

```csharp
            builder.RegisterEntryPoint<MiningClaimFeedbackHandler>();
```

- [ ] **Step 4: Run tests to verify they pass**

Run with `-testFilter "ServerFailureFeedbackTests"`.
Expected: all pass.

---

### Task 6: PlayMode fake, full verification, docs

**Files:**
- Modify: `Assets/_Project/Tests/PlayMode/PlanetSceneFlowTests.cs` (the `ValidateMining` responder in `SetUp`; the assertions in `Idle_mining_a_claimed_asteroid_grants_its_mineral`)
- Modify: `PROGRESS.md` (Known Issue #16 row; M6 server table `ValidateMining` row)

**Interfaces:**
- Consumes: the `ValidateMining` request shape (Task 2/3); `MiningGrantResult` field names (Task 3) — `FakeBackendClient` deserializes with `JsonUtility`, which is case-sensitive, so the JSON must use `Granted` / `MineralId`.

- [ ] **Step 1: Update the PlayMode responder and assertion**

In `SetUp`, replace the `ValidateMining` responder:

```csharp
            _backend.On("ValidateMining", args =>
                $"{{\"Granted\":{args["claimedQty"]},\"MineralId\":\"{args["mineralId"]}\"}}");
```

In `Idle_mining_a_claimed_asteroid_grants_its_mineral`, after `Assert.AreEqual(mineralId, call.Args["mineralId"]);` add:

```csharp
            Assert.AreEqual(_planet.PlanetId, call.Args["planetId"], "Claim should name the planet it was mined on");
            Assert.IsFalse(call.Args.ContainsKey("unitsPerSec"), "The client no longer sends a mining rate");
```

- [ ] **Step 2: Run the full EditMode and PlayMode suites**

Run the Unity test command with no `-testFilter`, once with `-testPlatform EditMode` and once with `-testPlatform PlayMode`.
Expected: EditMode all pass (previous total 365, minus 1 deleted cap test, plus the new tests); PlayMode 2/2.

- [ ] **Step 3: Update `PROGRESS.md`**

In the Known Issue #16 row, replace the sentence beginning "`ValidateMining` still trusts client `sessionDurationSec` + `unitsPerSec` …" with:

"`ValidateMining` now checks planet (`current_planet`), mineral-on-planet, drone tier and a claim budget of 6 per planet per rolling 4h (`mining_claim_log`), and clamps the grant to the best legit roll — see `docs/superpowers/specs/2026-09-29-mining-claim-budget-design.md`. Still open: a reinstall resets the client's local respawn timers but not the server budget (rare lost claim)"

and keep the remaining "Still open" items (GrantCoins/GrantStardust undeployed; player-writable access class — note the tier/multiplier checks depend on it).

In the M6 "Server (`ServerCode/`)" table, change the `ValidateMining (rewritten)` row's note to: "Server-side planet/mineral/tier checks + per-planet claim budget (2026-09-29). New request shape `{ planetId, mineralId, claimedQty }` — client and server must deploy together (Known Issue #13)".

- [ ] **Step 4: Final checks**

```bash
cd /c/Users/chris/UnityProjects/social-universe
for f in ServerCode/*.js; do node --check "$f" || echo "SYNTAX FAIL $f"; done
grep -rn "UnitsPerSec\|sessionDurationSec" Assets/_Project/Scripts ServerCode/ValidateMining.js
git -c safe.directory='*' status -s
```

Expected: no syntax failures; the grep prints nothing; status lists only the files in this plan's File Map plus their `.meta` files (and the earlier uncommitted work from this session).
