# Active Mining Scene Swap + Pre/Post-Game UI Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Turn ActiveMining from an overlay loaded on top of a still-running Planet into a true FSM scene swap (Planet unloads, ActiveMining runs alone, Planet reloads), and add pre-game/post-game UI panels.

**Architecture:** A new `ActiveMiningState : IGameState` (Core) mirrors `TravelState`. A new `ActiveMiningHandoff` (Core, plain data) carries the reward numbers and asteroid identity across the Planet→ActiveMining→Planet round trip, since `MiningController`/`IEconomyService`/`AsteroidSpawner` are all destroyed when Planet unloads. `ActiveMiningMinigameView` gains a three-phase (PreGame/InProgress/PostGame) UI driven locally inside the ActiveMining scene.

**Tech Stack:** Unity 6, VContainer DI, NUnit EditMode tests, hand-authored scene YAML for `Assets/Scenes/ActiveMining.unity` / `Assets/Scenes/Planet.unity`.

## Global Constraints

- `SocialUniverse.Core` assembly must never reference `SocialUniverse.Mining` (one-way dependency: Mining→Core only). Any type shared between `PlanetState`/`ActiveMiningState` (Core) and `MiningController` (Mining) must live in Core and expose only primitive/Config-layer types (`AsteroidDefinition` is fine — Core already references `SocialUniverse.Config`).
- The Planet→ActiveMining transition trigger must go through `EventBus`, not direct `PlanetState` injection into `MiningModePromptView` — `PlanetState` is only registered in `PlanetSceneScope` when a parent scope exists (production), and directly injecting it into an always-active `RegisterComponentInHierarchy` MonoBehaviour would break the standalone/no-Bootstrap dev workflow of opening `Planet.unity` directly. Mirror the existing `LaunchRequestedEvent`/`LaunchButtonHandler` pattern exactly.
- `ActiveMiningSceneScope` parents to `SocialUniverse.App.RootLifetimeScope` via `parentReference.TypeName` (same mechanism `PlanetSceneScope`/`TravelSceneScope` already use), not to `PlanetSceneScope` — Planet is unloaded while ActiveMining runs.
- No automated PlayMode test for the full FSM round-trip (Planet unload → ActiveMining → Planet reload) — matches existing precedent (`PlanetState`/`TravelState`/`HubState` have no tests either).
- Reward math/totals are unchanged. Idle mining is unaffected.

---

### Task 1: Simplify `ActiveMiningSession` — drop the unused `Asteroid` parameter

Once gameplay ticks happen entirely inside the ActiveMining scene (no Planet, no live `Asteroid` MonoBehaviour available), nothing needs `ActiveMiningSession.Asteroid` — it was only ever stored, never read internally by `Tick`/`RegisterHit`/`RegisterMiss`.

**Files:**
- Modify: `Assets/_Project/Scripts/Mining/ActiveMiningSession.cs`
- Modify: `Assets/_Project/Tests/EditMode/Mining/ActiveMiningSessionTests.cs`

**Interfaces:**
- Produces: `ActiveMiningSession(int tapsRequired, int maxErrors, float sessionDurationSeconds)` — the constructor every later task's `ActiveMiningSessionRunner` (Task 7) will call.

- [ ] **Step 1: Update the test file to the new constructor shape (this won't compile yet)**

Replace the whole file with:

```csharp
using NUnit.Framework;
using SocialUniverse.Mining;

namespace SocialUniverse.Tests
{
    public class ActiveMiningSessionTests
    {
        [Test]
        public void Reaching_required_taps_succeeds()
        {
            var session = new ActiveMiningSession(tapsRequired: 3, maxErrors: 3, sessionDurationSeconds: 10f);

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
            var session = new ActiveMiningSession(tapsRequired: 10, maxErrors: 3, sessionDurationSeconds: 10f);

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
            var session = new ActiveMiningSession(tapsRequired: 10, maxErrors: 3, sessionDurationSeconds: 1f);

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
            var session = new ActiveMiningSession(tapsRequired: 10, maxErrors: 3, sessionDurationSeconds: 1f);

            session.Tick(0.9f);
            session.RegisterHit();
            session.Tick(0.2f); // total elapsed 1.1s -> the overall clock keeps counting regardless of hits

            Assert.AreEqual(ActiveMiningStage.Failed, session.Stage);
        }

        [Test]
        public void Terminal_stages_ignore_further_hits_misses_and_ticks()
        {
            var session = new ActiveMiningSession(tapsRequired: 1, maxErrors: 3, sessionDurationSeconds: 10f);
            session.RegisterHit(); // -> Success

            session.RegisterMiss();
            session.Tick(1000f); // would fail on time if terminal stages didn't ignore Tick

            Assert.AreEqual(ActiveMiningStage.Success, session.Stage);
            Assert.AreEqual(0, session.ErrorCount);
        }

        [Test]
        public void OnStageChanged_fires_on_terminal_transition_only()
        {
            var session = new ActiveMiningSession(tapsRequired: 2, maxErrors: 3, sessionDurationSeconds: 10f);
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

- [ ] **Step 2: Verify it fails to compile against the current production code**

Run: `"C:\Program Files\Unity\Hub\Editor\6000.3.12f1\Editor\Unity.exe" -batchmode -runTests -projectPath . -testResults task1-red.xml -testPlatform EditMode -logFile task1-red.log`
Expected: compile error — `ActiveMiningSession` has no constructor taking 3 arguments (current constructor takes 4, starting with `Asteroid asteroid`).

- [ ] **Step 3: Update the production class**

In `Assets/_Project/Scripts/Mining/ActiveMiningSession.cs`, replace:

```csharp
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
```

with:

```csharp
    public class ActiveMiningSession
    {
        public int      TapsRequired           { get; }
        public int      SuccessfulTaps          { get; private set; }
        public int      MaxErrors               { get; }
        public int      ErrorCount              { get; private set; }
        public float    SessionDurationSeconds  { get; }
        public float    TimeRemainingSeconds    { get; private set; }

        public ActiveMiningStage Stage { get; private set; } = ActiveMiningStage.InProgress;

        public event Action<ActiveMiningStage> OnStageChanged;

        public ActiveMiningSession(int tapsRequired, int maxErrors, float sessionDurationSeconds)
        {
            TapsRequired           = Mathf.Max(1, tapsRequired);
            MaxErrors              = Mathf.Max(1, maxErrors);
            SessionDurationSeconds = Mathf.Max(0.1f, sessionDurationSeconds);
            TimeRemainingSeconds   = SessionDurationSeconds;
        }
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `"C:\Program Files\Unity\Hub\Editor\6000.3.12f1\Editor\Unity.exe" -batchmode -runTests -projectPath . -testResults task1-green.xml -testPlatform EditMode -logFile task1-green.log`
Expected: all `ActiveMiningSessionTests` pass. (Other tests referencing the old 4-arg constructor — `ActiveMiningMinigameTests`, `MiningControllerTests`'s helper — will fail to compile at this point; that's expected and fixed in Tasks 3–4. Confirm via the log that the *only* new failures are compile errors in those two files, not runtime failures elsewhere.)

- [ ] **Step 5: Commit**

```bash
git add Assets/_Project/Scripts/Mining/ActiveMiningSession.cs Assets/_Project/Tests/EditMode/Mining/ActiveMiningSessionTests.cs
git commit -m "mining: drop the unused Asteroid param from ActiveMiningSession"
```

---

### Task 2: `ActiveMiningHandoff` — carries reward data across the Planet↔ActiveMining swap

New Core-layer plain-data singleton. Deliberately has no reference to `Asteroid`/`MiningReward` (both Mining-layer types) — callers pass already-unpacked primitives, keeping this type resolvable from both `SocialUniverse.Core` and `SocialUniverse.Mining`.

**Files:**
- Create: `Assets/_Project/Scripts/Core/ActiveMiningHandoff.cs`
- Test: `Assets/_Project/Tests/EditMode/Core/ActiveMiningHandoffTests.cs`

**Interfaces:**
- Produces: `ActiveMiningHandoff.Begin(string planetId, string asteroidSlotId, AsteroidDefinition definition, int remainingYieldAtStart, int tapsRequired, int maxErrors, float sessionDurationSeconds)`, `SetResult(bool succeeded)`, `Clear()`, and read-only properties `PlanetId`/`AsteroidSlotId`/`Definition`/`RemainingYieldAtStart`/`TapsRequired`/`MaxErrors`/`SessionDurationSeconds`/`HasResult`/`Succeeded` — consumed by `MiningController` (Task 3), `ActiveMiningState` (Task 5), `ActiveMiningSessionRunner` (Task 7), `ActiveMiningSceneScope` (Task 8), and `ActiveMiningMinigameView` (Task 9).

- [ ] **Step 1: Write the failing tests**

Create `Assets/_Project/Tests/EditMode/Core/ActiveMiningHandoffTests.cs`:

```csharp
using NUnit.Framework;
using SocialUniverse.Config;
using SocialUniverse.Core;
using UnityEngine;

namespace SocialUniverse.Tests
{
    public class ActiveMiningHandoffTests
    {
        private AsteroidDefinition  _def;
        private ActiveMiningHandoff _handoff;

        [SetUp]
        public void SetUp()
        {
            _def     = ScriptableObject.CreateInstance<AsteroidDefinition>();
            _handoff = new ActiveMiningHandoff();
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_def);

        [Test]
        public void HasResult_starts_false()
        {
            Assert.IsFalse(_handoff.HasResult);
        }

        [Test]
        public void AsteroidSlotId_starts_null()
        {
            Assert.IsNull(_handoff.AsteroidSlotId);
        }

        [Test]
        public void Begin_captures_everything_needed_to_resume_and_finalize()
        {
            _handoff.Begin("earth", "slot_3", _def, remainingYieldAtStart: 16,
                tapsRequired: 2, maxErrors: 3, sessionDurationSeconds: 6f);

            Assert.AreEqual("earth", _handoff.PlanetId);
            Assert.AreEqual("slot_3", _handoff.AsteroidSlotId);
            Assert.AreEqual(_def, _handoff.Definition);
            Assert.AreEqual(16, _handoff.RemainingYieldAtStart);
            Assert.AreEqual(2, _handoff.TapsRequired);
            Assert.AreEqual(3, _handoff.MaxErrors);
            Assert.AreEqual(6f, _handoff.SessionDurationSeconds, 0.001f);
            Assert.IsFalse(_handoff.HasResult);
        }

        [Test]
        public void SetResult_records_the_outcome()
        {
            _handoff.Begin("earth", "slot_0", _def, 10, 2, 3, 6f);

            _handoff.SetResult(succeeded: true);

            Assert.IsTrue(_handoff.HasResult);
            Assert.IsTrue(_handoff.Succeeded);
        }

        [Test]
        public void Clear_resets_result_and_slot_tracking()
        {
            _handoff.Begin("earth", "slot_0", _def, 10, 2, 3, 6f);
            _handoff.SetResult(succeeded: false);

            _handoff.Clear();

            Assert.IsFalse(_handoff.HasResult);
            Assert.IsNull(_handoff.AsteroidSlotId);
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `"C:\Program Files\Unity\Hub\Editor\6000.3.12f1\Editor\Unity.exe" -batchmode -runTests -projectPath . -testResults task2-red.xml -testPlatform EditMode -logFile task2-red.log`
Expected: FAIL — compile error, `ActiveMiningHandoff` does not exist in `SocialUniverse.Core`.

- [ ] **Step 3: Implement `ActiveMiningHandoff`**

Create `Assets/_Project/Scripts/Core/ActiveMiningHandoff.cs`:

```csharp
using SocialUniverse.Config;

namespace SocialUniverse.Core
{
    // Carries an active-mining session's reward data across the Planet -> ActiveMining ->
    // Planet scene swap. MiningController/IEconomyService/AsteroidSpawner all live inside
    // PlanetSceneScope and are destroyed the moment Planet unloads, so this Root-level
    // singleton (registered in ProjectLifetimeScope) is the only thing that survives the
    // round trip. Deliberately holds no reference to Asteroid/MiningReward (Mining-layer
    // types) — Core must never depend on Mining — callers pass already-unpacked values.
    public class ActiveMiningHandoff
    {
        public string             PlanetId               { get; private set; }
        public string             AsteroidSlotId         { get; private set; }
        public AsteroidDefinition Definition             { get; private set; }
        public int                RemainingYieldAtStart  { get; private set; }
        public int                TapsRequired           { get; private set; }
        public int                MaxErrors              { get; private set; }
        public float              SessionDurationSeconds { get; private set; }

        public bool HasResult { get; private set; }
        public bool Succeeded { get; private set; }

        public void Begin(string planetId, string asteroidSlotId, AsteroidDefinition definition,
            int remainingYieldAtStart, int tapsRequired, int maxErrors, float sessionDurationSeconds)
        {
            PlanetId               = planetId;
            AsteroidSlotId         = asteroidSlotId;
            Definition             = definition;
            RemainingYieldAtStart  = remainingYieldAtStart;
            TapsRequired           = tapsRequired;
            MaxErrors              = maxErrors;
            SessionDurationSeconds = sessionDurationSeconds;
            HasResult = false;
        }

        public void SetResult(bool succeeded)
        {
            HasResult = true;
            Succeeded = succeeded;
        }

        public void Clear()
        {
            PlanetId       = null;
            AsteroidSlotId = null;
            Definition     = null;
            HasResult      = false;
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `"C:\Program Files\Unity\Hub\Editor\6000.3.12f1\Editor\Unity.exe" -batchmode -runTests -projectPath . -testResults task2-green.xml -testPlatform EditMode -logFile task2-green.log`
Expected: all 5 `ActiveMiningHandoffTests` pass.

- [ ] **Step 5: Commit**

```bash
git add Assets/_Project/Scripts/Core/ActiveMiningHandoff.cs Assets/_Project/Tests/EditMode/Core/ActiveMiningHandoffTests.cs
git commit -m "core: add ActiveMiningHandoff to carry reward data across the Planet/ActiveMining scene swap"
```

---

### Task 3: Rewrite `MiningController`'s active-mining surface

Replaces the local `ActiveMiningMinigame`-owned session with handoff-populate-and-later-finalize. `BeginActiveMining` now just validates + computes the reward + populates the handoff. A new private `TryFinalizePendingActiveMining`, called from `Initialize` (same spot idle-session restore already runs), resolves the asteroid back via `AsteroidSpawner.FindBySlotId` and finishes the grant/respawn flow.

**Files:**
- Modify: `Assets/_Project/Scripts/Mining/MiningController.cs`
- Modify: `Assets/_Project/Tests/EditMode/Mining/MiningControllerTests.cs`

**Interfaces:**
- Consumes: `ActiveMiningHandoff` (Task 2) — `Begin(...)`, `HasResult`, `Succeeded`, `AsteroidSlotId`, `Clear()`.
- Produces: `MiningController(IEconomyService, MiningRewardCalculator, AsteroidSpawner, EconomyConfig, PlanetDefinition, ActiveMiningHandoff)` — the new constructor shape every caller (production DI in Task 4, tests here) must use. `bool BeginActiveMining(Asteroid asteroid)` (unchanged return type/name, new internal behavior). Removes `CurrentActiveSession`, `OnActiveSessionChanged`, `TickActiveSession`, `RegisterActiveTap`.

- [ ] **Step 1: Update the test file's `SetUp`/helper and the active-mining tests (won't compile until Step 3)**

In `Assets/_Project/Tests/EditMode/Mining/MiningControllerTests.cs`, add `using SocialUniverse.Core;` to the usings block at the top (alongside the existing `using SocialUniverse.Config;` etc).

Replace the field declarations and `SetUp`:

```csharp
        private EconomyConfig          _config;
        private AsteroidDefinition     _asteroidDef;
        private PlanetDefinition       _planet;
        private Wallet                 _wallet;
        private LocalMockEconomy       _economy;
        private MiningRewardCalculator _rewardCalc;
        private ActiveMiningHandoff    _handoff;
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
            SetField(_config, "_asteroidRespawnHours", 4f);

            _asteroidDef = ScriptableObject.CreateInstance<AsteroidDefinition>();
            SetField(_asteroidDef, "_coinsPerUnit", 2);

            _planet = ScriptableObject.CreateInstance<PlanetDefinition>();
            SetField(_planet, "_planetId", "test_planet");

            _wallet     = new Wallet();
            _economy    = new LocalMockEconomy(_wallet, _config);
            _rewardCalc = new MiningRewardCalculator(_config);
            _handoff    = new ActiveMiningHandoff();

            var spawnerGo = new GameObject("TestSpawner");
            _spawner = spawnerGo.AddComponent<AsteroidSpawner>();

            _mining = new MiningController(_economy, _rewardCalc, _spawner, _config, _planet, _handoff);
        }
```

Replace `BeginActiveMining_does_not_require_the_drone_and_can_run_alongside_an_idle_session`:

```csharp
        [Test]
        public void BeginActiveMining_does_not_require_the_drone_and_can_run_alongside_an_idle_session()
        {
            var idleAsteroid   = MakeAndRegisterAsteroid("slot_0", 10);
            var activeAsteroid = MakeAndRegisterAsteroid("slot_1", 10);

            Assert.IsTrue(_mining.BeginIdleMining(idleAsteroid));
            Assert.IsTrue(_mining.BeginActiveMining(activeAsteroid));

            Assert.IsNotNull(_mining.CurrentIdleSession);
            Assert.AreEqual("slot_1", _handoff.AsteroidSlotId);
        }
```

Replace `BeginActiveMining_fails_when_the_same_asteroid_already_has_an_idle_mining_session`:

```csharp
        [Test]
        public void BeginActiveMining_fails_when_the_same_asteroid_already_has_an_idle_mining_session()
        {
            var asteroid = MakeAndRegisterAsteroid("slot_0", 10);

            Assert.IsTrue(_mining.BeginIdleMining(asteroid));
            Assert.IsFalse(_mining.BeginActiveMining(asteroid));
            Assert.IsNull(_handoff.AsteroidSlotId);
        }
```

Add a new test right after it:

```csharp
        [Test]
        public void BeginActiveMining_fails_while_a_previous_active_mining_result_is_still_pending_finalize()
        {
            var a1 = MakeAndRegisterAsteroid("slot_0", 10);
            var a2 = MakeAndRegisterAsteroid("slot_1", 10);

            Assert.IsTrue(_mining.BeginActiveMining(a1));
            Assert.IsFalse(_mining.BeginActiveMining(a2));
        }
```

Replace `Active_mining_success_grants_full_yield` and `Active_mining_failure_grants_nothing_and_clears_the_session` (delete both) with:

```csharp
        [Test]
        public async Task Initialize_finalizes_a_pending_active_mining_success()
        {
            var asteroid = MakeAndRegisterAsteroid("slot_0", remainingYield: 10);
            Assert.IsTrue(_mining.BeginActiveMining(asteroid));
            _handoff.SetResult(succeeded: true);
            int coinsBefore = _wallet.Coins;

            var droneDef = ScriptableObject.CreateInstance<DroneDefinition>();
            _mining.Initialize(new DroneRuntime(droneDef));
            await Task.Yield(); // let the fire-and-forget payout Task complete

            Assert.AreEqual(coinsBefore + 20, _wallet.Coins); // 10 yield * 2 coins/unit
            Assert.IsTrue(asteroid.IsDepleted);
            Assert.IsNull(_handoff.AsteroidSlotId, "handoff must be cleared after finalizing");

            Object.DestroyImmediate(droneDef);
        }

        [Test]
        public void Initialize_finalizes_a_pending_active_mining_failure()
        {
            var asteroid = MakeAndRegisterAsteroid("slot_0", remainingYield: 10);
            Assert.IsTrue(_mining.BeginActiveMining(asteroid));
            _handoff.SetResult(succeeded: false);
            int coinsBefore = _wallet.Coins;

            var droneDef = ScriptableObject.CreateInstance<DroneDefinition>();
            _mining.Initialize(new DroneRuntime(droneDef));

            Assert.AreEqual(coinsBefore, _wallet.Coins);
            Assert.IsTrue(asteroid.IsDepleted, "a failed asteroid is still consumed with zero payout");
            Assert.IsNull(_handoff.AsteroidSlotId);

            Object.DestroyImmediate(droneDef);
        }

        [Test]
        public void Initialize_clears_a_pending_result_without_throwing_when_the_asteroid_no_longer_resolves()
        {
            var asteroid = MakeAndRegisterAsteroid("slot_0", remainingYield: 10);
            Assert.IsTrue(_mining.BeginActiveMining(asteroid));
            _handoff.SetResult(succeeded: true);

            var active = (List<Asteroid>)typeof(AsteroidSpawner)
                .GetField("_active", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(_spawner);
            active.Remove(asteroid); // simulate the slot no longer resolving (e.g. already respawned)

            var droneDef = ScriptableObject.CreateInstance<DroneDefinition>();
            Assert.DoesNotThrow(() => _mining.Initialize(new DroneRuntime(droneDef)));

            Assert.IsNull(_handoff.AsteroidSlotId);

            Object.DestroyImmediate(droneDef);
        }

        [Test]
        public void Initialize_does_nothing_when_no_active_mining_result_is_pending()
        {
            int coinsBefore = _wallet.Coins;
            var droneDef = ScriptableObject.CreateInstance<DroneDefinition>();

            Assert.DoesNotThrow(() => _mining.Initialize(new DroneRuntime(droneDef)));

            Assert.AreEqual(coinsBefore, _wallet.Coins);

            Object.DestroyImmediate(droneDef);
        }
```

Update `ClaimIdleSessionAsync_still_schedules_respawn_when_the_grant_call_throws`'s `MiningController` construction line:

```csharp
            var mining = new MiningController(throwingEconomy, _rewardCalc, _spawner, _config, _planet, _handoff);
```

(replacing the old `new MiningController(throwingEconomy, _rewardCalc, _activeMinigame, _spawner, _config, _planet)` line — everything else in that test is unchanged.)

- [ ] **Step 2: Run tests to verify they fail**

Run: `"C:\Program Files\Unity\Hub\Editor\6000.3.12f1\Editor\Unity.exe" -batchmode -runTests -projectPath . -testResults task3-red.xml -testPlatform EditMode -logFile task3-red.log`
Expected: FAIL — compile error, `MiningController` has no 6-argument constructor matching `(IEconomyService, MiningRewardCalculator, AsteroidSpawner, EconomyConfig, PlanetDefinition, ActiveMiningHandoff)` yet, and no `ActiveMiningHandoff`-typed 6th argument accepted (current constructor's 3rd argument is `ActiveMiningMinigame`).

- [ ] **Step 3: Rewrite the production class's active-mining surface**

In `Assets/_Project/Scripts/Mining/MiningController.cs`, replace the constructor and fields:

```csharp
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
```

with:

```csharp
        private readonly IEconomyService        _economy;
        private readonly MiningRewardCalculator  _rewardCalc;
        private readonly AsteroidSpawner         _spawner;
        private readonly EconomyConfig           _config;
        private readonly PlanetDefinition        _planet;
        private readonly ActiveMiningHandoff     _handoff;

        public DroneRuntime Drone { get; private set; }

        public IdleMiningSession CurrentIdleSession { get; private set; }
        public Asteroid          ClaimingAsteroid    { get; private set; }

        public event Action<IdleMiningSession> OnIdleSessionChanged;

        public MiningController(IEconomyService economy, MiningRewardCalculator rewardCalc,
            AsteroidSpawner spawner, EconomyConfig config, PlanetDefinition planet, ActiveMiningHandoff handoff)
        {
            _economy    = economy;
            _rewardCalc = rewardCalc;
            _spawner    = spawner;
            _config     = config;
            _planet     = planet;
            _handoff    = handoff;
        }
```

(Note: this file already has `using SocialUniverse.Core;` — no new using needed for `ActiveMiningHandoff`.)

Update the guard in `BeginIdleMining` — replace:

```csharp
        public bool BeginIdleMining(Asteroid asteroid)
        {
            if (asteroid == null || asteroid.IsDepleted || CurrentIdleSession != null ||
                (_activeMinigame.CurrentSession != null && _activeMinigame.CurrentSession.Asteroid == asteroid))
                return false;
```

with:

```csharp
        public bool BeginIdleMining(Asteroid asteroid)
        {
            if (asteroid == null || asteroid.IsDepleted || CurrentIdleSession != null ||
                (_handoff.AsteroidSlotId != null && _handoff.AsteroidSlotId == asteroid.SlotId))
                return false;
```

Replace the entire "Active mining" section (`BeginActiveMining` through `FailActiveMining`):

```csharp
        // ---- Active mining ----

        public bool BeginActiveMining(Asteroid asteroid)
        {
            if (CurrentIdleSession != null && CurrentIdleSession.Asteroid == asteroid)
                return false;

            return _activeMinigame.Begin(asteroid);
        }

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
                try
                {
                    int granted = await _economy.GrantMiningRewardAsync(coins, reward.IdleDurationSeconds, reward.CoinsPerSec);
                    SULog.Info($"Active mining success: +{mined} {asteroid.Definition.MineralType} -> {granted} coins", SULog.Channel.Mining);
                }
                catch (Exception ex)
                {
                    // Same reasoning as ClaimIdleSessionAsync: the asteroid is already mined-out
                    // (intentional, for re-entrancy) — if the grant throws, the player loses the
                    // coins, but the asteroid must still respawn below instead of being stranded.
                    SULog.Error($"GrantMiningRewardAsync failed for active-mining success on {asteroid.Definition.MineralType} ({coins} coins): {ex.Message}", SULog.Channel.Mining);
                }
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
```

with:

```csharp
        // ---- Active mining ----

        // Validates and computes the reward, then hands off to ActiveMiningHandoff — the
        // actual minigame (timer/taps) now runs entirely inside the ActiveMining scene, which
        // this MiningController instance won't exist for (it's destroyed along with Planet).
        // The caller (MiningModePromptView, via ActiveMiningRequestedEvent) is responsible for
        // triggering the FSM transition once this returns true.
        public bool BeginActiveMining(Asteroid asteroid)
        {
            if (asteroid == null || asteroid.IsDepleted) return false;
            if (CurrentIdleSession != null && CurrentIdleSession.Asteroid == asteroid) return false;
            if (_handoff.AsteroidSlotId != null) return false; // a previous result is still pending finalize

            var reward = _rewardCalc.Compute(asteroid);
            _handoff.Begin(_planet.PlanetId, asteroid.SlotId, asteroid.Definition, asteroid.RemainingYield,
                reward.ActiveTapsRequired, _config.ActiveMaxErrors, reward.ActiveSessionDurationSeconds);
            return true;
        }

        // Called from Initialize (same spot idle-session restore already runs) once Planet has
        // reloaded after an active-mining round trip. Resolves the asteroid back by SlotId
        // (same tolerance TryRestoreIdleSession already has: if the slot no longer resolves,
        // silently drop it rather than throwing) and finishes the grant/respawn flow.
        private void TryFinalizePendingActiveMining()
        {
            if (!_handoff.HasResult) return;

            var asteroid = _spawner.FindBySlotId(_handoff.AsteroidSlotId);
            if (asteroid != null)
            {
                if (_handoff.Succeeded) _ = CompleteActiveMiningAsync(asteroid);
                else                     FailActiveMining(asteroid);
            }

            _handoff.Clear();
        }

        private async Task CompleteActiveMiningAsync(Asteroid asteroid)
        {
            var reward = _rewardCalc.Compute(asteroid);

            int mined = asteroid.Mine(asteroid.RemainingYield);
            int coins = mined * asteroid.Definition.CoinsPerUnit;

            if (coins > 0)
            {
                try
                {
                    int granted = await _economy.GrantMiningRewardAsync(coins, reward.IdleDurationSeconds, reward.CoinsPerSec);
                    SULog.Info($"Active mining success: +{mined} {asteroid.Definition.MineralType} -> {granted} coins", SULog.Channel.Mining);
                }
                catch (Exception ex)
                {
                    // Same reasoning as ClaimIdleSessionAsync: the asteroid is already mined-out
                    // (intentional, for re-entrancy) — if the grant throws, the player loses the
                    // coins, but the asteroid must still respawn below instead of being stranded.
                    SULog.Error($"GrantMiningRewardAsync failed for active-mining success on {asteroid.Definition.MineralType} ({coins} coins): {ex.Message}", SULog.Channel.Mining);
                }
            }

            _spawner.ScheduleRespawn(asteroid, _config.AsteroidRespawnHours);
        }

        private void FailActiveMining(Asteroid asteroid)
        {
            asteroid.Mine(asteroid.RemainingYield);

            SULog.Info($"Active mining failed on {asteroid.name} — asteroid lost", SULog.Channel.Mining);
            _spawner.ScheduleRespawn(asteroid, _config.AsteroidRespawnHours);
        }
```

Finally, wire the finalize step into `Initialize` — replace:

```csharp
        public void Initialize(DroneRuntime drone)
        {
            Drone = drone;
            TryRestoreIdleSession();
        }
```

with:

```csharp
        public void Initialize(DroneRuntime drone)
        {
            Drone = drone;
            TryRestoreIdleSession();
            TryFinalizePendingActiveMining();
        }
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `"C:\Program Files\Unity\Hub\Editor\6000.3.12f1\Editor\Unity.exe" -batchmode -runTests -projectPath . -testResults task3-green.xml -testPlatform EditMode -logFile task3-green.log`
Expected: all `MiningControllerTests` pass. `ActiveMiningMinigameTests.cs` and `PlanetSceneScope.cs` will still fail to compile (they reference `ActiveMiningMinigame`/old `MiningController` API) — fixed in Task 4. Confirm the log shows only those known, expected failures.

- [ ] **Step 5: Commit**

```bash
git add Assets/_Project/Scripts/Mining/MiningController.cs Assets/_Project/Tests/EditMode/Mining/MiningControllerTests.cs
git commit -m "mining: MiningController hands off active-mining to ActiveMiningHandoff instead of owning a live session"
```

---

### Task 4: Retire the old additive-overlay machinery

Deletes the three classes replaced by the handoff design, cleans up `PlanetSceneScope`'s registrations, and — since this task touches `PlanetSceneScope.cs`'s Mining/UI registration block anyway — finally removes the stale `ActiveMiningOverlay` GameObject left behind in `Planet.unity` from before this scene-swap redesign even started (it was never cleaned up after the previous plan's scene-based rewrite).

**Files:**
- Delete: `Assets/_Project/Scripts/Mining/ActiveMiningMinigame.cs`
- Delete: `Assets/_Project/Tests/EditMode/Mining/ActiveMiningMinigameTests.cs`
- Delete: `Assets/_Project/Scripts/Mining/ActiveMiningSessionController.cs`
- Delete: `Assets/_Project/Scripts/Mining/ActiveMiningSceneController.cs`
- Modify: `Assets/_Project/Scripts/App/PlanetSceneScope.cs`
- Modify: `Assets/Scenes/Planet.unity`

**Interfaces:**
- Consumes: nothing new — this task only removes dead surface introduced/superseded by Tasks 2–3.

- [ ] **Step 1: Delete the four files**

```bash
git rm Assets/_Project/Scripts/Mining/ActiveMiningMinigame.cs
git rm Assets/_Project/Tests/EditMode/Mining/ActiveMiningMinigameTests.cs
git rm Assets/_Project/Scripts/Mining/ActiveMiningSessionController.cs
git rm Assets/_Project/Scripts/Mining/ActiveMiningSceneController.cs
```

- [ ] **Step 2: Update `PlanetSceneScope.cs`**

Remove the `ActiveMiningMinigame` registration — in the "Mining" section, replace:

```csharp
            // Mining
            builder.Register<MiningRewardCalculator>(Lifetime.Singleton);
            builder.Register<ActiveMiningMinigame>(Lifetime.Singleton);
            builder.Register<MiningController>(Lifetime.Singleton);
```

with:

```csharp
            // Mining
            builder.Register<MiningRewardCalculator>(Lifetime.Singleton);
            builder.Register<MiningController>(Lifetime.Singleton);
```

Remove the now-dead `ActiveMiningMinigameView` registration (it only ever lives in the ActiveMining scene now) — in the "UI" section, replace:

```csharp
            // UI
            builder.RegisterComponentInHierarchy<SocialUniverse.UI.HUDController>();
            builder.RegisterComponentInHierarchy<SocialUniverse.UI.MiningModePromptView>();
            builder.RegisterComponentInHierarchy<SocialUniverse.UI.ActiveMiningMinigameView>();
            builder.RegisterComponentInHierarchy<SocialUniverse.UI.SocialDebugPanel>();
```

with:

```csharp
            // UI
            builder.RegisterComponentInHierarchy<SocialUniverse.UI.HUDController>();
            builder.RegisterComponentInHierarchy<SocialUniverse.UI.MiningModePromptView>();
            builder.RegisterComponentInHierarchy<SocialUniverse.UI.SocialDebugPanel>();
```

Remove the two dead entry-point registrations — replace:

```csharp
            builder.RegisterEntryPoint<PlanetSceneBootstrapper>();
            builder.RegisterEntryPoint<IdleMiningSessionController>();
            builder.RegisterEntryPoint<ActiveMiningSessionController>();
            builder.RegisterEntryPoint<ActiveMiningSceneController>();
            builder.RegisterEntryPoint<TilePurchaseHandler>();
```

with:

```csharp
            builder.RegisterEntryPoint<PlanetSceneBootstrapper>();
            builder.RegisterEntryPoint<IdleMiningSessionController>();
            builder.RegisterEntryPoint<TilePurchaseHandler>();
```

Finally, register `ActiveMiningHandoff` for the standalone (no-Bootstrap, open-`Planet.unity`-directly) dev path — `MiningController` now depends on it unconditionally, and standalone mode has no `RootLifetimeScope` to provide it from. In the `if (standalone)` block, replace:

```csharp
            if (standalone)
            {
                builder.Register<SceneLoader>(Lifetime.Singleton);
                builder.Register<NetworkBootstrap>(Lifetime.Singleton).AsImplementedInterfaces();
```

with:

```csharp
            if (standalone)
            {
                builder.Register<SceneLoader>(Lifetime.Singleton);
                builder.Register<ActiveMiningHandoff>(Lifetime.Singleton);
                builder.Register<NetworkBootstrap>(Lifetime.Singleton).AsImplementedInterfaces();
```

(`PlanetSceneScope.cs` already has `using SocialUniverse.Core;` — no new using needed.)

- [ ] **Step 3: Delete the stale `ActiveMiningOverlay` subtree from `Planet.unity`**

This GameObject (and its 4 children: MissArea, TargetPoint, ProgressText, ErrorText) was the pre-scene-swap 2D overlay, left inactive and unused since an earlier redesign — it carries a stale `ActiveMiningMinigameView` component reference that no longer matches that class's fields.

In `Assets/Scenes/Planet.unity`:

1. Delete every line from `--- !u!1 &9000000001` (the `GameObject: ... m_Name: ActiveMiningOverlay` block) through the end of the file's last real object before the `SceneRoots` pseudo-object — i.e. delete the entire contiguous block that starts at the line `--- !u!1 &9000000001` and ends at the line immediately before `--- !u!1660057539 &9223372036854775807` (the `SceneRoots` entry, which must NOT be touched).
2. In the Canvas's RectTransform block (`--- !u!224 &2027062701`), remove the line `  - {fileID: 9000000002}` from its `m_Children:` list (it's the last entry in that list — the reference to `ActiveMiningOverlay`'s own RectTransform, which no longer exists after step 1).

- [ ] **Step 4: Run tests to verify everything compiles and passes**

Run: `"C:\Program Files\Unity\Hub\Editor\6000.3.12f1\Editor\Unity.exe" -batchmode -runTests -projectPath . -testResults task4-green.xml -testPlatform EditMode -logFile task4-green.log`
Expected: full EditMode suite passes, no compile errors anywhere (this was the last file referencing the now-deleted classes).

- [ ] **Step 5: Commit**

```bash
git add -u Assets/_Project/Scripts/Mining/ActiveMiningMinigame.cs Assets/_Project/Tests/EditMode/Mining/ActiveMiningMinigameTests.cs Assets/_Project/Scripts/Mining/ActiveMiningSessionController.cs Assets/_Project/Scripts/Mining/ActiveMiningSceneController.cs
git add Assets/_Project/Scripts/App/PlanetSceneScope.cs Assets/Scenes/Planet.unity
git commit -m "mining: retire the additive-overlay machinery, clean up the stale ActiveMiningOverlay in Planet.unity"
```

---

### Task 5: Core FSM — `ActiveMiningState` + Root registrations + `PlanetState.EnterActiveMining`

**Files:**
- Create: `Assets/_Project/Scripts/Core/ActiveMiningState.cs`
- Modify: `Assets/_Project/Scripts/Core/ProjectLifetimeScope.cs`
- Modify: `Assets/_Project/Scripts/Core/PlanetState.cs`

**Interfaces:**
- Consumes: `ActiveMiningHandoff` (Task 2), `Constants.SceneNames.ActiveMining`/`LoadingScreen` (existing), `SceneLoader`/`GameStateMachine` (existing).
- Produces: `ActiveMiningState.Finish()` (called by `ActiveMiningMinigameView`'s Continue button in Task 9), `PlanetState.EnterActiveMining()` (called by `ActiveMiningRequestHandler` in Task 6).

No unit tests for this task — matches the existing precedent that `PlanetState`/`TravelState`/`HubState` (scene-orchestration FSM classes) have no test coverage in this codebase.

- [ ] **Step 1: Create `ActiveMiningState`**

Create `Assets/_Project/Scripts/Core/ActiveMiningState.cs`:

```csharp
using System.Threading.Tasks;
using UnityEngine.SceneManagement;
using VContainer;

namespace SocialUniverse.Core
{
    // Owns the ActiveMining scene as the sole running gameplay scene — mirrors TravelState's
    // shape. Entered from PlanetState.EnterActiveMining() once MiningController.BeginActiveMining
    // has populated ActiveMiningHandoff; Planet is unloaded via PlanetState.Exit() before this
    // state's Enter() runs (GameStateMachine.TransitionTo calls Exit() then Enter()).
    public class ActiveMiningState : IGameState
    {
        private readonly SceneLoader        _sceneLoader;
        private readonly GameStateMachine    _fsm;
        private readonly IObjectResolver     _resolver;
        private readonly ActiveMiningHandoff _handoff;

        public ActiveMiningState(SceneLoader sceneLoader, GameStateMachine fsm, IObjectResolver resolver, ActiveMiningHandoff handoff)
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
            SULog.Info("ActiveMining: entering");
            await _sceneLoader.LoadAsync(Constants.SceneNames.LoadingScreen);
            await _sceneLoader.LoadAsync(Constants.SceneNames.ActiveMining);
        }

        private async Task UnloadAsync()
        {
            await _sceneLoader.UnloadAsync(Constants.SceneNames.ActiveMining);
            var ls = SceneManager.GetSceneByName(Constants.SceneNames.LoadingScreen);
            if (ls.IsValid() && ls.isLoaded)
                await _sceneLoader.UnloadAsync(Constants.SceneNames.LoadingScreen);
        }

        // Called by ActiveMiningMinigameView's Continue button once the session has resolved
        // (Success or Failed) and the reward preview has been shown. Hands control back to
        // Planet, which re-resolves the asteroid by SlotId and finalizes the reward server-side
        // (see MiningController.Initialize -> TryFinalizePendingActiveMining).
        public void Finish()
        {
            var planetState = _resolver.Resolve<PlanetState>();
            planetState.TargetPlanetId = _handoff.PlanetId;
            _fsm.TransitionTo(planetState);
        }
    }
}
```

- [ ] **Step 2: Register it (and `ActiveMiningHandoff`) in `ProjectLifetimeScope`**

In `Assets/_Project/Scripts/Core/ProjectLifetimeScope.cs`, replace:

```csharp
            builder.Register<BootState>(Lifetime.Singleton);
            builder.Register<AuthState>(Lifetime.Singleton);
            builder.Register<HubState>(Lifetime.Singleton);
            builder.Register<TravelState>(Lifetime.Singleton);
            builder.Register<TravelLoadingState>(Lifetime.Singleton);
            builder.Register<PlanetState>(Lifetime.Singleton);
```

with:

```csharp
            builder.Register<BootState>(Lifetime.Singleton);
            builder.Register<AuthState>(Lifetime.Singleton);
            builder.Register<HubState>(Lifetime.Singleton);
            builder.Register<TravelState>(Lifetime.Singleton);
            builder.Register<TravelLoadingState>(Lifetime.Singleton);
            builder.Register<PlanetState>(Lifetime.Singleton);
            builder.Register<ActiveMiningState>(Lifetime.Singleton);

            builder.Register<ActiveMiningHandoff>(Lifetime.Singleton);
```

- [ ] **Step 3: Add `PlanetState.EnterActiveMining()`**

In `Assets/_Project/Scripts/Core/PlanetState.cs`, add this method right after `ReturnToHub()`:

```csharp
        public void ReturnToHub() => _fsm.TransitionTo(_resolver.Resolve<HubState>());

        // Called by ActiveMiningRequestHandler once MiningController.BeginActiveMining has
        // populated ActiveMiningHandoff — transitions the FSM to ActiveMiningState, which loads
        // the minigame scene as the sole running gameplay scene (Exit() below unloads Planet).
        public void EnterActiveMining() => _fsm.TransitionTo(_resolver.Resolve<ActiveMiningState>());
```

- [ ] **Step 4: Run tests to verify everything still compiles and passes**

Run: `"C:\Program Files\Unity\Hub\Editor\6000.3.12f1\Editor\Unity.exe" -batchmode -runTests -projectPath . -testResults task5-green.xml -testPlatform EditMode -logFile task5-green.log`
Expected: full EditMode suite passes (no new tests added this task; this confirms no compile regressions).

- [ ] **Step 5: Commit**

```bash
git add Assets/_Project/Scripts/Core/ActiveMiningState.cs Assets/_Project/Scripts/Core/ProjectLifetimeScope.cs Assets/_Project/Scripts/Core/PlanetState.cs
git commit -m "core: add ActiveMiningState FSM state and PlanetState.EnterActiveMining"
```

---

### Task 6: Wire the Planet-side trigger — `ActiveMiningRequestedEvent` + handler + `MiningModePromptView`

Mirrors `LaunchRequestedEvent`/`LaunchButtonHandler` exactly, so the standalone (no-Bootstrap) `Planet.unity` dev workflow keeps working: `MiningModePromptView` never injects `PlanetState` directly (it isn't registered in standalone mode), it just publishes an event that only does something when a real `PlanetState` exists to react to it.

**Files:**
- Create: `Assets/_Project/Scripts/Core/ActiveMiningRequestedEvent.cs`
- Create: `Assets/_Project/Scripts/App/ActiveMiningRequestHandler.cs`
- Modify: `Assets/_Project/Scripts/UI/MiningModePromptView.cs`
- Modify: `Assets/_Project/Scripts/App/PlanetSceneScope.cs`

**Interfaces:**
- Consumes: `MiningController.BeginActiveMining` (Task 3), `PlanetState.EnterActiveMining` (Task 5).
- Produces: nothing new consumed by later tasks.

No unit tests for `ActiveMiningRequestHandler` — matches `LaunchButtonHandler`'s existing precedent (thin EventBus-to-FSM glue, untested).

- [ ] **Step 1: Add the event**

Create `Assets/_Project/Scripts/Core/ActiveMiningRequestedEvent.cs`:

```csharp
namespace SocialUniverse.Core
{
    // Published by MiningModePromptView once MiningController.BeginActiveMining has populated
    // ActiveMiningHandoff. Indirected through the event bus (rather than MiningModePromptView
    // injecting PlanetState directly) so Planet's standalone/no-Bootstrap dev mode — which never
    // registers PlanetState — doesn't break; same reasoning as LaunchRequestedEvent.
    public class ActiveMiningRequestedEvent { }
}
```

- [ ] **Step 2: Add the handler**

Create `Assets/_Project/Scripts/App/ActiveMiningRequestHandler.cs`:

```csharp
using System;
using VContainer.Unity;
using SocialUniverse.Core;

namespace SocialUniverse.App
{
    public class ActiveMiningRequestHandler : IStartable, IDisposable
    {
        private readonly PlanetState _planetState;

        public ActiveMiningRequestHandler(PlanetState planetState) => _planetState = planetState;

        public void Start()   => EventBus.Subscribe<ActiveMiningRequestedEvent>(OnActiveMiningRequested);
        public void Dispose() => EventBus.Unsubscribe<ActiveMiningRequestedEvent>(OnActiveMiningRequested);

        private void OnActiveMiningRequested(ActiveMiningRequestedEvent e) => _planetState.EnterActiveMining();
    }
}
```

- [ ] **Step 3: Register it alongside `LaunchButtonHandler`**

In `Assets/_Project/Scripts/App/PlanetSceneScope.cs`, replace:

```csharp
            if (parentPlanetState != null)
            {
                builder.RegisterInstance(parentPlanetState);
                builder.RegisterEntryPoint<LaunchButtonHandler>();
            }
```

with:

```csharp
            if (parentPlanetState != null)
            {
                builder.RegisterInstance(parentPlanetState);
                builder.RegisterEntryPoint<LaunchButtonHandler>();
                builder.RegisterEntryPoint<ActiveMiningRequestHandler>();
            }
```

- [ ] **Step 4: Rewire `MiningModePromptView`**

`Assets/_Project/Scripts/UI/MiningModePromptView.cs` already has `using SocialUniverse.Core;` (needed for the existing `EventBus.Subscribe` call) — no using changes needed.

Replace `OnAsteroidSelected`:

```csharp
        private void OnAsteroidSelected(AsteroidSelectedEvent e)
        {
            var asteroid = e.Asteroid;
            if (asteroid == null || asteroid.IsDepleted) return;
            if (_mining.CurrentIdleSession   != null && _mining.CurrentIdleSession.Asteroid   == asteroid) return; // this asteroid is already idle-mining
            if (_mining.CurrentActiveSession != null && _mining.CurrentActiveSession.Asteroid == asteroid) return; // this asteroid already has an active-mining minigame running
            if (_mining.ClaimingAsteroid == asteroid) return; // final claim tap just completed

            _pendingAsteroid = asteroid;
            if (_titleText != null)
                _titleText.text = $"Mine {asteroid.Definition.MineralType}?";

            if (_root != null) _root.SetActive(true);
        }
```

with:

```csharp
        private void OnAsteroidSelected(AsteroidSelectedEvent e)
        {
            var asteroid = e.Asteroid;
            if (asteroid == null || asteroid.IsDepleted) return;
            if (_mining.CurrentIdleSession != null && _mining.CurrentIdleSession.Asteroid == asteroid) return; // this asteroid is already idle-mining
            if (_mining.ClaimingAsteroid == asteroid) return; // final claim tap just completed

            _pendingAsteroid = asteroid;
            if (_titleText != null)
                _titleText.text = $"Mine {asteroid.Definition.MineralType}?";

            if (_root != null) _root.SetActive(true);
        }
```

Replace `OnActiveMineClicked`:

```csharp
        private void OnActiveMineClicked()
        {
            if (_pendingAsteroid != null)
                _mining.BeginActiveMining(_pendingAsteroid);

            ClosePrompt();
        }
```

with:

```csharp
        private void OnActiveMineClicked()
        {
            if (_pendingAsteroid != null && _mining.BeginActiveMining(_pendingAsteroid))
                EventBus.Publish(new ActiveMiningRequestedEvent());

            ClosePrompt();
        }
```

- [ ] **Step 5: Run tests to verify everything compiles and passes**

Run: `"C:\Program Files\Unity\Hub\Editor\6000.3.12f1\Editor\Unity.exe" -batchmode -runTests -projectPath . -testResults task6-green.xml -testPlatform EditMode -logFile task6-green.log`
Expected: full EditMode suite passes.

- [ ] **Step 6: Commit**

```bash
git add Assets/_Project/Scripts/Core/ActiveMiningRequestedEvent.cs Assets/_Project/Scripts/App/ActiveMiningRequestHandler.cs Assets/_Project/Scripts/UI/MiningModePromptView.cs Assets/_Project/Scripts/App/PlanetSceneScope.cs
git commit -m "mining: wire MiningModePromptView -> ActiveMiningRequestedEvent -> PlanetState.EnterActiveMining"
```

---

### Task 7: `ActiveMiningSessionRunner` — drives the local session inside the ActiveMining scene

Relocates the role `ActiveMiningSessionController` played in `PlanetSceneScope` (Tick the session's countdown every frame), now living inside `ActiveMiningSceneScope` since Planet is unloaded. Also writes the outcome back to the handoff when the session resolves.

**Files:**
- Create: `Assets/_Project/Scripts/Mining/ActiveMiningSessionRunner.cs`

**Interfaces:**
- Consumes: `ActiveMiningHandoff` (Task 2), `ActiveMiningSession` (Task 1).
- Produces: `ActiveMiningSessionRunner.Session` (`ActiveMiningSession`, non-null once `IStartable.Start()` has run), `BeginTicking()` — consumed by `ActiveMiningMinigameView` (Task 9) and registered in `ActiveMiningSceneScope` (Task 8).

No unit tests — matches `ActiveMiningSessionController`/`IdleMiningSessionController`'s existing precedent (thin `ITickable`/`IStartable` wiring classes have no dedicated tests in this codebase; the logic they drive — `ActiveMiningSession`, `ActiveMiningHandoff` — is already fully covered).

- [ ] **Step 1: Create the class**

Create `Assets/_Project/Scripts/Mining/ActiveMiningSessionRunner.cs`:

```csharp
using UnityEngine;
using VContainer.Unity;
using SocialUniverse.Core;

namespace SocialUniverse.Mining
{
    // Drives the local ActiveMiningSession's countdown once the player presses Start —
    // relocates the role ActiveMiningSessionController played in PlanetSceneScope, now living
    // inside ActiveMiningSceneScope since Planet is unloaded while this scene runs. Also writes
    // the outcome back into ActiveMiningHandoff so MiningController can finalize the reward once
    // Planet reloads.
    public class ActiveMiningSessionRunner : IStartable, ITickable
    {
        private readonly ActiveMiningHandoff _handoff;

        public ActiveMiningSession Session   { get; private set; }
        public bool                IsRunning { get; private set; }

        public ActiveMiningSessionRunner(ActiveMiningHandoff handoff) => _handoff = handoff;

        public void Start()
        {
            Session = new ActiveMiningSession(_handoff.TapsRequired, _handoff.MaxErrors, _handoff.SessionDurationSeconds);
            Session.OnStageChanged += OnStageChanged;
        }

        // Called once the player presses "Start Mining" in the pre-game panel — nothing spawns
        // or counts down before this, so there's no race between scene-load and the first target.
        public void BeginTicking() => IsRunning = true;

        public void Tick()
        {
            if (IsRunning) Session.Tick(Time.deltaTime);
        }

        private void OnStageChanged(ActiveMiningStage stage)
        {
            if (stage == ActiveMiningStage.Success) _handoff.SetResult(succeeded: true);
            else if (stage == ActiveMiningStage.Failed) _handoff.SetResult(succeeded: false);
        }
    }
}
```

- [ ] **Step 2: Run tests to verify everything still compiles and passes**

Run: `"C:\Program Files\Unity\Hub\Editor\6000.3.12f1\Editor\Unity.exe" -batchmode -runTests -projectPath . -testResults task7-green.xml -testPlatform EditMode -logFile task7-green.log`
Expected: full EditMode suite passes (new file adds no new tests; not yet registered anywhere, so this only confirms it compiles).

- [ ] **Step 3: Commit**

```bash
git add Assets/_Project/Scripts/Mining/ActiveMiningSessionRunner.cs
git commit -m "mining: add ActiveMiningSessionRunner to drive the session inside the ActiveMining scene"
```

---

### Task 8: Rewrite `ActiveMiningSceneScope`

Stops parenting to `PlanetSceneScope` (Planet is unloaded by the time this scene loads); registers `ActiveMiningSessionRunner`; the bootstrapper now spawns the asteroid clone from the handoff's `Definition` instead of a live session's `Asteroid`.

**Files:**
- Modify: `Assets/_Project/Scripts/App/ActiveMiningSceneScope.cs`

**Interfaces:**
- Consumes: `ActiveMiningHandoff` (Task 2), `ActiveMiningSessionRunner` (Task 7), `ActiveMiningAsteroidStage` (existing, unchanged).
- Produces: `ActiveMiningSessionRunner` resolvable via both entry-point dispatch (`IStartable`/`ITickable`) and direct `[Inject]` (needed by `ActiveMiningMinigameView` in Task 9) — registered via `.AsSelf().AsImplementedInterfaces()` rather than `RegisterEntryPoint<T>()`, since this codebase has no existing precedent proving `RegisterEntryPoint` also self-registers for direct injection, and this is the one entry point that also needs to be injected elsewhere.

- [ ] **Step 1: Rewrite the scope and bootstrapper**

Replace the entire contents of `Assets/_Project/Scripts/App/ActiveMiningSceneScope.cs`:

```csharp
using VContainer;
using VContainer.Unity;
using SocialUniverse.Mining;
using SocialUniverse.Core;

namespace SocialUniverse.App
{
    // Scene scope for the ActiveMining minigame — loaded by SocialUniverse.Core.ActiveMiningState
    // as the sole running gameplay scene (Planet is unloaded first, see ActiveMiningState).
    // Parents to RootLifetimeScope (parentReference.TypeName in the scene file), not to
    // PlanetSceneScope — nothing here needs Planet-scoped services (IEconomyService,
    // AsteroidSpawner, MiningController); everything needed comes from ActiveMiningHandoff, a
    // Root-level singleton that survives the scene swap.
    public class ActiveMiningSceneScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterComponentInHierarchy<ActiveMiningAsteroidStage>();
            builder.RegisterComponentInHierarchy<SocialUniverse.UI.ActiveMiningMinigameView>();

            // Registered as both an entry point (IStartable/ITickable) and directly injectable
            // (AsSelf) — ActiveMiningMinigameView needs to inject the concrete type to read
            // .Session, which RegisterEntryPoint alone doesn't guarantee.
            builder.Register<ActiveMiningSessionRunner>(Lifetime.Singleton).AsSelf().AsImplementedInterfaces();

            builder.RegisterEntryPoint<ActiveMiningSceneBootstrapper>();
        }
    }

    // Spawns the visual asteroid clone from the handoff's AsteroidDefinition as soon as this
    // scene finishes loading — the handoff was already populated back in Planet, before the
    // scene swap, by MiningController.BeginActiveMining.
    public class ActiveMiningSceneBootstrapper : IStartable
    {
        private readonly ActiveMiningHandoff       _handoff;
        private readonly ActiveMiningAsteroidStage _stage;

        public ActiveMiningSceneBootstrapper(ActiveMiningHandoff handoff, ActiveMiningAsteroidStage stage)
        {
            _handoff = handoff;
            _stage   = stage;
        }

        public void Start() => _stage.SpawnClone(_handoff.Definition);
    }
}
```

- [ ] **Step 2: Run tests to verify everything still compiles and passes**

Run: `"C:\Program Files\Unity\Hub\Editor\6000.3.12f1\Editor\Unity.exe" -batchmode -runTests -projectPath . -testResults task8-green.xml -testPlatform EditMode -logFile task8-green.log`
Expected: full EditMode suite passes.

- [ ] **Step 3: Commit**

```bash
git add Assets/_Project/Scripts/App/ActiveMiningSceneScope.cs
git commit -m "app: ActiveMiningSceneScope resolves the handoff from Root instead of parenting to PlanetSceneScope"
```

---

### Task 9: Rewrite `ActiveMiningMinigameView` — pre-game / in-progress / post-game phases

**Files:**
- Modify: `Assets/_Project/Scripts/UI/ActiveMiningMinigameView.cs`

**Interfaces:**
- Consumes: `ActiveMiningSessionRunner` (Task 7, injected directly), `ActiveMiningHandoff` (Task 2), `SocialUniverse.Core.ActiveMiningState` (Task 5).
- Produces: new serialized fields (`_preGamePanel`, `_mineralTypeText`, `_startButton`, `_rewardText`, `_continueButton`) that Task 10's scene edit must wire by fileID.

No unit tests — this class has never had tests in this codebase (it's a MonoBehaviour View wired entirely through the Inspector); consistent with existing precedent.

- [ ] **Step 1: Replace the entire file**

Replace the entire contents of `Assets/_Project/Scripts/UI/ActiveMiningMinigameView.cs`:

```csharp
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using SocialUniverse.Mining;
using SocialUniverse.Core;

namespace SocialUniverse.UI
{
    // Owns the ActiveMining scene's three-phase flow: a pre-game panel (asteroid info + Start
    // button), the in-progress HUD (target point/timer/progress/miss counter, unchanged from the
    // previous scene-based redesign), and a post-game panel (result + reward preview + Continue).
    // Nothing spawns or ticks until the player presses Start — this also means there's no race
    // between scene-load and the first target point (a bug the previous overlay design had).
    // _targetButton must be a UI child rendered above _missAreaButton in the hierarchy so a tap
    // on the point hits the target first and any other tap in the asteroid area falls through to
    // the miss button (standard Unity UI raycast ordering).
    public class ActiveMiningMinigameView : MonoBehaviour
    {
        [SerializeField] private Camera                    _sceneCamera;
        [SerializeField] private ActiveMiningAsteroidStage  _stage;

        [Header("Pre-game")]
        [SerializeField] private GameObject _preGamePanel;
        [SerializeField] private Text       _mineralTypeText;
        [SerializeField] private Button     _startButton;

        [Header("In-progress")]
        [SerializeField] private RectTransform _targetPoint;
        [SerializeField] private Button        _targetButton;
        [SerializeField] private Button        _missAreaButton;
        [SerializeField] private Text          _progressText;
        [SerializeField] private Text          _errorText;
        [SerializeField] private Text          _timeText;

        [Header("Post-game")]
        [SerializeField] private GameObject _resultBanner;
        [SerializeField] private Text       _resultText;
        [SerializeField] private Text       _rewardText;
        [SerializeField] private Button     _continueButton;

        [Inject] private ActiveMiningSessionRunner _runner;
        [Inject] private ActiveMiningHandoff       _handoff;
        [Inject] private ActiveMiningState         _activeMiningState;

        private ActiveMiningTargetPoint _currentTargetAnchor;
        private bool                    _started;

        private void Awake()
        {
            if (_targetButton   != null) _targetButton.onClick.AddListener(() => OnTapped(hitTarget: true));
            if (_missAreaButton != null) _missAreaButton.onClick.AddListener(() => OnTapped(hitTarget: false));
            if (_startButton    != null) _startButton.onClick.AddListener(OnStartClicked);
            if (_continueButton != null) _continueButton.onClick.AddListener(OnContinueClicked);
        }

        private void Start()
        {
            SetInProgressUiActive(false);
            if (_resultBanner != null) _resultBanner.SetActive(false);

            if (_preGamePanel    != null) _preGamePanel.SetActive(true);
            if (_mineralTypeText != null) _mineralTypeText.text = _handoff.Definition != null ? _handoff.Definition.MineralType : "";
        }

        private void OnDestroy()
        {
            if (_started && _runner.Session != null) _runner.Session.OnStageChanged -= OnStageChanged;
            if (_currentTargetAnchor != null) Destroy(_currentTargetAnchor.gameObject);
        }

        private void Update()
        {
            if (!_started) return;

            var session = _runner.Session;
            if (session == null || session.Stage != ActiveMiningStage.InProgress) return;

            Refresh(session);
            ProjectTargetPointToScreen();
        }

        private void OnStartClicked()
        {
            if (_started || _runner.Session == null) return;
            _started = true;

            if (_preGamePanel != null) _preGamePanel.SetActive(false);
            SetInProgressUiActive(true);

            _runner.Session.OnStageChanged += OnStageChanged;
            _runner.BeginTicking();
            Refresh(_runner.Session);
            SpawnNextTargetPoint();
        }

        private void OnStageChanged(ActiveMiningStage stage)
        {
            if (stage == ActiveMiningStage.InProgress) return;

            SetInProgressUiActive(false);
            ShowResult(stage);
        }

        private void Refresh(ActiveMiningSession session)
        {
            if (_progressText != null) _progressText.text = $"{session.SuccessfulTaps}/{session.TapsRequired}";
            if (_errorText    != null) _errorText.text    = $"Misses: {session.ErrorCount}/{session.MaxErrors}";
            if (_timeText     != null) _timeText.text     = $"{Mathf.CeilToInt(session.TimeRemainingSeconds)}s";
        }

        private void ShowResult(ActiveMiningStage stage)
        {
            bool succeeded = stage == ActiveMiningStage.Success;

            if (_resultBanner != null) _resultBanner.SetActive(true);
            if (_resultText   != null) _resultText.text = succeeded ? "Success!" : "Failed";

            if (_rewardText != null)
            {
                if (succeeded && _handoff.Definition != null)
                {
                    int mined = _handoff.RemainingYieldAtStart;
                    int coins = mined * _handoff.Definition.CoinsPerUnit;
                    _rewardText.text = $"+{mined} {_handoff.Definition.MineralType} -> {coins} coins";
                }
                else
                {
                    _rewardText.text = "No reward";
                }
            }
        }

        private void OnContinueClicked() => _activeMiningState.Finish();

        private void SetInProgressUiActive(bool active)
        {
            if (_targetPoint    != null) _targetPoint.gameObject.SetActive(active);
            if (_missAreaButton != null) _missAreaButton.gameObject.SetActive(active);
            if (_progressText   != null) _progressText.gameObject.SetActive(active);
            if (_errorText      != null) _errorText.gameObject.SetActive(active);
            if (_timeText       != null) _timeText.gameObject.SetActive(active);
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
            if (!_started || _runner.Session.Stage != ActiveMiningStage.InProgress) return;

            if (hitTarget) _runner.Session.RegisterHit();
            else           _runner.Session.RegisterMiss();

            if (_runner.Session.Stage == ActiveMiningStage.InProgress)
                SpawnNextTargetPoint();
        }
    }
}
```

- [ ] **Step 2: Run tests to verify everything still compiles and passes**

Run: `"C:\Program Files\Unity\Hub\Editor\6000.3.12f1\Editor\Unity.exe" -batchmode -runTests -projectPath . -testResults task9-green.xml -testPlatform EditMode -logFile task9-green.log`
Expected: full EditMode suite passes. (The scene file still wires the *old* field names until Task 10 — Unity will show missing-field warnings for `ActiveMining.unity`'s serialized references on next load/open, but EditMode tests don't load that scene, so this doesn't fail the suite.)

- [ ] **Step 3: Commit**

```bash
git add Assets/_Project/Scripts/UI/ActiveMiningMinigameView.cs
git commit -m "ui: ActiveMiningMinigameView gains pre-game/post-game panels, gates ticking behind Start"
```

---

### Task 10: `Assets/Scenes/ActiveMining.unity` — parent fix + pre-game/post-game UI

This is the highest-risk task (hand-authored scene YAML) — use the most capable model for both implementation and review, matching the precedent set by the original ActiveMining scene's creation.

**Files:**
- Modify: `Assets/Scenes/ActiveMining.unity`

**Interfaces:**
- Consumes: every new serialized field on `ActiveMiningMinigameView` from Task 9.

- [ ] **Step 1: Fix the parent reference**

The `ActiveMiningSceneScope` component (`--- !u!114 &100032`) currently has:

```yaml
  parentReference:
    TypeName: SocialUniverse.App.PlanetSceneScope
```

Change it to:

```yaml
  parentReference:
    TypeName: SocialUniverse.App.RootLifetimeScope
```

(This matches exactly how `PlanetSceneScope`/`TravelSceneScope` parent to Root in their own scene files — Planet is no longer loaded when this scene runs.)

- [ ] **Step 2: Add the PreGamePanel subtree**

Add a new GameObject subtree as a **new child of the Canvas** (`fileID 100041`), alongside the existing MissArea/TargetPoint/ProgressText/ErrorText/TimeText/ResultBanner children — i.e. append one more entry, `{fileID: 100301}`, to Canvas's `m_Children:` list (`--- !u!224 &100041`).

New objects (pick unused fileIDs in the `1003xx` range):

- **PreGamePanel** (GameObject, active by default — `ActiveMiningMinigameView.Start()` also explicitly sets it active, so either scene-authored state works):
  - RectTransform `100301`: full-stretch under Canvas (`m_AnchorMin: {x: 0, y: 0}`, `m_AnchorMax: {x: 1, y: 1}`, `m_SizeDelta: {x: 0, y: 0}`, `m_Pivot: {x: 0.5, y: 0.5}`), `m_Father: {fileID: 100041}`, `m_Children:` listing the RectTransforms of MineralTypeText (`100311`) and StartButton (`100321`).
  - `UnityEngine.UI.Image` component (background dim, e.g. `m_Color: {r: 0.078, g: 0.078, b: 0.118, a: 0.85}` — same tone as ResultBanner's existing background at fileID `100102`) + matching `CanvasRenderer`.
- **MineralTypeText** (child of PreGamePanel): `UnityEngine.UI.Text`, anchored center-top-ish (e.g. `m_AnchorMin/Max: {x: 0.5, y: 0.5}`, `m_AnchoredPosition: {x: 0, y: 60}`, `m_SizeDelta: {x: 600, y: 90}`), font size ~48, alignment center, initial `m_Text: "Mine this asteroid?"` (placeholder — `ActiveMiningMinigameView.Start()` overwrites it with the real mineral type at runtime). Use the same font/material fileIDs as the existing Text components in this scene (`m_Font: {fileID: 10102, guid: 0000000000000000e000000000000000, type: 0}`, matching `TimeText`/`ResultText`'s font reference).
- **StartButton** (child of PreGamePanel): `UnityEngine.UI.Image` + `UnityEngine.UI.Button` (mirror the existing `TargetPoint`/`MissArea` button component structure exactly — same `m_Colors`/`m_SpriteState`/`m_AnimationTriggers` blocks used elsewhere in this file), with a child Text label reading "Start Mining". Anchored center, below the mineral text (e.g. `m_AnchoredPosition: {x: 0, y: -60}`, `m_SizeDelta: {x: 320, y: 100}`).

- [ ] **Step 3: Add reward-summary content to the existing ResultBanner**

`ResultBanner` (GameObject `100100`, RectTransform `100101`) currently has one child, `ResultText` (`100110`). Add two more children, appending their RectTransforms to `100101`'s `m_Children:` list:

- **RewardText** (new Text, similar styling to `ResultText` but smaller — e.g. font size 40, positioned below `ResultText` within the banner, e.g. `m_AnchorMin/Max: {x: 0.5, y: 0.5}`, `m_AnchoredPosition: {x: 0, y: -60}`, `m_SizeDelta: {x: 600, y: 70}`). Initial `m_Text: ""`.
- **ContinueButton** (new Button, same component shape as `StartButton` above), positioned near the bottom of the banner (e.g. `m_AnchoredPosition: {x: 0, y: -120}`, `m_SizeDelta: {x: 280, y: 90}`), with a child Text label reading "Continue".

- [ ] **Step 4: Wire the new `ActiveMiningMinigameView` fields**

The view's `MonoBehaviour` component (`--- !u!114 &100045`) currently has:

```yaml
  _sceneCamera: {fileID: 100011}
  _stage: {fileID: 100022}
  _targetPoint: {fileID: 100061}
  _targetButton: {fileID: 100064}
  _missAreaButton: {fileID: 100054}
  _progressText: {fileID: 100072}
  _errorText: {fileID: 100082}
  _timeText: {fileID: 100092}
  _resultBanner: {fileID: 100100}
  _resultText: {fileID: 100112}
```

Add the four new fields (using whatever fileIDs you assigned in Steps 2–3 for the RectTransform components of `PreGamePanel`, the `Text` component of `MineralTypeText`, the `Button` component of `StartButton`, the `Text` component of `RewardText`, and the `Button` component of `ContinueButton`):

```yaml
  _preGamePanel: {fileID: <PreGamePanel GameObject fileID>}
  _mineralTypeText: {fileID: <MineralTypeText Text component fileID>}
  _startButton: {fileID: <StartButton Button component fileID>}
  _rewardText: {fileID: <RewardText Text component fileID>}
  _continueButton: {fileID: <ContinueButton Button component fileID>}
```

All existing field wirings (`_sceneCamera` through `_resultText`) stay exactly as they are.

- [ ] **Step 5: Verify no duplicate fileIDs and the scene loads cleanly**

Run: `grep -oE '^--- !u![0-9]+ &[0-9]+' Assets/Scenes/ActiveMining.unity | awk '{print $3}' | sort | uniq -d`
Expected: no output (no duplicates).

Run: `"C:\Program Files\Unity\Hub\Editor\6000.3.12f1\Editor\Unity.exe" -batchmode -runTests -projectPath . -testResults task10-editmode.xml -testPlatform EditMode -logFile task10-editmode.log`
Expected: full EditMode suite still passes (scene edits don't affect EditMode tests, but this confirms no incidental damage elsewhere).

Run: `"C:\Program Files\Unity\Hub\Editor\6000.3.12f1\Editor\Unity.exe" -batchmode -runTests -projectPath . -testResults task10-playmode.xml -testPlatform PlayMode -logFile task10-playmode.log`
Expected: only the pre-existing "Known Issue #7" PlayMode failures (unrelated `PlanetSceneScope` standalone-parent-reference limitation) — no new failures, no missing-script/missing-reference warnings in the log for `ActiveMining.unity`.

- [ ] **Step 6: Commit**

```bash
git add Assets/Scenes/ActiveMining.unity
git commit -m "mining: ActiveMining.unity gets pre-game/post-game panels, parents to RootLifetimeScope"
```

---

## Self-Review Notes

- **Spec coverage:** §3 (FSM/scene architecture) → Tasks 5, 6, 10 (parent fix). §4 (handoff) → Task 2, consumed in 3/7/8/9. §5 (finalize on return) → Task 3. §6 (in-scene UI flow) → Tasks 7, 9, 10. §7 (testing) → covered per-task (EditMode tests in 1/2/3, no-test precedent noted in 5/7/9/10). §8 (removals) → Task 4 (old classes + stale overlay), Task 6 (stale concurrency guard in `MiningModePromptView`, already folded into Task 6 Step 4's replacement of `OnAsteroidSelected`).
- **Placeholder scan:** Task 10's Steps 2–3 describe scene additions structurally (exact anchors/colors/font references) rather than full literal YAML, matching the accepted precedent from the previous plan's Task 7 (checklist-style for the highest-risk hand-authored-scene task, explicitly approved by the user pre-flight on that branch). Every other task has complete, runnable code.
- **Type consistency:** Verified `ActiveMiningHandoff`'s method signature (`Begin(string, string, AsteroidDefinition, int, int, int, float)`) matches identically between Task 2's tests, Task 3's `MiningController.BeginActiveMining` call site, and Task 7/8/9's consumers. `MiningController`'s new constructor parameter order (`IEconomyService, MiningRewardCalculator, AsteroidSpawner, EconomyConfig, PlanetDefinition, ActiveMiningHandoff`) matches between Task 3's production code and its test call sites.
- **Assembly boundary check:** confirmed via `asmdef` inspection that `SocialUniverse.Core` references `SocialUniverse.Config` but not `SocialUniverse.Mining`; `ActiveMiningHandoff` (Core) therefore only ever takes `AsteroidDefinition` (Config) and primitives as parameters, never `Asteroid`/`MiningReward` (Mining) — this constraint is called out in Global Constraints and respected in every task.
