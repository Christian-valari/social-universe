# Active Mining Minigame as a Separate Scene

**Status:** Approved
**Date:** 2026-07-04
**Depends on:** `2026-07-03-asteroid-mining-redesign-design.md` (idle/active mining rework — already implemented on this branch)

## 1. Goal

Today's active-mining minigame is a 2D UI overlay rendered inside the Planet scene
(`ActiveMiningMinigameView`): a target point appears at a random position inside a
`RectTransform` bounding box, the player taps it or a miss-area button, and a per-point
timeout also counts as a miss. This spec replaces that with a real minigame presented in
its own scene:

- The actual asteroid prefab (`AsteroidDefinition.ModelPrefab`) is spawned and visibly
  rotates.
- Target points are genuine 3D locations anchored to the spawned asteroid's surface, not
  abstract 2D coordinates.
- The whole session runs under **one overall countdown**, scaled by the asteroid's size
  (derived from its existing yield/taps-required, not a new field).
- Missing (wrong-tapping) 3 target points fails the session, same threshold as today.
- Both mining modes continue to pay identical rewards — this spec does not touch reward
  totals, only how the active-mining minigame is presented and timed.

## 2. Scene & DI architecture

- New scene: `Assets/Scenes/ActiveMining.unity`. New constant:
  `Constants.SceneNames.ActiveMining = "ActiveMining"`.
- New `SocialUniverse.App.ActiveMiningSceneScope : LifetimeScope`, with
  `parentReference.TypeName` set to `SocialUniverse.App.PlanetSceneScope` — the same
  parent-scope mechanism `PlanetSceneScope` and `TravelSceneScope` already use (VContainer
  resolves the parent by type name against whatever `LifetimeScope` is already loaded).
  `MiningController`, `MiningRewardCalculator`, and `EconomyConfig` are resolved from the
  parent, not re-registered — the new scene is a pure presentation layer; all mining state
  and logic continues to live in `PlanetSceneScope` exactly as it does today.
- The Planet scene **stays loaded** underneath while the minigame runs (it is not unloaded
  and reloaded). A new entry point, `SocialUniverse.Mining.ActiveMiningSceneController`,
  registered in `PlanetSceneScope`, subscribes to `MiningController.OnActiveSessionChanged`:
  - Session goes from `null` → non-null: disable the Planet scene's camera, then
    `SceneLoader.LoadAsync(Constants.SceneNames.ActiveMining, LoadSceneMode.Additive)`.
  - Session goes back to `null` (after grant/cleanup completes, same as today's flow):
    `SceneLoader.UnloadAsync(Constants.SceneNames.ActiveMining)`, then re-enable the Planet
    camera.
  - This keeps exactly one active camera rendering at a time and requires no changes to
    `MiningModePromptView` or `MiningController`'s public API — `BeginActiveMining` still
    just starts the session; the scene load is a side effect of the existing
    `OnActiveSessionChanged` event.
- Idle mining, the asteroid field, respawn timers, and the HUD are unaffected underneath;
  they simply aren't visible while the overlay scene is on top, the same as today's 2D
  overlay obscuring the HUD.
- Standalone-mode support (opening `ActiveMining.unity` directly, no parent scope) is out of
  scope — this scene only ever loads as a child of an already-running `PlanetSceneScope`, so
  no standalone Net/Economy mock wiring is needed here (unlike `PlanetSceneScope`/
  `TravelSceneScope`, which support being opened directly for editor testing).

## 3. Session/timer rework

Per design discussion, the session now runs under **one overall countdown**, with no
per-point timeout layered on top:

- `ActiveMiningSession` (`Assets/_Project/Scripts/Mining/ActiveMiningSession.cs`):
  - Removes `TapWindowSeconds` and the per-point-timeout-triggers-a-miss logic
    (`_windowElapsed` / the timeout branch of `Tick`).
  - Adds `SessionDurationSeconds` (total time for the session, set once at construction) and
    `TimeRemainingSeconds` (counts down every `Tick(deltaTime)` call).
  - `Tick(deltaTime)`: decrements `TimeRemainingSeconds`; if it reaches 0 while
    `Stage == InProgress`, the session fails (`SetStage(Failed)`) — this is now the *only*
    thing `Tick` does.
  - `RegisterHit()`: unchanged in spirit — a correct tap increments `SuccessfulTaps`;
    reaching `TapsRequired` → `Success`.
  - `RegisterMiss()`: now called **only when the player taps the wrong spot** (never from a
    timeout). Reaching `MaxErrors` (still 3, `EconomyConfig.ActiveMaxErrors`, unchanged) →
    `Failed`.
- `MiningRewardCalculator` (`MiningReward` struct) gains a fourth computed value,
  `ActiveSessionDurationSeconds`:
  ```
  rawSessionSeconds = ActiveTapsRequired * ActiveSecondsPerTap
  ActiveSessionDurationSeconds = Clamp(rawSessionSeconds, MinActiveSessionSeconds, MaxActiveSessionSeconds)
  ```
  Because `ActiveTapsRequired` is already derived from the asteroid's `RemainingYield`
  (bigger/richer asteroids require more taps), this scales the countdown by the asteroid's
  effective size with no new `AsteroidDefinition`/`PlanetDefinition` field — reusing the
  yield-derived value that already exists.
- `EconomyConfig` changes (Mining — Active section):
  - Rename `_activeTapWindowSeconds` → `_activeSecondsPerTap` (repurposed: seconds
    contributed per required tap toward the total session time, not a per-point deadline).
    Existing default (1.2s) is too short for a total-session budget; new default is 3s/tap.
  - Add `_minActiveSessionSeconds` (default 12s) and `_maxActiveSessionSeconds` (default 60s)
    clamps, mirroring the existing `MinIdleSessionSeconds`/`MaxIdleSessionSeconds` pattern.
  - `_activeMaxErrors` (3) is unchanged.
- `ActiveMiningMinigame.Begin()` passes `reward.ActiveSessionDurationSeconds` into the new
  `ActiveMiningSession` constructor instead of `_config.ActiveTapWindowSeconds`.
- `ActiveMiningSessionController` (the existing `ITickable` driving
  `MiningController.TickActiveSession`) is unchanged — it keeps ticking every frame in
  `PlanetSceneScope` regardless of whether the visual overlay scene is loaded, so the
  countdown is always accurate even if scene load/unload takes a frame or two.

## 4. The minigame scene contents

All new types below live in the `ActiveMining.unity` scene and are resolved via
`ActiveMiningSceneScope`.

- **`ActiveMiningAsteroidStage`** (new, `SocialUniverse.Mining`): on scene start, reads
  `MiningController.CurrentActiveSession.Asteroid.Definition.ModelPrefab` and instantiates a
  **visual clone** on a fixed stage transform in front of the camera. The real field
  asteroid GameObject stays untouched back in the Planet scene — only its `Definition` and
  `RemainingYield` data matter for the minigame, not the GameObject identity. The clone
  tumbles slowly (same rotation approach as `Asteroid.Update()`) for atmosphere. If
  `ModelPrefab` is null, falls back to a primitive sphere, matching `AsteroidSpawner`'s
  existing fallback behavior.
- **`ActiveMiningTargetPoint`** (new, `SocialUniverse.Mining`): a marker component anchored
  to a random point on the visual clone's collider surface (a child transform offset from
  the clone's center along a random direction at the collider's radius), so it's a genuine
  3D point that moves as the asteroid rotates.
- **`ActiveMiningMinigameView`** (rewritten, `SocialUniverse.UI`, replaces the current
  RectTransform-based version): each frame, projects the live target's world position via
  `Camera.WorldToScreenPoint` onto a 2D UI marker positioned at that screen point — reusing
  the existing target-button/miss-button raycast-ordering trick from the current
  implementation, just re-anchored every frame from a real 3D point instead of picked once
  from a `RectTransform`. Displays:
  - Taps progress (`SuccessfulTaps`/`TapsRequired`)
  - Error count (`ErrorCount`/`MaxErrors`)
  - Countdown (`TimeRemainingSeconds`, formatted as seconds)
  - A brief Success/Failed result banner when the session's `Stage` resolves, matching the
    existing `OnSessionChanged` event wiring.
  - Taps still route through `MiningController.RegisterActiveTap(bool)` — the tap-scoring
    API is unchanged; only how the target's screen position is computed changes.
- **v1 scope note:** target points are placed on the hemisphere of the asteroid currently
  facing the camera at the moment they're generated. This avoids needing occlusion/backface
  hiding logic for a first pass (a point that later rotates to the far side while unclaimed
  is an accepted minor edge case, not a blocker — worth a follow-up if it proves annoying in
  practice).
- Scene also needs a dedicated Camera and Directional Light (per this project's existing
  scene-setup convention), a Canvas for the UI elements above, and an
  `ActiveMiningSceneBootstrapper` (`IStartable`) that wires `ActiveMiningAsteroidStage` to
  the in-progress session on scene start.

## 5. Entry point integration

- `MiningModePromptView.OnActiveMineClicked()` is **unchanged** — it still just calls
  `_mining.BeginActiveMining(asteroid)`. `ActiveMiningSceneController` reacting to
  `OnActiveSessionChanged` is solely responsible for triggering the scene load/unload, so
  the prompt view doesn't need to know a scene transition is involved.
- No changes to `IEconomyService`, `MiningController`'s claim/grant flow, or the
  same-asteroid concurrency guards already in place.

## 6. Testing

- **EditMode:**
  - Rewrite `ActiveMiningSessionTests` for the new timer semantics: `Tick` counting down
    `TimeRemainingSeconds` to 0 while `InProgress` → `Failed`; `RegisterMiss` only
    triggered by explicit wrong-tap calls (no timeout-induced misses); `RegisterHit`
    reaching `TapsRequired` → `Success`; `RegisterMiss` reaching `MaxErrors` → `Failed`.
  - Extend `MiningRewardCalculatorTests` to cover `ActiveSessionDurationSeconds` scaling
    with `RemainingYield`/taps and respecting the new min/max clamps.
  - Extend `ActiveMiningMinigameTests` (`Begin`) to assert the constructed session's
    `SessionDurationSeconds` matches the calculator's output instead of the old
    `TapWindowSeconds` assertion.
- **PlayMode:** structural-only verification that `ActiveMiningScene` loads cleanly (no
  missing-script warnings, `ActiveMiningSceneScope` resolves against a running
  `PlanetSceneScope` parent) — consistent with the "Known Issue #7" caveat already
  documented for `PlanetSceneScope`'s parent-reference limitation in standalone PlayMode
  tests (a dedicated automated PlayMode test asserting the full tap/raycast/camera-swap
  flow is not planned; that gets manual in-editor verification, same precedent as the
  hand-authored `ActiveMiningOverlay` UI from the previous mining redesign).

## 7. Removals

- `ActiveMiningMinigameView`'s current `RectTransform`-based random placement
  (`PlaceTargetPoint`, `_asteroidArea` field) is removed, replaced by the world-anchored
  projection described in §4.
- No other files are removed. `ActiveMiningSession`, `ActiveMiningMinigame`, and
  `MiningController`'s public surface stay almost identical — only the timer's internal
  meaning changes (per-point window → overall countdown).

## 8. Out of scope

- Standalone (no-parent) support for `ActiveMiningSceneScope`.
- Occlusion/backface hiding for target points rotated out of view.
- Any change to reward totals, idle mining, or the same-asteroid concurrency guards.
- Resolving the pre-existing "Known Issue #7" `PlanetSceneScope` standalone-PlayMode
  limitation.
