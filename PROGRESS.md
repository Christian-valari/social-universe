# Social Universe — Project Progress Tracker

> Last updated: 2026-06-10 — Fixed `PurchaseLand` Cloud Code function (broken SDK calls + response shape mismatch with `LandPurchaseService`)
> Engine: Unity 6 (URP 17.3.0) · Branch: `main`

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
| Age policy / content rating | 🔲 **Open**      | Decide before M4 (drives chat restrictions and moderation scope)                               |
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


### Scenes


| Scene               | Status | Notes                                      |
| ------------------- | ------ | ------------------------------------------ |
| `Bootstrap.unity`   | ✅      | DontDestroyOnLoad container, boots to Auth; `RootLifetimeScope` has `_devMode` flag for UGS-free testing |
| `Auth.unity`        | ✅      | Login + Register panels; no success modal — sign-in immediately publishes `PlayerReadyEvent` and transitions to Hub |
| `SolarSystem.unity` | ✅      | Shell — star map placeholder               |
| `Planet.unity`      | ✅      | **Fully wired for Earth** — see M1 detail  |
| `Station.unity`     | ✅      | Shell — guild hub placeholder              |


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
| `PlanetState`                               | `Core/`   | Concrete state — load/unload Planet scene additively                     | ✅      |
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
| `IBackendClient`         | `Net/`        | Contract for Cloud Code RPC calls                                                 | ✅      |                                                                                                               |
| `BackendClient`          | `Net/`        | Cloud Code wrapper with exponential-backoff retry on transient errors             | ✅      | Retries on `NoInternetConnection`, `ServiceUnavailable`, `Unknown`                                            |
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

## M3 — Land System Depth 🔲 NOT STARTED

**Exit criteria:** Networked ownership visible to others, visitor-driven yield, build mode.


| Script                | Path          | Responsibility                                                      | Status |
| --------------------- | ------------- | ------------------------------------------------------------------- | ------ |
| `LandRegistryService` | `Economy/`    | Fetch/subscribe tile ownership for a planet from server             | 🔲     |
| `YieldService`        | `Economy/`    | Compute and claim visitor-driven land income                        | 🔲     |
| `VisitorTracker`      | `Economy/`    | Count/attribute visits to plots (server-backed)                     | 🔲     |
| `UpkeepService`       | `Economy/`    | Recurring land tax sink — deduct upkeep from wallet                 | 🔲     |
| `BuildController`     | `World/`      | Place/move/remove buildables on an owned tile                       | 🔲     |
| `BuildPaletteService` | `World/`      | Available buildables by ownership/inventory                         | 🔲     |
| `TileExtrusionView`   | `World/`      | Reflect build level via tile height visual                          | 🔲     |
| `ItemDefinition` (SO) | `Config/`     | Buildables/decor: cost, rarity, yield bonus                         | 🔲     |
| `ClaimYield`          | `ServerCode/` | Server function — validate and grant land yield                     | 🔲     |
| `PlaceBuild`          | `ServerCode/` | Server function — validate ownership, commit build state            | 🔲     |
| `ApplyUpkeep`         | `ServerCode/` | Server function — deduct recurring upkeep cost                      | 🔲     |
| `SellLand`            | `ServerCode/` | Server function — validate ownership, transfer tile, settle payment | 🔲     |


### M3 Completion Checklist

**Automated Tests**

- [ ] EditMode: `LandRegistryService` — subscribing to a planet's tile stream receives ownership updates
- [ ] EditMode: `YieldService` — yield calculation matches expected formula from `EconomyConfig`
- [ ] EditMode: `UpkeepService` — upkeep is deducted on schedule, tile reverts when overdue
- [ ] EditMode: `BuildController` — placing an item on an owned tile updates `TileData.buildState`
- [ ] PlayMode: Player A purchases a tile → Player B sees tile change color to "other-owned"
- [ ] PlayMode: Visitor steps on tile → `VisitorTracker` increments count → yield accumulates

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

## M4 — Social: Presence, Chat, Friends, Profiles 🔲 NOT STARTED

**Exit criteria:** See others on a planet, chat with moderation, add friends, view profiles.

**Pre-requisite:** Age policy decision (drives chat restrictions and moderation scope).


| Script                  | Path          | Responsibility                                           | Status |
| ----------------------- | ------------- | -------------------------------------------------------- | ------ |
| `PresenceService`       | `Net/`        | Who is on this planet/shard right now                    | 🔲     |
| `ShardManager`          | `Net/`        | Pick/join a planet shard; follow a friend to their shard | 🔲     |
| `NetworkPlayer`         | `Net/`        | Replicated player object on a planet                     | 🔲     |
| `PlayerSyncController`  | `Net/`        | Sync position/avatar/state across clients                | 🔲     |
| `IChatService`          | `Social/`     | Contract for channels, send/receive                      | 🔲     |
| `ChatService`           | `Social/`     | Concrete implementation (Vivox or Nakama channels)       | 🔲     |
| `ChatChannelController` | `Social/`     | Global / local / guild / DM channel switching            | 🔲     |
| `ChatModerationFilter`  | `Social/`     | Client-side profanity filter (server also enforces)      | 🔲     |
| `ReportService`         | `Social/`     | Report / block / mute a player                           | 🔲     |
| `FriendsService`        | `Social/`     | Add/remove/list friends + show their presence            | 🔲     |
| `DirectMessageService`  | `Social/`     | Cross-planet DMs between friends                         | 🔲     |
| `ProfileService`        | `Social/`     | Fetch/update profile, badges, stats                      | 🔲     |
| `PlayerProfile`         | `Social/`     | Runtime model for a player's public profile data         | 🔲     |
| `SubmitReport`          | `ServerCode/` | Server function — log and queue report for moderation    | 🔲     |
| `BlockUser`             | `ServerCode/` | Server function — enforce block at server level          | 🔲     |
| `ModerateMessage`       | `ServerCode/` | Server function — filter/remove messages                 | 🔲     |


### M4 Completion Checklist

**Automated Tests**

- [ ] EditMode: `IChatService` contract — send message, receive message in same channel
- [ ] EditMode: `ChatModerationFilter` — known slurs are filtered on client before send
- [ ] EditMode: `FriendsService` — add friend creates pending request; accept updates both rosters
- [ ] EditMode: `ReportService` — report call constructs correct payload and invokes `SubmitReport` RPC
- [ ] PlayMode: Two clients on same planet — both `PresenceService` instances show each other
- [ ] PlayMode: Chat message sent from Client A appears in Client B's `ChatScreen`

**Manual Play Mode Verification**

- [ ] Two devices/editors on same planet — other player avatar is visible and moves
- [ ] Send a chat message — appears in global channel for both clients within 1 s
- [ ] Type a filtered word — message is blocked before send (client filter fires)
- [ ] Report a player — server acknowledges; reported user's messages can be hidden
- [ ] Add a friend — friend appears in friends list with online/offline indicator
- [ ] Follow a friend to their shard — scene transitions and player appears in correct shard
- [ ] View a profile — name, level, badges, land count displayed correctly

**Architecture Rules**

- [ ] `IChatService` abstracts the provider (Vivox/Nakama) — no SDK calls in gameplay code
- [ ] Age policy configuration gates chat features for minors (verify `AgeGateService` hook exists)
- [ ] `ReportService` / `BlockUser` always route to `ServerCode/` — client cannot self-moderate
- [ ] `SocialUniverse.Social.asmdef` created and does not depend on `SocialUniverse.Net` directly

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
| `PlanetSceneFlowTests.cs`      | PlayMode | ✅ Loads `Planet.unity`, resolves DI services, and verifies: (1) mining taps fill cargo and `CommitCargoAsync` grants `hauled × CoinsPerUnit` coins; (2) selecting an available tile fires `TileSelectedEvent` → `TilePurchaseHandler` → `LandPurchaseService`, debits the wallet, and transfers the tile to `OwnedByPlayer` |


**Missing tests (high priority):**

- [ ] EditMode: `LandmarkService` marks exactly 12 tiles as `IsLandmark = true`
- [ ] EditMode: `DatabaseRegistry` lookups (`GetPlanet`, `GetAsteroid`, `GetDrone`)

**Note:** The temporary `PlayModeVerifier.cs` smoke-test script (and its component on the `PlanetSceneScope` GameObject in `Planet.unity`) has been removed — its checks are now covered by `PlanetSceneFlowTests` under `Assets/_Project/Tests/PlayMode/` (assembly `SocialUniverse.PlayModeTests`). Both tests pass (2/2, ~3.2s total).