# Active Mining Minigame Scene Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the 2D UI-overlay active-mining minigame with a real minigame presented in its own additively-loaded scene — a spawned asteroid model, 3D world-anchored target points, and one overall countdown scaled by the asteroid's size.

**Architecture:** A new `ActiveMining.unity` scene loads additively on top of the (still-running) Planet scene whenever a session starts, via a new `ActiveMiningSceneController` reacting to `MiningController.OnActiveSessionChanged`. The new scene's `ActiveMiningSceneScope` is a child `LifetimeScope` of `PlanetSceneScope`, so `MiningController` and its session state are never duplicated or destroyed — the new scene is a pure presentation layer. `ActiveMiningSession`'s internal timer changes from a per-tap window to one overall countdown derived from the asteroid's existing yield-based tap count.

**Tech Stack:** Unity 6 (URP), VContainer DI, NUnit EditMode tests, C#.

## Global Constraints

- Server-authoritative economy: this plan makes no changes to reward totals, grant calls, or `IEconomyService` — only to how the active-mining minigame is presented and timed.
- Namespaces mirror folders exactly (`Mining/` → `SocialUniverse.Mining`, `App/` → `SocialUniverse.App`, `UI/` → `SocialUniverse.UI`, `Core/` → `SocialUniverse.Core`).
- Backend access stays behind `I*Service`; this plan doesn't touch that boundary.
- Both mining modes must keep paying identical rewards (unchanged from the prior mining redesign) — verified by not touching `MiningRewardCalculator.TotalCoins`/`IdleDurationSeconds`/`CoinsPerSec`.
- `Application.isPlaying` guard required around any `Destroy()` call that might run during an EditMode test (`Destroy()` throws outside Play Mode; use `DestroyImmediate()` instead) — this project has hit this bug twice already (`AsteroidSpawner`).
- Unity headless test command (exact, no `-nographics`, no `-quit` combined with `-runTests`):
  ```
  "C:\Program Files\Unity\Hub\Editor\6000.3.12f1\Editor\Unity.exe" -batchmode -runTests -projectPath . -testResults <path> -testPlatform EditMode -logFile <path>
  ```

---

### Task 1: `ActiveMiningSession` — overall countdown instead of a per-point window

**Files:**
- Modify: `Assets/_Project/Scripts/Mining/ActiveMiningSession.cs`
- Test: `Assets/_Project/Tests/EditMode/Mining/ActiveMiningSessionTests.cs`

**Interfaces:**
- Consumes: nothing new (no other production types referenced).
- Produces: `ActiveMiningSession(Asteroid asteroid, int tapsRequired, int maxErrors, float sessionDurationSeconds)`; `float SessionDurationSeconds { get; }`; `float TimeRemainingSeconds { get; }`; `void Tick(float deltaTime)`; `void RegisterHit()`; `void RegisterMiss()`. `ActiveMiningMinigame` (unchanged in this task) still compiles against this because the constructor keeps the same `(Asteroid, int, int, float)` shape — only the last parameter's *meaning* changes, so `ActiveMiningMinigame.Begin()` doesn't need to change until Task 2.

- [ ] **Step 1: Write the failing tests (replace the whole file)**

Replace `Assets/_Project/Tests/EditMode/Mining/ActiveMiningSessionTests.cs` with:

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
            var session = new ActiveMiningSession(MakeAsteroid(), tapsRequired: 3, maxErrors: 3, sessionDurationSeconds: 10f);

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
            var session = new ActiveMiningSession(MakeAsteroid(), tapsRequired: 10, maxErrors: 3, sessionDurationSeconds: 10f);

            session.RegisterMiss();
            session.RegisterMiss();
            Assert.AreEqual(ActiveMiningStage.InProgress, session.Stage);
            session.RegisterMiss();

            Assert.AreEqual(ActiveMiningStage.Failed, session.Stage);
            Assert.AreEqual(3, session.ErrorCount);
        }

        [Test]
        public void Running_out_of_time_fails_the_session_even_with_no_misses()
        {
            var session = new ActiveMiningSession(MakeAsteroid(), tapsRequired: 10, maxErrors: 3, sessionDurationSeconds: 1f);

            session.Tick(0.5f);
            Assert.AreEqual(ActiveMiningStage.InProgress, session.Stage);
            Assert.AreEqual(0, session.ErrorCount, "time running out is not counted as a miss");

            session.Tick(0.6f); // total 1.1s > 1s session duration

            Assert.AreEqual(ActiveMiningStage.Failed, session.Stage);
            Assert.AreEqual(0, session.ErrorCount);
        }

        [Test]
        public void Hits_do_not_extend_or_reset_the_overall_timer()
        {
            var session = new ActiveMiningSession(MakeAsteroid(), tapsRequired: 10, maxErrors: 3, sessionDurationSeconds: 1f);

            session.Tick(0.9f);
            session.RegisterHit();
            session.Tick(0.2f); // total elapsed 1.1s -> the overall clock keeps counting regardless of hits

            Assert.AreEqual(ActiveMiningStage.Failed, session.Stage);
        }

        [Test]
        public void Terminal_stages_ignore_further_hits_misses_and_ticks()
        {
            var session = new ActiveMiningSession(MakeAsteroid(), tapsRequired: 1, maxErrors: 3, sessionDurationSeconds: 10f);
            session.RegisterHit(); // -> Success

            session.RegisterMiss();
            session.Tick(1000f); // would fail on time if terminal stages didn't ignore Tick

            Assert.AreEqual(ActiveMiningStage.Success, session.Stage);
            Assert.AreEqual(0, session.ErrorCount);
        }

        [Test]
        public void OnStageChanged_fires_on_terminal_transition_only()
        {
            var session = new ActiveMiningSession(MakeAsteroid(), tapsRequired: 2, maxErrors: 3, sessionDurationSeconds: 10f);
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

- [ ] **Step 2: Run tests to verify they fail**

Run the headless EditMode command above. Expected: compile error or failures in `ActiveMiningSessionTests` — `ActiveMiningSession`'s constructor still names its last parameter `tapWindowSeconds` (named-argument mismatch isn't used here since the tests pass positionally... note the test file above uses `sessionDurationSeconds:` as a named argument, so this WILL fail to compile until Step 3 renames the parameter). Expected: CS1739 "no argument given that corresponds to required parameter" or similar.

- [ ] **Step 3: Rewrite `ActiveMiningSession.cs`**

Replace `Assets/_Project/Scripts/Mining/ActiveMiningSession.cs` with:

```csharp
using System;
using UnityEngine;

namespace SocialUniverse.Mining
{
    public enum ActiveMiningStage { InProgress, Success, Failed }

    // Player-vs-asteroid tap minigame: the whole session runs under one overall countdown
    // (SessionDurationSeconds, scaled by the asteroid's size via MiningRewardCalculator).
    // Running out of time fails the session directly. A "miss" only happens when the player
    // taps the wrong spot (ActiveMiningMinigameView.RegisterTap(false)) — there is no per-point
    // timeout. MaxErrors misses fails the asteroid; TapsRequired hits succeeds it.
    public class ActiveMiningSession
    {
        public Asteroid Asteroid               { get; }
        public int      TapsRequired           { get; }
        public int      SuccessfulTaps          { get; private set; }
        public int      MaxErrors               { get; }
        public int      ErrorCount              { get; private set; }
        public float    SessionDurationSeconds  { get; }
        public float    TimeRemainingSeconds    { get; private set; }

        public ActiveMiningStage Stage { get; private set; } = ActiveMiningStage.InProgress;

        public event Action<ActiveMiningStage> OnStageChanged;

        public ActiveMiningSession(Asteroid asteroid, int tapsRequired, int maxErrors, float sessionDurationSeconds)
        {
            Asteroid               = asteroid;
            TapsRequired           = Mathf.Max(1, tapsRequired);
            MaxErrors              = Mathf.Max(1, maxErrors);
            SessionDurationSeconds = Mathf.Max(0.1f, sessionDurationSeconds);
            TimeRemainingSeconds   = SessionDurationSeconds;
        }

        // Call every frame while Stage == InProgress; running out of time fails the session.
        public void Tick(float deltaTime)
        {
            if (Stage != ActiveMiningStage.InProgress) return;

            TimeRemainingSeconds -= deltaTime;
            if (TimeRemainingSeconds <= 0f)
                SetStage(ActiveMiningStage.Failed);
        }

        // The live target point was tapped.
        public void RegisterHit()
        {
            if (Stage != ActiveMiningStage.InProgress) return;

            SuccessfulTaps++;

            if (SuccessfulTaps >= TapsRequired)
                SetStage(ActiveMiningStage.Success);
        }

        // The player tapped the wrong spot.
        public void RegisterMiss()
        {
            if (Stage != ActiveMiningStage.InProgress) return;

            ErrorCount++;

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

- [ ] **Step 4: Run tests to verify they pass**

Run the headless EditMode command. Expected: all 6 tests in `ActiveMiningSessionTests` pass, 0 failures.

- [ ] **Step 5: Commit**

```bash
git add Assets/_Project/Scripts/Mining/ActiveMiningSession.cs Assets/_Project/Tests/EditMode/Mining/ActiveMiningSessionTests.cs
git commit -m "mining: ActiveMiningSession runs under one overall countdown, not a per-point window"
```

---

### Task 2: `MiningRewardCalculator` — derive the active-session countdown from existing yield/taps

**Files:**
- Modify: `Assets/_Project/Scripts/Config/EconomyConfig.cs`
- Modify: `Assets/_Project/Scripts/Mining/MiningRewardCalculator.cs`
- Modify: `Assets/_Project/Scripts/Mining/ActiveMiningMinigame.cs`
- Test: `Assets/_Project/Tests/EditMode/Mining/MiningRewardCalculatorTests.cs`
- Test: `Assets/_Project/Tests/EditMode/Mining/ActiveMiningMinigameTests.cs`
- Test: `Assets/_Project/Tests/EditMode/Mining/MiningControllerTests.cs`

**Interfaces:**
- Consumes: `ActiveMiningSession(Asteroid, int, int, float sessionDurationSeconds)` from Task 1.
- Produces: `EconomyConfig.ActiveSecondsPerTap`, `.MinActiveSessionSeconds`, `.MaxActiveSessionSeconds` (replaces `.ActiveTapWindowSeconds`, which is removed); `MiningReward.ActiveSessionDurationSeconds` (new field on the existing struct, alongside the unchanged `TotalCoins`/`IdleDurationSeconds`/`ActiveTapsRequired`/`CoinsPerSec`).

- [ ] **Step 1: Write the failing test — extend `MiningRewardCalculatorTests.cs`**

In `Assets/_Project/Tests/EditMode/Mining/MiningRewardCalculatorTests.cs`, update `SetUp` to add the three new config fields, and add a new test. The full updated file:

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
            SetField(_config, "_activeSecondsPerTap", 3f);
            SetField(_config, "_minActiveSessionSeconds", 20f);
            SetField(_config, "_maxActiveSessionSeconds", 45f);

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

        [Test]
        public void Active_session_duration_scales_with_taps_and_clamps_at_bounds()
        {
            // taps=5 (clamped up from ceil(1/8)=1) -> raw 5*3=15s, clamped up to min 20s
            Assert.AreEqual(20f, _calc.Compute(MakeAsteroid(1)).ActiveSessionDurationSeconds, 0.001f);
            // taps=13 -> raw 13*3=39s, within [20,45]
            Assert.AreEqual(39f, _calc.Compute(MakeAsteroid(100)).ActiveSessionDurationSeconds, 0.001f);
            // taps=20 (clamped down from a huge yield) -> raw 20*3=60s, clamped down to max 45s
            Assert.AreEqual(45f, _calc.Compute(MakeAsteroid(10000)).ActiveSessionDurationSeconds, 0.001f);
        }
    }
}
```

- [ ] **Step 2: Run tests to verify the new test fails**

Run the headless EditMode command. Expected: compile error — `MiningReward` has no `ActiveSessionDurationSeconds` member, and `EconomyConfig` has no `_activeSecondsPerTap`/`_minActiveSessionSeconds`/`_maxActiveSessionSeconds` fields yet.

- [ ] **Step 3: Update `EconomyConfig.cs`**

In `Assets/_Project/Scripts/Config/EconomyConfig.cs`, replace the `"Mining — Active"` section:

```csharp
        [Header("Mining — Active")]
        [SerializeField] private float _activeYieldPerTap       = 8f;    // how much RemainingYield one successful tap represents
        [SerializeField] private int   _minActiveTaps           = 5;     // clamp: smallest asteroids still take at least this many taps
        [SerializeField] private int   _maxActiveTaps            = 20;    // clamp: largest asteroids cap out at this many taps
        [SerializeField] private float _activeSecondsPerTap     = 3f;    // seconds contributed per required tap toward the overall session countdown
        [SerializeField] private float _minActiveSessionSeconds = 12f;   // clamp: smallest asteroids still get at least this long
        [SerializeField] private float _maxActiveSessionSeconds = 60f;   // clamp: largest asteroids cap out at this long
        [SerializeField] private int   _activeMaxErrors         = 3;     // wrong taps before the asteroid is lost
```

(this replaces the old five lines including `_activeTapWindowSeconds`.)

And replace the corresponding public properties:

```csharp
        public float ActiveYieldPerTap        => _activeYieldPerTap;
        public int   MinActiveTaps            => _minActiveTaps;
        public int   MaxActiveTaps            => _maxActiveTaps;
        public float ActiveSecondsPerTap      => _activeSecondsPerTap;
        public float MinActiveSessionSeconds  => _minActiveSessionSeconds;
        public float MaxActiveSessionSeconds  => _maxActiveSessionSeconds;
        public int   ActiveMaxErrors          => _activeMaxErrors;
```

(this replaces the old `ActiveYieldPerTap`/`MinActiveTaps`/`MaxActiveTaps`/`ActiveTapWindowSeconds`/`ActiveMaxErrors` property block — `ActiveMaxErrors` keeps its position, just re-list it alongside the others above.)

Note: `Assets/_Project/ScriptableObjects/EconomyConfig.asset` does not currently serialize `_activeTapWindowSeconds` at all (it only has stale, already-orphaned keys from an older field layout that no longer match any field in this class) — this rename does not lose any designer-tuned value; the new fields simply use their C# defaults, same as today.

- [ ] **Step 4: Update `MiningRewardCalculator.cs`**

Replace `Assets/_Project/Scripts/Mining/MiningRewardCalculator.cs` with:

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
        public readonly float ActiveSessionDurationSeconds;
        public readonly float CoinsPerSec;

        public MiningReward(int totalCoins, float idleDurationSeconds, int activeTapsRequired,
            float activeSessionDurationSeconds, float coinsPerSec)
        {
            TotalCoins                   = totalCoins;
            IdleDurationSeconds          = idleDurationSeconds;
            ActiveTapsRequired           = activeTapsRequired;
            ActiveSessionDurationSeconds = activeSessionDurationSeconds;
            CoinsPerSec                  = coinsPerSec;
        }
    }

    // Single source of truth for idle-mining duration, active-mining tap count, active-mining
    // session countdown, and total coin payout for a given asteroid — all three pacing values
    // derive from the same RemainingYield so both mining modes pay out identical totals (see
    // MiningRewardCalculatorTests) and the active-mining countdown scales with the asteroid's
    // effective size without needing a separate "size" field anywhere.
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

            float rawActiveSeconds = taps * _config.ActiveSecondsPerTap;
            float activeSeconds    = Mathf.Clamp(rawActiveSeconds, _config.MinActiveSessionSeconds, _config.MaxActiveSessionSeconds);

            // Computed per-claim from this asteroid's actual totalCoins/duration (not a fixed
            // per-type constant) so sessionDurationSec * coinsPerSec always equals totalCoins
            // exactly, even when duration was clamped — see EconomyService.GrantMiningRewardAsync.
            float coinsPerSec = duration > 0f ? totalCoins / duration : 0f;

            return new MiningReward(totalCoins, duration, taps, activeSeconds, coinsPerSec);
        }
    }
}
```

- [ ] **Step 5: Update `ActiveMiningMinigame.cs`'s call site**

In `Assets/_Project/Scripts/Mining/ActiveMiningMinigame.cs`, in `Begin()`, change:

```csharp
            var reward = _rewardCalc.Compute(asteroid);
            CurrentSession = new ActiveMiningSession(asteroid, reward.ActiveTapsRequired,
                _config.ActiveMaxErrors, _config.ActiveTapWindowSeconds);
```

to:

```csharp
            var reward = _rewardCalc.Compute(asteroid);
            CurrentSession = new ActiveMiningSession(asteroid, reward.ActiveTapsRequired,
                _config.ActiveMaxErrors, reward.ActiveSessionDurationSeconds);
```

- [ ] **Step 6: Run the new/updated `MiningRewardCalculatorTests` to verify they pass**

Run the headless EditMode command. Expected: all 5 tests in `MiningRewardCalculatorTests` pass.

- [ ] **Step 7: Fix the two other test files that reference the renamed config field**

These two files reflect on `_activeTapWindowSeconds` by name in their `SetUp`; since that field no longer exists, leaving them as-is would throw `NullReferenceException` at test run time (`GetField` returns `null` for a missing field, then `.SetValue` on `null` throws). Neither file's assertions depend on the exact session-duration value, so:

In `Assets/_Project/Tests/EditMode/Mining/MiningControllerTests.cs`, in `SetUp`, delete this line entirely (no replacement needed — the new fields' C# defaults are fine since no test in this file asserts on session duration or calls `Tick`):

```csharp
            SetField(_config, "_activeTapWindowSeconds", 5f);
```

In `Assets/_Project/Tests/EditMode/Mining/ActiveMiningMinigameTests.cs`, in `SetUp`, replace:

```csharp
            SetField(_config, "_activeTapWindowSeconds", 1f);
```

with:

```csharp
            SetField(_config, "_activeSecondsPerTap", 2f);
            SetField(_config, "_minActiveSessionSeconds", 0.1f);
            SetField(_config, "_maxActiveSessionSeconds", 999f);
```

Then, in the same file, extend `Begin_creates_a_session_sized_from_the_reward_calculator` to also assert the new duration (taps=1 here since `_minActiveTaps=1` in this file's `SetUp`, so duration = 1 tap × 2s = 2s):

```csharp
        [Test]
        public void Begin_creates_a_session_sized_from_the_reward_calculator()
        {
            bool started = _minigame.Begin(MakeAsteroid(remainingYield: 8)); // ceil(8/8)=1 tap, clamped up? min=1 so 1

            Assert.IsTrue(started);
            Assert.IsNotNull(_minigame.CurrentSession);
            Assert.AreEqual(1, _minigame.CurrentSession.TapsRequired);
            Assert.AreEqual(2f, _minigame.CurrentSession.SessionDurationSeconds, 0.001f);
        }
```

- [ ] **Step 8: Run the full EditMode suite to verify no regressions**

Run the headless EditMode command (no filter — full suite). Expected: all tests pass, 0 failures, including `MiningControllerTests` and `ActiveMiningMinigameTests`.

- [ ] **Step 9: Commit**

```bash
git add Assets/_Project/Scripts/Config/EconomyConfig.cs Assets/_Project/Scripts/Mining/MiningRewardCalculator.cs Assets/_Project/Scripts/Mining/ActiveMiningMinigame.cs Assets/_Project/Tests/EditMode/Mining/MiningRewardCalculatorTests.cs Assets/_Project/Tests/EditMode/Mining/ActiveMiningMinigameTests.cs Assets/_Project/Tests/EditMode/Mining/MiningControllerTests.cs
git commit -m "mining: derive active-session countdown from existing yield/taps, no new size field"
```

---

### Task 3: `ActiveMiningTargetPoint` — 3D world-anchored marker

**Files:**
- Create: `Assets/_Project/Scripts/Mining/ActiveMiningTargetPoint.cs`
- Test: `Assets/_Project/Tests/EditMode/Mining/ActiveMiningTargetPointTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces: `ActiveMiningTargetPoint : MonoBehaviour` with `static Vector3 PickFacingPoint(Vector3 center, float radius, Vector3 towardViewer)` and instance method `void PlaceOnAsteroid(Transform asteroidTransform, float radius, Vector3 towardViewer)`. Task 6 (`ActiveMiningMinigameView`) instantiates this component and calls `PlaceOnAsteroid`.

- [ ] **Step 1: Write the failing test**

Create `Assets/_Project/Tests/EditMode/Mining/ActiveMiningTargetPointTests.cs`:

```csharp
using NUnit.Framework;
using SocialUniverse.Mining;
using UnityEngine;

namespace SocialUniverse.Tests
{
    public class ActiveMiningTargetPointTests
    {
        [Test]
        public void PickFacingPoint_returns_a_point_on_the_sphere_facing_the_viewer()
        {
            var center = new Vector3(1f, 2f, 3f);
            const float radius = 2f;
            var towardViewer = Vector3.forward;

            for (int i = 0; i < 100; i++)
            {
                Vector3 point  = ActiveMiningTargetPoint.PickFacingPoint(center, radius, towardViewer);
                Vector3 offset = point - center;

                Assert.AreEqual(radius, offset.magnitude, 0.001f, "point must lie exactly on the sphere surface");
                Assert.GreaterOrEqual(Vector3.Dot(offset.normalized, towardViewer), 0f,
                    "point must be on the hemisphere facing the viewer, not the far side");
            }
        }

        [Test]
        public void PlaceOnAsteroid_parents_the_marker_and_positions_it_on_the_asteroid_surface()
        {
            var asteroidGo = new GameObject("Asteroid");
            asteroidGo.transform.position = new Vector3(5f, 0f, 0f);

            var markerGo = new GameObject("Marker");
            var marker   = markerGo.AddComponent<ActiveMiningTargetPoint>();

            marker.PlaceOnAsteroid(asteroidGo.transform, radius: 1.5f, towardViewer: Vector3.back);

            Assert.AreEqual(asteroidGo.transform, marker.transform.parent);
            float distanceFromCenter = Vector3.Distance(marker.transform.position, asteroidGo.transform.position);
            Assert.AreEqual(1.5f, distanceFromCenter, 0.001f);

            Object.DestroyImmediate(markerGo);
            Object.DestroyImmediate(asteroidGo);
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run the headless EditMode command. Expected: compile error — `ActiveMiningTargetPoint` does not exist yet.

- [ ] **Step 3: Create `ActiveMiningTargetPoint.cs`**

Create `Assets/_Project/Scripts/Mining/ActiveMiningTargetPoint.cs`:

```csharp
using UnityEngine;

namespace SocialUniverse.Mining
{
    // Marker for a single tap target during active mining. Anchored to a random point on the
    // spawned asteroid's surface that currently faces the camera, so it's a genuine 3D point
    // that moves as the asteroid rotates. Only ever placed on the hemisphere facing the viewer
    // at spawn time (no occlusion tracking) — see design spec 2026-07-04 §4.
    public class ActiveMiningTargetPoint : MonoBehaviour
    {
        // Picks a random point on the sphere (center, radius) that lies within the hemisphere
        // facing towardViewer. Pure/static so it's directly unit-testable without a scene.
        public static Vector3 PickFacingPoint(Vector3 center, float radius, Vector3 towardViewer)
        {
            Vector3 viewerDir = towardViewer.normalized;

            for (int attempt = 0; attempt < 64; attempt++)
            {
                Vector3 dir = Random.onUnitSphere;
                if (Vector3.Dot(dir, viewerDir) >= 0f)
                    return center + dir * radius;
            }

            // Fallback so this never loops forever: reflect a random point into the facing
            // hemisphere instead of retrying again.
            Vector3 fallback = Random.onUnitSphere;
            if (Vector3.Dot(fallback, viewerDir) < 0f) fallback = -fallback;
            return center + fallback * radius;
        }

        // Parents this marker to the asteroid and positions it at a random point on its surface
        // facing towardViewer, so subsequent asteroid rotation carries the marker along with it.
        public void PlaceOnAsteroid(Transform asteroidTransform, float radius, Vector3 towardViewer)
        {
            transform.SetParent(asteroidTransform, worldPositionStays: false);
            Vector3 worldPoint = PickFacingPoint(asteroidTransform.position, radius, towardViewer);
            transform.position = worldPoint;
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run the headless EditMode command. Expected: both tests in `ActiveMiningTargetPointTests` pass.

- [ ] **Step 5: Commit**

```bash
git add Assets/_Project/Scripts/Mining/ActiveMiningTargetPoint.cs Assets/_Project/Tests/EditMode/Mining/ActiveMiningTargetPointTests.cs
git commit -m "mining: add ActiveMiningTargetPoint, a 3D world-anchored tap marker"
```

---

### Task 4: `ActiveMiningAsteroidStage` — spawns the visual asteroid clone

**Files:**
- Create: `Assets/_Project/Scripts/Mining/ActiveMiningAsteroidStage.cs`
- Test: `Assets/_Project/Tests/EditMode/Mining/ActiveMiningAsteroidStageTests.cs`

**Interfaces:**
- Consumes: `AsteroidDefinition.ModelPrefab` (existing, `SocialUniverse.Config`).
- Produces: `ActiveMiningAsteroidStage : MonoBehaviour` with `GameObject StageClone { get; }`, `float ColliderRadius { get; }`, `GameObject SpawnClone(AsteroidDefinition definition)`. Task 5's `ActiveMiningSceneBootstrapper` calls `SpawnClone`; Task 6's `ActiveMiningMinigameView` reads `StageClone`/`ColliderRadius`.

- [ ] **Step 1: Write the failing test**

Create `Assets/_Project/Tests/EditMode/Mining/ActiveMiningAsteroidStageTests.cs`:

```csharp
using NUnit.Framework;
using SocialUniverse.Config;
using SocialUniverse.Mining;
using UnityEngine;

namespace SocialUniverse.Tests
{
    public class ActiveMiningAsteroidStageTests
    {
        private GameObject                 _stageGo;
        private ActiveMiningAsteroidStage  _stage;

        [SetUp]
        public void SetUp()
        {
            _stageGo = new GameObject("Stage");
            _stage   = _stageGo.AddComponent<ActiveMiningAsteroidStage>();
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_stageGo);

        [Test]
        public void SpawnClone_falls_back_to_a_primitive_sphere_when_no_model_prefab_is_set()
        {
            var def = ScriptableObject.CreateInstance<AsteroidDefinition>();

            GameObject clone = _stage.SpawnClone(def);

            Assert.IsNotNull(clone);
            Assert.IsNotNull(clone.GetComponent<Collider>());
            Assert.AreEqual(_stageGo.transform, clone.transform.parent);
            Assert.Greater(_stage.ColliderRadius, 0f);

            Object.DestroyImmediate(def);
        }

        [Test]
        public void SpawnClone_replaces_a_previous_clone_instead_of_stacking_them()
        {
            var def = ScriptableObject.CreateInstance<AsteroidDefinition>();

            var first  = _stage.SpawnClone(def);
            var second = _stage.SpawnClone(def);

            Assert.AreNotSame(first, second);
            Assert.IsTrue(first == null, "the previous clone must be destroyed, not left orphaned");

            Object.DestroyImmediate(def);
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run the headless EditMode command. Expected: compile error — `ActiveMiningAsteroidStage` does not exist yet.

- [ ] **Step 3: Create `ActiveMiningAsteroidStage.cs`**

Create `Assets/_Project/Scripts/Mining/ActiveMiningAsteroidStage.cs`:

```csharp
using UnityEngine;
using SocialUniverse.Config;

namespace SocialUniverse.Mining
{
    // Spawns a visual clone of an asteroid's model prefab for the active-mining minigame scene.
    // The clone is presentation-only — MiningController's in-progress ActiveMiningSession is the
    // single source of truth for RemainingYield/Definition; this never touches the original
    // field Asteroid instance back in the Planet scene.
    public class ActiveMiningAsteroidStage : MonoBehaviour
    {
        [SerializeField] private float _minRotationSpeed = 5f;  // degrees per second
        [SerializeField] private float _maxRotationSpeed = 15f;

        public GameObject StageClone    { get; private set; }
        public float      ColliderRadius { get; private set; }

        private Vector3 _rotationAxis;
        private float   _rotationSpeed;

        // Instantiates definition.ModelPrefab (or a fallback primitive sphere, matching
        // AsteroidSpawner's fallback) as a child of this transform, and records the collider
        // radius used for target-point placement.
        public GameObject SpawnClone(AsteroidDefinition definition)
        {
            if (StageClone != null)
            {
                if (Application.isPlaying) Destroy(StageClone);
                else                       DestroyImmediate(StageClone);
            }

            GameObject clone;
            if (definition.ModelPrefab != null)
            {
                clone = Instantiate(definition.ModelPrefab, transform.position, Quaternion.identity, transform);
            }
            else
            {
                clone = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                clone.transform.SetParent(transform);
                clone.transform.localPosition = Vector3.zero;
                clone.transform.localScale    = Vector3.one * 0.5f;
            }

            var collider = clone.GetComponent<Collider>();
            if (collider == null)
            {
                var sphere = clone.AddComponent<SphereCollider>();
                sphere.radius = 0.5f;
                collider = sphere;
            }
            ColliderRadius = Mathf.Max(collider.bounds.extents.x, collider.bounds.extents.y, collider.bounds.extents.z);

            _rotationAxis  = Random.onUnitSphere;
            _rotationSpeed = Random.Range(_minRotationSpeed, _maxRotationSpeed);

            StageClone = clone;
            return clone;
        }

        // Slow tumble to match the atmosphere of the field asteroids (Asteroid.Update()).
        private void Update()
        {
            if (StageClone != null)
                StageClone.transform.Rotate(_rotationAxis, _rotationSpeed * Time.deltaTime, Space.World);
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run the headless EditMode command. Expected: both tests in `ActiveMiningAsteroidStageTests` pass.

- [ ] **Step 5: Commit**

```bash
git add Assets/_Project/Scripts/Mining/ActiveMiningAsteroidStage.cs Assets/_Project/Tests/EditMode/Mining/ActiveMiningAsteroidStageTests.cs
git commit -m "mining: add ActiveMiningAsteroidStage, spawns a visual asteroid clone for the minigame"
```

---

### Task 5: Scene lifecycle — `ActiveMiningSceneScope` + `ActiveMiningSceneController`

**Files:**
- Create: `Assets/_Project/Scripts/App/ActiveMiningSceneScope.cs`
- Create: `Assets/_Project/Scripts/Mining/ActiveMiningSceneController.cs`
- Modify: `Assets/_Project/Scripts/Core/Constants.cs`
- Modify: `Assets/_Project/Scripts/App/PlanetSceneScope.cs`

**Interfaces:**
- Consumes: `MiningController.OnActiveSessionChanged` (existing event, `Action<ActiveMiningSession>`), `MiningController.CurrentActiveSession` (existing), `ActiveMiningAsteroidStage.SpawnClone` (Task 4), `SceneLoader.LoadAsync`/`UnloadAsync` (existing, `SocialUniverse.Core`), `PlanetCameraController` (existing, `SocialUniverse.World`).
- Produces: `Constants.SceneNames.ActiveMining = "ActiveMining"`; `ActiveMiningSceneScope : LifetimeScope` (parented to `PlanetSceneScope` via the Inspector, wired in Task 7); `ActiveMiningSceneBootstrapper : IStartable`; `ActiveMiningSceneController : IStartable` (registered as an entry point in `PlanetSceneScope`).

No automated test for this task: `ActiveMiningSceneController` and `ActiveMiningSceneBootstrapper` are thin scene-orchestration classes, matching this project's existing precedent (`TravelState`, `HubState`, `PlanetState`, `TravelSceneBootstrapper`, `PlanetSceneBootstrapper` have no dedicated unit tests either — they're verified by the full test suite staying green plus manual/PlayMode verification once the scene exists in Task 7).

- [ ] **Step 1: Add the scene name constant**

In `Assets/_Project/Scripts/Core/Constants.cs`, add a line to `SceneNames`:

```csharp
        public static class SceneNames
        {
            public const string Bootstrap     = "Bootstrap";
            public const string Auth          = "Auth";
            public const string SolarSystem   = "SolarSystem";
            public const string Travel        = "Travel";
            public const string TravelLoading = "TravelLoading";
            public const string Planet        = "Planet";
            public const string Station       = "Station";
            public const string LoadingScreen = "LoadingScreen";
            public const string ActiveMining  = "ActiveMining";
        }
```

- [ ] **Step 2: Create `ActiveMiningSceneScope.cs`**

Create `Assets/_Project/Scripts/App/ActiveMiningSceneScope.cs`:

```csharp
using VContainer;
using VContainer.Unity;
using SocialUniverse.Mining;
using SocialUniverse.Core;

namespace SocialUniverse.App
{
    // Scene scope for the ActiveMining minigame overlay — loaded additively on top of the
    // Planet scene while an active-mining session is running (see ActiveMiningSceneController,
    // which owns the load/unload). Always runs as a child of PlanetSceneScope (parentReference
    // set in the Inspector, wired in the scene file), so MiningController and everything else in
    // PlanetSceneScope resolve through the parent chain automatically — this scope only
    // registers the components that live in ActiveMining.unity itself.
    public class ActiveMiningSceneScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterComponentInHierarchy<ActiveMiningAsteroidStage>();
            builder.RegisterComponentInHierarchy<SocialUniverse.UI.ActiveMiningMinigameView>();

            builder.RegisterEntryPoint<ActiveMiningSceneBootstrapper>();
        }
    }

    // Spawns the visual asteroid clone for the in-progress active-mining session as soon as this
    // scene finishes loading. MiningController's session already exists by the time this scene
    // loads (ActiveMiningSceneController only loads it after a session has started).
    public class ActiveMiningSceneBootstrapper : IStartable
    {
        private readonly MiningController          _mining;
        private readonly ActiveMiningAsteroidStage _stage;

        public ActiveMiningSceneBootstrapper(MiningController mining, ActiveMiningAsteroidStage stage)
        {
            _mining = mining;
            _stage  = stage;
        }

        public void Start()
        {
            var session = _mining.CurrentActiveSession;
            if (session == null)
            {
                SULog.Warn("ActiveMiningSceneBootstrapper: no active-mining session in progress", SULog.Channel.Mining);
                return;
            }

            _stage.SpawnClone(session.Asteroid.Definition);
        }
    }
}
```

- [ ] **Step 3: Create `ActiveMiningSceneController.cs`**

Create `Assets/_Project/Scripts/Mining/ActiveMiningSceneController.cs`:

```csharp
using System.Threading.Tasks;
using UnityEngine;
using VContainer.Unity;
using SocialUniverse.World;
using SocialUniverse.Core;

namespace SocialUniverse.Mining
{
    // Owns loading/unloading the ActiveMining minigame scene on top of the (still-running)
    // Planet scene, and disabling the Planet camera while it's up so only one camera renders at
    // a time. Reacts to MiningController.OnActiveSessionChanged rather than being called
    // directly by MiningModePromptView, so starting/stopping an active-mining session is the
    // single source of truth for whether the minigame scene should be loaded.
    public class ActiveMiningSceneController : IStartable
    {
        private readonly MiningController       _mining;
        private readonly SceneLoader            _sceneLoader;
        private readonly PlanetCameraController _planetCamera;

        private bool _sceneLoaded;

        public ActiveMiningSceneController(MiningController mining, SceneLoader sceneLoader, PlanetCameraController planetCamera)
        {
            _mining       = mining;
            _sceneLoader  = sceneLoader;
            _planetCamera = planetCamera;
        }

        public void Start() => _mining.OnActiveSessionChanged += OnActiveSessionChanged;

        private void OnActiveSessionChanged(ActiveMiningSession session)
        {
            if (session != null && !_sceneLoaded)
                _ = EnterAsync();
            else if (session == null && _sceneLoaded)
                _ = ExitAsync();
        }

        private async Task EnterAsync()
        {
            _sceneLoaded = true;
            SetPlanetCameraEnabled(false);
            await _sceneLoader.LoadAsync(Constants.SceneNames.ActiveMining);
        }

        private async Task ExitAsync()
        {
            await _sceneLoader.UnloadAsync(Constants.SceneNames.ActiveMining);
            SetPlanetCameraEnabled(true);
            _sceneLoaded = false;
        }

        private void SetPlanetCameraEnabled(bool isEnabled)
        {
            var camera = _planetCamera.GetComponent<Camera>();
            if (camera != null) camera.enabled = isEnabled;
        }
    }
}
```

- [ ] **Step 4: Register the new entry point in `PlanetSceneScope.cs`**

In `Assets/_Project/Scripts/App/PlanetSceneScope.cs`, in the `Configure` method, find:

```csharp
            builder.RegisterEntryPoint<PlanetSceneBootstrapper>();
            builder.RegisterEntryPoint<IdleMiningSessionController>();
            builder.RegisterEntryPoint<ActiveMiningSessionController>();
```

and add the new controller immediately after `ActiveMiningSessionController`:

```csharp
            builder.RegisterEntryPoint<PlanetSceneBootstrapper>();
            builder.RegisterEntryPoint<IdleMiningSessionController>();
            builder.RegisterEntryPoint<ActiveMiningSessionController>();
            builder.RegisterEntryPoint<ActiveMiningSceneController>();
```

- [ ] **Step 5: Run the full EditMode suite to verify no regressions**

Run the headless EditMode command (no filter). Expected: all tests still pass — this task adds no new automated tests, only new production types plus one new registration line; a green suite here just confirms the project still compiles cleanly.

- [ ] **Step 6: Commit**

```bash
git add Assets/_Project/Scripts/Core/Constants.cs Assets/_Project/Scripts/App/ActiveMiningSceneScope.cs Assets/_Project/Scripts/Mining/ActiveMiningSceneController.cs Assets/_Project/Scripts/App/PlanetSceneScope.cs
git commit -m "mining: add ActiveMining scene lifecycle (load/unload + camera swap on session start/end)"
```

---

### Task 6: `ActiveMiningMinigameView` — world-anchored projection UI

**Files:**
- Modify: `Assets/_Project/Scripts/UI/ActiveMiningMinigameView.cs`

**Interfaces:**
- Consumes: `MiningController.CurrentActiveSession`/`OnActiveSessionChanged`/`RegisterActiveTap(bool)` (existing, unchanged); `ActiveMiningAsteroidStage.StageClone`/`ColliderRadius` (Task 4); `ActiveMiningTargetPoint.PlaceOnAsteroid` (Task 3).
- Produces: rewritten `ActiveMiningMinigameView` with new serialized fields (`_sceneCamera`, `_stage`, `_timeText`, `_resultBanner`, `_resultText`) wired in Task 7's scene file; `_asteroidArea` field is removed.

No automated test for this task: the previous version of this view had none either (it's a `MonoBehaviour` UI overlay driven by clicks and `Update()`, verified manually/in Play Mode, same precedent as `HUDController`/`MiningModePromptView`).

- [ ] **Step 1: Rewrite `ActiveMiningMinigameView.cs`**

Replace `Assets/_Project/Scripts/UI/ActiveMiningMinigameView.cs` with:

```csharp
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using SocialUniverse.Mining;

namespace SocialUniverse.UI
{
    // Overlay UI for the active-mining minigame scene: renders the current target point (a real
    // 3D anchor on the spawned asteroid clone, projected to screen space every frame so it moves
    // as the asteroid rotates), the countdown/progress/error counters, and forwards player taps
    // to MiningController. Unlike the old Planet-scene overlay, this view's GameObject doesn't
    // need to hide/show itself — the whole ActiveMining scene only exists while a session is
    // running, so scene load/unload (see ActiveMiningSceneController) is the visibility switch.
    // _targetButton must be a UI child rendered above _missAreaButton in the hierarchy so a tap
    // on the point hits the target first and any other tap in the asteroid area falls through to
    // the miss button (standard Unity UI raycast ordering).
    public class ActiveMiningMinigameView : MonoBehaviour
    {
        [SerializeField] private Camera                    _sceneCamera;
        [SerializeField] private ActiveMiningAsteroidStage  _stage;
        [SerializeField] private RectTransform              _targetPoint;
        [SerializeField] private Button                     _targetButton;
        [SerializeField] private Button                     _missAreaButton;
        [SerializeField] private Text                       _progressText;
        [SerializeField] private Text                       _errorText;
        [SerializeField] private Text                       _timeText;
        [SerializeField] private GameObject                 _resultBanner;
        [SerializeField] private Text                       _resultText;

        [Inject] private MiningController _mining;

        private ActiveMiningTargetPoint _currentTargetAnchor;

        private void Awake()
        {
            if (_resultBanner != null) _resultBanner.SetActive(false);
            if (_targetButton   != null) _targetButton.onClick.AddListener(() => OnTapped(hitTarget: true));
            if (_missAreaButton != null) _missAreaButton.onClick.AddListener(() => OnTapped(hitTarget: false));
        }

        private void Start() => _mining.OnActiveSessionChanged += OnSessionChanged;

        private void OnDestroy()
        {
            _mining.OnActiveSessionChanged -= OnSessionChanged;
            if (_currentTargetAnchor != null) Destroy(_currentTargetAnchor.gameObject);
        }

        private void Update()
        {
            var session = _mining.CurrentActiveSession;
            if (session == null) return;

            Refresh(session);
            ProjectTargetPointToScreen();
        }

        private void OnSessionChanged(ActiveMiningSession session)
        {
            if (session == null) return;

            if (session.Stage != ActiveMiningStage.InProgress)
            {
                ShowResult(session.Stage);
                return;
            }

            if (_resultBanner != null) _resultBanner.SetActive(false);
            Refresh(session);
            SpawnNextTargetPoint();
        }

        private void Refresh(ActiveMiningSession session)
        {
            if (_progressText != null) _progressText.text = $"{session.SuccessfulTaps}/{session.TapsRequired}";
            if (_errorText    != null) _errorText.text    = $"Misses: {session.ErrorCount}/{session.MaxErrors}";
            if (_timeText     != null) _timeText.text     = $"{Mathf.CeilToInt(session.TimeRemainingSeconds)}s";
        }

        private void ShowResult(ActiveMiningStage stage)
        {
            if (_resultBanner != null) _resultBanner.SetActive(true);
            if (_resultText   != null) _resultText.text = stage == ActiveMiningStage.Success ? "Success!" : "Failed";
        }

        private void SpawnNextTargetPoint()
        {
            if (_stage == null || _stage.StageClone == null) return;

            if (_currentTargetAnchor != null) Destroy(_currentTargetAnchor.gameObject);

            var anchorGo = new GameObject("ActiveMiningTargetAnchor");
            _currentTargetAnchor = anchorGo.AddComponent<ActiveMiningTargetPoint>();

            Vector3 towardViewer = _sceneCamera != null
                ? _sceneCamera.transform.position - _stage.StageClone.transform.position
                : Vector3.back;

            _currentTargetAnchor.PlaceOnAsteroid(_stage.StageClone.transform, _stage.ColliderRadius, towardViewer);
        }

        private void ProjectTargetPointToScreen()
        {
            if (_targetPoint == null || _currentTargetAnchor == null || _sceneCamera == null) return;

            _targetPoint.position = _sceneCamera.WorldToScreenPoint(_currentTargetAnchor.transform.position);
        }

        private void OnTapped(bool hitTarget)
        {
            if (_mining.CurrentActiveSession == null) return;

            _mining.RegisterActiveTap(hitTarget);

            if (_mining.CurrentActiveSession != null && _mining.CurrentActiveSession.Stage == ActiveMiningStage.InProgress)
                SpawnNextTargetPoint();
        }
    }
}
```

- [ ] **Step 2: Run the full EditMode suite to verify no regressions**

Run the headless EditMode command (no filter). Expected: all tests pass — this is a `MonoBehaviour` rewrite with no dedicated tests, so a green suite here confirms the project compiles cleanly and nothing else broke.

- [ ] **Step 3: Commit**

```bash
git add Assets/_Project/Scripts/UI/ActiveMiningMinigameView.cs
git commit -m "ui: ActiveMiningMinigameView projects a real 3D target anchor instead of a random 2D point"
```

---

### Task 7: Author the `ActiveMining.unity` scene and wire everything together

**Files:**
- Create: `Assets/Scenes/ActiveMining.unity`
- Create: `Assets/Scenes/ActiveMining.unity.meta`
- Modify: `ProjectSettings/EditorBuildSettings.asset`
- Modify (Inspector wiring only, no code changes): `Assets/Scenes/Planet.unity` (add the `PlanetCameraController` reference is not needed — `ActiveMiningSceneController` already resolves it via DI; Planet.unity itself needs no edits for this task since the camera-toggle happens entirely in code against the already-registered `PlanetCameraController` component)

**Interfaces:**
- Consumes: `ActiveMiningSceneScope` (Task 5), `ActiveMiningSceneBootstrapper` (Task 5), `ActiveMiningAsteroidStage` (Task 4), `ActiveMiningMinigameView` (Task 6), `Constants.SceneNames.ActiveMining` (Task 5).
- Produces: the loadable `ActiveMining` scene referenced by `ActiveMiningSceneController.EnterAsync`/`ExitAsync`.

This task has no unit-testable code — it's scene authoring. Unity auto-generates `.meta` files (with fresh GUIDs) for any script that doesn't already have one the first time the project is imported/compiled, which is exactly what the headless test runs in Tasks 1–6 already did. Follow these steps in order.

- [ ] **Step 1: Record the auto-generated GUIDs for the new scripts**

Read the `.meta` files Unity already generated during Tasks 1–6's headless test runs (each new `.cs` file gets a matching `.meta` with a `guid:` line):

```bash
grep -H "guid:" Assets/_Project/Scripts/Mining/ActiveMiningTargetPoint.cs.meta
grep -H "guid:" Assets/_Project/Scripts/Mining/ActiveMiningAsteroidStage.cs.meta
grep -H "guid:" Assets/_Project/Scripts/Mining/ActiveMiningSceneController.cs.meta
grep -H "guid:" Assets/_Project/Scripts/App/ActiveMiningSceneScope.cs.meta
grep -H "guid:" Assets/_Project/Scripts/UI/ActiveMiningMinigameView.cs.meta
```

If any of these `.meta` files don't exist yet (no headless run has happened since that file was created), run the headless EditMode command once first — Unity generates missing `.meta` files as part of the project import that precedes running tests, even though these particular scripts aren't referenced by any test directly.

Record each GUID; you'll reference `ActiveMiningSceneScope`'s GUID on the scope `GameObject`, `ActiveMiningAsteroidStage`'s on the stage `GameObject`, and `ActiveMiningMinigameView`'s on the view `GameObject` (the `ActiveMiningSceneBootstrapper`/`ActiveMiningSceneController` classes are plain C# entry points, not `MonoBehaviour`s, so they are never placed directly on a scene `GameObject` — VContainer instantiates them from the DI container instead).

- [ ] **Step 2: Create the scene's `.meta` file**

Create `Assets/Scenes/ActiveMining.unity.meta`:

```
fileFormatVersion: 2
guid: 1229d4ec0c754b3798ee90affc974bc6
DefaultImporter:
  externalObjects: {}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
```

- [ ] **Step 3: Hand-author `Assets/Scenes/ActiveMining.unity`**

Base the scene's boilerplate header (`OcclusionCullingSettings`, `RenderSettings`, `LightmapSettings`, `NavMeshSettings` — these four blocks are identical scene-wide metadata, not gameplay content) on the corresponding blocks at the top of `Assets/Scenes/Travel.unity` (lines 1–121), copied verbatim except for `m_SceneGUID` (leave as `00000000000000000000000000000000`, matching every other scene in this project).

Below that header, hand-author the following GameObjects, each as its own `GameObject`/`Transform`/component YAML block using sequential unique `fileID` numbers (any non-conflicting integers, e.g. starting from `100001` and incrementing), following the exact block style already used in `Assets/Scenes/Travel.unity` and `Assets/Scenes/Planet.unity` (a `GameObject` block listing its components by `fileID`, followed by one block per component):

1. **Directional Light** — `GameObject` named `Directional Light` with a `Transform` (rotated to face down/forward, e.g. `m_LocalEulerAnglesHint: {x: 50, y: -30, z: 0}`) and a `Light` component (`m_Type: 1` for Directional, `m_Color: {r: 1, g: 1, b: 1, a: 1}`, `m_Intensity: 1`).

2. **Main Camera** — `GameObject` named `ActiveMiningCamera` with a `Transform` (positioned at e.g. `{x: 0, y: 0, z: -10}` looking at the origin, where the asteroid stage will spawn), a `Camera` component (copy the field values from `Assets/Scenes/Travel.unity`'s Camera block, lines 366–416, but do **not** include an `AudioListener` component — the Planet scene's camera underneath already has one active, and adding a second produces Unity's "2 audio listeners in the scene" warning), and a `UniversalAdditionalCameraData` `MonoBehaviour` (copy verbatim from `Assets/Scenes/Travel.unity` lines 432–475, script guid `a79441f348de89743a2939f4d699eac1`). Do **not** add a `CinemachineBrain` — this scene has no orbit camera, the camera is static.

   Do **not** add an `EventSystem` GameObject to this scene — the Planet scene underneath already has one, and Unity only supports one active `EventSystem` across all loaded scenes combined; a second one triggers a runtime warning and gets auto-disabled. Button clicks in this scene's Canvas will route through Planet's existing `EventSystem`, exactly like every other UI in this project's overlay scenes.

3. **ActiveMiningAsteroidStage** — empty `GameObject` named `AsteroidStage`, `Transform` positioned at the origin (in front of the camera, e.g. `{x: 0, y: 0, z: 0}`), with an `ActiveMiningAsteroidStage` `MonoBehaviour` component (`m_Script: {fileID: 11500000, guid: <ActiveMiningAsteroidStage's GUID from Step 1>, type: 3}`, `m_EditorClassIdentifier: SocialUniverse.Mining::SocialUniverse.Mining.ActiveMiningAsteroidStage`). Leave `_minRotationSpeed`/`_maxRotationSpeed` at their script defaults (omit them from the YAML — Unity fills in serialized defaults for any field not explicitly listed).

4. **ActiveMiningSceneScope** — empty `GameObject` named `ActiveMiningSceneScope`, `Transform` at the origin, with an `ActiveMiningSceneScope` `MonoBehaviour` component (`guid: <ActiveMiningSceneScope's GUID from Step 1>`, `m_EditorClassIdentifier: SocialUniverse.App::SocialUniverse.App.ActiveMiningSceneScope`) whose serialized fields include:
   ```yaml
   parentReference:
     TypeName: SocialUniverse.App.PlanetSceneScope
   autoRun: 1
   ```
   (this is the exact shape `PlanetSceneScope`'s own `parentReference` uses in `Assets/Scenes/Planet.unity` to point at `RootLifetimeScope` — here it points at `PlanetSceneScope` instead, so `ActiveMiningSceneScope` becomes a child of whatever `PlanetSceneScope` instance is already running in the loaded Planet scene.)

5. **Canvas** — `GameObject` named `Canvas` with `RectTransform`, `Canvas` (`m_RenderMode: 0` for Screen Space - Overlay), `CanvasScaler`, and `GraphicRaycaster` components — copy these four component blocks' structure verbatim from `Assets/Scenes/Travel.unity`'s `Canvas` GameObject (lines 1120–1151+, continuing past the truncated view — every field on `CanvasScaler`/`GraphicRaycaster` is boilerplate, not gameplay-specific). Under this Canvas, create the UI hierarchy:
   - `TargetPoint` (`RectTransform` + `Image`) — the visible marker; a `Button` component (`_targetButton`) on the same object or a child, sized small (e.g. 80x80).
   - `MissArea` (`RectTransform` stretched to fill the Canvas + `Image` with near-zero alpha + `Button` component (`_missAreaButton`)) — must be **earlier** in the Canvas's sibling order than `TargetPoint` so `TargetPoint` renders/raycasts on top (matches the existing raycast-ordering comment in `ActiveMiningMinigameView`).
   - `ProgressText`, `ErrorText`, `TimeText` (each `RectTransform` + `Text` or `TextMeshProUGUI`, positioned in a HUD corner, e.g. top-left stacked).
   - `ResultBanner` (`RectTransform` centered + `Image` background + child `ResultText`), inactive by default (`m_IsActive: 0`).

6. **ActiveMiningMinigameView** — can live on the `Canvas` GameObject itself (or a dedicated empty child) — an `ActiveMiningMinigameView` `MonoBehaviour` component (`guid: <ActiveMiningMinigameView's GUID from Step 1>`, `m_EditorClassIdentifier: SocialUniverse.UI::SocialUniverse.UI.ActiveMiningMinigameView`) with its serialized fields wired by `fileID` reference to: `_sceneCamera` → the `ActiveMiningCamera`'s `Camera` component, `_stage` → the `AsteroidStage`'s `ActiveMiningAsteroidStage` component, `_targetPoint` → `TargetPoint`'s `RectTransform`, `_targetButton` → `TargetPoint`'s `Button`, `_missAreaButton` → `MissArea`'s `Button`, `_progressText`/`_errorText`/`_timeText` → the three text components, `_resultBanner` → the `ResultBanner` `GameObject`, `_resultText` → its child `Text`.

- [ ] **Step 4: Register the scene in Build Settings**

In `ProjectSettings/EditorBuildSettings.asset`, add a new entry to `m_Scenes` (after the existing `Station.unity` entry, before `LoadingScreen.unity`, matching the file's existing order-of-addition style — order doesn't affect load-by-name behavior):

```yaml
  - enabled: 1
    path: Assets/Scenes/ActiveMining.unity
    guid: 1229d4ec0c754b3798ee90affc974bc6
```

- [ ] **Step 5: Verify the scene loads cleanly (structural check)**

Run the headless EditMode command (no filter) once to let Unity import the new scene/meta files and confirm the whole project still compiles:

```
"C:\Program Files\Unity\Hub\Editor\6000.3.12f1\Editor\Unity.exe" -batchmode -runTests -projectPath . -testResults .superpowers/sdd/task7-editmode-results.xml -testPlatform EditMode -logFile .superpowers/sdd/task7-editmode.log
```

Expected: all EditMode tests still pass, and the log contains no `GUID not found` / `missing script` errors referencing `ActiveMining.unity`.

Then run a headless PlayMode pass and check the log specifically for scene-load correctness (same exact command shape as prior PlayMode runs in this project, per `.superpowers/sdd/task-17-report.md`'s documented working invocation — no `-nographics`, no `-quit` combined with `-runTests`):

```
"C:\Program Files\Unity\Hub\Editor\6000.3.12f1\Editor\Unity.exe" -batchmode -runTests -projectPath . -testResults .superpowers/sdd/task7-playmode-results.xml -testPlatform PlayMode -logFile .superpowers/sdd/task7-playmode.log
```

Grep the resulting log for `missing script`, `missing mono`, and `GUID not found` — expect zero matches. (Existing PlayMode failures from the pre-existing, out-of-scope "Known Issue #7" `PlanetSceneScope` standalone-parent limitation are expected and unrelated to this task — do not attempt to fix that here.)

- [ ] **Step 6: Manual verification note**

Automated coverage stops at "the scene loads without errors and every reference resolves." The actual tap/raycast/camera-swap/countdown gameplay flow requires manual in-editor play-testing (open `Planet.unity`, enter Play Mode, tap an asteroid, choose Active Mine, confirm the camera swaps to the asteroid clone, tap the projected marker, watch the countdown, and confirm 3 wrong taps fails the session) — this matches the same manual-verification precedent already used for the hand-authored `ActiveMiningOverlay` UI in the prior mining redesign. Record the outcome of this manual pass in the task's completion report; if Unity Editor automation isn't available in the current environment, say so explicitly rather than claiming it was done.

- [ ] **Step 7: Commit**

```bash
git add Assets/Scenes/ActiveMining.unity Assets/Scenes/ActiveMining.unity.meta ProjectSettings/EditorBuildSettings.asset
git commit -m "mining: add ActiveMining.unity scene, wire scope/stage/view, register in Build Settings"
```

---

## Self-Review Notes

- **Spec coverage:** §2 (scene/DI architecture) → Task 5 + Task 7. §3 (timer rework) → Tasks 1–2. §4 (minigame contents: asteroid stage, target points, view) → Tasks 3, 4, 6. §5 (entry point integration — `MiningModePromptView` unchanged) → confirmed by omission; no task touches that file. §6 (testing) → each task's own test-running steps, plus Task 7 Step 5's structural check and Step 6's manual-verification note. §7 (removals — `_asteroidArea`/`PlaceTargetPoint`) → Task 6. §8 (out of scope) → not touched by any task.
- **Placeholder scan:** no TBD/TODO markers; Task 7's scene-authoring steps are necessarily descriptive (Unity scene YAML can't be fully hand-written without runtime-generated GUIDs), but every value, GameObject, and wiring target is fully specified — nothing is left for the implementer to invent.
- **Type consistency:** `ActiveMiningSession` constructor shape `(Asteroid, int, int, float)` is consistent from Task 1 through its Task 2 call-site update. `MiningReward.ActiveSessionDurationSeconds` (Task 2) matches the field name used in Task 5's bootstrapper indirectly via `ActiveMiningMinigame.Begin()` (unchanged after Task 2). `ActiveMiningAsteroidStage.StageClone`/`ColliderRadius` (Task 4) match the exact names read in Task 6's `ActiveMiningMinigameView`. `Constants.SceneNames.ActiveMining` (Task 5) matches the name used in Task 7's Build Settings entry and `ActiveMiningSceneController`.
