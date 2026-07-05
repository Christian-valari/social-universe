# Active Mining as a True Separate Scene (with Pre-/Post-Game UI)

**Status:** Approved
**Date:** 2026-07-06
**Depends on:** `2026-07-04-active-mining-minigame-scene-design.md` (ActiveMining scene,
world-anchored target points — already implemented on this branch)
**Supersedes:** the additive-overlay-on-Planet architecture from the spec above (§2/§5 of
that spec). Everything else from it (world-anchored target points, asteroid clone, overall
countdown, Cinemachine framing) is retained unchanged.

## 1. Goal

Today, ActiveMining is loaded additively on top of a Planet scene that keeps running behind
it (only the camera is disabled). This spec makes ActiveMining a true separate scene: Planet
fully unloads before the minigame starts and reloads only after it ends, matching the
Hub → Travel → Hub pattern already used for planetary travel. It also adds proper pre-game
and post-game screens in place of the current bare Start-immediately / brief-flash-banner
behavior.

- Starting active mining unloads Planet and loads `ActiveMining.unity` as the sole running
  gameplay scene.
- A pre-game panel shows the asteroid's mineral type and a **Start Mining** button; the
  countdown and target spawning don't begin until the player presses it.
- Ending the session (Success or Failed) shows a post-game panel with the result, the mined
  amount, and coins earned, plus a **Continue** button.
- Pressing Continue unloads ActiveMining and reloads Planet, at which point the reward is
  actually granted server-side.
- Reward math and totals are unchanged — this spec only touches how the scene is entered,
  presented, and exited.

## 2. Why the reward grant can't happen inside ActiveMining

`MiningController`, `IEconomyService`, `AsteroidSpawner`, and `MiningRewardCalculator` are all
registered inside `PlanetSceneScope` (`Assets/_Project/Scripts/App/PlanetSceneScope.cs`).
Once Planet unloads, all of them are destroyed along with it — there is no live `Asteroid`
MonoBehaviour and no economy service available while ActiveMining is up. The reward grant
(`IEconomyService.GrantMiningRewardAsync`) must happen after Planet reloads, resolved back
onto the correct asteroid. §4 below covers exactly how.

## 3. FSM / scene architecture

- New `SocialUniverse.Core.ActiveMiningState : IGameState`, registered as a singleton in
  `ProjectLifetimeScope` alongside `PlanetState`/`TravelState`/`HubState`. Shape mirrors
  `TravelState`:
  ```csharp
  public void Enter() => _ = LoadAsync();
  public void Tick()  { }
  public void Exit()  => _ = UnloadAsync();

  private async Task LoadAsync()
  {
      await _sceneLoader.LoadAsync(Constants.SceneNames.LoadingScreen);
      await _sceneLoader.LoadAsync(Constants.SceneNames.ActiveMining);
  }

  private async Task UnloadAsync()
  {
      await _sceneLoader.UnloadAsync(Constants.SceneNames.ActiveMining);
  }
  ```
  Same `LoadingScreen` convention `PlanetState`/`TravelState` already use for scene
  transitions — no special takeoff/landing animation is in scope here.
- `MiningModePromptView.OnActiveMineClicked()` no longer calls
  `MiningController.BeginActiveMining`. Instead it calls a new
  `MiningController.StartActiveMining(Asteroid asteroid)`, which:
  1. Computes the reward via the existing `MiningRewardCalculator.Compute(asteroid)`.
  2. Populates `ActiveMiningHandoff` (§4) with everything the ActiveMining scene and the
     later finalize step need.
  3. Transitions the FSM: `_fsm.TransitionTo(_resolver.Resolve<ActiveMiningState>())` — same
     resolver-transition pattern `HubState.TravelToPlanet`/`LandOnPlanet` already use.
- `ActiveMiningSceneScope` (`Assets/_Project/Scripts/App/ActiveMiningSceneScope.cs`) stops
  parenting to `PlanetSceneScope` (Planet is no longer loaded when this scene is). It becomes
  standalone/Root-resolvable like `TravelSceneScope`: no `parentReference.TypeName` set in
  the scene file, and it registers what it needs to run gameplay locally (see §4/§5) —
  `IEconomyService` and `AsteroidSpawner` are deliberately **not** re-registered here; nothing
  in ActiveMining needs them (see §2).
- Round trip: `MiningController` doesn't exist while ActiveMining is loaded, so the post-game
  Continue button can't call back into it directly. Instead it resolves `GameStateMachine`/
  `PlanetState` directly (both Root-registered, reachable from `ActiveMiningSceneScope` the
  same way `TravelSceneScope` reaches `PlanetState` today) and transitions back:
  ```csharp
  var state = _resolver.Resolve<PlanetState>();
  state.TargetPlanetId = _handoff.PlanetId;
  _fsm.TransitionTo(state);
  ```

## 4. `ActiveMiningHandoff` — carrying state across the swap

New plain C# class, `SocialUniverse.Mining.ActiveMiningHandoff`, registered
`Lifetime.Singleton` in `ProjectLifetimeScope` (survives scene swaps the same way
`GameStateMachine` and `PlanetState.TargetPlanetId` do).

```csharp
public class ActiveMiningHandoff
{
    public string           PlanetId              { get; private set; }
    public string           AsteroidSlotId        { get; private set; }
    public AsteroidDefinition Definition          { get; private set; }
    public int              RemainingYieldAtStart { get; private set; }
    public int              TapsRequired          { get; private set; }
    public int              MaxErrors             { get; private set; }
    public float             SessionDurationSeconds { get; private set; }

    public bool              HasResult { get; private set; }
    public bool              Succeeded { get; private set; }

    public void Begin(string planetId, Asteroid asteroid, MiningReward reward) { ... }
    public void SetResult(bool succeeded) { HasResult = true; Succeeded = succeeded; }
    public void Clear() { /* reset all fields, HasResult = false */ }
}
```

`AsteroidDefinition` is a ScriptableObject project asset, not a scene object — safe to hold a
reference to it across a scene unload. `RemainingYieldAtStart` is captured once, since only
one mining session can run on a given asteroid at a time (existing guard), so it can't change
while the minigame is in progress.

- **Populated** by `MiningController.StartActiveMining` before the FSM transition (§3).
- **Read** by `ActiveMiningSceneScope`'s bootstrapper to build the pre-game panel (mineral
  type) and construct the local `ActiveMiningSession` (`TapsRequired`/`MaxErrors`/
  `SessionDurationSeconds` — the class itself is unchanged from the previous spec).
- **Written** (`SetResult`) by the ActiveMining scene when the session resolves.
- **Consumed and cleared** by `MiningController` once Planet reloads (§5).

## 5. Finalizing on return to Planet

`MiningController.Initialize(DroneRuntime drone)` — the same place `TryRestoreIdleSession`
already runs — gains a new step, `TryFinalizePendingActiveMining()`:

```csharp
private void TryFinalizePendingActiveMining()
{
    if (!_handoff.HasResult) return;

    var asteroid = _spawner.FindBySlotId(_handoff.AsteroidSlotId);
    if (asteroid == null) { _handoff.Clear(); return; } // respawned/gone — nothing to finalize

    if (_handoff.Succeeded) CompleteActiveMiningAsync(asteroid); // fire-and-forget, same as today
    else                     FailActiveMining(asteroid);

    _handoff.Clear();
}
```

`CompleteActiveMiningAsync`/`FailActiveMining` keep their current bodies (mine the yield,
call `_economy.GrantMiningRewardAsync`, schedule respawn via `_spawner.ScheduleRespawn`) —
they just take an `Asteroid` parameter directly now instead of reading `session.Asteroid`,
since there is no more `ActiveMiningSession` living in `MiningController` at all.

This mirrors `TryRestoreIdleSession`'s existing `_spawner.FindBySlotId` + null-guard pattern
exactly, including the "asteroid no longer there → silently drop" fallback (same tolerance
idle-session restore already has for a slot that no longer resolves).

## 6. In-scene session & UI flow

- **`ActiveMiningSession`** (`Assets/_Project/Scripts/Mining/ActiveMiningSession.cs`) is
  reused as-is (already decoupled from any live `Asteroid` reference — see the 2026-07-04
  spec). It's now constructed inside the ActiveMining scene itself, from the handoff's
  `TapsRequired`/`MaxErrors`/`SessionDurationSeconds`, by a new
  `ActiveMiningSessionRunner : IStartable, ITickable` registered in `ActiveMiningSceneScope`
  (relocates the role `ActiveMiningSessionController` played in `PlanetSceneScope`, since that
  class is removed — see §8).
- **`ActiveMiningMinigameView`** gains a local
  `enum ActiveMiningPhase { PreGame, InProgress, PostGame }` and three panel GameObjects
  (new fields `_preGamePanel`, `_postGamePanel`, reusing the existing in-progress UI as the
  third panel):
  - **PreGame:** shows `_handoff.Definition.MineralType` and a "Start Mining" button. The
    `ActiveMiningSessionRunner`'s `Tick()` and target-point spawning are both gated on
    `Phase == InProgress` — this also fixes the first-target-spawn timing gap the previous
    branch's final review flagged, since there's no longer a race between scene-load and the
    first target: nothing spawns until the player explicitly presses Start.
  - **InProgress:** unchanged existing target-point/timer/progress/miss-count UI.
  - **PostGame:** replaces today's bare `ResultBanner`/`ResultText` with a heading
    (Success/Failed), mined amount, coins earned (computed client-side from
    `_handoff.RemainingYieldAtStart` and `_handoff.Definition.CoinsPerUnit` — a preview, not
    the authoritative server value, matching how idle-mining claims already display a
    client-computed number before the async grant resolves), and a **Continue** button that
    calls the FSM-transition-back described in §3.
- Taps still route through the session's `RegisterHit`/`RegisterMiss` directly (no more
  `MiningController.RegisterActiveTap` — that indirection is gone along with
  `ActiveMiningMinigame`).

## 7. Testing

- **EditMode:**
  - New `ActiveMiningHandoffTests`: `Begin` populates all fields correctly from an
    `Asteroid`/`MiningReward`; `SetResult`/`Clear` behave as expected; `HasResult` starts
    `false`.
  - `MiningControllerTests`: replace `BeginActiveMining`/`RegisterActiveTap`/
    `TickActiveSession`/`OnActiveSessionChanged` coverage with tests for
    `StartActiveMining` (populates the handoff, does not itself touch the FSM types directly
    testable — verify handoff state) and `TryFinalizePendingActiveMining` (mirrors existing
    `TryRestoreIdleSession` test shape: asteroid found → grants reward + respawns; asteroid
    not found → clears handoff without throwing).
  - `ActiveMiningSession`, `MiningRewardCalculator`, `ActiveMiningTargetPoint`,
    `ActiveMiningAsteroidStage` tests are unaffected.
  - Delete `ActiveMiningMinigameTests` (class under test is removed).
- **PlayMode:** same structural-only precedent as the 2026-07-04 spec — no automated
  assertion of the full FSM round-trip (Planet unload → ActiveMining → Planet reload); that's
  manual in-editor verification. `ActiveMiningSceneScope` no longer depends on a running
  parent scope, so the existing "Known Issue #7" `PlanetSceneScope` standalone-PlayMode
  limitation does not apply to it anymore (may be worth a follow-up smoke test later, not in
  this plan).

## 8. Removals

- `ActiveMiningSceneController.cs` — the old reactive additive-load/camera-disable
  controller. Fully replaced by `ActiveMiningState`.
- `ActiveMiningSessionController.cs` — the old Planet-side `ITickable` driving
  `MiningController.TickActiveSession`. Replaced by `ActiveMiningSessionRunner` inside
  `ActiveMiningSceneScope`.
- `ActiveMiningMinigame.cs` (+ `ActiveMiningMinigameTests.cs`) — its role (owning
  `CurrentSession`, `Begin`/`Tick`/`RegisterTap`/`Clear`) is replaced by
  `ActiveMiningHandoff` + `ActiveMiningSessionRunner`.
- `MiningController`: removes `CurrentActiveSession`, `OnActiveSessionChanged`,
  `BeginActiveMining`, `TickActiveSession`, `RegisterActiveTap`. Adds `StartActiveMining`,
  `TryFinalizePendingActiveMining`.
- `PlanetSceneScope`: removes registrations for `ActiveMiningMinigame`,
  `ActiveMiningSceneController`, `ActiveMiningSessionController`. Adds a call to
  `TryFinalizePendingActiveMining` at the appropriate point in `MiningController.Initialize`
  (already planned in §5, no separate DI registration needed).
- `MiningModePromptView`: the same-asteroid concurrency guards
  (`_mining.CurrentActiveSession != null && ...`) that referenced the removed
  `CurrentActiveSession` are deleted — moot once Planet fully unloads during active mining
  (the prompt view doesn't exist to show a conflicting prompt while ActiveMining is up).
- The Cinemachine zoom setup added directly to `ActiveMining.unity` (commit `2f6ea481`) is
  retained unchanged — this spec doesn't touch camera framing.

## 9. Out of scope

- Any takeoff/landing-style transition animation for entering/exiting ActiveMining (uses the
  plain `LoadingScreen`, same as `PlanetState`'s default path).
- Handling an app kill/crash mid-active-mining-session (no persistence across app restarts —
  matches today's behavior; idle-mining is the only session type persisted to disk).
- Changing reward totals, idle mining, or reward math.
- A follow-up automated PlayMode smoke test for the new FSM round-trip.
