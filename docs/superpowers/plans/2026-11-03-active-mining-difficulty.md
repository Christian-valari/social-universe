# Active Mining Difficulty Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make active mining a real challenge: closing-ring targets, payouts graded by play, difficulty set by asteroid tier, and idle mining at 70% so the two modes are a real choice.

**Architecture:** All run logic stays in the plain C# `ActiveMiningSession`, driven by `ActiveMiningRunSettings` that `EconomyConfig` builds per tier and `ActiveMiningHandoff` carries into the ActiveMining scene. The session reports a banked share. `MiningController` turns it into a normal claim or a forfeit on return to Planet, and a local marker settles runs the player quit. `ServerCode/` doesn't change.

**Tech Stack:** Unity 6 (6000.3.12f1, URP), uGUI, VContainer, NUnit EditMode and PlayMode tests.

**Spec:** `docs/superpowers/specs/2026-11-03-active-mining-difficulty-design.md`

## Global Constraints

- No changes to `ServerCode/` and no deploy. The `ServerCode/` drift tests must pass untouched.
- Every payout is at or below `round(RemainingYield × EffectiveYieldMult)`. Active never pays above 100%.
- `Core` must not reference `Mining`. The handoff holds only `Config` types and plain values.
- Namespaces mirror folders, and each new public type gets its own file.
- All tunable values live in `EconomyConfig` (Architecture Rule 3). Constants are for engineering limits only.
- Pool target markers, anchors and hit effects (Rule 5).
- Add new `SfxId` values at the end of the enum only. `AudioCatalog.asset` stores them by number.
- Dev-only code sits inside `#if UNITY_EDITOR || DEVELOPMENT_BUILD`.
- Never commit `_devMode: 1` in `Bootstrap.unity`, or any signing change.
- Every git command needs `-c safe.directory='*'`. Stage only the files a task names, plus their `.meta` files.
- End commit messages with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.

## Review Focus

1. A tap lands on the frame a ring closes: the player takes one penalty at most, and the tap isn't also a wrong tap. (Task 2)
2. A long frame or a return from the background must not burn several targets at once. (Task 2)
3. A low-yield asteroid with many targets (Helium-3, yield 6, 16 targets): any hit pays at least 1, and all Perfect pays exactly the full amount. (Task 3)
4. The player closes the app on the pre-game panel, before pressing Start: no penalty and the asteroid is untouched. (Task 7)
5. A claim's response was lost but the server committed it: the next load finds the slot empty and doesn't claim again. (Task 7)

## Before Starting

`main` has about 285 uncommitted changes, including `MiningController.cs`, `MiningModePromptView.cs` and `MiningControllerTests.cs`. A branch or worktree made now wouldn't contain that work, and staging those files would mix it into this plan's commits. **The project owner must commit that baseline first.** Then create `feature/active-mining-difficulty` from it.

### Running tests

The Editor is usually open on the repo, so tests run in the scratch copy at `C:\su`. Tell the owner before the first run, because the import is CPU-heavy.

```bash
MSYS_NO_PATHCONV=1 robocopy 'C:\Users\chris\UnityProjects\social-universe\Assets' 'C:\su\Assets' /MIR /XF '*.meta' 'ZZ*' /NFL /NDL /NJH /NJS
```

```bash
MSYS_NO_PATHCONV=1 robocopy 'C:\Users\chris\UnityProjects\social-universe\ServerCode' 'C:\su\ServerCode' /MIR /NFL /NDL /NJH /NJS
```

```bash
"/c/Program Files/Unity/Hub/Editor/6000.3.12f1/Editor/Unity.exe" -runTests -batchmode -projectPath 'C:\su' -testPlatform EditMode -testFilter "SocialUniverse.Tests.ActiveMiningSessionTests" -testResults 'C:\su\results.xml' -logFile 'C:\su\run.log'
```

```bash
grep -o '<test-run [^>]*' /c/su/results.xml | grep -oE '(result|total|passed|failed)="[^"]*"'
```

- A robocopy exit code under 8 means success.
- Tasks below say "Run EditMode: `<filter>`". Swap in that filter, with `SocialUniverse.Tests.` in front of each class name and `;` between classes. Omit `-testFilter` for a full suite, and use `-testPlatform PlayMode` for PlayMode.
- Compile errors produce no results file, so check `C:\su\run.log` for `error CS`.
- After a run, copy any new `.cs.meta` files Unity generated from `C:\su\Assets` back into the repo, so GUIDs stay stable.

### Editing scenes

Scenes change through a scratch editor script, never hand-edited YAML. Put it at `C:\su\Assets\Editor\ZZ<Name>.cs`, where the `ZZ*` robocopy exclusion keeps it out of the repo. Don't commit it.

```bash
"/c/Program Files/Unity/Hub/Editor/6000.3.12f1/Editor/Unity.exe" -batchmode -quit -projectPath 'C:\su' -executeMethod ZZ<Name>.Run -logFile 'C:\su\edit.log'
```

Copy the scene back to the repo and read its diff object by object before committing. To check visually, use a scratch `ZZ*` PlayMode test that renders the scene to a PNG.

---

# Phase 1: Core loop

## Task 1: Tier settings in EconomyConfig

**Files:**
- Create: `Assets/_Project/Scripts/Config/ActiveMiningTierSettings.cs`
- Create: `Assets/_Project/Scripts/Config/ActiveMiningRunSettings.cs`
- Modify: `Assets/_Project/Scripts/Config/EconomyConfig.cs`
- Modify: `Assets/_Project/ScriptableObjects/EconomyConfig.asset`
- Test: `Assets/_Project/Tests/EditMode/Mining/ActiveMiningConfigTests.cs`

**Interfaces:**
- Produces:
  - `[Serializable] class ActiveMiningTierSettings`: a parameterless constructor whose field defaults equal Tier 1; `ActiveMiningTierSettings(int targets, float ringSeconds, float perfectBySeconds, float goodBySeconds, int maxMisses)`; read-only properties of the same names, PascalCase.
  - `sealed class ActiveMiningRunSettings(int tierNumber, ActiveMiningTierSettings tier, ActiveMiningTierSettings previousTier, float perfectShare, float goodShare, float lateShare, float spawnGapSeconds, float missGraceSeconds)`, with matching read-only properties. `PreviousTier` is null at Tier 1.
  - `EconomyConfig.ActiveRunSettings(int tier)` and `EconomyConfig.IdleYieldFraction`.

- [ ] **Step 1: Write the failing tests**

```csharp
private static EconomyConfig Real() =>
    AssetDatabase.LoadAssetAtPath<EconomyConfig>("Assets/_Project/ScriptableObjects/EconomyConfig.asset");

[TestCase(1, 8, 1.6f, 0.8f, 1.2f)]
[TestCase(2, 10, 1.3f, 0.75f, 1.05f)]
[TestCase(3, 12, 1.1f, 0.7f, 0.9f)]
[TestCase(4, 14, 1.0f, 0.65f, 0.85f)]
[TestCase(5, 16, 0.9f, 0.6f, 0.75f)]
public void Real_asset_holds_the_tier_table(int tier, int targets, float ring, float perfect, float good)
{
    var s = Real().ActiveRunSettings(tier);
    Assert.AreEqual(tier, s.TierNumber);
    Assert.AreEqual(targets, s.Tier.Targets);
    Assert.AreEqual(ring, s.Tier.RingSeconds, 0.001f);
    Assert.AreEqual(perfect, s.Tier.PerfectBySeconds, 0.001f);
    Assert.AreEqual(good, s.Tier.GoodBySeconds, 0.001f);
    Assert.AreEqual(3, s.Tier.MaxMisses);
    Assert.Less(s.Tier.PerfectBySeconds, s.Tier.GoodBySeconds);
    Assert.Less(s.Tier.GoodBySeconds, s.Tier.RingSeconds);
}

[Test] public void Real_asset_holds_the_shared_values()
{
    var s = Real().ActiveRunSettings(1);
    Assert.AreEqual((1f, 0.8f, 0.6f, 0.25f, 0.2f),
        (s.PerfectShare, s.GoodShare, s.LateShare, s.SpawnGapSeconds, s.MissGraceSeconds));
    Assert.AreEqual(0.7f, Real().IdleYieldFraction, 0.001f);
}

[Test] public void Previous_tier_is_null_at_tier_1_and_set_above_it()
{
    Assert.IsNull(Real().ActiveRunSettings(1).PreviousTier);
    Assert.AreEqual(8, Real().ActiveRunSettings(2).PreviousTier.Targets);
}

[Test] public void A_tier_outside_the_table_uses_the_nearest_row()
{
    Assert.AreEqual(8, Real().ActiveRunSettings(0).Tier.Targets);
    Assert.AreEqual(16, Real().ActiveRunSettings(9).Tier.Targets);
}

[Test] public void An_empty_table_falls_back_to_tier_1_defaults()
{
    var config = ScriptableObject.CreateInstance<EconomyConfig>();
    Assert.AreEqual(8, config.ActiveRunSettings(3).Tier.Targets);
    Object.DestroyImmediate(config);
}
```

- [ ] **Step 2: Run EditMode: `ActiveMiningConfigTests`.** Expected: compile failure, because the types don't exist yet.

- [ ] **Step 3: Implement the two settings types and `EconomyConfig` additions**

Add `_activeTiers` (`ActiveMiningTierSettings[]`), `_activePerfectShare = 1f`, `_activeGoodShare = 0.8f`, `_activeLateShare = 0.6f`, `_activeSpawnGapSeconds = 0.25f`, `_activeMissGraceSeconds = 0.2f` and `_idleYieldFraction = 0.7f`. `ActiveRunSettings` clamps the tier to the table and uses `new ActiveMiningTierSettings()` when the table is empty. Leave the old active-mining fields for Task 3. Add the five table rows and shared values to `EconomyConfig.asset`.

- [ ] **Step 4: Run EditMode: `ActiveMiningConfigTests`.** Expected: all pass.

- [ ] **Step 5: Commit** with `feat(mining): add per-tier active mining settings`.

## Task 2: Run logic in ActiveMiningSession

The old session stays alive under another name until Task 4 swaps the scene over, so every commit compiles.

**Files:**
- Create: `Assets/_Project/Scripts/Mining/LegacyActiveMiningSession.cs` (the current session, renamed)
- Create: `Assets/_Project/Scripts/Mining/ActiveMiningGrade.cs`, `ActiveMiningTargetKind.cs`, `ActiveMiningTarget.cs`
- Modify: `Assets/_Project/Scripts/Mining/ActiveMiningSession.cs` (full rewrite)
- Modify: `Assets/_Project/Scripts/Mining/ActiveMiningSessionRunner.cs`, `Assets/_Project/Scripts/UI/ActiveMiningMinigameView.cs` (type name only)
- Test: `Assets/_Project/Tests/EditMode/Mining/ActiveMiningSessionTests.cs` (full rewrite)

**Interfaces:**
- Consumes: `ActiveMiningRunSettings` (Task 1).
- Produces:
  - `enum ActiveMiningGrade { Perfect, Good, Late, Miss }`
  - `enum ActiveMiningTargetKind { Normal }`, extended in Tasks 9 and 12
  - `class ActiveMiningTarget { int Id; ActiveMiningTargetKind Kind; float ElapsedSeconds; float RingSeconds; float Progress; }`, where `Progress` is `Elapsed / Ring` clamped to 0–1
  - `class ActiveMiningSession(ActiveMiningRunSettings settings, Func<float> random = null)`:
    - `int Targets, ResolvedTargets, MaxErrors, ErrorCount, PerfectCount, GoodCount, LateCount`
    - `float BankedFraction`, `ActiveMiningStage Stage`, `IReadOnlyList<ActiveMiningTarget> LiveTargets`
    - `event Action<ActiveMiningStage> OnStageChanged`
    - `event Action<ActiveMiningTarget> OnTargetSpawned`
    - `event Action<ActiveMiningTarget, ActiveMiningGrade> OnTargetResolved`
    - `event Action OnWrongTap`
    - `void Tick(float deltaTime)`, `void TapTarget(int targetId)`, `void TapOffTarget()`
  - `enum ActiveMiningStage { InProgress, Success, Failed }` stays in `ActiveMiningSession.cs`, where the old class's stage enum already lives.

- [ ] **Step 1: Rename the old class.** Move the current class to `LegacyActiveMiningSession.cs` as `LegacyActiveMiningSession`, keeping its behaviour. Point `ActiveMiningSessionRunner` and `ActiveMiningMinigameView.Refresh` at the new name.

- [ ] **Step 2: Write the failing tests.** Replace the test file. Use these helpers: `Settings(targets, maxMisses)` builds ring 1.6, perfect 0.8, good 1.2, shares 1 / 0.8 / 0.6, gap 0.25 and grace 0.2. `Advance(s, seconds)` calls `Tick` in 0.05s steps. `HitAt(s, seconds)` spawns the next target, advances that long and taps it.

```csharp
[Test] public void The_first_tick_spawns_the_first_target()
{ var s = New(); s.Tick(0f); Assert.AreEqual(1, s.LiveTargets.Count); }

// Off the exact boundaries, because Advance's 0.05s steps accumulate float error.
[TestCase(0.75f, ActiveMiningGrade.Perfect)]
[TestCase(0.85f, ActiveMiningGrade.Good)]
[TestCase(1.15f, ActiveMiningGrade.Good)]
[TestCase(1.25f, ActiveMiningGrade.Late)]
[TestCase(1.55f, ActiveMiningGrade.Late)]
public void Earlier_taps_grade_better(float tapAt, ActiveMiningGrade expected) { /* assert via OnTargetResolved */ }

[Test] public void A_closed_ring_is_a_miss_that_uses_up_the_target()
{ s.Tick(0f); Advance(s, 1.65f);
  Assert.AreEqual((1, 1, 0f), (s.ErrorCount, s.ResolvedTargets, s.BankedFraction)); }

[Test] public void Banked_share_matches_the_spec_example()
{ // 8 targets: 6 Perfect, 1 Good, 1 Late
  Assert.AreEqual(ActiveMiningStage.Success, s.Stage);
  Assert.AreEqual(0.925f, s.BankedFraction, 0.0001f); }

[Test] public void The_miss_limit_fails_the_run_and_keeps_the_banked_share()
{ // 8 targets: 4 Perfect, then 3 closed rings
  Assert.AreEqual(ActiveMiningStage.Failed, s.Stage);
  Assert.AreEqual(0.5f, s.BankedFraction, 0.0001f); }

[Test] public void A_wrong_tap_adds_a_miss_but_the_target_and_its_ring_carry_on()
{ s.Tick(0f); var t = s.LiveTargets[0]; Advance(s, 0.5f); s.TapOffTarget();
  Assert.AreEqual((1, 0, t), (s.ErrorCount, s.ResolvedTargets, s.LiveTargets[0]));
  Assert.GreaterOrEqual(t.ElapsedSeconds, 0.5f); }

[Test] public void An_off_target_tap_just_after_a_hit_is_ignored()
{ HitAt(s, 0.3f); s.TapOffTarget(); Assert.AreEqual(0, s.ErrorCount); }

[Test] public void An_off_target_tap_with_no_live_target_is_ignored()
{ s.TapOffTarget(); Assert.AreEqual(0, s.ErrorCount); }

[Test] public void The_next_target_waits_for_the_spawn_gap()
{ HitAt(s, 0.3f); Advance(s, 0.2f); Assert.AreEqual(0, s.LiveTargets.Count);
  Advance(s, 0.1f); Assert.AreEqual(1, s.LiveTargets.Count); }

[Test] public void A_third_miss_on_the_last_target_is_a_failure_not_a_clear() { /* 3 targets, all rings close */ }
[Test] public void Finished_runs_ignore_taps_and_ticks() { /* Clear, then TapTarget, TapOffTarget, Tick: nothing changes */ }
[Test] public void OnStageChanged_fires_once() { /* count across a whole run */ }

// Review Focus 1
[Test] public void Tapping_a_target_whose_ring_already_closed_changes_nothing()
{ s.Tick(0f); int id = s.LiveTargets[0].Id; Advance(s, 1.65f); s.TapTarget(id);
  Assert.AreEqual((1, 0f), (s.ErrorCount, s.BankedFraction)); }

// Review Focus 2
[Test] public void A_long_frame_costs_at_most_a_tenth_of_a_second()
{ s.Tick(0f); var t = s.LiveTargets[0]; s.Tick(10f);
  Assert.AreEqual(0.1f, t.ElapsedSeconds, 0.001f); Assert.AreEqual(0, s.ErrorCount); }
```

- [ ] **Step 3: Run EditMode: `ActiveMiningSessionTests`.** Expected: compile failure.

- [ ] **Step 4: Implement the session.** The bounds are inclusive: `Elapsed <= PerfectBy` is Perfect, `<= GoodBy` is Good, otherwise Late. `BankedFraction = sum(shares) / Targets`. Clamp `deltaTime` to a `MaxTickSeconds = 0.1f` constant. Check the miss limit before the all-resolved check. A tap on a target that isn't live does nothing and doesn't fire `OnWrongTap`. A hit starts the grace timer. `TapOffTarget` counts only when a target is live and grace has run out.

- [ ] **Step 5: Run EditMode: `ActiveMiningSessionTests`.** Expected: all pass.

- [ ] **Step 6: Commit** with `feat(mining): grade active mining targets by closing ring`.

## Task 3: Banked payouts, pinned quantity and idle at 70%

**Files:**
- Create: `Assets/_Project/Scripts/Core/ActiveMiningPayout.cs`
- Modify: `Assets/_Project/Scripts/Core/ActiveMiningHandoff.cs`, `Assets/_Project/Scripts/Mining/MiningRewardCalculator.cs`, `Assets/_Project/Scripts/Mining/MiningController.cs`, `Assets/_Project/Scripts/Mining/ActiveMiningSessionRunner.cs`, `Assets/_Project/Scripts/UI/ActiveMiningMinigameView.cs`, `Assets/_Project/Scripts/Config/EconomyConfig.cs`, `Assets/_Project/ScriptableObjects/EconomyConfig.asset`
- Test: create `Assets/_Project/Tests/EditMode/Core/ActiveMiningPayoutTests.cs`; modify `ActiveMiningHandoffTests.cs`, `MiningRewardCalculatorTests.cs`, `MiningControllerTests.cs`

**Interfaces:**
- Produces:
  - `static int ActiveMiningPayout.Quantity(int fullQuantity, float bankedFraction)`
  - `ActiveMiningHandoff.Begin(string planetId, string asteroidSlotId, AsteroidDefinition definition, int fullQuantity, ActiveMiningRunSettings settings)`
  - `ActiveMiningHandoff.SetResult(bool succeeded, float bankedFraction)`
  - Handoff properties `int FullQuantity`, `ActiveMiningRunSettings Settings`, `float BankedFraction`, `int BankedQuantity`, `string MineralId`
  - `MiningReward.IdleQuantity`
- Removes: `RemainingYieldAtStart`, `TapsRequired`, `MaxErrors`, `SessionDurationSeconds`, `MiningReward.ActiveTapsRequired`, `MiningReward.ActiveSessionDurationSeconds`, and `EconomyConfig`'s `_activeYieldPerTap`, `_minActiveTaps`, `_maxActiveTaps`, `_activeSecondsPerTap`, `_minActiveSessionSeconds`, `_maxActiveSessionSeconds`, `_activeMaxErrors`.

- [ ] **Step 1: Write the failing tests**

```csharp
// ActiveMiningPayoutTests
[TestCase(80, 1f, 80)] [TestCase(80, 0.925f, 74)] [TestCase(80, 0.5f, 40)]
[TestCase(80, 0f, 0)]  [TestCase(0, 1f, 0)]
public void Quantity_is_the_banked_share_of_the_full_amount(int full, float banked, int expected) =>
    Assert.AreEqual(expected, ActiveMiningPayout.Quantity(full, banked));

// Review Focus 3
[Test] public void A_low_yield_asteroid_still_pays_for_any_hit()
{
    Assert.AreEqual(1, ActiveMiningPayout.Quantity(6, 0.6f / 16f));
    Assert.AreEqual(4, ActiveMiningPayout.Quantity(6, 0.6f));
    Assert.AreEqual(6, ActiveMiningPayout.Quantity(6, 1f));
}

// MiningRewardCalculatorTests (config _idleYieldFraction = 0.7)
[Test] public void Idle_pays_its_fraction_of_the_full_quantity()
{
    Assert.AreEqual(56, _calc.Compute(MakeAsteroid(80), 1f, RefSpeed).IdleQuantity);
    Assert.AreEqual(84, _calc.Compute(MakeAsteroid(80), 1.5f, RefSpeed).IdleQuantity);
    Assert.AreEqual(80, _calc.Compute(MakeAsteroid(80), 1f, RefSpeed).MineralQuantity);
}

// MiningControllerTests
[Test] public async Task A_failed_run_with_a_banked_share_claims_that_share()
{ // yield 80, Begin, SetResult(false, 0.5f), Initialize, Task.Yield
  Assert.AreEqual(40, _minerals.LastQty); Assert.IsNull(_minerals.LastForfeit);
  Assert.AreEqual("Iron#1", _minerals.LastSlotId); Assert.IsTrue(asteroid.IsDepleted); }

[Test] public async Task A_cleared_run_claims_its_banked_share()
{ /* yield 80, SetResult(true, 0.925f): LastQty == 74 */ }

[Test] public async Task The_claim_uses_the_quantity_pinned_when_the_run_began()
{ // yield 10, Begin, then set the backing field to 12 (Planet's reload re-rolled it)
  // SetResult(true, 1f), Initialize, Task.Yield
  Assert.AreEqual(10, _minerals.LastQty); }

[Test] public async Task An_idle_claim_pays_the_idle_fraction()
{ /* SetField _idleYieldFraction 0.7f, yield 80, StartAndFinish, claim: LastQty == 56 */ }

// ActiveMiningHandoffTests: Begin stores FullQuantity and Settings;
// SetResult stores BankedFraction; BankedQuantity == 40 for (80, 0.5f); MineralId prefers Mineral.MineralId.
```

Mechanical updates:
- `MiningControllerTests`: `SetResult(succeeded: true)` becomes `(succeeded: true, bankedFraction: 1f)` and `false` becomes `0f`.
- `MiningControllerTests.SetUp`: delete the `_activeYieldPerTap`, `_minActiveTaps`, `_maxActiveTaps` and `_activeMaxErrors` lines, and add `SetField(_config, "_idleYieldFraction", 1f)` so the existing idle tests keep their numbers.
- `MiningRewardCalculatorTests`: delete the removed fields from `SetUp`, delete the two active tap and duration tests, and drop the two active assertions from `Speed_does_not_change_the_mined_quantity_or_the_active_minigame`, renaming it `Speed_does_not_change_the_mined_quantity`.

- [ ] **Step 2: Run EditMode: `ActiveMiningPayoutTests;ActiveMiningHandoffTests;MiningRewardCalculatorTests;MiningControllerTests`.** Expected: compile failure.

- [ ] **Step 3: Implement the payout and handoff**

`Quantity` returns 0 when `bankedFraction <= 0` or `fullQuantity <= 0`, otherwise `Clamp(RoundToInt(full × banked), 1, full)`. `Clear()` also resets `FullQuantity`, `Settings` and `BankedFraction`.

- [ ] **Step 4: Implement the calculator and controller**

- `BeginActiveMining` passes `reward.MineralQuantity` and `_config.ActiveRunSettings(asteroid.Definition.Tier)` to `Begin`.
- `TryFinalizePendingActiveMining` calls `CompleteActiveMiningAsync(asteroid, _handoff.BankedQuantity)` when `BankedQuantity > 0`, otherwise `FailActiveMining(asteroid)`.
- `CompleteActiveMiningAsync` takes the quantity as a parameter and no longer recomputes it.
- `ClaimIdleSessionAsync` pays `reward.IdleQuantity`.

- [ ] **Step 5: Bridge the old minigame so the game still runs.** In `ActiveMiningSessionRunner`, build the legacy session from `Settings.Tier.Targets`, `Settings.Tier.MaxMisses` and `Targets × RingSeconds`, and call `SetResult(true, 1f)` or `SetResult(false, 0f)`. In `ActiveMiningMinigameView.ShowResult`, use `_handoff.BankedQuantity`.

- [ ] **Step 6: Remove the old `EconomyConfig` fields and accessors,** from the class and the asset.

- [ ] **Step 7: Run the Step 2 filter, then a full EditMode run.** Expected: all pass. The drift tests pass without any `ServerCode/` change.

- [ ] **Step 8: Commit** with `feat(mining): pay active runs by banked share and idle at 70%`.

## Task 4: Swap the scene to closing rings

**Files:**
- Create: `Assets/_Project/Scripts/UI/ActiveMiningTargetView.cs`
- Modify: `Assets/_Project/Scripts/UI/ActiveMiningMinigameView.cs`, `Assets/_Project/Scripts/Mining/ActiveMiningSessionRunner.cs`, `Assets/Scenes/ActiveMining.unity`
- Delete: `Assets/_Project/Scripts/Mining/LegacyActiveMiningSession.cs`
- Test: create `Assets/_Project/Tests/PlayMode/ActiveMiningSceneFlowTests.cs`; modify `Assets/_Project/Tests/PlayMode/PlanetTestRootScope.cs`

**Interfaces:**
- Consumes: `ActiveMiningSession` (Task 2), `ActiveMiningHandoff` (Task 3).
- Produces:
  - `ActiveMiningSessionRunner.BeginRun()` creates the session from `_handoff.Settings`, returns it and starts ticking. `Session` is null before then.
  - `ActiveMiningTargetView : MonoBehaviour` with `_button`, `_marker` (Image), `_ring` (RectTransform), plus `Show(ActiveMiningTarget target, Action<ActiveMiningTarget> onTapped)`, `Hide()`, `ActiveMiningTarget Target`, `SetScreenPosition(Vector3 position)` and `SetRingProgress(float progress)`.

- [ ] **Step 1: Write the failing PlayMode tests.** Add `builder.Register<ActiveMiningState>(Lifetime.Singleton);` to `PlanetTestRootScope`. In `SetUp`, create the root scope as `MineralSaleFlowTests` does and resolve the handoff from its container. Then call `Begin("earth", "Iron#1", def, 80, settings)` with long rings (5s, Perfect by 4s, Good by 4.5s) so quick test taps grade Perfect, and load `Assets/Scenes/ActiveMining.unity`. The helpers press Start through `Button.onClick.Invoke()` and tap `FindObjectsByType<ActiveMiningTargetView>` entries that are live.

```csharp
[UnityTest] public IEnumerator Hitting_every_target_clears_the_run_and_shows_the_full_quantity()
{ // 2 targets
  Assert.IsTrue(_handoff.HasResult); Assert.IsTrue(_handoff.Succeeded);
  Assert.AreEqual(1f, _handoff.BankedFraction, 0.001f);
  Assert.AreEqual("+80 minerals", RewardText()); }

[UnityTest] public IEnumerator A_wrong_tap_adds_a_miss_and_the_target_stays()
{ Assert.AreEqual("Misses: 1/3", ErrorText()); Assert.AreSame(before, LiveTarget().Target); }

[UnityTest] public IEnumerator Reaching_the_miss_limit_shows_what_was_banked()
{ // 2 targets, maxMisses 1: hit one Perfect, then a wrong tap
  Assert.IsFalse(_handoff.Succeeded); Assert.AreEqual("+40 minerals", RewardText()); }

[UnityTest] public IEnumerator The_counter_climbs_as_targets_are_hit()
{ Assert.AreEqual("+40 minerals", BankedText()); Assert.AreEqual("1/2", ProgressText()); }

[UnityTest] public IEnumerator A_run_that_banks_nothing_shows_no_reward()
{ Assert.AreEqual("No reward", RewardText()); }
```

- [ ] **Step 2: Run PlayMode: `ActiveMiningSceneFlowTests`.** Expected: compile failure.

- [ ] **Step 3: Implement `ActiveMiningTargetView` and the runner**

Delete `LegacyActiveMiningSession`. `SetRingProgress` scales the ring from `RingStartScale = 2.2f` at 0 down to 1 at 1. Since a session's first tick spawns its first target, the view subscribes right after `BeginRun()`.

- [ ] **Step 4: Rewrite `ActiveMiningMinigameView`**

- Replace `_targetPoint` and `_targetButton` with `[SerializeField] ActiveMiningTargetView _targetTemplate`, and `_timeText` with `_bankedText`.
- Keep a list of views. Grow it with `Instantiate(_targetTemplate, parent)`, placing each clone right after the template in sibling order so it stays below the HUD. Each view owns one reused `ActiveMiningTargetPoint` anchor.
- On `OnTargetSpawned`, take a free view and place its anchor. On `OnTargetResolved`, hide it.
- In `Update`, project each anchor to the screen and set ring progress from `Target.Progress`.
- `_missAreaButton` calls `TapOffTarget`.
- Text: `_progressText` is `{Resolved}/{Targets}`, `_errorText` is `Misses: {ErrorCount}/{MaxErrors}`, and `_bankedText` is `+{quantity} {mineralName}`, using `ActiveMiningPayout.Quantity(_handoff.FullQuantity, session.BankedFraction)`.
- Result text: `Perfect run!` when `PerfectCount == Targets`, `Cleared!` on other successes, and `Run over` on failure. Reward text: `+{BankedQuantity} {mineralName}`, or `No reward` when it is 0.
- Replace hit-effect `Instantiate` and `Destroy` with a pool of up to 4 instances, reusing the oldest. The prefab has `playOnAwake`, so `SetActive(false)` then `SetActive(true)` replays it. `_hitVfxLifetime` goes away.

- [ ] **Step 5: Edit the scene with a scratch script** (`ZZActiveMiningRings`)

- `TargetPoint`: size 140×140, root `Image` alpha 0 and still the raycast target, plus an `ActiveMiningTargetView` component.
- New child `Marker`: `Image`, 80×80, the current sprite and colour, raycast off.
- New child `Ring`: `Image`, 80×80, the `Circle Stroke - 2px` sprite from `UltimateCleanGUIPack`, raycast off.
- `TimeText`: rename to `BankedText`, width 600.
- `ActiveMiningMinigameView`: wire `_targetTemplate` and `_bankedText`.

- [ ] **Step 6: Run PlayMode: `ActiveMiningSceneFlowTests`, then a full EditMode run.** Expected: all pass.

- [ ] **Step 7: Check it by eye.** Render a scratch PNG mid-run, and confirm the ring reads clearly around the marker and the HUD isn't clipped.

- [ ] **Step 8: Commit** with `feat(ui): play active mining with closing rings and a banked counter`, including the new `.meta` file and the scene.

## Task 5: Feedback and instructions

**Files:**
- Create: `Assets/_Project/Scripts/UI/ActiveMiningRulesText.cs`
- Modify: `Assets/_Project/Scripts/Config/SfxId.cs`, `Assets/_Project/ScriptableObjects/AudioCatalog.asset`, `Assets/_Project/Scripts/UI/ActiveMiningMinigameView.cs`, `Assets/_Project/Scripts/Mining/ActiveMiningAsteroidStage.cs`, `Assets/Scenes/ActiveMining.unity`
- Test: create `Assets/_Project/Tests/EditMode/UI/ActiveMiningRulesTextTests.cs`; modify `Assets/_Project/Tests/PlayMode/ActiveMiningSceneFlowTests.cs`, `Assets/_Project/Tests/EditMode/Safety/AudioManagerTests.cs`

**Interfaces:**
- Produces: `static string ActiveMiningRulesText.For(ActiveMiningRunSettings settings)`, `ActiveMiningAsteroidStage.Shake(float seconds)`, `SfxId.ActiveMiningMiss`, `SfxId.ActiveMiningFail`.

- [ ] **Step 1: Write the failing tests**

```csharp
[Test] public void Rules_explain_the_ring() => Assert.AreEqual(
    "Tap each target before its ring closes. Earlier taps bank more.",
    ActiveMiningRulesText.For(TierOneSettings()));

[Test] public void New_sfx_ids_are_appended_so_catalog_numbers_hold()
{ Assert.AreEqual(13, (int)SfxId.BuildRemove); Assert.AreEqual(14, (int)SfxId.ActiveMiningMiss);
  Assert.AreEqual(15, (int)SfxId.ActiveMiningFail); }

// PlayMode
[UnityTest] public IEnumerator A_perfect_hit_shows_its_grade() { Assert.AreEqual("Perfect", GradeText()); }
[UnityTest] public IEnumerator A_wrong_tap_shows_a_miss() { Assert.AreEqual("Miss", GradeText()); }
[UnityTest] public IEnumerator The_pre_game_panel_shows_the_rules() { StringAssert.StartsWith("Tap each target", RulesText()); }
```

- [ ] **Step 2: Run EditMode and PlayMode with those classes.** Expected: compile failure.

- [ ] **Step 3: Implement**

- Add the two `SfxId` values at the end of the enum. Add catalog entries `Id: 14` and `Id: 15` that reuse Cancel's clip (`f77b39d91801cb64ba1de71db7837449`) as a placeholder.
- A miss plays `ActiveMiningMiss`, shows a red `Miss` in `_gradeText` and calls `Shake(0.2f)`. A failed run plays `ActiveMiningFail` and calls `Shake(0.5f)`.
- A hit shows `Perfect`, `Good` or `Late` at the target's position, fading over 0.5 seconds.
- `Shake` jitters the clone's local position from `Update`, then restores it.

- [ ] **Step 4: Edit the scene with a scratch script** (`ZZActiveMiningFeedback`). Add `GradeText` (centre-anchored, raycast off, styled like `ProgressText`) and `RulesText` under `PreGamePanel` (900 wide, wrapped, font size 36, raycast off, placed so it clears `MineralTypeText` and `StartButton`). Wire `_gradeText` and `_rulesText`.

- [ ] **Step 5: Run the Step 2 tests and check a scratch PNG of the pre-game panel.** Expected: all pass, and the text fits.

- [ ] **Step 6: Commit** with `feat(ui): add miss feedback, grade labels and run instructions`.

## Task 6: Show the idle and active choice on the prompt

**Files:**
- Modify: `Assets/_Project/Scripts/UI/AsteroidTierPrompt.cs`, `Assets/_Project/Scripts/UI/MiningModePromptView.cs`, `Assets/_Project/Scripts/Mining/MiningController.cs`
- Test: `Assets/_Project/Tests/EditMode/UI/AsteroidTierPromptTests.cs`

**Interfaces:**
- Produces: `static string AsteroidTierPrompt.PayLines(float idleFraction)`, `float MiningController.IdleYieldFraction`.

- [ ] **Step 1: Write the failing test**

```csharp
[Test] public void Pay_lines_give_percentages_not_amounts()
{
    Assert.AreEqual("Idle: safe, pays 70%\nActive: up to 100%, depends on how you play",
        AsteroidTierPrompt.PayLines(0.7f));
    StringAssert.StartsWith("Idle: safe, pays 75%", AsteroidTierPrompt.PayLines(0.75f));
}
```

- [ ] **Step 2: Run EditMode: `AsteroidTierPromptTests`.** Expected: compile failure.

- [ ] **Step 3: Implement.** In `MiningModePromptView.ShowTier`, append `"\n" + PayLines(_mining.IdleYieldFraction)` to the status only when `verdict.CanMine`.

- [ ] **Step 4: Run EditMode: `AsteroidTierPromptTests`.** Expected: all pass.

- [ ] **Step 5: Check it by eye.** Render the prompt with a scratch PNG. If three lines overflow `_tierStatusText`, turn on TMP auto-size with a scratch script.

- [ ] **Step 6: Commit** with `feat(ui): show idle and active pay on the mining prompt`.

## Task 7: Settle runs the player quit

**Files:**
- Create: `Assets/_Project/Scripts/Mining/ActiveMiningRunRecord.cs`, `Assets/_Project/Scripts/Mining/ActiveMiningRunStore.cs`
- Modify: `Assets/_Project/Scripts/Core/SaveKeys.cs`, `Assets/_Project/Scripts/Mining/ActiveMiningSessionRunner.cs`, `Assets/_Project/Scripts/Mining/MiningController.cs`, `Assets/_Project/Scripts/UI/ActiveMiningMinigameView.cs`, `Assets/_Project/Scripts/App/ActiveMiningSceneScope.cs`, `Assets/_Project/Scripts/App/PlanetSceneScope.cs`
- Test: create `Assets/_Project/Tests/EditMode/Mining/ActiveMiningRunStoreTests.cs`, `ActiveMiningSessionRunnerTests.cs`; modify `MiningControllerTests.cs`

**Interfaces:**
- Consumes: `IAuthService.PlayerId`, `ActiveMiningPayout.Quantity`.
- Produces:
  - `[Serializable] class ActiveMiningRunRecord { string PlayerId, PlanetId, SlotId, MineralId; int FullQuantity; float BankedFraction; }`
  - `class ActiveMiningRunStore(IAuthService auth)` with `Save(string planetId, string slotId, string mineralId, int fullQuantity, float bankedFraction)`, `ActiveMiningRunRecord Load()`, `Clear()`
  - `ActiveMiningSessionRunner(ActiveMiningHandoff handoff, ActiveMiningRunStore store)`, `SaveProgress()`
  - `MiningController` gains a final constructor parameter, `ActiveMiningRunStore runStore`
  - `SaveKeys.ActiveMiningRun = "active_mining_run"`

- [ ] **Step 1: Write the failing tests.** `SetUp` and `TearDown` delete the key. `LocalMockAuthService._playerId` is set by reflection. `CapturingMineralService` gains an `int GrantCalls` counter.

```csharp
// ActiveMiningRunStoreTests
[Test] public void Save_then_Load_round_trips_and_stamps_the_player() { /* PlayerId == "p1", other fields equal */ }
[Test] public void Another_players_marker_is_dropped()
{ Save as "p1"; set "p2"; Assert.IsNull(store.Load()); set "p1"; Assert.IsNull(store.Load()); }
[Test] public void A_malformed_marker_is_dropped() { /* SetString garbage: Load is null and the key is gone */ }

// ActiveMiningSessionRunnerTests
// Review Focus 4
[Test] public void Nothing_is_saved_before_the_run_begins()
{ new runner + handoff.Begin(...); Assert.IsNull(store.Load()); }
[Test] public void BeginRun_saves_a_marker_with_nothing_banked() { /* BankedFraction == 0, FullQuantity == 80 */ }
[Test] public void SaveProgress_records_the_banked_share() { /* hit one of two: 0.5 */ }
[Test] public void The_final_result_is_saved_and_handed_off() { /* run to the end: record and handoff agree */ }

// MiningControllerTests
[Test] public async Task A_leftover_marker_claims_its_banked_share_and_uses_up_the_asteroid()
{ // store.Save("test_planet", "Iron#1", "iron", 80, 0.5f); asteroid in slot
  Assert.AreEqual(40, _minerals.LastQty); Assert.IsTrue(asteroid.IsDepleted); Assert.IsNull(_runStore.Load()); }
[Test] public async Task A_leftover_marker_with_nothing_banked_forfeits() { /* LastForfeit set, marker cleared */ }
[Test] public async Task A_marker_for_another_planet_is_dropped_without_a_claim() { /* GrantCalls 0, LastForfeit null */ }
[Test] public async Task The_marker_stays_when_the_claim_throws() { /* Throw = true: Load not null, slot empties */ }
[Test] public async Task The_marker_stays_when_the_forfeit_throws() { /* ThrowOnForfeit = true: Load not null */ }
[Test] public async Task The_marker_clears_when_the_server_rejects() { /* RejectReason set: Load null */ }
[Test] public async Task The_normal_return_and_the_marker_claim_only_once()
{ // Begin, Save, SetResult(true, 1f), Initialize, Task.Yield
  Assert.AreEqual(1, _minerals.GrantCalls); }
// Review Focus 5
[Test] public async Task A_marker_whose_slot_is_gone_clears_without_a_claim()
{ // no asteroid in the slot: the server already logged it
  Assert.AreEqual(0, _minerals.GrantCalls); Assert.IsNull(_minerals.LastForfeit); Assert.IsNull(_runStore.Load()); }
```

- [ ] **Step 2: Run EditMode: `ActiveMiningRunStoreTests;ActiveMiningSessionRunnerTests;MiningControllerTests`.** Expected: compile failure.

- [ ] **Step 3: Implement the record and store.** Use `JsonUtility` and `PlayerPrefs.Save()` on each write. `Load` deletes the key and returns null for malformed data or another player's marker.

- [ ] **Step 4: Implement the runner.** `BeginRun` saves with banked 0. The stage handler saves the final banked share, then calls `SetResult`. `SaveProgress` is a no-op before the run begins. Don't save on each hit.

- [ ] **Step 5: Implement the controller**

- `TryFinalizePendingActiveMining` returns whether it handled a result, and clears the store if the slot no longer resolves.
- `Initialize` calls a new `TrySettleLeftoverRun` only when nothing was handled.
- `TrySettleLeftoverRun`: a null record does nothing. Another planet, or a slot that is gone or depleted, clears the store. Otherwise it settles through the same claim or forfeit path.
- `CompleteActiveMiningAsync` and `ForfeitAsync` clear the store after the call returns, whether it granted or rejected, and keep it when the call throws.

- [ ] **Step 6: Wire it up.** Register `ActiveMiningRunStore` as a singleton in `ActiveMiningSceneScope` and `PlanetSceneScope`. Add `OnApplicationPause(bool paused)` and `OnApplicationQuit()` to `ActiveMiningMinigameView`, both calling `_runner.SaveProgress()`.

- [ ] **Step 7: Run the Step 2 filter, then PlayMode: `ActiveMiningSceneFlowTests`.** Expected: all pass.

- [ ] **Step 8: Commit** with `feat(mining): settle quit active mining runs from a local marker`.

A new run overwrites a marker kept after a failed claim, which loses that one reward. This is accepted: it needs a failed call followed by another run without a Planet reload.

## Task 8: Dev tools and Phase 1 wrap-up

**Files:**
- Create: `Assets/_Project/Scripts/Mining/ActiveMiningRunLog.cs`, `Assets/_Project/Scripts/UI/ActiveMiningDevTierPicker.cs`
- Modify: `Assets/_Project/Scripts/Core/ActiveMiningHandoff.cs`, `Assets/_Project/Scripts/Mining/MiningController.cs`, `Assets/_Project/Scripts/Mining/ActiveMiningSessionRunner.cs`, `Assets/_Project/Scripts/UI/ActiveMiningMinigameView.cs`, `PROGRESS.md`, `CLAUDE.md`
- Test: create `Assets/_Project/Tests/EditMode/Mining/ActiveMiningRunLogTests.cs`; modify `ActiveMiningHandoffTests.cs`

**Interfaces:**
- Produces, all inside `#if UNITY_EDITOR || DEVELOPMENT_BUILD`:
  - `static string ActiveMiningRunLog.Format(int tierNumber, ActiveMiningSession session)`
  - `ActiveMiningHandoff.DevSettingsForTier` (`Func<int, ActiveMiningRunSettings>`), `ActiveMiningHandoff.UseSettings(ActiveMiningRunSettings settings)`
  - `ActiveMiningDevTierPicker : MonoBehaviour`, with `Action<int> OnTierPicked`

- [ ] **Step 1: Write the failing tests**

```csharp
[Test] public void A_run_logs_its_tier_grades_misses_and_bank()
{ // 2 targets: one Perfect, one Good, then finished
  Assert.AreEqual("ActiveMining run: tier=3 perfect=1 good=1 late=0 misses=0 banked=0.90 result=Success",
      ActiveMiningRunLog.Format(3, s)); }

[Test] public void UseSettings_replaces_the_runs_settings()
{ _handoff.Begin(..., settingsA); _handoff.UseSettings(settingsB); Assert.AreSame(settingsB, _handoff.Settings); }
```

- [ ] **Step 2: Run EditMode: `ActiveMiningRunLogTests;ActiveMiningHandoffTests`.** Expected: compile failure.

- [ ] **Step 3: Implement.** `Format` uses invariant culture. The runner logs it through `SULog` on run end. `BeginActiveMining` sets `DevSettingsForTier = _config.ActiveRunSettings`. The view adds the picker in `Start` and removes it when Start is pressed. The picker draws Tier 1–5 buttons with `OnGUI`, large enough for a phone. Picking a tier calls `UseSettings`, and the panel's rules text refreshes.

- [ ] **Step 4: Run the Step 2 filter.** Expected: all pass.

- [ ] **Step 5: Confirm the dev code is compiled out.** Check that each dev-only member above is inside the `#if`. Expected: `grep -n "UNITY_EDITOR || DEVELOPMENT_BUILD"` lists all four changed files, and no release path calls these members outside an `#if`.

- [ ] **Step 6: Update the docs.** In `PROGRESS.md`, rewrite the `ActiveMiningMinigame` and `ActiveMiningSessionController` rows (lines 267–268, which describe the older tap window) and add this plan's status. In `CLAUDE.md`, update the test counts.

- [ ] **Step 7: Run full EditMode and PlayMode.** Expected: all pass, and the `ServerCode/` drift tests pass untouched.

- [ ] **Step 8: Commit** with `feat(mining): add dev tier picker and run log, update status docs`.

## STOP: Phase 1 play session (project owner)

Don't start Phase 2 until the owner has played and said to continue. The targets are guides, not pass or fail.

1. Set `_devMode` to on in `Bootstrap.unity`, without saving it to git.
2. In Build Settings, turn on **Development Build**, connect the phone over USB and choose **Build And Run**.
3. If Unity asks for a keystore password, the owner can temporarily switch to the debug keystore, without committing that change.
4. Play Tiers 1 and 2 with the dev picker, and tune `EconomyConfig` if needed. Check for:
   - Tier 1 clears nearly every time and banks at least 60%, even played casually.
   - A lost run feels like the player's mistake, not the game's.

This is the first time M6-era code runs on a device, so unrelated device problems may show up. Record them separately rather than fixing them in this plan.

---

# Phase 2: Tap-based challenges

## Task 9: Decoys

**Files:**
- Modify: `Assets/_Project/Scripts/Config/ActiveMiningTierSettings.cs`, `Assets/_Project/ScriptableObjects/EconomyConfig.asset`, `Assets/_Project/Scripts/Mining/ActiveMiningTargetKind.cs`, `Assets/_Project/Scripts/Mining/ActiveMiningSession.cs`, `Assets/_Project/Scripts/Mining/ActiveMiningTargetPoint.cs`, `Assets/_Project/Scripts/UI/ActiveMiningTargetView.cs`, `Assets/_Project/Scripts/UI/ActiveMiningMinigameView.cs`, `Assets/_Project/Scripts/UI/ActiveMiningRulesText.cs`, `Assets/Scenes/ActiveMining.unity`
- Test: `ActiveMiningConfigTests.cs`, `ActiveMiningSessionTests.cs`, `ActiveMiningTargetPointTests.cs`, `ActiveMiningRulesTextTests.cs`

**Interfaces:**
- Produces: `ActiveMiningTierSettings.DecoyChance` (optional constructor parameter `float decoyChance = 0f`), `ActiveMiningTargetKind.Decoy`, `static bool ActiveMiningTargetPoint.IsFarEnough(Vector2 candidate, IReadOnlyList<Vector2> others, float minDistance)`.

- [ ] **Step 1: Write the failing tests**

```csharp
[TestCase(1, 0f)] [TestCase(2, 0.25f)] [TestCase(3, 0.25f)] [TestCase(4, 0.35f)] [TestCase(5, 0.35f)]
public void Real_asset_holds_decoy_chances(int tier, float chance) { /* ... */ }

[Test] public void A_decoy_spawns_with_a_target_when_the_roll_is_under_the_chance()
{ var s = New(decoyChance: 0.25f, random: () => 0.1f); s.Tick(0f);
  Assert.AreEqual(2, s.LiveTargets.Count);
  Assert.AreEqual(1, s.LiveTargets.Count(t => t.Kind == ActiveMiningTargetKind.Decoy)); }

[Test] public void No_decoy_spawns_when_the_roll_is_over_the_chance() { /* random 0.5: 1 live */ }

[Test] public void Tapping_a_decoy_is_a_miss_that_costs_no_share()
{ s.TapTarget(decoyId); Assert.AreEqual((1, 0, 0f), (s.ErrorCount, s.ResolvedTargets, s.BankedFraction));
  Assert.IsTrue(s.LiveTargets.Any(t => t.Kind == ActiveMiningTargetKind.Normal)); }

[Test] public void An_ignored_decoy_disappears_at_no_cost()
{ /* hit the real target, advance past the ring: ErrorCount 0, ResolvedTargets 1, no decoy live */ }

[Test] public void Decoys_do_not_count_toward_targets() { /* 2 targets, a decoy each: Success after 2 hits */ }
[Test] public void An_off_target_tap_with_only_a_decoy_live_is_ignored() { /* ErrorCount 0 */ }

[Test] public void IsFarEnough_rejects_a_point_inside_the_distance()
{ var others = new[] { Vector2.zero };
  Assert.IsFalse(ActiveMiningTargetPoint.IsFarEnough(new Vector2(100, 0), others, 160f));
  Assert.IsTrue(ActiveMiningTargetPoint.IsFarEnough(new Vector2(200, 0), others, 160f)); }

[Test] public void Tier_2_rules_name_the_decoy() => StringAssert.Contains(
    "New: red X spots are decoys, so leave them alone.", ActiveMiningRulesText.For(TierTwo()));
[Test] public void Tier_3_rules_do_not_repeat_the_decoy() { /* Tier 3 has the same chance as Tier 2 */ }
```

- [ ] **Step 2: Run EditMode with those classes.** Expected: compile failure.

- [ ] **Step 3: Implement the session and rules text.** Roll once per real target spawned, through the injected `random`, defaulting to `UnityEngine.Random.value`. A decoy has the same ring time as its tier's targets. The rules text names a challenge only if this tier has it and `PreviousTier` doesn't.

- [ ] **Step 4: Implement the view.** `Show` sets a red X-shaped sprite for a decoy, so it differs in shape as well as colour. Placement retries up to 16 times until `IsFarEnough` holds against live markers, at `160 × canvas.scaleFactor` screen pixels, then accepts the last try. A tapped decoy shows `Miss` and plays the miss feedback. Add `_decoySprite` and wire it with a scratch script, choosing an X icon from `UltimateCleanGUIPack`.

- [ ] **Step 5: Add the chances to `EconomyConfig.asset`, then run the Step 2 tests and PlayMode: `ActiveMiningSceneFlowTests`.** Expected: all pass.

- [ ] **Step 6: Commit** with `feat(mining): add decoy spots from tier 2`.

## Task 10: Two live targets

**Files:**
- Modify: `Assets/_Project/Scripts/Config/ActiveMiningTierSettings.cs`, `Assets/_Project/Scripts/Config/ActiveMiningRunSettings.cs`, `Assets/_Project/Scripts/Config/EconomyConfig.cs`, `Assets/_Project/ScriptableObjects/EconomyConfig.asset`, `Assets/_Project/Scripts/Mining/ActiveMiningSession.cs`, `Assets/_Project/Scripts/UI/ActiveMiningRulesText.cs`
- Test: `ActiveMiningConfigTests.cs`, `ActiveMiningSessionTests.cs`, `ActiveMiningRulesTextTests.cs`

**Interfaces:**
- Produces: `ActiveMiningTierSettings.LiveTargets` (optional constructor parameter `int liveTargets = 1`), `ActiveMiningRunSettings.SecondTargetStagger` (from `EconomyConfig._activeSecondTargetStagger = 0.4f`).

- [ ] **Step 1: Write the failing tests**

```csharp
[TestCase(1, 1)] [TestCase(2, 1)] [TestCase(3, 2)] [TestCase(4, 2)] [TestCase(5, 2)]
public void Real_asset_holds_live_target_counts(int tier, int live) { /* ... */ }

[Test] public void A_second_target_joins_at_40_percent_of_the_first_ring()
{ // ring 1.1s, liveTargets 2
  s.Tick(0f); Advance(s, 0.4f); Assert.AreEqual(1, s.LiveTargets.Count);
  Advance(s, 0.05f); Assert.AreEqual(2, s.LiveTargets.Count); }

[Test] public void Live_targets_never_exceed_the_tier_limit() { /* advance 1s: 2 live, ignoring decoys */ }
[Test] public void The_remaining_target_keeps_its_own_ring_when_the_other_resolves() { /* Elapsed unchanged */ }
[Test] public void No_more_targets_spawn_than_the_run_holds() { /* 2 targets: total spawned == 2 */ }
[Test] public void The_spawn_gap_applies_only_when_nothing_is_live() { /* resolve both, then 0.25s gap */ }
[Test] public void Tier_3_rules_name_two_targets() => StringAssert.Contains(
    "New: two targets can be live at once.", ActiveMiningRulesText.For(TierThree()));
```

- [ ] **Step 2: Run EditMode with those classes.** Expected: fail.

- [ ] **Step 3: Implement the spawn rule**

```
each Tick, after ring updates, while targets remain unspawned:
  live normals == 0: spawn when the gap timer has run out
  live normals < LiveTargets and the newest normal's Progress >= SecondTargetStagger: spawn
```

- [ ] **Step 4: Add the values to the asset, run the Step 2 tests and PlayMode: `ActiveMiningSceneFlowTests`.** Expected: all pass.

- [ ] **Step 5: Commit** with `feat(mining): add two live targets from tier 3`.

## Task 11: Faster spin, placement cone and Phase 2 wrap-up

**Files:**
- Modify: `Assets/_Project/Scripts/Config/ActiveMiningTierSettings.cs`, `Assets/_Project/ScriptableObjects/EconomyConfig.asset`, `Assets/_Project/Scripts/Mining/ActiveMiningAsteroidStage.cs`, `Assets/_Project/Scripts/Mining/ActiveMiningTargetPoint.cs`, `Assets/_Project/Scripts/UI/ActiveMiningMinigameView.cs`, `Assets/_Project/Scripts/UI/ActiveMiningRulesText.cs`, `PROGRESS.md`, `CLAUDE.md`
- Test: `ActiveMiningConfigTests.cs`, `ActiveMiningAsteroidStageTests.cs`, `ActiveMiningTargetPointTests.cs`, `ActiveMiningRulesTextTests.cs`

**Interfaces:**
- Produces:
  - `ActiveMiningTierSettings.MinSpinDegrees`, `MaxSpinDegrees` (optional constructor parameters, defaulting to 5 and 15)
  - `ActiveMiningAsteroidStage.SetSpinRange(float minDegrees, float maxDegrees)`, `float RotationSpeed`
  - `static float ActiveMiningTargetPoint.MaxPlacementAngle(float maxSpinDegrees, float ringSeconds)`
  - `PickFacingPoint(Vector3 center, float radius, Vector3 towardViewer, float maxAngleDegrees = 90f)` and `PlaceOnAsteroid(Transform asteroidTransform, float radius, Vector3 towardViewer, float maxAngleDegrees = 90f)`

- [ ] **Step 1: Write the failing tests**

```csharp
[TestCase(1, 5f, 15f)] [TestCase(2, 5f, 15f)] [TestCase(3, 5f, 15f)] [TestCase(4, 30f, 45f)] [TestCase(5, 30f, 45f)]
public void Real_asset_holds_spin_ranges(int tier, float min, float max) { /* ... */ }

[TestCase(15f, 1.6f, 61f)] [TestCase(45f, 0.9f, 44.5f)]
public void Placement_angle_leaves_room_for_the_spin(float spin, float ring, float expected) =>
    Assert.AreEqual(expected, ActiveMiningTargetPoint.MaxPlacementAngle(spin, ring), 0.01f);

[Test] public void Placement_angle_never_drops_below_15_degrees() =>
    Assert.AreEqual(15f, ActiveMiningTargetPoint.MaxPlacementAngle(100f, 5f), 0.01f);

[Test] public void Points_stay_inside_the_cone_and_on_the_sphere()
{ for 200 samples at maxAngle 44.5f:
    Assert.AreEqual(radius, offset.magnitude, 0.001f);
    Assert.LessOrEqual(Vector3.Angle(offset, towardViewer), 44.6f); }

[Test] public void SetSpinRange_picks_a_speed_inside_the_range()
{ _stage.SpawnClone(def); _stage.SetSpinRange(30f, 45f);
  Assert.That(_stage.RotationSpeed, Is.InRange(30f, 45f)); }

[Test] public void Tier_4_rules_name_the_spin() => StringAssert.Contains(
    "New: the asteroid spins faster, so targets move.", ActiveMiningRulesText.For(TierFour()));
```

- [ ] **Step 2: Run EditMode with those classes.** Expected: compile failure.

- [ ] **Step 3: Implement.** `MaxPlacementAngle` is `Clamp(85 − spin × ring, 15, 85)`. Sample inside the cone directly: choose `cos θ` uniformly between `cos(maxAngle)` and 1, and a random angle around the viewer axis. The existing hemisphere test stays valid with the default of 90°. `OnStartClicked` calls `SetSpinRange` from `Settings.Tier` and passes the placement angle to `PlaceOnAsteroid`.

- [ ] **Step 4: Add the spin ranges to the asset. Run full EditMode and PlayMode.** Expected: all pass.

- [ ] **Step 5: Update `PROGRESS.md` and the test counts in `CLAUDE.md`.**

- [ ] **Step 6: Commit** with `feat(mining): add faster spin and placement cone from tier 4`.

## STOP: Phase 2 play session (project owner)

Don't start Phase 3 until the owner has played and said to continue. Use the same build steps as Phase 1. Check for:

- At Tier 5, without veins, a skilled run usually banks 60–85%, with full-Perfect runs rare but achieved.
- A target never reaches the asteroid's far side before its ring closes.
- Decoys are easy to tell apart at a glance.
- Tune `EconomyConfig`, and note the results.

---

# Phase 3: Hold and drag

This phase has a go or no-go. If veins don't feel good, set `_veinEvery: 0` for Tier 5 in `EconomyConfig.asset`. Tier 5 then ships without them and no other change is needed.

## Task 12: Vein rules in the session

**Files:**
- Modify: `Assets/_Project/Scripts/Config/ActiveMiningTierSettings.cs`, `Assets/_Project/Scripts/Config/ActiveMiningRunSettings.cs`, `Assets/_Project/Scripts/Config/EconomyConfig.cs`, `Assets/_Project/ScriptableObjects/EconomyConfig.asset`, `Assets/_Project/Scripts/Mining/ActiveMiningTargetKind.cs`, `Assets/_Project/Scripts/Mining/ActiveMiningSession.cs`
- Test: `ActiveMiningConfigTests.cs`, `ActiveMiningSessionTests.cs`

**Interfaces:**
- Produces:
  - `ActiveMiningTierSettings.VeinEvery` (optional constructor parameter `int veinEvery = 0`)
  - `ActiveMiningRunSettings.VeinRingSeconds`, `VeinPerfectBySeconds`, `VeinGoodBySeconds`, `VeinSpinDegrees`, from `EconomyConfig` fields defaulting to `1.6f`, `1.1f`, `1.35f` and `5f`. The Perfect and Good times are starting guesses for playtest.
  - `ActiveMiningTargetKind.Vein`
  - `ActiveMiningSession.IsVeinLive`, `CompleteVein(int targetId)`, `FailVein(int targetId)`

- [ ] **Step 1: Write the failing tests**

```csharp
[Test] public void Real_asset_has_veins_only_at_tier_5() { /* VeinEvery 0 for tiers 1–4, 4 for tier 5 */ }
[Test] public void Every_fourth_target_is_a_vein() { /* kinds in order: N, N, N, Vein */ }
[Test] public void A_vein_waits_for_live_targets_to_resolve() { /* target 3 live: target 4 doesn't spawn */ }
[Test] public void Nothing_spawns_while_a_vein_is_live()
{ /* decoy chance 1, liveTargets 2: only the vein is live for its whole ring */ }
[Test] public void Completing_a_vein_grades_by_its_own_times()
{ /* at 1.0s Perfect, 1.3s Good, 1.5s Late */ }
[Test] public void A_failed_vein_is_a_miss_that_uses_it_up()
{ s.FailVein(id); Assert.AreEqual((1, 1, 0f), (s.ErrorCount, s.ResolvedTargets, s.BankedFraction));
  Assert.IsFalse(s.IsVeinLive); }
[Test] public void A_closed_vein_ring_is_a_miss() { /* 1.65s */ }
[Test] public void A_tap_does_not_complete_a_vein() { /* TapTarget(id): still live, no miss */ }
[Test] public void Off_target_taps_are_ignored_while_a_vein_is_live() { /* ErrorCount 0 */ }
```

- [ ] **Step 2: Run EditMode with those classes.** Expected: fail.

- [ ] **Step 3: Implement.** A target's number is a vein when `VeinEvery > 0` and `number % VeinEvery == 0`. Before spawning a vein, wait until nothing is live, then apply the gap. While a vein is live, nothing else spawns.

- [ ] **Step 4: Run the Step 2 tests and PlayMode: `ActiveMiningSceneFlowTests`.** Expected: all pass.

- [ ] **Step 5: Commit** with `feat(mining): add vein rules to the active mining session`.

## Task 13: Vein input, view and wrap-up

**Files:**
- Create: `Assets/_Project/Scripts/Mining/ActiveMiningVeinPath.cs`, `Assets/_Project/Scripts/UI/ActiveMiningVeinView.cs`
- Modify: `Assets/_Project/Scripts/Mining/ActiveMiningAsteroidStage.cs`, `Assets/_Project/Scripts/UI/ActiveMiningMinigameView.cs`, `Assets/_Project/Scripts/UI/ActiveMiningRulesText.cs`, `Assets/Scenes/ActiveMining.unity`, `PROGRESS.md`, `CLAUDE.md`, `Social_Universe_Architecture.md`
- Test: create `Assets/_Project/Tests/EditMode/Mining/ActiveMiningVeinPathTests.cs`; modify `ActiveMiningAsteroidStageTests.cs`, `ActiveMiningRulesTextTests.cs`, `Assets/_Project/Tests/PlayMode/ActiveMiningSceneFlowTests.cs`

**Interfaces:**
- Consumes: `CompleteVein`, `FailVein`, `IsVeinLive`, `VeinSpinDegrees` (Task 12).
- Produces:
  - `static float ActiveMiningVeinPath.DistanceToSegment(Vector2 point, Vector2 start, Vector2 end)`
  - `static float ActiveMiningVeinPath.ProgressAlong(Vector2 point, Vector2 start, Vector2 end)`, returning 0–1
  - `ActiveMiningVeinView : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler`
  - `ActiveMiningAsteroidStage.EaseSpinTo(float degrees)`, `RestoreSpin()`

- [ ] **Step 1: Write the failing tests**

```csharp
[Test] public void Distance_is_measured_to_the_nearest_point_on_the_segment()
{ var a = Vector2.zero; var b = new Vector2(100, 0);
  Assert.AreEqual(30f, ActiveMiningVeinPath.DistanceToSegment(new Vector2(50, 30), a, b), 0.01f);
  Assert.AreEqual(50f, ActiveMiningVeinPath.DistanceToSegment(new Vector2(-50, 0), a, b), 0.01f); }

[Test] public void Progress_runs_from_0_to_1_and_clamps()
{ Assert.AreEqual(0.5f, ActiveMiningVeinPath.ProgressAlong(new Vector2(50, 10), a, b), 0.01f);
  Assert.AreEqual(1f, ActiveMiningVeinPath.ProgressAlong(new Vector2(200, 0), a, b), 0.01f); }

[Test] public void EaseSpinTo_slows_the_asteroid_and_RestoreSpin_returns_it()
{ /* speed 40, EaseSpinTo(5): reaches 5 within 0.2s of ticks; RestoreSpin: back to 40 */ }

[Test] public void Tier_5_rules_name_the_veins() => StringAssert.Contains(
    "New: press and drag along glowing veins.", ActiveMiningRulesText.For(TierFive()));

// PlayMode, driving OnPointerDown, OnDrag and OnPointerUp directly with PointerEventData
[UnityTest] public IEnumerator Dragging_start_to_end_completes_the_vein() { /* ResolvedTargets up, grade shown */ }
[UnityTest] public IEnumerator Lifting_early_fails_the_vein() { /* ErrorCount 1, ResolvedTargets 1 */ }
[UnityTest] public IEnumerator Straying_off_the_line_fails_the_vein() { /* the same */ }
[UnityTest] public IEnumerator A_press_away_from_the_start_costs_nothing() { /* ErrorCount 0, vein still live */ }
```

- [ ] **Step 2: Run EditMode and PlayMode with those classes.** Expected: compile failure.

- [ ] **Step 3: Implement the path helpers, stage easing and vein view**

- Use the first pointer only and ignore others.
- A press counts only within the start marker's tap area.
- Complete at `ProgressAlong >= 0.95`.
- Fail when the distance is over 90 canvas units or the pointer lifts early. These two values are starting points for playtest.
- Both endpoints use `PlaceOnAsteroid` with `MaxPlacementAngle(VeinSpinDegrees, VeinRingSeconds)`, kept at least 300 canvas units apart.
- The view draws a stretched `Image` between the projected endpoints each frame, and shows the same closing ring at the start marker.
- `ActiveMiningMinigameView` calls `EaseSpinTo` when a vein spawns and `RestoreSpin` when it resolves, and hides all other input visuals for that time.

- [ ] **Step 4: Edit the scene with a scratch script** (`ZZActiveMiningVeins`). Add an inactive `VeinView` under `Canvas`, above `MissArea`, with `Line`, `StartMarker`, `EndMarker` and `Ring` children, and wire `_veinView`.

- [ ] **Step 5: Run full EditMode and PlayMode.** Expected: all pass.

- [ ] **Step 6: Update the docs.** Mark this work done in `PROGRESS.md`, update test counts in `CLAUDE.md`, and replace the `ActiveMiningMinigame` row in `Social_Universe_Architecture.md` (line 171) with `ActiveMiningSession` and the new types.

- [ ] **Step 7: Commit** with `feat(mining): add hold-and-drag veins at tier 5`.

## STOP: Phase 3 play session (project owner)

Same build steps as Phase 1. Decide go or no-go on veins. For a no-go, set `_veinEvery: 0` for Tier 5. The spec's later targets (new players, active versus idle, small and large phones) wait until other people can play, after the key rotation and backend setup.
