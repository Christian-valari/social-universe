# Social Universe — Project Progress Tracker

> Last updated: 2026-06-18 — `refactor/vivox-only-social`: removed Netcode for GameObjects and
> UGS Multiplayer Sessions/Relay; presence is now `VivoxPresenceService`, derived from the Vivox
> channel roster (see `MIGRATION.md`). EditMode suite 83/83 passing (was 79/79). PlayMode
> regression (Known Issue #7) confirmed still open and unrelated to this migration.
> Engine: Unity 6 (URP 17.3.0) · Branch: `refactor/vivox-only-social`

---

## Legend


| Symbol | Meaning                    |
| ------ | -------------------------- |
| ✅      | Done & verified            |
| ⚠️     | Done but has a known issue |
| 🔲     | Not started                |
| 🚧     | In progress                |


---

## Open Decisions


| Decision                    | Status           | Notes                                                                                          |
| --------------------------- | ---------------- | ---------------------------------------------------------------------------------------------- |
| DI Framework                | ✅ **VContainer** | `ProjectLifetimeScope` + `PlanetSceneScope` in place                                           |
| Hexasphere Grid System      | ✅ **Installed**  | `Assets/Plugins/Hexasphere/`, assembly defs created                                            |
| DOTween / DOTweenPro        | ✅ **Installed**  | `Assets/Plugins/Demigiant/`                                                                    |
| Lean Touch                  | ✅ **Installed**  | `Assets/Plugins/CW/LeanTouch/` — replaces legacy `Input` for camera orbit/zoom (touch + mouse) |
| Backend (UGS vs Nakama)     | ✅ **UGS**        | Unity Gaming Services — Auth, Economy, Cloud Save, Cloud Code. Packages added to manifest.json |
| Sky Discovery (AR vs gyro)  | 🔲 **Open**      | Decide before M5                                                                               |
| Age policy / content rating | 🔲 **Open**      | `SocialConfig` ships a provisional teen-safe default (`ChatFilterLevel.Strict` for all players) so M4 isn't blocked; revisit once decided — see "M4 — Chat & Moderation Notes" |
| Land resale model           | 🔲 **Open**      | Coins-only confirmed; confirm no real-money cash-out before M8                                 |


---

## Known Issues


| #   | Severity | Description                                                                                                                                                                             | Fix                                                                                                                                                                           |
| --- | -------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 1   | ✅ Fixed  | `PlanetCameraController` and the Hexasphere plugin use legacy `UnityEngine.Input`, but the project had the new Input System active → `InvalidOperationException` in Play Mode           | **Active Input Handling** set to **Both** in Player Settings                                                                                                                  |
| 2   | ✅ Fixed  | `Planet_TerraPrime.asset` and old `Asteroid_Iron.asset` existed in `Assets/_Project/ScriptableObjects/` root (superseded by organized assets in `Asteroids/` and `Planets/` subfolders) | Stale assets deleted                                                                                                                                                          |
| 3   | ✅ Fixed  | `FixInputSettings.cs` editor script was created but menu item execution was cancelled — input setting had not been applied                                                              | Applied manually; `FixInputSettings.cs` removed                                                                                                                               |
| 4   | ✅ Fixed  | `PlanetCameraController` depended on legacy `UnityEngine.Input` (right-mouse orbit, scroll-wheel zoom), which doesn't translate to mobile touch                                         | Rewritten against **Lean Touch** (`Lean.Touch.LeanGesture`/`LeanTouch.Fingers`): one-finger drag orbits, two-finger pinch zooms — works uniformly across mouse and touch      |
| 5   | ⚠️ Open  | Unity MCP `execute_code` tool fails on every invocation in this environment — even `return 1;` — with `Error running ...mono.exe: The filename or extension is too long`                | Environment/tooling issue (not project code). Blocks live in-editor smoke-testing via injected C#; use manual Play Mode tap-throughs or PlayMode tests instead until resolved |
| 6   | ✅ Fixed  | `ServerCode/PurchaseLand.js` used incorrect UGS SDK call signatures (Economy/Cloud Save client construction, `getItems`/`setItem` shapes, unnecessary `ConfigurationApi`/`configAssignmentHash`) and returned extra `tileId`/`ownerId` fields, causing Cloud Code's strict deserializer to throw `Could not find member 'ownerId' on object of type 'PurchaseLandResponse'` | Rewrote to match `SpendCoins.js`/`CLOUD_CODE_FUNCTIONS.md` conventions; trimmed success response to `{ success, newBalance }` matching `PurchaseLandResponse` |
| 7   | ⚠️ Open  | `PlanetSceneFlowTests` (PlayMode) now fails both tests at `SetUp` with `PlanetSceneScope.Container not initialized`. Root cause: `Planet.unity`'s `PlanetSceneScope` has `parentReference.TypeName = SocialUniverse.App.RootLifetimeScope` set (production config). VContainer's `LifetimeScope.Awake()` sees `parentReference.Type != null` and calls `EnqueueParent`, queuing the scope to wait for a `RootLifetimeScope` instance before `Configure`/`Build` run. The test loads `Planet.unity` standalone via `LoadSceneMode.Single` (no `Bootstrap.unity`, no `RootLifetimeScope` ever created), so the wait never resolves, `Container` stays `null`, and downstream `[Inject]` fields (`CurrencyView._wallet`, `HUDController._wallet`/`_playerState`/`_mining`) throw NREs | Not yet fixed. Needs either: (a) a test-only bootstrap that instantiates `RootLifetimeScope` before loading `Planet.unity`, or (b) clearing `parentReference` on the scene's `PlanetSceneScope` and relying on the `parentReference.Type == null` standalone-mock path (would need re-adding `_socialConfig` + Net mocks to that scene instance) |


---

## Installed Plugins & Packages


| Package                | Version | Status |
| ---------------------- | ------- | ------ |
| URP                    | 17.3.0  | ✅      |
| Unity Input System     | 1.19.0  | ✅      |
| Unity UGUI             | 2.0.0   | ✅      |
| Unity Test Framework   | 1.6.0   | ✅      |
| Multiplayer Center     | 1.0.1   | ✅      |
| VContainer             | —       | ✅      |
| Hexasphere Grid System | —       | ✅      |
| DOTween Pro            | —       | ✅      |
| Lean Touch             | —       | ✅      |
| UGS Core               | 1.13.0  | ✅      |
| UGS Authentication     | 3.6.1   | ✅      |
| UGS Economy            | 3.5.3   | ✅      |
| UGS Cloud Save         | 3.4.0   | ✅      |
| UGS Cloud Code         | 2.10.2  | ✅      |
| UGS Friends            | 1.1.1   | ✅      |
| UGS Vivox              | 16.11.0 | ✅      |
| ParrelSync             | —       | ✅ (Editor-only) — multi-instance editor cloning for local multiplayer testing (`Assets/ParrelSync/`) |


---

## Assets

### Prefabs


| Folder                     | Assets                                                                                |
| -------------------------- | ------------------------------------------------------------------------------------- |
| `Assets/Prefabs/Planets/`  | Earth, Jupiter, Mars, Mercury, Moon, Neptune, Pluto, Saturn, Star, Sun, Uranus, Venus |
| `Assets/Prefabs/Asteroid/` | Asteroid1, Asteroid2, Asteroid3, Asteroid4, Asteroid5, Asteroid6                      |
| `Assets/Prefabs/`          | ProbePrefab                                                                           |


### ScriptableObjects — `Assets/_Project/ScriptableObjects/`


| Asset                               | Status | Notes                                                      |
| ----------------------------------- | ------ | ---------------------------------------------------------- |
| `DatabaseRegistry.asset`            | ✅      | Populated: 10 planets, 6 asteroids, 1 drone                |
| `EconomyConfig.asset`               | ✅      |                                                            |
| `Drone_Scout.asset`                 | ✅      |                                                            |
| `Planets/Planet_Mercury.asset`      | ✅      | Tier 1, 162 tiles, ×0.8 price                              |
| `Planets/Planet_Venus.asset`        | ✅      | Tier 1, 322 tiles, ×1.0 price                              |
| `Planets/Planet_Earth.asset`        | ✅      | Tier 1, 642 tiles, ×1.5 price — **active in Planet scene** |
| `Planets/Planet_Moon.asset`         | ✅      | Tier 1, 162 tiles, ×1.2 price                              |
| `Planets/Planet_Mars.asset`         | ✅      | Tier 2, 322 tiles, ×1.0 price                              |
| `Planets/Planet_Jupiter.asset`      | ✅      | Tier 2, 642 tiles, ×2.0 price                              |
| `Planets/Planet_Saturn.asset`       | ✅      | Tier 2, 642 tiles, ×1.8 price                              |
| `Planets/Planet_Uranus.asset`       | ✅      | Tier 3, 322 tiles, ×2.5 price                              |
| `Planets/Planet_Neptune.asset`      | ✅      | Tier 3, 322 tiles, ×3.0 price                              |
| `Planets/Planet_Pluto.asset`        | ✅      | Tier 3, 162 tiles, ×5.0 price                              |
| `Asteroids/Asteroid_Iron.asset`     | ✅      | Tier 1, yield 80, rarity 70%, 2 coins/unit                 |
| `Asteroids/Asteroid_Carbon.asset`   | ✅      | Tier 1, yield 65, rarity 65%, 3 coins/unit                 |
| `Asteroids/Asteroid_Silicon.asset`  | ✅      | Tier 2, yield 50, rarity 45%, 7 coins/unit                 |
| `Asteroids/Asteroid_Nickel.asset`   | ✅      | Tier 2, yield 40, rarity 35%, 10 coins/unit                |
| `Asteroids/Asteroid_Platinum.asset` | ✅      | Tier 3, yield 25, rarity 18%, 22 coins/unit                |
| `Asteroids/Asteroid_Iridium.asset`  | ✅      | Tier 3, yield 15, rarity 8%, 40 coins/unit                 |


**⚠️ Misplaced asset:** `Assets/SocialConfig.asset` (M4, referenced by `Bootstrap.unity`'s
`RootLifetimeScope._socialConfig`) was created at the `Assets/` root instead of
`Assets/_Project/ScriptableObjects/`. Move it into this folder and re-point the `RootLifetimeScope`
and `PlanetSceneScope` (Planet.unity) `_socialConfig` references to match project convention.

### Scenes


| Scene               | Status | Notes                                      |
| ------------------- | ------ | ------------------------------------------ |
| `Bootstrap.unity`      | ✅      | DontDestroyOnLoad container, boots to Auth; `RootLifetimeScope` has `_devMode` flag for UGS-free testing |
| `Auth.unity`           | ✅      | Login + Register panels; no success modal — sign-in immediately publishes `PlayerReadyEvent` and transitions to Hub |
| `SolarSystem.unity`    | ✅      | Shell — star map placeholder               |
| `Planet.unity`         | ✅      | **Fully wired for Earth** — see M1 detail  |
| `Station.unity`        | ✅      | Shell — guild hub placeholder              |
| `LoadingScreen.unity`  | 🔲      | **To create** — additive overlay scene; add Canvas + `LoadingScreenView` + Slider + TMP_Text, then add to Build Settings. Loaded by `PlanetState` before `Planet.unity`; self-unloads when `PlanetSceneReadyEvent` fires |


---

## M0 — Foundation & Bootstrap ✅ COMPLETE

**Exit criteria:** Empty app boots through state machine, configs load, events fire.


| Script                                      | Path      | Responsibility                                                           | Status |
| ------------------------------------------- | --------- | ------------------------------------------------------------------------ | ------ |
| `Bootstrapper`                              | `Core/`   | Entry point; build the service container, init in order, load Auth scene | ✅      |
| `ProjectLifetimeScope` / `RootLifetimeScope` | `Core/` / `App/` | DI root scope — `ProjectLifetimeScope` (Core) registers Core services; `RootLifetimeScope` (App) extends it and adds Net services | ✅ |
| `GameManager`                               | `Core/`   | Owns global app state; coordinates top-level systems                     | ✅      |
| `GameStateMachine`                          | `Core/`   | FSM driving Boot/Auth/Hub/Planet/Station transitions                     | ✅      |
| `IGameState`                                | `Core/`   | Contract all concrete states implement                                   | ✅      |
| `BootState`                                 | `Core/`   | Concrete state — service init, data load, advance to Auth                | ✅      |
| `AuthState`                                 | `Core/`   | Concrete state — wait for auth result, advance to Hub                    | ✅      |
| `HubState`                                  | `Core/`   | Concrete state — activate SolarSystem scene                              | ✅      |
| `PlanetState`                               | `Core/`   | Concrete state — loads `LoadingScreen` additively first, then `Planet`; defensively unloads `LoadingScreen` on exit if still present | ✅      |
| `SceneLoader`                               | `Core/`   | Async additive scene load/unload with progress callback                  | ✅      |
| `EventBus`                                  | `Core/`   | Global typed publish/subscribe (decouple systems via events)             | ✅      |
| `GameEvent` / `GameEventListener`           | `Core/`   | ScriptableObject event channels for inspector wiring                     | ✅      |
| `AppConfig` (SO)                            | `Config/` | Global tunables, environment selection                                   | ✅      |
| `SULog`                                     | `Core/`   | Logging wrapper with channels/levels                                     | ✅      |
| `Constants` / `SaveKeys`                    | `Core/`   | Centralized keys and magic values                                        | ✅      |


### M0 Completion Checklist

**Automated Tests**

- [x] `GameStateMachineTests` — FSM transitions (EditMode)
- [x] `EventBusTests` — publish/subscribe (EditMode)
- [ ] EditMode: verify `AppConfig` SO loads without errors via `DatabaseRegistry`

**Manual Play Mode Verification**

- [ ] Press Play in Bootstrap scene — no console errors on boot
- [ ] State machine advances: `BootState → AuthState → HubState` (verify via logs)
- [ ] `DontDestroyOnLoad` container persists across scene loads (check Hierarchy)
- [ ] `EventBus` publish fires all registered handlers (manual log test)

**Architecture Rules**

- [x] No gameplay logic in Bootstrap scene
- [x] All systems registered through VContainer DI
- [x] `EventBus` used for cross-system communication (no direct references)

---

## M1 — Core Loop Prototype (offline, local mock) ✅ COMPLETE

**Exit criteria:** Single planet, mine, buy a tile — all against LocalMock services.

### World


| Script                    | Path      | Responsibility                                                             | Status | Notes                                                                  |
| ------------------------- | --------- | -------------------------------------------------------------------------- | ------ | ---------------------------------------------------------------------- |
| `PlanetController`        | `World/`  | Spawn planet model + hexasphere for a `PlanetDefinition`                   | ✅      | Spawns Earth model + generates hex grid at runtime                     |
| `HexasphereManager`       | `World/`  | Wrap Hexasphere Grid System; generate tiles, expose selection/hover events | ✅      | Real plugin integration — 642 tiles @ numDivisions=8                   |
| `TileData`                | `World/`  | Per-tile runtime model (id, owner, buildState, yield, isLandmark)          | ✅      |                                                                        |
| `TileSelectionController` | `World/`  | Raycast pick a tile, raise `TileSelected` event                            | ✅      | Wired to `Hexasphere.OnTileClick`                                      |
| `TileColorizer`           | `World/`  | Color tiles by state (owned/other/available/landmark)                      | ✅      | Available=grey, Owned=green, Other=blue, Landmark=gold                 |
| `LandmarkService`         | `World/`  | Identify the 12 pentagons; flag as legendary landmark                      | ✅      | Uses `tile.isPentagon` — marks exactly 12 pentagon tiles               |
| `PlanetCameraController`  | `World/`  | Orbit/zoom camera around the sphere                                        | ✅      | Migrated to Lean Touch: one-finger drag orbits, two-finger pinch zooms |
| `PlanetDefinition` (SO)   | `Config/` | Planet theme, tile count, land multiplier, asteroid tier, model ref        | ✅      | 10 assets created                                                      |
| `DatabaseRegistry`        | `Config/` | Central lookup of all SO definitions                                       | ✅      | Populated with all planets, asteroids, drone                           |


### Mining


| Script                        | Path      | Responsibility                                                                              | Status | Notes                                                                                                                                                  |
| ----------------------------- | --------- | ------------------------------------------------------------------------------------------- | ------ | ------------------------------------------------------------------------------------------------------------------------------------------------------ |
| `AsteroidSpawner`             | `Mining/` | Spawn the asteroid field for a planet; handle timed respawn                                 | ✅      | Spawns 4 Earth asteroids; `ScheduleRespawn()` destroys claimed asteroids and queues same-type replacements; persists across sessions via `PlayerPrefs` |
| `Asteroid`                    | `Mining/` | Asteroid runtime (mineral type/amount, depletion)                                           | ✅      | Publishes `AsteroidSelectedEvent` on tap; guarantees `SphereCollider` for raycasting                                                                   |
| `AsteroidSelectionController` | `Mining/` | Lean Touch tap → raycast → `AsteroidSelectedEvent`                                          | ✅      | Attached to Main Camera                                                                                                                                |
| `DroneController`             | `Mining/` | Drone movement/visual toward target asteroid                                                | ✅      |                                                                                                                                                        |
| `DroneRuntime`                | `Mining/` | Live drone instance + current stats                                                         | ✅      |                                                                                                                                                        |
| `MiningController`            | `Mining/` | Orchestrate a mining session (idle + active); gate sessions so they don't fight over drone  | ✅      | Exposes `CurrentIdleSession`, `BeginIdleMining`, `RegisterIdleClaimTapAsync`; on claim calls `ScheduleRespawn`                                         |
| `IdleMiningSession`           | `Mining/` | State machine: `Traveling → Mining → ReadyToClaim → Complete`; tracks progress + claim taps | ✅      |                                                                                                                                                        |
| `IdleMiningSessionController` | `Mining/` | Drive the session: send drone, spawn mining VFX, tick timer, listen for claim taps          | ✅      |                                                                                                                                                        |
| `IdleMiningCalculator`        | `Mining/` | Compute offline haul up to cargo cap                                                        | ✅      |                                                                                                                                                        |
| `ActiveMiningMinigame`        | `Mining/` | Tap/combo/crit logic and feedback hooks                                                     | ⚠️     | Stubbed — "Active Mine" logs and closes prompt; mini-game deferred to later milestone                                                                  |
| `MiningInputHandler`          | `Mining/` | Translates keyboard/touch input events into mining actions; registered as `ITickable` entry point | ✅ |                                                                                                                                                        |


### Economy


| Script                | Path       | Responsibility                                                            | Status | Notes                                                                                |
| --------------------- | ---------- | ------------------------------------------------------------------------- | ------ | ------------------------------------------------------------------------------------ |
| `IEconomyService`     | `Economy/` | Balances/spend/grant interface                                            | ✅      |                                                                                      |
| `LocalMockEconomy`    | `Economy/` | Offline stub — no server calls                                            | ✅      |                                                                                      |
| `Wallet`              | `Economy/` | Client-cached balances + change events                                    | ✅      |                                                                                      |
| `LandPurchaseService` | `Economy/` | Buy a tile: request → (mock) commit ownership                             | ✅      |                                                                                      |
| `EconomyConfig` (SO)  | `Config/`  | Prices, mining yields, cargo cap, idle session tunables, respawn cooldown | ✅      | `IdleSessionDuration` (30s), `IdleSessionClaimTaps` (5), `AsteroidRespawnHours` (4h) |


### Progression


| Script        | Path           | Responsibility                            | Status |
| ------------- | -------------- | ----------------------------------------- | ------ |
| `PlayerState` | `Progression/` | Runtime player data (level, fuel, caches) | ✅      |


### UI


| Script                 | Path  | Responsibility                                       | Status | Notes                                                                      |
| ---------------------- | ----- | ---------------------------------------------------- | ------ | -------------------------------------------------------------------------- |
| `MiningModePromptView` | `UI/` | Tap-an-asteroid prompt: "Idle Mine" or "Active Mine" | ✅      |                                                                            |
| `HUDController`        | `UI/` | Persistent HUD: mining status, coins, tile info      | ✅      | Surfaces idle-session state: "Heading to…", "Mining: NN%", "Tap to claim!" |


### Planet Scene Wiring


| Item                                               | Status | Notes                                                                               |
| -------------------------------------------------- | ------ | ----------------------------------------------------------------------------------- |
| `PlanetSceneScope` refs wired                      | ✅      | EconomyConfig, DatabaseRegistry, Planet_Earth                                       |
| `PlanetCameraController._target`                   | ✅      | → PlanetRoot                                                                        |
| `LeanTouch` GameObject added to scene              | ✅      | Required so `LeanTouch.Fingers` is populated from mouse/touch input                 |
| `HexasphereManager._hexasphere`                    | ✅      | → Hexasphere plugin component                                                       |
| `TileColorizer._hexasphere`                        | ✅      | → HexasphereManager                                                                 |
| `TileSelectionController._hexasphere`              | ✅      | → HexasphereManager                                                                 |
| `Hexasphere.cameraMain`                            | ✅      | → Main Camera                                                                       |
| `Hexasphere.numDivisions`                          | ✅      | 8 (~642 tiles)                                                                      |
| `Hexasphere.rotationEnabled`                       | ✅      | false (PlanetCameraController orbits instead)                                       |
| `AsteroidSelectionController` added to Main Camera | ✅      | Subscribes to `LeanTouch.OnFingerTap`, raycasts for `Asteroid`                      |
| `MiningPrompt` panel added under Canvas            | ✅      | `Image` + `MiningModePromptView`; starts inactive; shown on `AsteroidSelectedEvent` |


### M1 Completion Checklist

**Automated Tests**

- [x] `WalletTests` — balance changes (EditMode)
- [x] `IdleMiningCalculatorTests` — offline haul calculation (EditMode)
- [x] `LocalMockEconomyTests` — mock economy grant/spend (EditMode)
- [x] `PlanetSceneFlowTests` — mining taps fill cargo + `CommitCargoAsync` grants coins (PlayMode)
- [x] `PlanetSceneFlowTests` — select available tile → purchase → ownership transfers to player (PlayMode)
- [ ] EditMode: `LandmarkService` marks exactly 12 tiles as `IsLandmark = true`
- [ ] EditMode: `DatabaseRegistry` lookups — `GetPlanet`, `GetAsteroid`, `GetDrone` all return correct assets

**Manual Play Mode Verification**

- [x] 642 tiles generated on Earth
- [x] 12 landmark (pentagon) tiles colored gold
- [x] 4 asteroids spawned in orbit
- [x] Mining session started with `Drone_Scout`
- [x] Camera orbit (one-finger drag) and zoom (two-finger pinch) working via Lean Touch
- [x] Tile click selection working via `Hexasphere.OnTileClick`
- [x] Earth model renders correctly over the hexasphere
- [x] Tap asteroid → prompt appears → choose "Idle Mine" → drone travels to asteroid
- [x] Mining VFX/timer plays while drone is mining
- [x] 5 claim taps on asteroid → coins granted → HUD balance updates
- [x] Asteroid disappears after claim; respawns after cooldown (4 h in `EconomyConfig`)
- [x] Select an available tile → purchase deducted from wallet → tile turns green
- [x] Selecting an already-owned tile shows correct "owned" state

**Architecture Rules**

- [x] All economy ops go through `IEconomyService` — no direct coin grants in gameplay code
- [x] `EconomyConfig` SO holds all tunable numbers (no magic values in scripts)
- [x] Systems communicate via `EventBus` events (no direct cross-namespace calls)
- [x] `LocalMockEconomy` is the only economy implementation referenced — swappable for real backend

---

## M2 — Networking, Auth & Persistence 🚧 CODE COMPLETE — AWAITING UGS SETUP

**Exit criteria:** Real login, server-authoritative wallet, state persists across sessions.

**Backend:** ✅ Unity Gaming Services (UGS)


| Script                   | Path          | Responsibility                                                                    | Status | Notes                                                                                                         |
| ------------------------ | ------------- | --------------------------------------------------------------------------------- | ------ | ------------------------------------------------------------------------------------------------------------- |
| `IAuthService`           | `Core/`       | Contract for anonymous + Apple/Google sign-in                                     | ✅      | Placed in Core (not Net) to avoid circular assembly dep — `BootState`/`AuthState` inject it                   |
| `INetworkBootstrap`      | `Core/`       | Contract for UGS SDK initialization                                               | ✅      | Placed in Core so `BootState` can call `InitializeAsync()` without a Net → Core → Net cycle                   |
| `LocalMockAuthService`   | `Net/`        | Offline stub for IAuthService — simulates sign-in with fixed mock player ID       | ✅      | Used in standalone Auth scene and standalone Planet scene (no parent scope)                                   |
| `MockNetworkBootstrap`   | `Net/`        | Offline stub for INetworkBootstrap — 200 ms delay, no UGS SDK call               | ✅      | Registered by `RootLifetimeScope` when `_devMode = true`                                                      |
| `LocalMockBackendClient` | `Net/`        | Offline stub for IBackendClient — logs and returns `default`                      | ✅      | Registered by `RootLifetimeScope` when `_devMode = true`                                                      |
| `LocalMockCloudSave`     | `Net/`        | Offline stub for ICloudSave — in-memory dictionary store                          | ✅      | Registered by `RootLifetimeScope` when `_devMode = true`                                                      |
| `AuthService`            | `Net/`        | Wraps UGS Authentication SDK — anon + Apple/Google sign-in                        | ✅      |                                                                                                               |
| `IBackendClient`         | `Core/`       | Contract for Cloud Code RPC calls                                                 | ✅      | Placed in Core (not Net) — same circular-dep reason as `IAuthService`                                        |
| `BackendClient`          | `Net/`        | Cloud Code wrapper with exponential-backoff retry on transient errors             | ✅      | Retries on `NoInternetConnection`, `ServiceUnavailable`, `Unknown`                                            |
| `CloudCodeTestHarness`   | `Net/`        | Dev-only harness for smoke-testing Cloud Code functions against a live UGS project | ✅     | Not wired into production DI — used manually in Editor via menu/inspector                                    |
| `NetworkBootstrap`       | `Net/`        | Calls `UnityServices.InitializeAsync()` using `AppConfig.Environment`             | ✅      |                                                                                                               |
| `ICloudSave`             | `Net/`        | Contract for load/save player state records                                       | ✅      |                                                                                                               |
| `CloudSaveService`       | `Net/`        | Wraps UGS Cloud Save player data; swallows `NotFound` silently                    | ✅      |                                                                                                               |
| `EconomyService` (real)  | `Economy/`    | Replaces `LocalMockEconomy` — reads via Economy SDK, writes via Cloud Code        | ✅      | `PlanetSceneScope` registers this; `LocalMockEconomy` retained but no longer wired                           |
| `LandRegistry`           | `Economy/`    | Client-side `HashSet<string>` cache of locally-owned tile IDs                    | ✅      | Hydrated on Planet scene start from Cloud Save; updated on each successful purchase                          |
| `ConnectionManager`      | `Net/`        | Orchestrates connect/reconnect; falls back to `Offline` state gracefully          | ✅      | Registered in `RootLifetimeScope`                                                                            |
| `ServerTime`             | `Net/`        | Calls `GetServerTime` Cloud Code; computes local clock offset                     | ✅      | Registered in `RootLifetimeScope`                                                                            |
| `AuthScreen`             | `UI/`         | Auth scene UI — signs-in status text, player ID display, retry/continue buttons   | ✅      | Uses `UnityEngine.UI.Text`; wired via `AuthSceneScope` injection                                             |
| `AuthSceneScope`         | `App/`        | Standalone LifetimeScope for Auth scene; auto-triggers sign-in via `IStartable`   | ✅      | Set `Parent = RootLifetimeScope` in Inspector for production; mock used standalone                           |
| `RootLifetimeScope`      | `App/`        | Bootstrap scene scope — extends `ProjectLifetimeScope`, registers all Net services | ✅      | Registers: Auth, BackendClient, CloudSave, NetworkBootstrap, ServerTime, ConnectionManager                   |
| `GrantOfflineIncome`     | `ServerCode/` | Validates idle haul against server timestamp; caps at `MAX_OFFLINE_SECONDS`       | ✅      |                                                                                                               |
| `PurchaseLand`           | `ServerCode/` | Deducts coins, records per-tile ownership, maintains `owned_tiles_{planetId}` list | ✅      | Returns `newBalance`; list enables batch tile restore on login                                               |
| `ValidateMining`         | `ServerCode/` | Validates idle mining claim (caps at `sessionDurationSec × coinsPerSec`), grants coins | ✅  | Hard cap 10 000 coins/session; full session-token anti-cheat deferred to M3                                  |
| `SpendCoins`             | `ServerCode/` | Server-side balance check + decrement                                             | ✅      |                                                                                                               |
| `GrantCoins`             | `ServerCode/` | Increments balance with sanity cap (100 000/call)                                 | ✅      |                                                                                                               |
| `GrantStardust`          | `ServerCode/` | Increments Stardust balance with sanity cap                                       | ✅      |                                                                                                               |
| `GetBootstrapState`      | `ServerCode/` | Returns wallet balances + player profile in one round-trip                        | ✅      |                                                                                                               |
| `GetServerTime`          | `ServerCode/` | Returns `Date.now()` for client clock sync                                        | ✅      |                                                                                                               |


### M2 Assembly & DI Changes

| Change | Detail |
| ------ | ------ |
| New assembly | `SocialUniverse.Net` at `Assets/_Project/Scripts/Net/` |
| `SocialUniverse.Economy.asmdef` | Added `SocialUniverse.Net` reference (for `IBackendClient` in `EconomyService` and `LandPurchaseService`) |
| `SocialUniverse.App.asmdef` | Added `SocialUniverse.Net` reference |
| `EconomyConfig` | Added `CoinsCurrencyId` / `StardustCurrencyId` fields (UGS currency IDs) |
| `SaveKeys` | Added `OwnedTilesKey(planetId)` helper → `"owned_tiles_{planetId}"` Cloud Save key |
| `BootState` | Now injects `INetworkBootstrap` and calls `InitializeAsync()` before loading Auth scene |
| `AuthState` | Now injects `IAuthService`; event-driven: subscribes `OnSignedIn` in `Enter()`, auto-advances FSM on retry |
| `LandPurchaseService` | Replaced `SpendCoinsAsync` call with `IBackendClient.CallAsync("PurchaseLand", …)`; updates `LandRegistry` + `Wallet` from server response |
| `TilePurchaseHandler` | Now injects `IAuthService`; uses `_auth.PlayerId` instead of hardcoded `"local_player"` |
| `PlanetSceneScope` | Added `LandRegistry`; standalone guard: registers Net mocks only when `parentReference.Type == null`; updated `PlanetSceneBootstrapper` to hydrate wallet + owned tiles from server on scene start |
| `PlanetSceneBootstrapper` | `Start()` now `async void`; awaits `GetWalletAsync()` + Cloud Save tile restore before starting mining session |
| `ProjectLifetimeScope` | Stripped to Core-only services; **do not place in Bootstrap scene directly** |
| `RootLifetimeScope` | **Place this on the Bootstrap scene** — registers Auth, BackendClient, CloudSave, NetworkBootstrap, ServerTime, ConnectionManager |


### M2 Completion Checklist

**Automated Tests**

- [ ] EditMode: `IAuthService` contract — anonymous login returns a valid session token
- [ ] EditMode: `BackendClient` — retries on transient errors, maps error codes correctly
- [ ] EditMode: `CloudSaveService` — round-trip save/load of `PlayerProfile`
- [ ] EditMode: `EconomyService` (real) — grant/spend calls hit server and reflect in wallet
- [ ] EditMode: `ServerTime` — returned timestamp is within acceptable drift of local clock
- [ ] PlayMode: Full login → load bootstrap state → wallet and land registry hydrated

**Setup Required (Unity Dashboard + Inspector — do these before play-testing)**

- [ ] Create UGS project and link via `Edit > Project Settings > Services`
- [ ] Define `COINS` and `STARDUST` currencies in UGS Economy dashboard
- [ ] Deploy all `ServerCode/*.js` functions to UGS Cloud Code (including new `ValidateMining`)
- [ ] Set `AppConfig.Environment` to `Development` for testing
- [ ] Bootstrap scene: replace `ProjectLifetimeScope` component with `RootLifetimeScope` on scope GameObject
- [ ] `AuthSceneScope` Inspector: set `Parent = RootLifetimeScope` (production) — currently uses `LocalMockAuthService`
- [ ] `PlanetSceneScope` Inspector: set `Parent = RootLifetimeScope` (production) — currently uses `LocalMockAuthService` in standalone mode

**Manual Play Mode Verification**

- [ ] App boots, `RootLifetimeScope` wired in Bootstrap scene, no console errors
- [ ] Sign in anonymously → player ID assigned → advances to Hub
- [ ] Wallet balance matches server record (not a local default)
- [ ] Mine a tile, kill and relaunch app — wallet balance persists
- [ ] Purchase a tile — ownership is server-committed (visible after fresh login)
- [ ] `ConnectionManager` shows offline indicator when network is unavailable
- [ ] On reconnect, state re-syncs without duplication

**Architecture Rules**

- [x] `EconomyService` (real) fully replaces `LocalMockEconomy` behind `IEconomyService` — no gameplay code changed
- [x] Client never mints coins or grants ownership — all economy changes come from server responses (`PurchaseLand`, `ValidateMining`, `GrantCoins`, `SpendCoins`)
- [x] `ServerCode/` functions are not bundled in the Unity build
- [ ] Auth tokens are never stored in plain `PlayerPrefs` (UGS SDK uses platform secure storage — verify on device)

---

## M3 — Land System Depth 🚧 CODE COMPLETE — AWAITING DEPLOY/SETUP

**Exit criteria:** Networked ownership visible to others, visitor-driven yield, build mode.

**Phase 1 (done):** `LandRegistryService` — a global, cross-player-readable land registry so
other players' tiles render as "owned by other".

**Phase 2 (done):** Build mode — players spend coins to place items on owned tiles,
incrementing a per-tile build level reflected via tile extrusion.

**Phase 3 (done):** Visitor-driven yield — owners claim accrued coin income on their tiles,
boosted by build level and recorded visits from other players.
**Includes an M4-dependency caveat** — see "Phase 3 — Visitor-Driven Yield Notes" below.

**Phase 4 (done):** Upkeep & resale — recurring land tax with auto-revert on non-payment, and
voluntary tile resale for a partial refund. See "Phase 4 — Upkeep & Resale Notes" below.

All four phases are code-complete and covered by EditMode tests (39/39 passing). What remains
is Cloud Code deployment and manual/PlayMode verification — see "Setup Required" and the
"M3 Completion Checklist" below.


| Script                | Path          | Responsibility                                                      | Status |
| --------------------- | ------------- | ------------------------------------------------------------------- | ------ |
| `LandRegistryService` | `Economy/`    | Fetch/subscribe tile ownership for a planet from server             | ✅ (poll-based; see notes) |
| `YieldService`        | `Economy/`    | Compute and claim visitor-driven land income                        | ✅     |
| `VisitorTracker`      | `Economy/`    | Count/attribute visits to plots (server-backed)                     | ✅ (see M4 caveat) |
| `UpkeepService`       | `Economy/`    | Recurring land tax sink — deduct upkeep from wallet                 | ✅     |
| `LandSaleService`     | `Economy/`    | Sell an owned tile back for a partial refund                        | ✅     |
| `BuildModeController` | `App/`        | Place buildables on an owned tile (responds to `BuildItemRequestedEvent`) | ✅ |
| `UpkeepController`    | `App/`        | Poll loop — apply upkeep, revert tiles that fall behind             | ✅     |
| `LandSaleHandler`     | `App/`        | Sell an owned tile (responds to `TileSellRequestedEvent`)           | ✅     |
| `BuildPaletteService` | `Economy/`    | Available buildables by ownership/build-level progression           | ✅     |
| `TileExtrusionView`   | `World/`      | Reflect build level via tile height visual                          | ✅     |
| `ItemDefinition` (SO) | `Config/`     | Buildables/decor: cost, rarity, yield bonus, build level            | ✅     |
| `ClaimYield`          | `ServerCode/` | Server function — validate and grant land yield                     | ✅     |
| `RecordVisit`         | `ServerCode/` | Server function — increment a tile's visit count (M3 stand-in, see notes) | ✅ |
| `PlaceBuild`          | `ServerCode/` | Server function — validate ownership, commit build state            | ✅     |
| `ApplyUpkeep`         | `ServerCode/` | Server function — deduct recurring upkeep cost, revert overdue tiles | ✅    |
| `SellLand`            | `ServerCode/` | Server function — validate ownership, transfer tile, settle payment | ✅     |
| `GetLandRegistry`     | `ServerCode/` | Server function — return the planet's tile-ownership map (Custom Data) | ✅ |


### Phase 1 — Networked Ownership Notes

- **New shared storage:** `PurchaseLand` now also writes to a per-planet Cloud Save **Custom
  Data** item (`customId = planetId.toLowerCase()`, `key = "land_registry"`,
  `value = { tileId: { ownerId, buildLevel, lastYieldClaimTs, lastUpkeepTs, visitCount } }` —
  see "Phase 2 — Build Mode Notes" for the schema v2 upgrade). Custom Data is shared across
  players (unlike the existing player-scoped `tile_{tileId}_owner` / `owned_tiles_{planetId}`
  keys), making it readable by every client via the new `GetLandRegistry` function.
- **`LandRegistryService`** (Economy) fetches this map via `GetLandRegistry` and is the
  authoritative source for ALL tile ownership on the planet (own + others').
  `LandRegistrySyncController` (App) polls it every `EconomyConfig.LandRegistryPollIntervalSec`
  (default 20s) and applies `OwnedByPlayer`/`OwnedByOther` state + `TileColorizer.RefreshTile` to
  every tile in the registry. This is the phase-1 stand-in for "subscribe" — true realtime push
  needs the M4 presence/realtime layer.
- The existing M2 `LandRegistry` (private per-player "my tiles" cache, hydrated from
  `owned_tiles_{planetId}`) is kept as a resilience fallback for restoring "my tiles" if the new
  global-registry call fails — both paths are idempotent and converge to the same state.
- `TilePurchaseHandler` calls `LandRegistryService.SetOwner()` immediately after a successful
  purchase so the buyer's own client doesn't wait for the next poll.

### Phase 2 — Build Mode Notes

- **Registry schema v2:** since Phase 1 hasn't been deployed yet, the `land_registry` Custom
  Data entry shape was upgraded from a bare `ownerId` string to
  `{ ownerId, buildLevel, lastYieldClaimTs, lastUpkeepTs, visitCount }` (`LandTileEntry` in
  `LandRegistryService`). `PurchaseLand` now writes the full entry with defaults
  (`buildLevel: 0`, timestamps = now, `visitCount: 0`). `GetLandRegistry` needed **no code
  change** — it's a generic passthrough of whatever object is stored.
- **`ItemDefinition`** (Config) is a new SO describing a buildable: `itemId`, `displayName`,
  `cost`, `rarity`, `yieldBonus`, and the tile `buildLevel` it represents/unlocks.
  `DatabaseRegistry.AllItems` / `GetItem(itemId)` mirror the existing drone accessors.
- **`BuildPaletteService`** (Economy) returns the items a tile can build next:
  `tile.State == OwnedByPlayer && tile.BuildLevel < EconomyConfig.MaxBuildLevel`, filtered to
  `ItemDefinition.BuildLevel == tile.BuildLevel + 1` (linear progression — one item per level).
  It lives in `Economy/` (not `World/` as originally sketched) since it depends on
  `DatabaseRegistry`/`EconomyConfig`; `SocialUniverse.Economy`'s asmdef now references
  `SocialUniverse.World` for `TileData`/`TileState`.
- **`TileExtrusionView`** (World) mirrors `TileColorizer`: `RefreshTile(tile)` calls
  `HexasphereManager.SetTileExtrudeAmount(tileId, tile.BuildLevel / EconomyConfig.MaxBuildLevel)`.
  The Hexasphere plugin's `SetTileExtrudeAmount` works whether or not the "Extruded" flag is
  enabled on the Hexasphere component (falls back to vertex elevation), so **no scene setup is
  required** for this to work.
- **`BuildModeController`** (App, `IStartable`/`IDisposable`) mirrors `TilePurchaseHandler`:
  subscribes to a new `HexasphereManager.BuildItemRequestedEvent { TileData Tile; ItemDefinition
  Item; }` (published by a future build-mode UI — none exists yet, see "No new UI screens"
  below), validates ownership/level progression, calls `PlaceBuild`, and on success increments
  `tile.BuildLevel`, updates `LandRegistryService` and `TileExtrusionView`, and applies the
  returned balance to `Wallet`.
- **No new UI screens.** As with Phase 1, build placement is wired as an `EventBus` event +
  App-layer controller with no screen to publish it yet — a future build-mode UI just needs to
  call `EventBus.Publish(new BuildItemRequestedEvent { Tile = ..., Item = ... })`.
- **Test coverage deviation:** the plan called for a `BuildModeControllerTests.cs` with a fake
  backend, but `BuildModeController` follows the same shape as the (untested)
  `TilePurchaseHandler`/`LandPurchaseService` — an `async void` event handler with a private
  response DTO. Consistent with that existing precedent (no unit tests for App-layer purchase
  handlers), only `BuildPaletteServiceTests.cs` was added; `BuildModeController`'s logic is
  exercised end-to-end once a UI exists, via a future PlayMode test (see
  `PlanetSceneFlowTests.cs`).

### Phase 3 — Visitor-Driven Yield Notes

- **`YieldService.ClaimYieldAsync(tileId, planetId)`** calls the new `ClaimYield` server
  function, which computes accrued coin income for an owned tile:
  `granted = floor(BaseYieldPerTilePerHour * (1 + buildBonus + visitBonus) * elapsedHours)`,
  where `buildBonus = buildLevel * BuildLevelYieldMultiplier`,
  `visitBonus = min(visitCount, MaxVisitCount) * VisitYieldBonus`, and `elapsedHours` is capped
  at `MaxYieldAccrualHours`. On success, `Wallet.SetCoins(newBalance)` is applied and
  `LandRegistryService.ResetYieldState(tileId)` zeroes `visitCount` and resets
  `lastYieldClaimTs` locally. The yield-formula constants in `ClaimYield.js` are duplicated from
  `EconomyConfig`'s `[Header("Yield")]` values (same "must match" pattern as
  `GrantOfflineIncome.js`'s idle-rate constants) — if those tunables change, update both places.
- **`VisitorTracker.RecordVisitAsync(tileId, planetId)`** calls the new `RecordVisit` server
  function, which increments `visitCount` (capped at `MaxVisitCount`) on a tile's registry entry
  if the caller isn't the owner. No economy mutation.
- **`VisitorTrackingController`** (App, `IStartable`/`IDisposable`) subscribes to
  `TileSelectedEvent`. When the selected tile's `State == OwnedByOther` and differs from the
  last-recorded tile (avoids spamming `RecordVisit` on repeated clicks of the same tile), it
  calls `VisitorTracker.RecordVisitAsync`.
- **⚠️ M4 dependency caveat — visitor tracking is a stand-in.** True "a player is physically
  standing on this tile" detection needs M4's presence/position-sync layer
  (`NetworkPlayer`/`PlayerSyncController`), which doesn't exist yet. For M3, **selecting a tile
  you don't own counts as a "visit"** — this exercises the entire yield pipeline end-to-end
  (registry `visitCount` → `ClaimYield` bonus → wallet) but is not real cross-player visit
  attribution. **This will need revisiting once M4's presence layer ships** — likely replacing
  `TileSelectedEvent` with a proximity/presence trigger as the call site for `RecordVisit`,
  with no change needed to `ClaimYield`, `YieldService`, or the registry schema.
- **No new UI screens.** As with Phases 1–2, yield claiming is wired as an `EventBus`-free
  direct service call (`YieldService.ClaimYieldAsync`) ready for a future HUD "Claim Yield"
  button — no controller is needed on the claim side since there's no event to react to yet.

### Phase 4 — Upkeep & Resale Notes

- **`UpkeepService.ApplyUpkeepAsync(planetId)`** calls the new `ApplyUpkeep` server function,
  which charges `EconomyConfig.UpkeepPerTilePerDay` coins per full day elapsed since each owned
  tile's `lastUpkeepTs`. If the player can afford it, the cost is deducted and `lastUpkeepTs`
  advances by the elapsed days (`chargedTiles`); if not, the registry entry is deleted and the
  tile reverts to `Available` for everyone (`revertedTiles`). On the client, `Wallet.SetCoins`
  is applied from `newBalance`, and `LandRegistryService.RemoveTile(tileId)` is called for each
  reverted tile.
- **`UpkeepController`** (App, `IStartable`/`IDisposable`) is a poll loop mirroring
  `LandRegistrySyncController`: every `EconomyConfig.UpkeepPollIntervalSec` (default 60s) it
  calls `UpkeepService.ApplyUpkeepAsync`. For each reverted tile it looks up the tile via
  `HexasphereManager.GetTile`, resets `State = Available`, `OwnerId = null`, `BuildLevel = 0`,
  and refreshes both `TileColorizer` and `TileExtrusionView`. The poll interval is
  intentionally short relative to the once-per-day charge — the function is a cheap no-op when
  no tile is due.
- **`LandSaleService.SellAsync(tileId, planet)`** computes the refund client-side as
  `round(EconomyConfig.BaseLandPrice * planet.LandPriceMultiplier * EconomyConfig.LandResaleRate)`
  (default resale rate 0.5 — half the current purchase price) and calls the new `SellLand`
  server function with `{ tileId, planetId, refund }`. On success it removes the tile from the
  M2 `LandRegistry` cache (new `RemoveOwned` helper), removes the entry from
  `LandRegistryService` via `RemoveTile`, and applies `newBalance` to `Wallet`. The server still
  gates the payout on `entry.ownerId === playerId` — same trust model as `PurchaseLand`'s
  client-supplied `price`.
- **`LandSaleHandler`** (App, `IStartable`/`IDisposable`) mirrors `TilePurchaseHandler`:
  subscribes to a new `HexasphereManager.TileSellRequestedEvent { TileData Tile }` (published by
  a future "Sell" UI — none exists yet, see "No new UI screens" below), validates
  `tile.State == OwnedByPlayer`, calls `LandSaleService.SellAsync`, and on success resets
  `tile.State = Available`, `tile.OwnerId = null`, `tile.BuildLevel = 0`, then refreshes
  `TileColorizer` and `TileExtrusionView`.
- **DTO simplification:** the plan called for a `LandSaleRequest`/private-`SellLandResponse`
  pair (mirroring `LandPurchaseService`). Instead, a single public `LandSaleResult` class is
  used directly as both `_backend.CallAsync<LandSaleResult>(...)`'s type parameter and the
  service's return type — same public-DTO pattern as `YieldClaimResult`/`UpkeepResult`/
  `RecordVisitResult`, needed because a `FakeBackendClient` in the test assembly can't reference
  a private nested type for `typeof(T)` comparisons. There's no unused request wrapper since the
  refund is a single computed value passed straight through.
- **No new UI screens.** As with Phases 1–3, selling a tile is wired as an `EventBus` event +
  App-layer handler with no screen to publish it yet — a future "Sell Land" button just needs to
  call `EventBus.Publish(new TileSellRequestedEvent { Tile = ... })`.

**Setup Required (new, in addition to M2's pending checklist):**

- [ ] Deploy `GetLandRegistry` to Cloud Code; redeploy updated `PurchaseLand`; deploy
      `PlaceBuild`, `ClaimYield`, `RecordVisit`, `ApplyUpkeep`, `SellLand`.
- [ ] Verify the `@unity-services/cloud-save-1.4` Custom Data API surface used in
      `GetLandRegistry.js`/`PurchaseLand.js`/`PlaceBuild.js`/`ClaimYield.js`/`RecordVisit.js`/
      `ApplyUpkeep.js`/`SellLand.js`
      (`CustomDataManagementApi.getCustomItems` / `.setCustomItem`) against the dashboard's
      bundled SDK types — written from best knowledge, not yet confirmed against an actual
      deploy. If the names/shape differ, fix and add a "Known Issue" entry, same as #6
      (`PurchaseLand` SDK signature mismatch).
- [ ] Author at least one `ItemDefinition` asset per build level (1..`EconomyConfig.MaxBuildLevel`)
      and add them to `DatabaseRegistry._items` so `BuildPaletteService` has items to offer.


### M3 Completion Checklist

**Automated Tests**

- [x] EditMode: `LandRegistryService` — `RefreshAsync` populates the tile-ownership map from `GetLandRegistry`; `GetOwner`/`SetOwner`/`GetEntry`/`SetBuildLevel`/`ResetYieldState`/`RemoveTile` behave correctly (`LandRegistryServiceTests`)
- [x] EditMode: `BuildPaletteService` — available items filtered by ownership and build-level progression (`BuildPaletteServiceTests`)
- [x] EditMode: `YieldService` — `ClaimYieldAsync` applies `newBalance` to `Wallet` and resets registry yield state on success, leaves both unchanged on failure (`YieldServiceTests`)
- [x] EditMode: `VisitorTracker` — `RecordVisitAsync` calls `RecordVisit` with `tileId`/`planetId` and returns the updated visit count (`VisitorTrackerTests`)
- [x] EditMode: `UpkeepService` — `ApplyUpkeepAsync` applies `newBalance` to `Wallet` and removes registry entries for reverted tiles, leaves both unchanged when no tile is due (`UpkeepServiceTests`)
- [x] EditMode: `LandSaleService` — `SellAsync` applies `newBalance` to `Wallet` and clears ownership (`LandRegistry`/`LandRegistryService`) on success, leaves both unchanged on failure (`LandSaleServiceTests`)
- [ ] EditMode: `BuildModeController` — placing an item on an owned tile updates `TileData.BuildLevel` (deferred — see Phase 2 notes)
- [ ] PlayMode: Player A purchases a tile → Player B sees tile change color to "other-owned"
- [ ] PlayMode: Visitor selects another player's tile → `VisitorTracker` increments count → owner's `ClaimYield` reflects the bonus (M3 stand-in for true presence-based visits — see Phase 3 notes)

**Manual Play Mode Verification**

- [ ] Own a tile — other players see it as blue ("other-owned") in their client
- [ ] Place a building on an owned tile — `TileExtrusionView` animates the tile height
- [ ] Claim yield on a visited tile — coins added server-side, reflected in HUD
- [ ] Unpaid upkeep causes tile to revert to available (per config schedule)
- [ ] Sell a tile — ownership transfers, seller receives coins, buyer's client updates

**Architecture Rules**

- [ ] `LandRegistryService` is the single source of tile ownership — `TileData` is a view cache only
- [ ] All yield/build/sell ops route through `ServerCode/` functions
- [ ] `ItemDefinition` SO drives buildable costs/bonuses — no hardcoded values

---

## M4 — Social: Presence, Chat, Friends, Profiles 🚧 CODE COMPLETE — AWAITING DEPLOY/SETUP

**Exit criteria:** See others on a planet, chat with moderation, add friends, view profiles.

**Pre-requisite:** Age policy decision — **not yet resolved** (see Open Decisions). `SocialConfig`
ships a provisional teen-safe default (`ChatFilterLevel.Strict`) so M4 isn't blocked on it; see
"Chat & Moderation Notes" below.

All four areas (presence/shards, chat, friends/DMs, profiles/reporting) are code-complete, wired
into both `RootLifetimeScope` (dev-mode mocks vs. production UGS services) and `PlanetSceneScope`
(standalone-mode mocks), and covered by EditMode tests (79/79 passing, up from 39/39 in M3). What
remains is UGS dashboard configuration, Cloud Code deployment, and manual/PlayMode verification —
see "Setup Required" and the "M4 Completion Checklist" below. (Presence was reworked onto
Vivox-only in `refactor/vivox-only-social` — see `MIGRATION.md`.)
**A pre-existing PlayMode regression (Known Issue #7) currently blocks PlayMode verification for
M3 and M4 alike.**


| Script                                  | Path          | Responsibility                                                              | Status |
| ---------------------------------------- | ------------- | ----------------------------------------------------------------------------- | ------ |
| `SocialConfig` (SO)                     | `Config/`     | M4 tunables: chat filter level/words, channel/message limits, display-name length | ✅ |
| `IPresenceService` / `VivoxPresenceService` | `Net/`    | Who is on this planet right now, derived from the roster of the planet's Vivox text channel | ✅ |
| `LocalMockPresenceService`              | `Net/`        | Offline stub — `SimulatePlayerJoined`/`SimulatePlayerLeft` test helpers     | ✅     |
| ~~`ShardManager`~~                      | ~~`Net/`~~    | **Removed** (`refactor/vivox-only-social`) — no Multiplayer Sessions/Relay; see `MIGRATION.md` | — |
| ~~`NetworkPlayer`~~ / ~~`PlayerSyncController`~~ | ~~`Net/`~~ | **Removed** (`refactor/vivox-only-social`) — no replicated player markers or position sync; players never see each other move | — |
| `IChatService` / `ChatService`          | `Social/`     | Contract + Vivox-backed implementation — connect, join channel, send/receive | ✅ |
| `LocalMockChatService`                  | `Social/`     | Offline loopback — `SimulateIncoming` test helper                          | ✅     |
| `ChatMessage` / `ChatSendStatus`        | `Social/`     | Message DTO + send-result enum (`Sent`, `Empty`, `TooLong`, `Filtered`, `NoChannel`, `NotFriend`, `Blocked`) | ✅ |
| `ChatChannelController`                 | `Social/`     | Active-channel management, `ChatMessageReceivedEvent` on `EventBus`, `SwitchToGlobal/Local/GuildAsync`, `SendAsync` with moderation | ✅ |
| `ChatModerationFilter`                  | `Social/`     | `IsClean`/`Sanitize`/`Apply`/`SanitizeIncoming` — char-substitution normalization (`@→a`, `1`/`!→i`, `0→o`, `3→e`, `$`/`5→s`, `7→t`) | ✅ |
| `IFriendsService` / `FriendsService`    | `Social/`     | UGS Friends SDK-backed — roster, incoming/outgoing requests, send/accept/decline/remove | ✅ |
| `LocalMockFriendsService`               | `Social/`     | In-memory mock — `SimulateIncomingRequest` test helper                     | ✅     |
| `DirectMessageService`                  | `Social/`     | Wraps `IChatService` DMs with friends-only/moderation/block rules, `DirectMessageReceivedEvent` | ✅ |
| `ProfileService` / `PlayerProfile`      | `Social/`     | `GetProfileAsync`/`UpdateDisplayNameAsync`; `PlayerProfile` DTO (PlayerId, DisplayName, Level, Xp, Badges[], TilesOwned) | ✅ |
| `ReportService`                         | `Social/`     | `ReportPlayerAsync`/`Block`/`UnblockPlayerAsync` + local-only `MutePlayer`; `ReportResult`/`BlockResult` DTOs | ✅ |
| `PlanetPresenceController`              | `App/`        | `IStartable`/`IDisposable` — joins planet presence + local chat channel on scene start, leaves on dispose, logs join/leave | ✅ |
| `SocialServicesInitializer`             | `App/`        | `IStartable`/`IDisposable` in Root scope — on `PlayerReadyEvent`, connects chat, joins global channel, initializes friends roster | ✅ |
| `SubmitReport`                          | `ServerCode/` | Writes to Custom Data `moderation`/`reports` (capped 500); returns `{ success, reportId }` | ✅ |
| `BlockUser`                             | `ServerCode/` | Reads/writes player's `blocked_users` Cloud Save key (capped 200); returns `{ success, blockedUsers }` | ✅ |
| `ModerateMessage`                       | `ServerCode/` | Standalone moderation function (`BLOCKED_WORDS`/`CHAR_MAP`) | ⚠️ appears **unused/orphaned** — no caller found; `UpdateProfile.js` does its own inline moderation. Decide whether to wire it in server-side or remove it |
| `GetPlayerProfile`                      | `ServerCode/` | Reads target player's `player_profile` Cloud Save + sums `owned_tiles_*` for `tilesOwned`; defaults to `"Pilot {id6}"` if unset | ✅ |
| `UpdateProfile`                         | `ServerCode/` | Validates/commits `displayName` into `player_profile`, merging with existing; re-moderates server-side (`BLOCKED_WORDS`/`CHAR_MAP`/`MAX_DISPLAY_NAME_LENGTH=20` duplicated from `SocialConfig`) | ✅ |
| `SocialDebugPanel`                      | `UI/`         | In-editor/dev overlay — opens a chat panel with channel selector and message list; opened via HUD chat button | ✅ |
| `ChatMessageItemView`                   | `UI/`         | Reusable chat message row — binds sender name, message text, timestamp from a `ChatMessage` DTO | ✅ |
| `ChatSendProbe`                         | `UI/`         | Input field + Send button wired to `ChatChannelController.SendAsync`; shows send-status feedback | ✅ |
| `ChatBubbleMaxWidth`                    | `UI/`         | Layout helper — clamps chat bubble width to a fraction of screen width for readability | ✅ |
| `DisplayNameModal`                      | `UI/`         | Modal overlay for updating the player's display name — calls `ProfileService.UpdateDisplayNameAsync`; registered in `PlanetSceneScope` | ✅ |


### Assembly & DI Changes

| Change | Detail |
| ------ | ------ |
| New assembly | `SocialUniverse.Social` at `Assets/_Project/Scripts/Social/` — references `VContainer`, `SocialUniverse.Core`, `SocialUniverse.Config`, `Unity.Services.Vivox`, `Unity.Services.Friends` (no dependency on `SocialUniverse.Net`) |
| `SocialUniverse.Net.asmdef` | References `Unity.Services.Vivox`, `SocialUniverse.Social`. (`Unity.Services.Multiplayer`/`Unity.Netcode.Runtime`/`Unity.Collections` removed in `refactor/vivox-only-social` — see `MIGRATION.md`) |
| `SocialUniverse.App.asmdef` | Added `SocialUniverse.Social` reference |
| `SocialUniverse.Tests.asmdef` | Added `SocialUniverse.Net`, `SocialUniverse.Social`, `SocialUniverse.World` references |
| `RootLifetimeScope` | New `[SerializeField] SocialConfig _socialConfig`. Dev mode (`_devMode = true`) registers `LocalMockChatService`/`LocalMockFriendsService`/`LocalMockPresenceService`; production registers `ChatService`/`FriendsService`/`VivoxPresenceService` (all `As<I*Service>`). Both modes register `ChatModerationFilter`, `ReportService`, `ChatChannelController`, `DirectMessageService`, `ProfileService`, `RegisterInstance(_socialConfig)`, `RegisterEntryPoint<SocialServicesInitializer>()` |
| `PlanetSceneScope` | New `[SerializeField] SocialConfig _socialConfig` (standalone mode only — production gets it from `RootLifetimeScope`). Standalone (`parentReference.Type == null`) registers the same M4 mock set as `RootLifetimeScope`'s dev mode, plus `RegisterInstance(_socialConfig ?? ScriptableObject.CreateInstance<SocialConfig>())`. New `RegisterEntryPoint<PlanetPresenceController>()` (both modes) |
| `Bootstrap.unity` | `RootLifetimeScope._devMode = 0` (production); `_socialConfig` assigned → `Assets/SocialConfig.asset` (misplaced — see Assets section above) |
| `Packages/manifest.json` | Added `com.unity.services.friends@1.1.1`, `com.unity.services.vivox@16.11.0`. (`com.unity.netcode.gameobjects`, `com.unity.services.multiplayer` removed in `refactor/vivox-only-social` — see `MIGRATION.md`) |


### Presence Notes

- **Local (per-planet) channel deferred.** `ChatChannelController.SwitchToLocalAsync`/
  `LocalChannelName` were removed (post-`refactor/vivox-only-social` follow-up) — for now there
  is one shared **Global** channel for everyone, doubling as "the planet channel" until
  per-planet chat is actually needed. `VivoxPresenceService.JoinPlanetAsync`/
  `LocalMockPresenceService.JoinPlanetAsync` both ignore their `planetId` argument and join/mock
  the Global channel; `IPresenceService.JoinPlanetAsync` keeps the `planetId` parameter so the
  per-planet behavior can come back without another interface change.
- **`VivoxPresenceService`** derives presence from the roster of the shared Vivox text
  channel — `VivoxService.Instance.ActiveChannels[channelName]` *is* the player list. It
  delegates channel join/leave to `ChatChannelController` (`SwitchToGlobalAsync`), so joining
  for chat and joining for presence are the same Vivox channel join — there is no separate
  session, shard, or host. `ParticipantAddedToChannel`/`ParticipantRemovedFromChannel` drive
  `PlayerJoined`/`PlayerLeft`.
- **`PlanetPresenceController`** (App, `IStartable`/`IDisposable`) is the glue: on Planet scene
  start it calls `IPresenceService.JoinPlanetAsync(planetId)` and
  `ChatChannelController.SwitchToLocalAsync` (joins the planet's local chat channel), and leaves
  both on dispose. In production the two calls converge on the same channel join; in dev mode
  `IPresenceService` is a standalone mock so both calls are needed independently.
- There are no replicated player markers or position sync (removed `NetworkPlayer`/
  `PlayerSyncController` — see `MIGRATION.md`): players never see each other move, so presence
  is purely "who's in this channel," not where they are.

### Chat & Moderation Notes

- **`ChatChannelController`** is the single point of contact for chat: it tracks the active
  channel, exposes `SwitchToGlobal/Local/GuildAsync`, and publishes `ChatMessageReceivedEvent` on
  the `EventBus` for incoming messages (no `ChatScreen` UI exists yet — same "wire the event, no
  screen yet" pattern as M3's build/sell/yield events).
- **`ChatModerationFilter`** applies `SocialConfig.BlockedWords` with character-substitution
  normalization (`@→a`, `1`/`!→i`, `0→o`, `3→e`, `$`/`5→s`, `7→t`) before checking against the
  list, so simple letter-for-symbol evasion is caught. Behavior is gated by
  `SocialConfig.ChatFilterLevel`: `Off` = no client-side filtering, `Moderate` = blocked words
  masked (message still sends), `Strict` = message rejected outright (`ChatSendStatus.Filtered`).
- **Provisional age-policy default:** `SocialConfig.ChatFilterLevel` defaults to `Strict` for
  *every* player — a deliberate "teen-safe by default" stand-in for the still-open age-policy
  decision (Open Decisions table). When that decision lands, per-age-band behavior should be
  layered in via `AgeGateService` (M10) rather than changing the social services themselves —
  `SocialConfig` already isolates the tunable.
- **"Must match" duplication, again:** `SocialConfig.BlockedWords`/`MaxDisplayNameLength` must
  match `BLOCKED_WORDS`/`CHAR_MAP`/`MAX_DISPLAY_NAME_LENGTH` duplicated in
  `ServerCode/UpdateProfile.js` (and `ServerCode/ModerateMessage.js`, if kept) — same pattern as
  the M3 yield-formula constants in `ClaimYield.js`. If the word list or limits change, update
  both places.
- **`ModerateMessage.js` looks orphaned** — `ChatChannelController`/`ChatService` don't call it,
  and `UpdateProfile.js` re-implements its own inline moderation rather than calling it. Either
  wire it in as the server-side enforcement path for chat messages, or remove it as dead code.

### Friends & Direct Messages Notes

- **`FriendsService`** wraps the UGS Friends SDK for roster/request management;
  `LocalMockFriendsService` is an in-memory mock with a `SimulateIncomingRequest` helper for
  tests/dev.
- **`DirectMessageService`** layers friends-only + moderation + block checks on top of
  `IChatService`'s DM primitives, publishing `DirectMessageReceivedEvent` on the `EventBus`. As
  with chat channels, no `FriendsScreen`/DM UI exists yet.
- **`SocialServicesInitializer`** (Root scope, `IStartable`/`IDisposable`) subscribes to
  `PlayerReadyEvent` and, once the player is ready, connects `IChatService`, joins the global
  channel (`SocialConfig.GlobalChannelName`), and initializes the friends roster — this is the
  app-wide (not per-planet) social bring-up, complementing `PlanetPresenceController`'s
  per-planet bring-up.

### Profiles & Reporting Notes

- **`ProfileService.GetProfileAsync`** calls `GetPlayerProfile`, which reads the target player's
  `player_profile` Cloud Save record and sums `owned_tiles_{planetId}` list lengths across
  planets for `TilesOwned`; if no profile has been saved yet it returns sensible defaults
  (`"Pilot {first 6 chars of playerId}"`, level 0, etc.).
- **`ProfileService.UpdateDisplayNameAsync`** calls `UpdateProfile`, which re-validates/moderates
  the name server-side and merges it into the existing `player_profile` record — the client-side
  `ChatModerationFilter`/`SocialConfig.MaxDisplayNameLength` check is advisory only, matching the
  "server is authoritative" rule.
- **`ReportService`** continues the M3 public-DTO testability pattern: `ReportResult`,
  `BlockResult`, `PlayerProfile`, and `ProfileUpdateResult` are all public top-level types so
  `FakeBackendClient.CallAsync<T>` in the test assembly can use them as type parameters.
  `MutePlayer` is local-only (no server round-trip) — it just suppresses incoming messages from
  that player on this client.

**Setup Required (new, in addition to M2/M3's pending checklists):**

- [ ] Deploy `SubmitReport`, `BlockUser`, `GetPlayerProfile`, `UpdateProfile` to Cloud Code;
      decide on `ModerateMessage` (wire it in server-side or remove it as dead code) before
      deploying it.
- [ ] UGS Dashboard: enable/configure **Vivox** (text chat channels) and **Friends** for this
      project. (Multiplayer Sessions/Relay no longer needed — removed in `refactor/vivox-only-social`,
      see `MIGRATION.md`.)
- [x] ~~Create a `NetworkPlayer` + `PlayerSyncController` + `NetworkObject` prefab...~~ —
      superseded: the NGO player-marker/session model was removed in `refactor/vivox-only-social`.
      Presence no longer needs a spawned prefab; it reads the Vivox channel roster directly.
- [ ] Move `Assets/SocialConfig.asset` into `Assets/_Project/ScriptableObjects/` per project
      convention and re-point `Bootstrap.unity`'s `RootLifetimeScope._socialConfig`.
- [ ] Assign `_socialConfig` on `Planet.unity`'s `PlanetSceneScope` (the field exists in code but
      the scene hasn't been re-saved since it was added, so it's currently unassigned).
- [ ] Known Issue #7 (`PlanetSceneScope.Container not initialized` in `PlanetSceneFlowTests`)
      **confirmed still present** after `refactor/vivox-only-social` — re-ran PlayMode tests
      post-refactor and both `PlanetSceneFlowTests` still fail at `SetUp` with the same error.
      It was not caused by `ShardManager.WithRelayNetwork()`/`NetworkManager.Singleton`; the
      real cause is still open and unrelated to this migration.


### M4 Completion Checklist

**Automated Tests**

- [x] EditMode: `ChatModerationFilterTests` — `IsClean`/`Sanitize`/`Apply`/`SanitizeIncoming`, including char-substitution normalization
- [x] EditMode: `ChatChannelControllerTests` — channel switching, `SendAsync` moderation outcomes, `ChatMessageReceivedEvent`
- [x] EditMode: `LocalMockFriendsServiceTests` — send/accept/decline/remove requests update both rosters
- [x] EditMode: `DirectMessageServiceTests` — friends-only/moderation/block rules, `DirectMessageReceivedEvent`
- [x] EditMode: `ProfileServiceTests` — `GetProfileAsync`/`UpdateDisplayNameAsync` against `FakeBackendClient`
- [x] EditMode: `ReportServiceTests` — `ReportPlayerAsync`/`Block`/`UnblockPlayerAsync` payloads and `MutePlayer` local suppression
- [ ] PlayMode: Two clients on same planet — both `VivoxPresenceService` instances show each other via the channel roster (blocked on Known Issue #7 + Vivox setup)
- [ ] PlayMode: Chat message sent from Client A appears in Client B's channel (blocked on Known Issue #7 + Vivox setup; no `ChatScreen` UI yet either)

**Manual Play Mode Verification**

- [ ] Two devices/editors on same planet — other player marker is visible and moves
- [ ] Send a chat message — appears in global channel for both clients within 1 s
- [ ] Type a filtered word — message is blocked before send (Strict) or masked (Moderate)
- [ ] Report a player — server acknowledges; reported user's messages can be hidden via `MutePlayer`
- [ ] Add a friend — friend appears in friends list with online/offline indicator
- [ ] Follow a friend to their shard — scene transitions and player appears in correct shard
- [ ] View a profile — name, level, badges, land count displayed correctly
- [ ] Update display name — re-moderated server-side, persists across sessions

**Architecture Rules**

- [x] `IChatService` abstracts the provider (Vivox) — no SDK calls outside `ChatService`/`FriendsService`
- [ ] Age policy configuration gates chat features for minors — `SocialConfig` provides a provisional default, but `AgeGateService` (M10) doesn't exist yet to apply per-age-band behavior
- [x] `ReportService` / `BlockUser` always route to `ServerCode/` — client cannot self-moderate (`MutePlayer` is intentionally local-only and non-authoritative)
- [x] `SocialUniverse.Social.asmdef` created and does not depend on `SocialUniverse.Net` directly (dependency runs the other way: `SocialUniverse.Net` → `SocialUniverse.Social`)

---

## Post-M4 Infrastructure — Planet Loading Screen ✅ CODE COMPLETE

**Goal:** Show a full-screen loading overlay while the Planet scene and all server data load, with a smooth animated progress bar and a minimum 2-second display time.

**New files:**

| File | Path | Responsibility |
|---|---|---|
| `PlanetSceneReadyEvent` | `Core/` | Published by `PlanetSceneBootstrapper` when all async setup is complete |
| `LoadingStatusEvent` | `Core/` | Published at each setup step with a `float Progress` (0–1); drives the slider |
| `LoadingScreenView` | `UI/` | `MonoBehaviour` in the `LoadingScreen` scene — animates `Slider` toward each progress target, shows live `%` text, enforces 2 s minimum via coroutine, then calls `SceneManager.UnloadSceneAsync(gameObject.scene)` |

**Modified files:**

| File | Change |
|---|---|
| `Core/Constants.cs` | Added `SceneNames.LoadingScreen = "LoadingScreen"` |
| `Core/PlanetState.cs` | `LoadAsync` now loads `LoadingScreen` first, then `Planet`; `UnloadAsync` defensively unloads `LoadingScreen` if still present |
| `App/PlanetSceneScope.cs` | `SceneLoader` added to standalone-mode registrations; `PlanetSceneBootstrapper.Start()` checks if `LoadingScreen` is already loaded (standalone guard); publishes `LoadingStatusEvent` at five milestones (0.15 → 0.35 → 0.55 → 0.75 → 0.90) then `PlanetSceneReadyEvent` |

**Load sequence (production):**

```
PlanetState.Enter()
  └─ SceneLoader.LoadAsync("LoadingScreen")   ← visible before Planet loads
  └─ SceneLoader.LoadAsync("Planet")

PlanetSceneBootstrapper.Start()
  ├─ LoadingStatusEvent(0.15)  — planet + asteroids initialised
  ├─ LoadingStatusEvent(0.35)  — wallet hydrated
  ├─ LoadingStatusEvent(0.55)  — profile loaded
  ├─ LoadingStatusEvent(0.75)  — land tiles restored
  ├─ LoadingStatusEvent(0.90)  — session started
  └─ PlanetSceneReadyEvent

LoadingScreenView
  ├─ Slider animates via Mathf.MoveTowards (speed: 0.5/s, Inspector-tunable)
  ├─ Text shows live "N%" derived from animated value
  ├─ Sets target to 1.0 on PlanetSceneReadyEvent
  └─ Waits: max(2 s elapsed, fill animation reaches 100%) → UnloadSceneAsync
```

**Standalone fallback:** when the Planet scene is opened directly in the Editor (no `Bootstrap`/`PlanetState`), `PlanetSceneBootstrapper` detects `LoadingScreen` is not loaded and loads it itself before publishing any events.

**Setup Required:**

- [ ] Create `Assets/Scenes/LoadingScreen.unity` (new scene)
- [ ] Add a full-screen Canvas (Sort Order > Planet scene canvases) with a Panel background, a `Slider` (non-interactable), and a `TMP_Text` for the percentage
- [ ] Attach `LoadingScreenView` to a root GameObject; assign `_slider` and `_percentageText` in Inspector
- [ ] Add `LoadingScreen.unity` to **File → Build Settings** (must be present for `SceneManager.LoadSceneAsync` to find it by name)

---

## M5 — Travel & Solar System 🔲 NOT STARTED

**Exit criteria:** Star map travel, fuel as a recharging gauge, gyro Sky Discovery with star map fallback.

**Pre-requisite:** Sky Discovery decision (AR Foundation vs gyroscope starfield).


| Script                   | Path      | Responsibility                                                        | Status |
| ------------------------ | --------- | --------------------------------------------------------------------- | ------ |
| `SolarSystemController`  | `Travel/` | Owns the star-map scene/hub; manages planet orbit visuals             | 🔲     |
| `StarMapController`      | `Travel/` | Render planets/orbits, selection, travel info panel                   | 🔲     |
| `TravelService`          | `Travel/` | Validate fuel, run scene transition, switch to correct planet + shard | 🔲     |
| `FuelSystem`             | `Travel/` | Fuel state, time-based recharge, free trip home, manual refill        | 🔲     |
| `RocketController`       | `Travel/` | Travel animation + optional dodge minigame                            | 🔲     |
| `SkyDiscoveryController` | `Travel/` | Gyroscope sky view, lock-on to celestial bodies                       | 🔲     |
| `GyroInputProvider`      | `Travel/` | Read attitude sensor; graceful fallback to drag input                 | 🔲     |


### M5 Completion Checklist

**Automated Tests**

- [ ] EditMode: `FuelSystem` — fuel decrements on travel; recharges at correct rate from `EconomyConfig`
- [ ] EditMode: `TravelService` — travel rejected when insufficient fuel; succeeds otherwise
- [ ] EditMode: `GyroInputProvider` — returns fallback input when gyroscope is unavailable
- [ ] PlayMode: Travel from SolarSystem to Planet — `Planet.unity` loads additively, correct `PlanetDefinition` bound

**Manual Play Mode Verification**

- [ ] `SolarSystem.unity` shows all planets in orbit with labels
- [ ] Tap a planet on star map — travel info panel shows fuel cost
- [ ] Confirm travel with sufficient fuel — rocket animation plays, Planet scene loads
- [ ] Fuel gauge in HUD decrements after travel; recharges over time
- [ ] Free trip home works regardless of fuel level
- [ ] Sky Discovery: tilt device (or rotate mouse) — starfield responds to gyro/attitude input
- [ ] Sky Discovery: lock-on to a planet body triggers travel info panel

**Architecture Rules**

- [ ] `SocialUniverse.Travel.asmdef` created with correct namespace
- [ ] Fuel state is server-backed via `FuelState` record — client cannot grant free fuel
- [ ] `GyroInputProvider` gracefully falls back on desktop/simulator builds
- [ ] `SkyDiscoveryController` uses `GyroInputProvider` abstraction — no direct `Input.gyro` calls

---

## M6 — Drones & Mining Depth 🔲 NOT STARTED

**Exit criteria:** Drone upgrade tree, slots, asteroid tiers gating exploration.


| Script                    | Path          | Responsibility                                                 | Status | Notes                                                |
| ------------------------- | ------------- | -------------------------------------------------------------- | ------ | ---------------------------------------------------- |
| `DroneGarageController`   | `Mining/`     | Garage screen logic, slot management, display fleet            | 🔲     |                                                      |
| `DroneUpgradeService`     | `Mining/`     | Apply upgrades server-validated; update `DroneRuntime` stats   | 🔲     |                                                      |
| `DroneDefinition` (SO)    | `Config/`     | Base stats + upgrade curves per drone type                     | ✅      | `Drone_Scout.asset` exists — expand for upgrade tree |
| `UpgradeDefinition` (SO)  | `Config/`     | Individual upgrade step: cost, stat delta, prerequisites       | 🔲     |                                                      |
| `AsteroidDefinition` (SO) | `Config/`     | Tier, mineral table, rarity, value — gates exploration by tier | ✅      | 6 assets exist                                       |
| `MineralInventory`        | `Mining/`     | Track mined minerals server-backed; expose to UI               | 🔲     |                                                      |
| `UpgradeDrone`            | `ServerCode/` | Server function — validate currency, apply upgrade             | 🔲     |                                                      |
| `UnlockDroneSlot`         | `ServerCode/` | Server function — validate cost, add fleet slot                | 🔲     |                                                      |


### M6 Completion Checklist

**Automated Tests**

- [ ] EditMode: `DroneUpgradeService` — upgrade increments correct stat per `UpgradeDefinition`
- [ ] EditMode: `MineralInventory` — adding minerals updates count; over-cap is rejected
- [ ] EditMode: `DroneDefinition` — all upgrade tiers chain correctly without orphaned prereqs
- [ ] PlayMode: Purchase upgrade from DroneGarage → drone stat reflected in next mining session

**Manual Play Mode Verification**

- [ ] `DroneGarageScreen` shows current drone fleet and available upgrades
- [ ] Upgrade a stat (e.g. cargo cap) — cost deducted, stat increases, HUD reflects new cap
- [ ] Unlock a second drone slot — second drone appears in fleet; can assign to different asteroid
- [ ] Tier 2 asteroid requires a Tier 2 drone — lower-tier drone is blocked with clear feedback
- [ ] `MineralInventory` screen shows accumulated minerals by type

**Architecture Rules**

- [ ] `DroneUpgradeService` routes through `UpgradeDrone` server function — no client-side stat grants
- [ ] `DroneDefinition` + `UpgradeDefinition` SOs hold all tunable values
- [ ] `MineralInventory` is server-backed — client is a cache

---

## M7 — Space Stations & Guilds 🔲 NOT STARTED

**Exit criteria:** Join/found a station, co-build, perks, scheduled events.


| Script                | Path          | Responsibility                                               | Status |
| --------------------- | ------------- | ------------------------------------------------------------ | ------ |
| `StationController`   | `Guild/`      | Station scene/hub; manage co-build layout                    | 🔲     |
| `GuildService`        | `Guild/`      | Create/join guild, roster management, roles                  | 🔲     |
| `GuildUpgradeService` | `Guild/`      | Contributions, station level, apply perks (e.g. −fuel cost)  | 🔲     |
| `EventService`        | `Guild/`      | Scheduled festivals/tournaments; timer + reward distribution | 🔲     |
| `CreateGuild`         | `ServerCode/` | Server function — create guild record, assign founder        | 🔲     |
| `JoinGuild`           | `ServerCode/` | Server function — validate invite/open, add member           | 🔲     |
| `Contribute`          | `ServerCode/` | Server function — accept contribution, update station XP     | 🔲     |
| `StartEvent`          | `ServerCode/` | Server function — schedule and broadcast event               | 🔲     |


### M7 Completion Checklist

**Automated Tests**

- [ ] EditMode: `GuildService` — create guild assigns founder with correct role
- [ ] EditMode: `GuildUpgradeService` — contribution adds correct XP; level-up grants perk
- [ ] EditMode: `EventService` — event timer fires reward distribution at expiry
- [ ] PlayMode: Two players in same guild — both see co-build changes on `Station.unity`

**Manual Play Mode Verification**

- [ ] Create a guild — guild record created on server, founder appears as leader
- [ ] Second player joins guild — appears in roster; station scene loads for both
- [ ] Contribute minerals to station — station XP bar fills; level-up perk unlocks
- [ ] Start a scheduled event — countdown visible to all guild members
- [ ] Event ends — rewards distributed server-side and appear in participants' wallets

**Architecture Rules**

- [ ] All guild economy ops (contributions, perks, rewards) go through `ServerCode/` functions
- [ ] `SocialUniverse.Guild.asmdef` created; does not import `SocialUniverse.Social` directly
- [ ] Station scene loaded additively like Planet scene — no hardcoded scene dependencies

---

## M8 — Marketplace & Economy Depth 🔲 NOT STARTED

**Exit criteria:** Player-to-player land/mineral trade, auctions.

**Pre-requisite:** Confirm land resale model (coins-only, no real-money cash-out).


| Script               | Path           | Responsibility                                                           | Status |
| -------------------- | -------------- | ------------------------------------------------------------------------ | ------ |
| `MarketplaceService` | `Economy/`     | Listings search, buy now, escrow management                              | 🔲     |
| `AuctionService`     | `Economy/`     | Place bids, timers, settlement on expiry                                 | 🔲     |
| `LeaderboardService` | `Progression/` | Wealth / visitor count / guild rankings                                  | 🔲     |
| `ListItem`           | `ServerCode/`  | Server function — validate ownership, create listing with escrow         | 🔲     |
| `BuyListing`         | `ServerCode/`  | Server function — deduct buyer coins, transfer ownership, release escrow | 🔲     |
| `PlaceBid`           | `ServerCode/`  | Server function — validate bid > current; hold coins in escrow           | 🔲     |
| `SettleAuction`      | `ServerCode/`  | Server function — on expiry, transfer to winner, refund losers           | 🔲     |


### M8 Completion Checklist

**Automated Tests**

- [ ] EditMode: `MarketplaceService` — listing search returns correct results by type/tier
- [ ] EditMode: `AuctionService` — bid placed correctly; outbid triggers refund of previous bidder
- [ ] EditMode: `LeaderboardService` — rankings update after wealth/visitor changes
- [ ] PlayMode: Player A lists a tile → Player B buys it → ownership transfers server-side

**Manual Play Mode Verification**

- [ ] List a tile for sale — appears on `MarketplaceScreen` for other players
- [ ] Buy a listing — coins deducted, tile ownership transferred, seller receives coins
- [ ] Create an auction — bid placed by another player; escrow holds coins
- [ ] Auction expires — winner receives tile, losers' bids refunded
- [ ] Leaderboard screen shows top players by wealth, visitor count, and guild ranking

**Architecture Rules**

- [ ] Escrow managed server-side — client cannot release funds unilaterally
- [ ] No real-money cash-out path exists in `MarketplaceService` or `AuctionService`
- [ ] All listing/bid/settlement ops route through `ServerCode/` functions

---

## M9 — Monetization 🔲 NOT STARTED

**Exit criteria:** Store, premium currency purchase, season pass, opt-in ads — all receipt-validated.


| Script                      | Path          | Responsibility                                                | Status |
| --------------------------- | ------------- | ------------------------------------------------------------- | ------ |
| `IStoreService`             | `Store/`      | Contract for product catalog and purchase flow                | 🔲     |
| `IAPService`                | `Store/`      | Unity IAP wrapper; product list, initiate purchase            | 🔲     |
| `StoreCatalog`              | `Store/`      | Packs, bundles, fuel refills — designer-editable via SO       | 🔲     |
| `SeasonPassService`         | `Store/`      | Tiers, XP track, reward claims                                | 🔲     |
| `SeasonPassDefinition` (SO) | `Config/`     | Tier thresholds, rewards, duration                            | 🔲     |
| `AdService`                 | `Store/`      | Rewarded ads, opt-in only; callback on completion             | 🔲     |
| `ValidateReceipt`           | `ServerCode/` | Server function — verify platform receipt, prevent replay     | 🔲     |
| `GrantPurchase`             | `ServerCode/` | Server function — grant currency/item after receipt validated | 🔲     |
| `ClaimPassTier`             | `ServerCode/` | Server function — validate XP threshold, grant pass reward    | 🔲     |


### M9 Completion Checklist

**Automated Tests**

- [ ] EditMode: `IAPService` — purchase flow initiates correctly; receipt forwarded to `ValidateReceipt`
- [ ] EditMode: `AdService` — reward only granted after `OnAdCompleted` callback (not on start)
- [ ] EditMode: `SeasonPassService` — tier unlocks when XP threshold reached; rewards claimed once
- [ ] PlayMode: Sandbox purchase → `ValidateReceipt` succeeds → `GrantPurchase` adds currency to wallet

**Manual Play Mode Verification**

- [ ] Open `StoreScreen` — products load from catalog with correct prices
- [ ] Complete a sandbox IAP purchase — receipt validated server-side, Stardust added to wallet
- [ ] Opt-in to rewarded ad — ad plays to completion, reward granted (coins/fuel)
- [ ] Season pass XP fills a tier — claim button activates, reward granted once
- [ ] Replay attack: resubmit a used receipt — server rejects it, no duplicate grant

**Architecture Rules**

- [ ] `IStoreService` abstracts Unity IAP — no `UnityPurchasing` calls in gameplay code
- [ ] Receipt validation always server-side (`ValidateReceipt`) — client never self-grants IAP rewards
- [ ] `AdService` is opt-in only — no forced ads
- [ ] `SeasonPassDefinition` SO drives all tier/reward config — no hardcoded values

---

## M10 — Safety, Settings & Platform 🔲 NOT STARTED

**Exit criteria:** Moderation enforced, age policy, settings screen, analytics, notifications.


| Script                | Path      | Responsibility                                               | Status |
| --------------------- | --------- | ------------------------------------------------------------ | ------ |
| `SettingsService`     | `Safety/` | Gyro on/off, notifications, reduce-motion, chat-filter level | 🔲     |
| `AgeGateService`      | `Safety/` | Age policy check; apply minor-mode restrictions globally     | 🔲     |
| `ModerationService`   | `Safety/` | Hooks to server moderation pipeline; enforce bans/mutes      | 🔲     |
| `AnalyticsService`    | `Safety/` | Funnel/retention/economy events; GDPR-compliant opt-in       | 🔲     |
| `NotificationService` | `Safety/` | Local + push: cargo full, fuel ready, guild events           | 🔲     |


### M10 Completion Checklist

**Automated Tests**

- [ ] EditMode: `AgeGateService` — minor-mode flag disables chat, DMs, and marketplace as expected
- [ ] EditMode: `ModerationService` — banned user flag blocks login; muted user cannot send chat
- [ ] EditMode: `AnalyticsService` — events are queued and not sent until opt-in consent given
- [ ] EditMode: `NotificationService` — local notification scheduled when cargo hits cap

**Manual Play Mode Verification**

- [ ] First launch prompts age gate — minor mode restricts chat and social features
- [ ] `SettingsScreen` — toggle gyro, reduce-motion, notification permission, chat filter level
- [ ] Analytics opt-in / opt-out — events stop transmitting after opt-out
- [ ] Cargo full notification fires when mining session fills drone cargo
- [ ] Fuel-ready notification fires when recharge completes
- [ ] Banned account receives clear feedback and cannot progress past Auth

**Architecture Rules**

- [ ] `AgeGateService` is checked at app boot and gates features globally — not per-feature ad hoc
- [ ] `AnalyticsService` respects GDPR/COPPA consent before sending any events
- [ ] `ModerationService` enforces bans/mutes via server state — client cannot bypass
- [ ] `SocialUniverse.Safety.asmdef` created; no dependency on gameplay namespaces

---

## M11 — UI, Progression Juice & Onboarding 🔲 NOT STARTED

**Exit criteria:** Polish + first-session win in under 60 seconds.

### Core UI Infrastructure


| Script          | Path  | Responsibility                                        | Status |
| --------------- | ----- | ----------------------------------------------------- | ------ |
| `UIManager`     | `UI/` | Root UI, screen stack/navigation, show/hide lifecycle | 🔲     |
| `ScreenBase`    | `UI/` | Base class for screens (show/hide/bind to presenter)  | 🔲     |
| `HUDController` | `UI/` | Persistent HUD: level, XP, currencies, fuel gauge     | 🔲     |
| `CurrencyView`  | `UI/` | Animated coin/stardust balance display                | 🔲     |
| `XPBarView`     | `UI/` | XP fill bar with level milestone markers              | 🔲     |
| `FuelGaugeView` | `UI/` | Fuel gauge with recharge countdown                    | 🔲     |
| `QuestCardView` | `UI/` | Compact quest progress card for HUD                   | 🔲     |
| `RarityFrame`   | `UI/` | Rarity-colored frame for items/minerals               | 🔲     |


### Screens


| Screen               | Responsibility                               | Status |
| -------------------- | -------------------------------------------- | ------ |
| `HomeScreen`         | Root landing — enter planet or hub           | 🔲     |
| `PlanetHUDScreen`    | In-planet overlay — tile info, mining status | 🔲     |
| `MiningScreen`       | Drone status, active mining minigame         | 🔲     |
| `LandPurchaseSheet`  | Bottom sheet for tile purchase confirmation  | 🔲     |
| `MyLandBuildScreen`  | Build mode for owned tiles                   | 🔲     |
| `DroneGarageScreen`  | Fleet view, upgrade purchase                 | 🔲     |
| `StarMapScreen`      | Solar system travel view                     | 🔲     |
| `SkyDiscoveryScreen` | Gyro sky view + planet lock-on               | 🔲     |
| `ChatScreen`         | Channel switcher + message list              | 🔲     |
| `FriendsScreen`      | Friends list + presence + DM                 | 🔲     |
| `ProfileScreen`      | Player profile, badges, stats                | 🔲     |
| `StationScreen`      | Guild station hub                            | 🔲     |
| `MarketplaceScreen`  | Listings browse and purchase                 | 🔲     |
| `StoreScreen`        | IAP packs, season pass, ads                  | 🔲     |
| `SettingsScreen`     | All settings toggles                         | 🔲     |


### Juice & Feedback


| Script              | Path  | Responsibility                               | Status |
| ------------------- | ----- | -------------------------------------------- | ------ |
| `LevelUpModal`      | `UI/` | Full-screen level-up celebration moment      | 🔲     |
| `RewardPopup`       | `UI/` | Floating reward burst (coins, XP, items)     | 🔲     |
| `ToastService`      | `UI/` | Short-lived non-blocking notification toasts | 🔲     |
| `ButtonPressEffect` | `UI/` | 3D press spring animation on tap             | 🔲     |
| `TweenHelper`       | `UI/` | DOTween wrappers for common UI animations    | 🔲     |
| `RewardBurst`       | `UI/` | Particle burst FX on reward grant            | 🔲     |


### Progression


| Script                 | Path           | Responsibility                                        | Status |
| ---------------------- | -------------- | ----------------------------------------------------- | ------ |
| `ProgressionService`   | `Progression/` | XP/level curve + trigger level-up rewards             | 🔲     |
| `QuestService`         | `Progression/` | Daily quests, progress tracking, claim rewards        | 🔲     |
| `QuestDefinition` (SO) | `Config/`      | Quest goals, XP rewards, unlock conditions            | 🔲     |
| `DailyRewardService`   | `Progression/` | Login streak rewards — day-N calendar                 | 🔲     |
| `InventoryService`     | `Progression/` | Owned items/cosmetics/minerals cache                  | 🔲     |
| `OnboardingController` | `Progression/` | Guided first session; sub-60s first win scripted flow | 🔲     |


### M11 Completion Checklist

**Automated Tests**

- [ ] EditMode: `UIManager` — push/pop screen stack navigates correctly; back button works
- [ ] EditMode: `ProgressionService` — XP addition triggers level-up at correct threshold
- [ ] EditMode: `QuestService` — quest progress updates on matching event; claim grants reward once
- [ ] EditMode: `DailyRewardService` — streak increments on consecutive days; resets after missed day
- [ ] EditMode: `InventoryService` — add/remove items correctly; server sync on commit
- [ ] PlayMode: `OnboardingController` — first-session flow completes mine + land purchase under 60 s

**Manual Play Mode Verification**

- [ ] Fresh install: onboarding tutorial guides player to first mine and first tile purchase ≤ 60 s
- [ ] Level up — `LevelUpModal` plays with correct new level and rewards
- [ ] Earn coins — animated `CurrencyView` counter rolls up
- [ ] Complete a daily quest — `QuestCardView` updates, claim button animates
- [ ] Login streak — day-N reward shown on `HomeScreen`; day counter increments
- [ ] All 15 screens reachable from their natural entry point with no dead ends
- [ ] Fuel gauge countdown accurate; `FuelGaugeView` refills smoothly on recharge
- [ ] Button press effect fires on every tappable button
- [ ] `ToastService` shows non-blocking messages without obscuring key UI

**Architecture Rules**

- [ ] `UIManager` is the only entry point for screen navigation — no `GameObject.SetActive` calls elsewhere
- [ ] All screens use MVP pattern — `ScreenBase` passive view, separate presenter/controller
- [ ] `TweenHelper` wraps DOTween — no raw `DOTween.To` calls scattered in screens
- [ ] `OnboardingController` uses `QuestDefinition` SO for first-session goals — not hardcoded

---

## Test Coverage


| Test File                      | Suite    | Coverage                                                                                                                                                                                                                                                                                                                    |
| ------------------------------ | -------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `EventBusTests.cs`             | EditMode | `EventBus` publish/subscribe                                                                                                                                                                                                                                                                                                |
| `GameStateMachineTests.cs`     | EditMode | FSM transitions                                                                                                                                                                                                                                                                                                             |
| `WalletTests.cs`               | EditMode | `Wallet` balance changes                                                                                                                                                                                                                                                                                                    |
| `IdleMiningCalculatorTests.cs` | EditMode | Offline haul calculation                                                                                                                                                                                                                                                                                                    |
| `LocalMockEconomyTests.cs`     | EditMode | Mock economy grant/spend                                                                                                                                                                                                                                                                                                    |
| `LandRegistryServiceTests.cs`  | EditMode | `LandRegistryService.RefreshAsync` populates the tile-ownership map from a fake `IBackendClient`'s `GetLandRegistry` response; `GetOwner`/`SetOwner`/`GetEntry`/`SetBuildLevel`/`ResetYieldState`/`RemoveTile` behavior (schema v2 `LandTileEntry`) |
| `BuildPaletteServiceTests.cs`  | EditMode | `BuildPaletteService.GetAvailableItems` returns items matching `tile.BuildLevel + 1`, only for `OwnedByPlayer` tiles below `EconomyConfig.MaxBuildLevel` |
| `YieldServiceTests.cs`         | EditMode | `YieldService.ClaimYieldAsync` applies `newBalance` to `Wallet` and resets registry yield state on success; leaves both unchanged on failure |
| `VisitorTrackerTests.cs`       | EditMode | `VisitorTracker.RecordVisitAsync` calls `RecordVisit` with the correct `tileId`/`planetId` and returns the response |
| `UpkeepServiceTests.cs`        | EditMode | `UpkeepService.ApplyUpkeepAsync` applies `newBalance` to `Wallet` and removes registry entries for `RevertedTiles`; leaves wallet/registry unchanged when no tile is due |
| `LandSaleServiceTests.cs`      | EditMode | `LandSaleService.SellAsync` applies `newBalance` to `Wallet` and clears ownership in `LandRegistry`/`LandRegistryService` on success; leaves both unchanged on failure |
| `ChatModerationFilterTests.cs` | EditMode | `ChatModerationFilter.IsClean`/`Sanitize`/`Apply`/`SanitizeIncoming`, including char-substitution normalization (`@→a`, `1`/`!→i`, `0→o`, `3→e`, `$`/`5→s`, `7→t`) and `ChatFilterLevel` (Off/Moderate/Strict) behavior |
| `ChatChannelControllerTests.cs` | EditMode | `ChatChannelController` channel switching (`SwitchToGlobal/Local/GuildAsync`), `SendAsync` moderation outcomes (`ChatSendStatus`), and `ChatMessageReceivedEvent` publication on `EventBus` |
| `LocalMockFriendsServiceTests.cs` | EditMode | `LocalMockFriendsService` send/accept/decline/remove friend requests update both rosters; `SimulateIncomingRequest` helper |
| `DirectMessageServiceTests.cs` | EditMode | `DirectMessageService` friends-only/moderation/block rules and `DirectMessageReceivedEvent` publication |
| `ProfileServiceTests.cs`       | EditMode | `ProfileService.GetProfileAsync`/`UpdateDisplayNameAsync` against a `FakeBackendClient` returning `PlayerProfile`/`ProfileUpdateResult` |
| `ReportServiceTests.cs`        | EditMode | `ReportService.ReportPlayerAsync`/`Block`/`UnblockPlayerAsync` payloads (`ReportResult`/`BlockResult`) and local-only `MutePlayer` suppression |
| `FakeSocialDoubles.cs`         | EditMode | Shared `FakeBackendClient`/test doubles for the `Social/` EditMode test suite |
| `PlanetSceneFlowTests.cs`      | PlayMode | ⚠️ Both tests now **fail at `SetUp`** with `PlanetSceneScope.Container not initialized` — see Known Issue #7. Previously (M3) verified: (1) mining taps fill cargo and `CommitCargoAsync` grants `hauled × CoinsPerUnit` coins; (2) selecting an available tile fires `TileSelectedEvent` → `TilePurchaseHandler` → `LandPurchaseService`, debits the wallet, and transfers the tile to `OwnedByPlayer` |


**EditMode total: 79/79 passing** (up from 39/39 in M3 — 40 new tests added across the 7 `Social/` test files above).

**Missing tests (high priority):**

- [ ] EditMode: `LandmarkService` marks exactly 12 tiles as `IsLandmark = true`
- [ ] EditMode: `DatabaseRegistry` lookups (`GetPlanet`, `GetAsteroid`, `GetDrone`)
- [ ] Fix `PlanetSceneFlowTests` PlayMode regression (Known Issue #7) — currently 0/2 passing

**Note:** The temporary `PlayModeVerifier.cs` smoke-test script (and its component on the `PlanetSceneScope` GameObject in `Planet.unity`) has been removed — its checks are now covered by `PlanetSceneFlowTests` under `Assets/_Project/Tests/PlayMode/` (assembly `SocialUniverse.PlayModeTests`). As of M4, both tests fail at `SetUp` — see Known Issue #7.