# Social Universe — Project Progress Tracker

> Last updated: 2026-09-30 — on `main` (M6 is merged). **M0–M6 are code-complete**, but the game
> is **not ready for internal testing**: the server blockers (#10–#12) and mining validation
> (#16, partly) are fixed **in source only** — no `ServerCode/` deploy has been confirmed — and
> the release keystore is still public (#14). See Known Issues and "Future Tasks" for the ordered
> fix list. Everything since 2026-09-25 is uncommitted until the maintainer commits it.
>
> **Verified 2026-09-17** (headless Unity 6000.3.12f1 run on a scratch copy of this branch, plus
> a fresh asset import):
> - EditMode: **326/326 passing** across 68 files (`ValidateMiningCapAlignmentTests` reads
>   `ServerCode/ValidateMining.js`, so the repo root must be present when running).
> - PlayMode: **0/2** — both `PlanetSceneFlowTests` still fail at `SetUp` (Known Issue #7).
>   **Update 2026-09-30:** EditMode **405/405**, PlayMode **2/2** after fixing #7, #10–#12, #15, #17, #18,
>   part of #16, and adding the server-side mining claim budget.
> - Android player scripts compile for release **and** development (12 `SocialUniverse.*`
>   assemblies, 0 errors). This is a script compile, not a full IL2CPP/Gradle build.
> - The Editor compiles the project with no errors.
>
> Since the last update, M6 (drones, minerals, mining depth) landed on this branch: mineral
> inventory + sale, a 5-drone fleet with upgrade curves, tier-gated asteroids, the Drone Garage
> and Mineral Inventory UI, and 7 Cloud Code functions (written, **not deployed**). The M0–M5
> feature work summarized in "Post-M5 Features Merged to `main`" is unchanged.
> Pending work remains mostly **server fixes, Cloud Code deploy, UGS/Firebase dashboard config,
> and on-device verification** rather than new client code.
> Engine: Unity 6 (URP 17.3.0) · Branch: `main`

---

## Legend


| Symbol | Meaning                    |
| ------ | -------------------------- |
| ✅      | Done & verified            |
| ⚠️     | Done but has a known issue |
| 🔲     | Not started                |
| 🚧     | In progress                |
| 🚨     | Blocker — must be fixed before a build goes to testers |


---

## Open Decisions


| Decision                    | Status           | Notes                                                                                          |
| --------------------------- | ---------------- | ---------------------------------------------------------------------------------------------- |
| DI Framework                | ✅ **VContainer** | `ProjectLifetimeScope` + `PlanetSceneScope` in place                                           |
| Hexasphere Grid System      | ✅ **Installed**  | `Assets/Plugins/Hexasphere/`, assembly defs created                                            |
| DOTween / DOTweenPro        | ✅ **Installed**  | `Assets/Plugins/Demigiant/`                                                                    |
| Lean Touch                  | ✅ **Installed**  | `Assets/Plugins/CW/LeanTouch/` — replaces legacy `Input` for camera orbit/zoom (touch + mouse) |
| Backend (UGS vs Nakama)     | ✅ **UGS**        | Unity Gaming Services — Auth, Economy, Cloud Save, Cloud Code. Packages added to manifest.json |
| Sky Discovery (AR vs gyro)  | ✅ **Gyroscope** | Input System `AttitudeSensor` + mouse/touch-drag fallback; no AR Foundation dependency. See M5 "Sky Discovery Notes" |
| Age policy / content rating | 🔲 **Open**      | `SocialConfig` ships a provisional teen-safe default (`ChatFilterLevel.Strict` for all players) so M4 isn't blocked; revisit once decided — see "M4 — Chat & Moderation Notes" |
| Land resale model           | 🔲 **Open**      | Coins-only confirmed; confirm no real-money cash-out before M8                                 |


---

## Known Issues


| #   | Severity | Description                                                                                                                                                                             | Fix                                                                                                                                                                           |
| --- | -------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 1   | ✅ Fixed  | `PlanetCameraController` and the Hexasphere plugin use legacy `UnityEngine.Input`, but the project had the new Input System active → `InvalidOperationException` in Play Mode           | **Active Input Handling** set to **Both** in Player Settings                                                                                                                  |
| 2   | ✅ Fixed (2026-09-29) | `Planet_TerraPrime.asset` and the old `Asteroid_Iron.asset` sit in `Assets/_Project/ScriptableObjects/` root (superseded by the organized assets in `Asteroids/` and `Planets/`). Previously recorded as fixed, but both files are **still present** (verified 2026-09-25); the root `Asteroid_Iron.asset` has no mineral reference, so it would break tier gating if anything picked it up | Both assets deleted after confirming no GUID references outside each other |
| 3   | ✅ Fixed  | `FixInputSettings.cs` editor script was created but menu item execution was cancelled — input setting had not been applied                                                              | Applied manually; `FixInputSettings.cs` removed                                                                                                                               |
| 4   | ✅ Fixed  | `PlanetCameraController` depended on legacy `UnityEngine.Input` (right-mouse orbit, scroll-wheel zoom), which doesn't translate to mobile touch                                         | Rewritten against **Lean Touch** (`Lean.Touch.LeanGesture`/`LeanTouch.Fingers`): one-finger drag orbits, two-finger pinch zooms — works uniformly across mouse and touch      |
| 5   | ⚠️ Open  | Unity MCP `execute_code` tool fails on every invocation in this environment — even `return 1;` — with `Error running ...mono.exe: The filename or extension is too long`                | Environment/tooling issue (not project code). Blocks live in-editor smoke-testing via injected C#; use manual Play Mode tap-throughs or PlayMode tests instead until resolved |
| 6   | ✅ Fixed  | `ServerCode/PurchaseLand.js` used incorrect UGS SDK call signatures (Economy/Cloud Save client construction, `getItems`/`setItem` shapes, unnecessary `ConfigurationApi`/`configAssignmentHash`) and returned extra `tileId`/`ownerId` fields, causing Cloud Code's strict deserializer to throw `Could not find member 'ownerId' on object of type 'PurchaseLandResponse'` | Rewrote to match `SpendCoins.js`/`CLOUD_CODE_FUNCTIONS.md` conventions; trimmed success response to `{ success, newBalance }` matching `PurchaseLandResponse` |
| 7   | ✅ Fixed (2026-09-29) | `PlanetSceneFlowTests` (PlayMode) now fails both tests at `SetUp` with `PlanetSceneScope.Container not initialized`. Root cause: `Planet.unity`'s `PlanetSceneScope` has `parentReference.TypeName = SocialUniverse.App.RootLifetimeScope` set (production config). VContainer's `LifetimeScope.Awake()` sees `parentReference.Type != null` and calls `EnqueueParent`, queuing the scope to wait for a `RootLifetimeScope` instance before `Configure`/`Build` run. The test loads `Planet.unity` standalone via `LoadSceneMode.Single` (no `Bootstrap.unity`, no `RootLifetimeScope` ever created), so the wait never resolves, `Container` stays `null`, and downstream `[Inject]` fields (`CurrencyView._wallet`, `HUDController._wallet`/`_playerState`/`_mining`) throw NREs | Test-only parent scope: `PlanetTestRootScope` (subclass of `RootLifetimeScope`, so VContainer's parent-type lookup finds it) registers the dev-mode mocks plus a scripted `FakeBackendClient`, and is created `DontDestroyOnLoad` before `Planet.unity` loads. Setup now waits for `SceneReadyEvent`. Test 1 retargeted to mineral grants, test 2 to `TilePurchaseConfirmedEvent` (the modal flow). Also fixed `AudioCatalog.GetSfxClip` throwing on an empty catalog — the same fallback `RootLifetimeScope` uses when none is assigned. PlayMode 2/2 |
| 8   | ✅ Fixed  | `ServerCode/GetFuelState.js`/`SpendFuel.js`/`RefillFuel.js` (M5 fuel functions) had the same SDK-shape bug as the original Known Issue #6 `PurchaseLand.js`: `new DataApi({ headers: { Authorization: ... } })` (constructor doesn't read that field) and options-object `getItems`/`setItem` calls (the SDK wants positional `(projectId, playerId, keys[])`/`(projectId, playerId, { key, value })`). Surfaced live as a 422 `ScriptError` on `SpendFuel` when `TravelService` tried to spend fuel | Rewrote all three to `new DataApi(context)` + positional `getItems`/`setItem` args, matching the convention already used in `PurchaseLand`/`UpdateProfile`/`BlockUser`/etc.; `CLOUD_CODE_FUNCTIONS.md` had the same bug baked into its "fixed" fuel-function listings and was corrected to match |
| 9   | ✅ Fixed  | The same Known Issue #6 SDK-shape bug (`new CurrenciesApi({ headers: { Authorization: ... } })` / `new DataApi(authHeader)` instead of `{ accessToken }` / `context`, plus options-object `getItems`/`setItem`) turned out to still be present in 11 more functions that had never actually been live-tested: `ClaimYield.js`, `PlaceBuild.js`, `ApplyUpkeep.js`, `SellLand.js`, `GrantOfflineIncome.js`, `GrantCoins.js`, `SpendCoins.js`, `GrantStardust.js`, `ValidateMining.js`, `BlockUser.js`, and — critically — `GetBootstrapState.js`, which runs on every app launch. Worse, **`PurchaseLand.js` itself (the original Known Issue #6) still had the broken pattern in the actual deployable file** — only `CLOUD_CODE_FUNCTIONS.md`'s reference copy had ever been corrected; the real fix was never applied to the file that gets deployed. Surfaced live as a 422 `ScriptError` on `ClaimYield` after it was deployed and a Claim was attempted in Play Mode | Rewrote all 12 files (the 11 above plus `PurchaseLand.js`) to the proven-correct pattern (`CurrenciesApi({ accessToken })`, `DataApi(context)`/`PlayerDataApi(context)`, positional `getItems`/`setItem`, and — for `PurchaseLand.js` specifically — the `ConfigurationApi`/`configAssignmentHash` requirement and `getPlayerCurrencies` instead of the nonexistent `getPlayerCurrencyBalance`). Verified via a full grep of every `new CurrenciesApi(`/`new DataApi(`/`new PlayerDataApi(`/`new ConfigurationApi(` call site across all 30 `ServerCode/*.js` files — all now consistent. `CLOUD_CODE_FUNCTIONS.md`'s stale code blocks corrected to match for the 11 that had drifted (`BlockUser.js`'s doc block and `PurchaseLand.js`'s doc block were already correct — only the real files were stale for those two) |
| 10  | ✅ Fixed (source, 2026-09-29 — not yet deployed) | **Drone purchases always fail.** `AcquireDrone.js:9-10` still lists `scout/hauler/prospector`, but the shipped `DroneDefinition` assets are `scout/extractor/excavator/surveyor/titan` (renamed in `3ae3971e`). Every purchase returns `UNKNOWN_DRONE`, so a tester can never leave the tier-1 Scout — which also makes tier-2+ asteroids unreachable | `UNLOCK_COSTS` rebuilt from the five `DroneDefinition` assets; dead `FLEET_KEY`/`DRONE_TIERS` removed. `DroneCatalogAlignmentTests` now parses `AcquireDrone.js` and asserts ids + costs match `DatabaseRegistry.asset` |
| 11  | ✅ Fixed (source, 2026-09-29 — not yet deployed) | **Scout never exists on the server.** `GetBootstrapState.js:29` is the only script that seeds the starter fleet and nothing in the game calls it — the only caller is the Editor-only `CloudCodeTestHarness.cs:69`. The client fabricates Scout locally (`PlanetSceneScope.cs:475-479`), so the server's fleet stays empty and `UpgradeDrone.js:26` / `SetActiveDrone.js:10` answer `NOT_OWNED` for it | `loadFleet` in `AcquireDrone`/`UpgradeDrone`/`SetActiveDrone`/`UnlockDroneSlot` seeds `STARTER_DRONE_ID` into an empty fleet, matching `GetBootstrapState` and the client fallback. `DroneCatalogAlignmentTests` checks all five functions' `STARTER_DRONE_ID` and `START_SLOTS` against the assets |
| 12  | ✅ Fixed (source, 2026-09-29 — not yet deployed) | **`getPlayerCurrencyBalance` does not exist** in `@unity-services/economy-2.5` — `CurrenciesApi` exposes only `getPlayerCurrencies` and `increment`/`decrement`/`setPlayerCurrencyBalance` (checked against the live SDK reference, 2026-09-17). It is still called in `SellLand.js:49` and `ApplyUpkeep.js:42` (main path — every call throws), `ClaimYield.js:70` (the "nothing accrued" path), `GrantOfflineIncome.js:37`, `SpendCoins.js:19`, and `RefillFuel.js:44` (swallowed by a `try/catch`). `PurchaseLand.js:13` and `PlaceBuild.js:7` already document the method as nonexistent | All six call sites now read via `getPlayerCurrencies` + find `COINS` (`currentBalance` helper; inline in `RefillFuel.readBalance`). `ServerCodeEconomyApiTests` fails if any `ServerCode/*.js` calls `.getPlayerCurrencyBalance(` again |
| 13  | ⚠️ Open  | **One client generation per environment.** `ValidateMining.js` on this branch requires `planetId` + `mineralId` + `claimedQty` (`planetId` added by the 2026-09-29 claim budget); `main` and every APK in `Build/` send `claimedCoins`, and M6 builds from before the claim budget send no `planetId`. Whichever version is deployed, mining claims fail for every other build (older M6 builds get `INVALID_PARAMS`) | Deploy the new function and move all testers to a build with the claim-budget client together — client and server must ship as a pair. Stop distributing `Build/*.apk` and the root `social-universe-build-1.0.apk` once it is deployed |
| 14  | 🚨 Blocker | **The release keystore is public.** `zKeystore/user.keystore` has been tracked since `ee9398a4` (2026-07-02) and the GitHub remote `Christian-valari/social-universe` reports `"visibility": "public"` (checked 2026-09-17). No passwords are in the repo, but the key file itself is downloadable by anyone | Treat the key as exposed: request an upload-key reset in Play Console (Play App Signing), then `git rm --cached zKeystore/user.keystore` and add `zKeystore/` + `*.keystore` to `.gitignore`. Consider making the repo private — `ServerCode/` publishes the whole economy surface, including the grant functions in #16 |
| 15  | ✅ Fixed (2026-09-30) | **Cargo and Speed upgrades do nothing.** `DroneRuntime.EffectiveCargoCap`/`EffectiveTravelSpeed` (`:35`, `:37`) are read only by `DroneGarageView`'s comparison panel; no gameplay path and no server function applies them (`ValidateMining.js` never reads upgrades). Coins are spent for no effect. The unlock-slot button is also hidden (`DroneGarageView.cs:59-61`), so the fleet is capped at the starting 2 slots | **Speed** now shortens idle mining: duration × `EconomyConfig.ReferenceDroneSpeed` (5) / the active drone's effective speed, still clamped to the idle min/max (`MiningRewardCalculator`). No server change — `ValidateMining`'s claim budget bounds pace. **Cargo** is no longer offered (`DroneGarageView.OfferedUpgradeStats`; `DroneRowView` hides meters for unoffered stats) because nothing in the mining loop caps a claim by cargo; `UpgradeDrone.js` still accepts it. The **unlock-slot button** is shown again. Tests: `MiningRewardCalculatorTests`, `DroneUpgradeTracksTests` |
| 16  | ⚠️ Partly fixed | **Economy hardening gaps** (all Architecture Rule 1). `GrantCoins.js:6` / `GrantStardust.js:4` accept any client-supplied amount up to 100k/10k and are callable by any signed-in player. `ValidateMining` (before 2026-09-29) trusted the client's `unitsPerSec`. `drone_fleet`, `mineral_inventory`, `current_planet`, `mining_claim_log`, fuel and travel records are written with `DataApi.setItem`, i.e. the player-writable default access class. `SellMinerals.js` read-modify-writes without `writeLock` and `MineralSaleHandler.cs:17-19` has no in-flight guard, so a double-tap can pay twice. `BackendClient.cs:51` retries on `Unknown`, so a timed-out purchase can be charged twice | **Partly fixed 2026-09-29:** `SellMinerals` and `ValidateMining` now write `mineral_inventory` under its `writeLock` (409 → `CONFLICT` / retry), and `SellMinerals` restores the inventory if the coin grant fails; `MineralSaleHandler` drops taps while a sale is in flight; `BackendClient` retries `Unknown` only for read-only `Get*` functions (`BackendRetryPolicy`); `ValidateMining` no longer reads `unitsPerSec` — it checks planet (`current_planet`), mineral-on-planet, drone tier and a claim budget of 6 per planet per rolling 4h (`mining_claim_log`), clamps the grant to the best legit roll, and logs every rejection — see `docs/superpowers/specs/2026-09-29-mining-claim-budget-design.md`. **Still open:** leave `GrantCoins`/`GrantStardust` undeployed; a reinstall resets the client's local respawn timers but not the server budget (rare lost claim); access-class migration — `drone_fleet`, `mineral_inventory`, `current_planet` and `mining_claim_log` (plus fuel/travel records) still use the player-writable class, so a client that writes its own Cloud Save can reset the claim budget, null `current_planet` (any `planetId` is then accepted, i.e. all 10 planets' budgets in parallel) and raise its drone tier (needs a client read migration) |
| 17  | ✅ Fixed (2026-09-29) | **Server rejections are invisible to players.** `DroneGarageHandler.cs:39-40` and `MineralSaleHandler.cs:21` only `SULog.Warn` on failure, and the project has no toast/feedback service, so a rejected purchase, upgrade or sale looks like a dead button. With #10–#12 unfixed this is how testers will experience every M6 action | `DroneGarageHandler` / `MineralSaleHandler` publish `ServerActionFailedEvent` with text from `ServerFailureMessages`; `ToastView` (self-built overlay canvas, created by `HUDController.Start`) shows it above every panel. Covered by `ServerFailureFeedbackTests`. Not yet seen on a device |
| 18  | ✅ Fixed (2026-09-30) | **Cloud Code response contracts.** The UGS Cloud Code SDK deserializes responses with `MissingMemberHandling.Error` (`JsonObject.GetAs<T>`), so any field a function returns that its C# result type lacks makes the call throw after the server has already committed. An audit of all 31 called functions (34 call sites) found: every `RefillFuel` failure (`already_full`, `insufficient_funds`, `write_failed`) returned `reason`, which `FuelStateResult` lacked → threw; and `StartTravel`'s `already_traveling` reply was applied as trip state, wiping the client's in-progress trip and resume hint. Uncalled server functions: `GetBootstrapState`, `GrantOfflineIncome` (test harness only), `ModerateMessage` (none) | Added `FuelStateResult.Reason`; `TravelTripSystem.StartTravelAsync` applies only fuel on failure and resyncs via `GetTravelState` on `already_traveling`. `CloudCodeResponseContractTests` and `MiningGrantResultContractTests` deserialize each at-risk response shape with the SDK's settings. **Rule for new/changed functions:** every returned field must exist on the C# type, and int fields must be sent as integers (`1.0` into an int also throws) |
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
| `DatabaseRegistry.asset`            | ✅      | Populated: 10 planets · 6 asteroids · 5 drones · 6 minerals · 3 upgrades · 5 items · 25 avatars |
| `EconomyConfig.asset`               | ✅      |                                                            |
| `Drone_Scout.asset`                 | ✅      | Tier 1, unlock cost 0 — the starter drone                  |
| `Drone_Extractor.asset`             | ✅      | Tier 2, 600 coins                                          |
| `Drone_Excavator.asset`             | ✅      | Tier 4, 3000 coins — no tier-4 asteroid exists yet         |
| `Drone_Surveyor.asset`              | ✅      | Tier 4, 6000 coins — no tier-4 asteroid exists yet         |
| `Drone_Titan.asset`                 | ✅      | Tier 5, 15000 coins — no tier-5 asteroid exists yet        |
| `Minerals/*.asset` (6)              | ✅      | Iron, Carbon, Silicon, Nickel, Platinum, Iridium — one per asteroid |
| `Upgrades/*.asset` (3)              | ✅      | Cargo, Yield, Speed — only Yield affects gameplay (Known Issue #15) |
| `Items/*.asset` (5)                 | ✅      | Buildables for `BuildPaletteService`                       |
| `LandBuildingThemes/*.asset` (10)   | ✅      | One per planet, referenced from each `PlanetDefinition`     |
| `AudioCatalog.asset` / `AudioConfig.asset` / `TravelTimeTable.asset` | ✅ | Post-M5 feature assets                    |
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


`SocialConfig.asset` (M4) now lives in this folder — moved from the `Assets/` root on 2026-09-29
with its `.meta`, so the GUID and the `Bootstrap.unity` / `Planet.unity` references are unchanged.

### Scenes


| Scene               | Status | Notes                                      |
| ------------------- | ------ | ------------------------------------------ |
| `Bootstrap.unity`      | ✅      | DontDestroyOnLoad container, boots to Auth; `RootLifetimeScope` has `_devMode` flag for UGS-free testing |
| `Auth.unity`           | ✅      | Login + Register panels; no success modal — sign-in immediately publishes `PlayerReadyEvent` and transitions to Hub |
| `SolarSystem.unity`    | ✅      | Shell — star map placeholder               |
| `Planet.unity`         | ✅      | **Fully wired for Earth** — see M1 detail  |
| `Station.unity`        | ✅      | Shell — guild hub placeholder              |
| `LoadingScreen.unity`  | ✅      | Additive overlay loaded by `PlanetState`; self-unloads on `PlanetSceneReadyEvent`. In Build Settings |
| `Travel.unity` / `TravelLoading.unity` | ✅ | M5 travel transition + loading overlay     |
| `ActiveMining.unity`   | ✅      | Active-mining minigame scene (post-M5 redesign) |
| `LandBuilding.unity`   | ✅      | Hexatile build scene with per-planet themes |
| `SampleScene.unity`    | ⚠️     | Unused URP template leftover — not in Build Settings; safe to delete |


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
| `MiningController`            | `Mining/` | Orchestrate a mining session (idle + active); gate sessions so they don't fight over drone  | ✅      | Wall-clock idle persistence via `PlayerPrefs` (survives app restart); exposes `CurrentIdleSession`, `BeginIdleMining`, single-tap `ClaimIdleSessionAsync`; on claim calls `ScheduleRespawn`                                         |
| `IdleMiningSession`           | `Mining/` | State machine: `Traveling → Mining → ReadyToClaim → Complete`, driven by wall-clock `DateTime.UtcNow` deltas rather than per-frame ticks so progress survives app close/background | ✅      |                                                                                                                                                        |
| `IdleMiningSessionController` | `Mining/` | Drive the session: send drone, spawn mining VFX, tick timer, single tap on the ready asteroid claims via `MiningController.ClaimIdleSessionAsync`          | ✅      |                                                                                                                                                        |
| `MiningRewardCalculator`      | `Mining/` | Shared idle/active/payout formula (yield, duration, coins/sec); replaces the removed `IdleMiningCalculator` and is used by both idle and active mining payout paths | ✅      | `IdleMiningCalculator` removed                                                                                                                        |
| `ActiveMiningMinigame`        | `Mining/` | Tap-timing minigame: one "live" target point must be tapped within `TapWindowSeconds` or it counts as a miss; `TapsRequired` hits succeeds, `MaxErrors` misses fails the asteroid | ✅      | Was stubbed; now a real minigame driven by `ActiveMiningSession` + `ActiveMiningSessionController`, presented via the `ActiveMiningMinigameView` overlay on the Planet scene Canvas |
| `ActiveMiningSessionController` | `Mining/` | Drives active-mining tap-window timeouts each frame                                        | ✅      | New in the idle/active rework                                                                                                                          |


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
| `ValidateMining`         | `ServerCode/` | *Superseded:* now grants minerals with planet/mineral/tier checks and a per-planet claim budget — see the M6 Server table | ✅  | Coin cap removed; see Known Issue #16 for what is still open |
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
| `HUDController` (M4 addition)           | `UI/`         | Injects `IPresenceService`; `_explorersText` shows "{Players.Count} explorers here", refreshed on `PresenceChanged` | ✅ |
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
- **Vivox login-race hardening (post-presence follow-up).** Production smoke-testing surfaced
  `LoginSession: must be logged out` crashes from three overlapping races between our managed
  login and Vivox's own internal auto-login/auto-init:
  1. A second concurrent `ConnectAsync()` (e.g. `PlayerReadyEvent` re-firing across a domain
     reload) issued a second `VivoxService.LoginAsync` while the first was still in flight.
     Fixed by caching the in-flight connect `Task` so concurrent callers await the same login
     instead of starting a new one; a failed attempt still allows a fresh retry next call.
  2. Channel/message calls (`JoinChannelAsync`, `SendMessageAsync`, DMs, block/unblock) could run
     while a login was in flight and Vivox's own auto-login collided with ours. Fixed by having
     each of these wait on any in-flight `ConnectAsync` before touching Vivox.
  3. Callers that can run **before `SocialServicesInitializer` ever calls `ConnectAsync`**
     (e.g. `SocialDebugPanel.Open()`) hit Vivox directly on an uninitialized client, which
     auto-inits/logs in internally — colliding with our explicit login moments later. Fixed by
     having every Vivox-touching `ChatService` method call a new `EnsureConnectedAsync()` first,
     which starts the same managed `ConnectAsync` (falling back to the last known display name)
     if nothing has connected yet. Also skips the explicit `LoginAsync` when
     `VivoxService.IsLoggedIn` is already `true`, covering a stale native session that survives a
     domain reload across Play Mode sessions even though our own `_initialized`/`_connectTask`
     reset.

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
- [x] Move `Assets/SocialConfig.asset` into `Assets/_Project/ScriptableObjects/` per project
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

- [ ] Two devices/editors on same planet — HUD "N explorers here" count updates for both
      clients as each joins/leaves (no player marker/movement — removed in
      `refactor/vivox-only-social`, see Presence Notes)
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

## M5 — Travel & Solar System 🚧 CODE COMPLETE — AWAITING DEPLOY/SETUP

**Exit criteria:** Star map travel, fuel as a recharging gauge, gyro Sky Discovery with star map fallback.

**Sky Discovery decision: resolved — gyroscope starfield** (Input System `AttitudeSensor`, with
mouse/touch-drag fallback when no gyro is present), per the architecture doc's own
recommendation. No AR Foundation dependency added. See "Sky Discovery Notes" below.

All Travel scripts are code-complete, wired into a new `SolarSystemScope` (parented to
`RootLifetimeScope`, same standalone/production split as `PlanetSceneScope`), and the
`SolarSystem.unity` scene has been built out with a working StarMap UI, SkyDiscovery UI, and
rocket departure overlay via Unity MCP. Covered by EditMode tests (97/97 passing, up from
83/83 in M4). What remains is Cloud Code deployment and manual/PlayMode verification — see
"Setup Required" and the "M5 Completion Checklist" below.


| Script                   | Path      | Responsibility                                                        | Status |
| ------------------------ | --------- | --------------------------------------------------------------------- | ------ |
| `SolarSystemController`  | `Travel/` | Owns the SolarSystem (Hub) scene shell; toggles StarMap ↔ SkyDiscovery panels, idle orbit-pivot rotation | ✅ |
| `StarMapController`      | `Travel/` | Lists planets from `DatabaseRegistry` (ordered by `PlanetDefinition.OrbitOrder`); travel-cost/affordability info panel, fuel refill button; publishes `TravelRequestedEvent` | ✅ |
| `TravelService`          | `Travel/` | Computes fuel cost (`PlanetDefinition.TravelFuelCost`, free for the home planet), validates+spends fuel via `FuelSystem` | ✅ |
| `FuelSystem`              | `Travel/` | Server-backed fuel: `RefreshAsync`/`TrySpendAsync`/`RefillAsync` against `GetFuelState`/`SpendFuel`/`RefillFuel`; keeps `PlayerState.Fuel`/`MaxFuel` in sync | ✅ |
| `RocketController`       | `Travel/` | Plays a brief departure overlay (status text + icon slide) before the FSM scene transition | ✅ (dodge minigame deferred — see Notes) |
| `SkyDiscoveryController` | `Travel/` | Gyro/drag-driven "look at a body" lock-on (`SkyLockOnMath`), same travel-cost/confirm flow as StarMap | ✅ |
| `GyroInputProvider`      | `Travel/` | Wraps Input System `AttitudeSensor`; falls back to mouse/touch drag when unavailable | ✅ |
| `SkyLockOnMath`          | `Travel/` | Pure math (Fibonacci-sphere body layout, closest-direction lookup) — extracted for unit testing | ✅ |
| `SolarSystemScope`       | `App/`    | Scene scope for `SolarSystem.unity` — fresh `Wallet`/`PlayerState`/`FuelSystem`/`TravelService` per scene, same pattern as `PlanetSceneScope` | ✅ |
| `TravelController`       | `App/`    | Subscribes to `TravelRequestedEvent` → `TravelService.TravelToPlanetAsync` → `RocketController.PlayDepartureAsync` → `HubState.TravelToPlanet` | ✅ |
| `GetFuelState`/`SpendFuel`/`RefillFuel` | `ServerCode/` | Player-scoped Cloud Save `fuel_state` record; server-side recharge-since-last-update math | ✅ |


### Travel Fuel Notes

- **`EconomyConfig`** gained a `[Header("Travel — Fuel")]` block: `MaxFuel` (100),
  `FuelRechargePerHour` (10), `FuelRefillCost` (50 coins) — same "must match the ServerCode
  constants" duplication pattern as the M3 yield/upkeep formulas.
- **`PlanetDefinition`** gained `TravelFuelCost` (per-planet fuel cost) and `OrbitOrder`
  (star-map display order). All 10 planet assets were given differentiated values — Earth is
  free (home), Mercury/Venus/Moon are cheap, Pluto costs nearly a full tank (100) — so the fuel
  gauge is actually meaningful rather than a flat per-trip cost.
- **Free trip home** is `TravelService.GetFuelCost` returning `0` when
  `target.PlanetId == Constants.PlanetIds.Earth`, regardless of configured cost — `TravelToPlanetAsync`
  never calls the backend at all for a home trip (no `SpendFuel` round-trip needed).
- **Server is authoritative for fuel**, same trust model as coins: `fuel_state` is a
  player-scoped Cloud Save record (`{ fuel, maxFuel, lastUpdateTs }`); `GetFuelState`/`SpendFuel`
  both recompute the recharge-since-`lastUpdateTs` amount server-side before reading/spending, so
  a client can never claim more fuel than has actually elapsed. `RefillFuel` validates the coin
  balance server-side before topping the tank.
- **Manual refill** is wired end-to-end: `StarMapController` shows a "Refill Fuel" button
  (disabled when already full or unaffordable) that calls `FuelSystem.RefillAsync()`.
- **`SolarSystemScope`** registers its own `Wallet`/`PlayerState`/`IEconomyService`/`FuelSystem`
  — a fresh instance per Hub-scene visit, rehydrated from the server by `SolarSystemBootstrapper`
  on scene start, exactly mirroring how `PlanetSceneScope` rehydrates its own `Wallet`/`PlayerState`
  rather than sharing one persistent instance across scenes.

### Star Map / Scene Transition Notes

- **Fixed a pre-existing gap**: `Core.PlanetState.TargetPlanetId` was already set by
  `HubState.TravelToPlanet`/`BootState`/`AuthState`, but `PlanetSceneScope` ignored it and always
  loaded the Inspector-fixed `_startPlanet` (Earth) — so traveling to e.g. Mars would still load
  Earth. `PlanetSceneScope.Configure` now resolves
  `Parent.Container.Resolve<PlanetState>().TargetPlanetId` (production) and looks it up via
  `DatabaseRegistry.GetPlanet`, falling back to `_startPlanet`/the registry's first planet only in
  standalone mode (no parent) or if the ID doesn't resolve. This is the actual fix that makes the
  star map "switch scene + shard" requirement do something — without it, M5 travel would always
  cosmetically "succeed" but every trip would land back on Earth.
- **No `EventBus`-free direct calls from UI to fuel/scenes.** `StarMapController`/
  `SkyDiscoveryController` only ever publish `TravelRequestedEvent` — `TravelController` (App
  layer) owns the entire spend → animate → transition sequence, same "UI publishes intent, App
  layer owns side effects" pattern as M3's `TilePurchaseHandler`/`BuildModeController`.
- **`RocketController`'s dodge minigame is deferred/stubbed** — same treatment as
  `ActiveMiningMinigame` in M1. `PlayDepartureAsync` plays a ~1.5s overlay (status text + a
  sliding icon) and that's the entire "travel game" for now; a future minigame would slot in
  between `TravelService` success and the FSM transition without changing either.

### Sky Discovery Notes

- **Decision resolved: gyroscope starfield**, not camera AR. Reasons: no AR Foundation
  dependency, works the same in-editor (mouse drag) and on-device (tilt), and the architecture
  doc itself flagged gyro as "simpler, recommended."
- **`GyroInputProvider`** checks `AttitudeSensor.current` each frame; if present, reads device
  attitude directly. If absent (desktop, simulator, or a device without one), it degrades to a
  mouse-drag yaw/pitch accumulator clamped to ±80° pitch — same `Quaternion CurrentAttitude`
  output either way, so `SkyDiscoveryController` doesn't know or care which source is active.
- **`SkyLockOnMath.GetSkyDirection`** lays bodies out on a unit sphere via a Fibonacci/golden-angle
  spiral indexed by `OrbitOrder` — deterministic (same planet always at the same sky position),
  no new per-planet "sky direction" SO field needed. `FindClosest` is a simple linear
  angle-distance search (10 planets — no spatial index needed). Both are pure functions in their
  own file specifically so they're unit-testable without a scene (`SkyLockOnMathTests`).
- **Lock-on UX**: holding the look direction within `_lockOnAngleThresholdDeg` (15°) of a body for
  `_lockOnDwellSeconds` (1.5s) locks onto it and shows the same fuel-cost/confirm panel as the
  StarMap list — pressing "Travel" publishes the same `TravelRequestedEvent`, so `TravelController`
  doesn't need to know which screen requested the trip.

### Planet Spacing & Travel Range Notes (post-M5 follow-up)

- **`PlanetDefinition.OrbitDistanceAU`** (new field, set on all 10 planet assets to
  real-ish AU values — Mercury 0.39 … Pluto 39.5, Moon ≈ Earth's 1.0) drives Sky Discovery's
  placement: each sky body's distance along its `SkyLockOnMath` bearing is
  `sqrt(|body.OrbitDistanceAU - currentPlanet.OrbitDistanceAU|)`, normalized across just the
  bodies visible this session and lerped into `[_minOrbitRadius, _maxOrbitRadius]` (also scales
  the model down toward `_farScaleFactor` at the far end). The sqrt compresses the huge outer-system
  gaps so the inner planets don't collapse into an indistinguishable cluster. The camera no longer
  relocates to the current planet's old fixed-radius bearing (removed — didn't make sense once
  radius varies per body); it now stays at the dome center and distance alone conveys "near vs far."
  This is a relative simulation of spacing, not literal AU-to-unit conversion.
- **`TravelRangeMath.IsInRange`** (new pure class, `Travel/TravelRangeMath.cs`) gates travel to
  only planets whose `OrbitOrder` is at most one step from the current planet's (ties — Earth and
  Moon share `OrbitOrder = 3` — count as in range of each other). `TravelService.IsInRange` wraps
  it with the injected current planet and exempts the home planet entirely, same unconditional
  exemption `GetFuelCost` already gives it. `TravelToPlanetAsync` now checks range before spending
  fuel, returning `FailureReason = "OutOfRange"` if it fails.
- **`SkyDiscoveryController`** still lets the player lock onto *any* body (so the wider solar
  system is visible), but `UpdateConfirmPanel` disables the Travel button and shows
  "{name} — out of range, too far to reach directly" for out-of-range locks. A new persistent
  `_rangeInfoText` ("Only neighboring planets are within travel range — hop from world to world to
  reach distant ones.") explains the rule up front, wired in the scene under `SkyDiscoveryPanel`.
- New `TravelService` constructor parameter `PlanetDefinition currentPlanet` — already available
  via the same `RegisterInstance(currentPlanet)` `SolarSystemScope` uses for
  `SkyDiscoveryController`'s injected current planet, no scope changes needed.

**Setup Required:**

- [ ] Deploy `GetFuelState`, `SpendFuel`, `RefillFuel` to Cloud Code.
- [ ] Verify the `@unity-services/cloud-save-1.4` player-scoped `getItems`/`setItem` calls in the
      new fuel functions against the dashboard's bundled SDK types — written from the same
      pattern as `GrantOfflineIncome.js`/`SpendCoins.js`, not yet confirmed against an actual
      deploy. If the shape differs, fix and add a "Known Issue" entry (same as #6).
- [ ] `SolarSystemScope` Inspector (`SolarSystem.unity`): confirm `parentReference` resolves to
      `RootLifetimeScope` in a full Bootstrap → Auth → Hub run (set via MCP scene editing, not yet
      verified by an actual Play Mode session — blocked on Known Issue #7 same as M3/M4).
- [ ] Known Issue #7 (`PlanetSceneScope.Container not initialized` in `PlanetSceneFlowTests`)
      still blocks PlayMode verification for M5 same as M3/M4 — unrelated to this milestone.


### M5 Completion Checklist

**Automated Tests**

- [x] EditMode: `FuelSystem` — `RefreshAsync` applies fuel/maxFuel; `TrySpendAsync` deducts on
      success and resyncs on failure; zero/negative spends short-circuit without calling the
      backend; `RefillAsync` updates fuel + wallet on success, leaves wallet unchanged on failure
      (`FuelSystemTests`)
- [x] EditMode: `TravelService` — home planet is always free regardless of configured cost; other
      planets return their configured cost; travel succeeds/fails based on `FuelSystem.TrySpendAsync`;
      home travel never calls the backend (`TravelServiceTests`)
- [x] EditMode: `SkyLockOnMath` — sky directions are unit-length; `FindClosest` returns the
      nearest direction and angle, `-1` for an empty set (`SkyLockOnMathTests`)
- [ ] PlayMode: Travel from SolarSystem to Planet — `Planet.unity` loads additively, correct
      `PlanetDefinition` bound (blocked on Known Issue #7)

**Manual Play Mode Verification**

- [ ] `SolarSystem.unity` StarMap lists all planets in `OrbitOrder`
- [ ] Tap a planet on star map — travel info panel shows fuel cost (or "Free trip home" for Earth)
- [ ] Confirm travel with sufficient fuel — rocket departure overlay plays, correct Planet loads
- [ ] Attempting to travel without enough fuel disables the Travel button
- [ ] Fuel gauge updates after travel; recharges over time; "Refill Fuel" tops the tank for coins
- [ ] Sky Discovery: tilt device (or drag with mouse) — reticle text updates as look direction
      changes; holding on a body locks on and shows the same travel panel as the list

**Architecture Rules**

- [x] `SocialUniverse.Travel.asmdef` created with correct namespace
- [x] Fuel state is server-backed (`fuel_state` Cloud Save record via `GetFuelState`/`SpendFuel`/
      `RefillFuel`) — client cannot grant free fuel; `PlayerState.Fuel`/`MaxFuel` are a view cache
      only, same relationship as `Wallet` to `IEconomyService`
- [x] `GyroInputProvider` gracefully falls back to mouse/touch drag on desktop/simulator builds
- [x] `SkyDiscoveryController` only depends on `GyroInputProvider.CurrentAttitude` — no direct
      `AttitudeSensor`/`Input.gyro` calls outside `GyroInputProvider`
- [x] UI (`StarMapController`/`SkyDiscoveryController`) never spends fuel or transitions scenes
      directly — both only publish `TravelRequestedEvent`; `TravelController` (App layer) owns
      the spend/animate/transition sequence

---

## Post-M5 Features Merged to `main` (2026-07 → 2026-08)

Work that landed on `main` after the M5 update. None of it is a new milestone — it deepens
existing milestones (mostly M3 build depth, M4 social, and M11 UI/auth polish) and was built
feature-by-feature via the SDD spec/plan workflow (`docs/superpowers/specs/` + `plans/`). All of
it is **code-complete and merged to `main` with EditMode tests green**; what remains for most is
UGS/Firebase dashboard config, Cloud Code deploy, and on-device verification (see "Future Tasks").

| Feature | Landed | Maps to | Status | Key scripts / assets |
|---|---|---|---|---|
| **ActiveMining scene redesign** | 2026-07-04→06 | M1/M6 | ✅ code-complete | Dedicated `ActiveMining.unity` (true scene swap, not overlay); `ActiveMiningState` FSM, `ActiveMiningHandoff`, `ActiveMiningSessionRunner`, `ActiveMiningAsteroidStage`, Cinemachine close-up framing, pre/post-game panels, tap VFX |
| **Avatar selection** | 2026-07-06→07 | M4/M11 | ✅ code-complete | `AvatarDefinition` SO + 25-avatar catalog on `DatabaseRegistry`; `AvatarSelectionModal` grid picker; `PlayerState.AvatarId`; `AvatarAssignment.ResolveAvatarId` (random on first hydrate); `avatarId` threaded through `UpdateProfile`/`GetPlayerProfile`/`PlayerProfile`/`ProfileService.UpdateAvatarAsync`; HUD avatar + picker |
| **Chat avatars + display names** | 2026-07-09→14 | M4 | ✅ code-complete | `AvatarId` through `ChatMessage`/`IChatService`; sender avatar rendered in chat rows; `ChatDisplayNameResolver`; fixed IDs leaking as names and the "Player" display-name bug |
| **TileInfo yield/claim rows** | 2026-07-09 | M3 | ✅ code-complete | `TileInfoPanel` grown to host yield + claim rows without shifting siblings |
| **Settings panel + AudioManager** | 2026-07-10→13 | M10/M11 | ✅ code-complete | New `SocialUniverse.Safety` assembly; `AudioSettingsService` (music/SFX volume); `SettingsPanel` (logout + volume) on HUD gear; `AudioManager` + `AudioCatalog`; `PlanetDefinition.BgmClip`; per-scene BGM + mining/modal/coins SFX |
| **Cross-device planet resume** | 2026-07-14 | M2/M5 | ✅ code-complete | Current planet persisted server-side; `PlanetResumeResolver` picks the resume planet on login |
| **Auth overhaul** | 2026-07-16→29 | M2 | ✅ code-complete | Email verify + two-panel forgot-password + `CheckEmailAvailable` + `DeleteAccountAsync` → Google Sign-In (choose-name panel, `DisplayNameValidator`, Play Games Services v2) → **Firebase Auth via UGS OIDC** (`FirebaseAuthHandler`, `FirebaseAuthConfig` SO, FirebaseApp init in `NetworkBootstrap`; retired the email Cloud Code + Play Games plugin). Firebase SDK + `google-services.json` committed |
| **First-time profile onboarding** | 2026-07-30 | M4/M11 | ✅ code-complete | `ProfileOnboarding.NeedsOnboarding`; `ShowProfileOnboardingEvent`; non-dismissable name+avatar onboarding modes on `DisplayNameModal`/`AvatarSelectionModal`; HUD triggers it for nameless first-time (Google/SSO) accounts |
| **Land Building Mode** | 2026-08-04→13 | M3 (build depth) | ✅ code-complete + verified | See detail below |

### Land Building Mode (flagship — 4 phases)

A dedicated build scene reachable via "View Land" on `TileInfoModal`, replacing M3's in-planet
`BuildModeController`. Built in four SDD passes:

1. **Slot-model foundation** (2026-08-04) — new `LandBuilding.unity` scene + `LandBuildingSceneScope`
   (parented to `RootLifetimeScope`), `LandBuildingState` FSM state + `LandBuildingHandoff` (carries
   tile context across the scene swap), `LandBuildService` client, `LandBuildMath`, slot-aware
   `PlaceBuild`/`RemoveBuild`/`MoveBuild` Cloud Code functions, `LandBuildingController` +
   `LandBuildPaletteView` (owner edit mode vs. visitor view mode).
2. **Hex-grid redesign** (2026-08-05) — retired the fixed slot model for a **hexatile board**:
   `HexBoardMath` (geometry/neighbors/price), `PlotHexBoard` renderer + `PlotBoardInputController`
   (tap vs. cell-drag routing), `PurchaseHexatile` (adjacency-gated, server-priced unlock), build
   ops re-keyed to hex index, per-hexatile unlocked mask carried through the land registry + handoff.
3. **Palette + juice** (2026-08-07→10) — item categories + category-tab filter, affordability
   gating, price labels, transparent 3D drag-ghost preview, mobile touch, build-level slider;
   `BuildFeedback` coroutine juice (place pop-in, remove poof, slider punch, ghost green/red tint by
   drop validity), place/remove/invalid SFX.
4. **Per-planet themes** (2026-08-12→13) — `LandBuildingThemeDefinition` SO, `LandBuildingThemeResolver`,
   `LandBuildingThemeApplier` (swaps sky texture + ambient), per-planet hex materials via
   `PlanetDefinition.LandBuildingTheme`; 12 theme assets + sky textures under
   `ScriptableObjects/LandBuildingThemes/` and `Plugins/SimpleSky/Textures/`. Earth theme wired in
   the scene; the remaining 11 planets fall back to Earth until each is authored/wired
   (`docs/superpowers/HANDOFF-landbuilding-planet-themes.md` §C — no code needed per planet).

**Tests:** `LandBuildMathTests`, `LandBuildServiceTests`, `HexBoardMathTests`, `BuildFeedbackTests`,
`HexCellVisualTests`, `LandSlotResolverTests`, `PointerGestureTests`, `LandBuildingHandoffTests`,
`LandBuildingThemeResolverTests` — all green.

**Land Building — remaining (Editor/deploy, not code):**

- [ ] Deploy the hex build Cloud Code functions (`PurchaseHexatile`, hex-keyed `PlaceBuild`/`RemoveBuild`/`MoveBuild`) to UGS.
- [ ] Author + wire the 11 non-Earth `LandBuildingTheme` assets (per-planet sky/hex/ambient).
- [ ] Manual visual verification of the theme swap (confirm SimpleSky's dome shader actually picks
      up `Material.mainTexture` — HANDOFF §B.5; otherwise set the shader's real texture property).
- [ ] On-device Play Mode walkthrough (enter LandBuilding → purchase hexatile → place/move/remove → Back).

---

## M6 — Drones & Mining Depth 🚧 CODE COMPLETE (branch) — BLOCKED ON SERVER FIXES + DEPLOY

**Exit criteria:** Drone upgrade tree, slots, asteroid tiers gating exploration.

**Status (2026-09-25):** all client code, UI, SO assets and scene wiring are done on
`feature/m6-drones-mining-depth`, and the EditMode suite (326 cases) passes. The loop is **not
playable against the live backend**: `AcquireDrone` rejects every drone the game ships (Known
Issue #10), Scout is never saved server-side (#11), and no M6 Cloud Code function has been
deployed. Nothing in M6 has been exercised in Play Mode or on a device.

### Client

| Script                                                                       | Path              | Responsibility                                     | Status | Notes                                                                                                          |
| ---------------------------------------------------------------------------- | ----------------- | -------------------------------------------------- | ------ | -------------------------------------------------------------------------------------------------------------- |
| `MineralDefinition` (SO)                                                     | `Config/`         | Mineral id, display name, icon, coin value         | ✅      | 8 assets in `ScriptableObjects/Minerals/` (Palladium T4 and Helium-3 T5 added 2026-09-30)                                                                      |
| `UpgradeDefinition` (SO) + `DroneStat`                                       | `Config/`         | Upgrade curve per stat: cost, step, max level      | ✅      | 3 assets (`Upgrade_Cargo`/`Yield`/`Speed`) — Yield and Speed are offered; Cargo is hidden (Known Issue #15)      |
| `DroneDefinition` (SO)                                                       | `Config/`         | Base stats, tier, unlock cost, model + icon        | ✅      | 5 assets: scout (T1, 0) · extractor (T2, 600) · excavator (T4, 3000) · surveyor (T4, 6000) · titan (T5, 15000) |
| `AsteroidDefinition` (SO)                                                    | `Config/`         | Tier, mineral ref, rarity, value                   | ✅      | 8 assets, tiers 1–5 — Palladium (T4: Neptune, Uranus, Pluto) and Helium-3 (T5: Pluto) added 2026-09-30 with placeholder art                                            |
| `DatabaseRegistry`                                                           | `Config/`         | `_minerals` / `_upgrades` / `_drones` lookups      | ✅      | All M6 assets registered                                                                                       |
| `MineralInventory`                                                           | `Mining/`         | Client cache of mined minerals + change event      | ✅      | Hydrated from Cloud Save in `PlanetSceneScope`                                                                 |
| `IMineralService` + `MineralService` + `LocalMockMineralService`             | `Mining/`         | Grant minerals on claim, sell for coins            | ✅      | Calls `ValidateMining` / `SellMinerals`                                                                        |
| `DroneFleet` + snapshot DTOs                                                 | `Mining/`         | Client fleet cache: slots, owned drones, active    | ✅      | Falls back to a local-only Scout when Cloud Save is empty — the client half of Known Issue #11                  |
| `DroneRuntime` + `DroneUpgradeMath`                                          | `Mining/`         | Effective stats from base + upgrade levels         | ✅      | Pure math, unit-tested                                                                                         |
| `IDroneService` + `DroneService` + `LocalMockDroneService`                    | `Mining/`         | Acquire / upgrade / unlock slot / set active       | ✅      | Server-validated; all four calls currently fail live — Known Issues #10/#11                                    |
| `DroneEvents` + `DroneGarageHandler`                                         | `Mining/`, `App/` | Intent events → service calls                      | ✅      | Failures only log — Known Issue #17                                                                            |
| `SellMineralsRequestedEvent` + `MineralSaleHandler`                          | `Mining/`, `App/` | Sell-all / sell-one intent                         | ✅      | No in-flight guard — Known Issue #16                                                                           |
| `MiningController` + `MiningRewardCalculator`                                | `Mining/`         | Mineral payout + tier gate on the active drone     | ✅      | Publishes `MiningBlockedEvent` → HUD message                                                                   |
| `DroneController` + `IdleMiningSessionController` + `DroneModelView`         | `Mining/`         | Swap the spawned model to the active drone         | ✅      | 4 new drone model prefabs                                                                                      |
| `DroneGarageView` + `DroneRowView` + `DroneComparison`                       | `UI/`             | Garage carousel, upgrade pips, stat comparison     | ✅      | Unlock-slot button hidden — Known Issue #15                                                                    |
| `MineralInventoryView` + `MineralRowView`                                    | `UI/`             | Mineral list + sell actions                        | ✅      | Opened from the Planet HUD                                                                                     |
| `MiningClaimRewardModal` + `IdleClaimCompletedEvent`                         | `UI/`, `Mining/`  | Idle-claim reward popup                            | ⚠️     | Reads `def.Icon` without a null check                                                                          |

### Server (`ServerCode/`) — written, **none confirmed deployed**

| Function                     | Status | Notes                                                                                  |
| ---------------------------- | ------ | -------------------------------------------------------------------------------------- |
| `ValidateMining` (rewritten) | ⚠️     | Server-side planet/mineral/tier checks + per-planet claim budget (2026-09-29). New request shape `{ planetId, mineralId, claimedQty }` — client and server must deploy together (Known Issue #13) |
| `SellMinerals`               | ⚠️     | Read-modify-write without `writeLock` (Known Issue #16)                                |
| `AcquireDrone`               | 🚨     | Drone catalog does not match the shipped assets (Known Issue #10)                      |
| `UpgradeDrone`               | 🚨     | Returns `NOT_OWNED` for Scout (Known Issue #11)                                        |
| `SetActiveDrone`             | 🚨     | Same root cause as above                                                                |
| `UnlockDroneSlot`            | ⚠️     | Logic fine, but the UI entry point is hidden                                            |
| `GetBootstrapState` (ext.)   | 🚨     | Seeds the starter fleet but the game never calls it (Known Issue #11)                  |

### M6 Completion Checklist

**Automated tests** — all passing in the 2026-09-17 headless run

- [x] EditMode: `DroneUpgradeMathTests` — effective stat per level and curve
- [x] EditMode: `DroneRuntimeTests` — effective stats from fleet state
- [x] EditMode: `DroneFleetTests` — slots, active drone, snapshot round-trip
- [x] EditMode: `DroneServiceTests` — acquire/upgrade/unlock/set-active request shapes
- [x] EditMode: `MineralInventoryTests` + `MineralServiceTests` — add, sell, change events
- [x] EditMode: `DroneGarageHandlerTests` — intent events reach the service
- [x] EditMode: `DatabaseRegistryM6Tests` — mineral/upgrade/drone lookups
- [x] EditMode: `DroneComparisonTests` — stat comparison rows
- [x] EditMode: `DroneCatalogAlignmentTests` — `AcquireDrone.js`'s catalog, starter drone and starting slots match the real assets
- [ ] PlayMode: purchase an upgrade → stat reflected in the next mining session (blocked by Known Issue #7)

**Server deploy — fix before deploying**

- [x] Known Issue #10 (drone catalog), #11 (seed Scout server-side), #12 (`getPlayerCurrencyBalance`) — fixed in source 2026-09-29
- [ ] Wrap the 6 M6 functions in the `try/catch { logger.error(...); throw }` pattern the other functions use; prune the dead `FLEET_KEY`/`DRONE_TIERS` constants in `AcquireDrone.js`
- [ ] Deploy `ValidateMining`, `SellMinerals`, `AcquireDrone`, `UnlockDroneSlot`, `UpgradeDrone`, `SetActiveDrone`, `GetBootstrapState`; confirm `mineral_inventory` + `drone_fleet` round-trip
- [ ] Add the M6 drone/mineral functions to `ServerCode/CLOUD_CODE_FUNCTIONS.md` (absent) and refresh its stale `GetBootstrapState` block (`ValidateMining` refreshed 2026-09-29)

**Manual Play Mode / device verification** — none performed

- [ ] Drone Garage lists owned and acquirable drones with correct costs
- [ ] Acquire a drone — coins deducted, card moves to owned, model swaps
- [ ] Upgrade a stat — cost deducted, pips advance, stat applies in the next session
- [ ] Tier gate: a T1 drone is blocked from a T2 asteroid and the HUD says why
- [ ] Mine → minerals appear in the inventory → sell → coins arrive
- [x] Fleet-slot purchases are in scope for testing — unlock-slot button shown (2026-09-30)

**Architecture rules**

- [x] Acquisitions and upgrades route through Cloud Code — no client-side grants
- [x] `DroneDefinition` / `UpgradeDefinition` / `MineralDefinition` SOs hold the tunables
- [x] `MineralInventory` and `DroneFleet` are client caches of server state
- [ ] Constants duplicated in JS (`UNLOCK_COSTS`, `DRONE_TIERS`, starting slots) must be derived from — or tested against — the SO assets; this duplication caused Known Issue #10
- [ ] `drone_fleet` / `mineral_inventory` are written to a player-writable Cloud Save access class (Known Issue #16)

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
| `MiningRewardCalculatorTests.cs` | EditMode | Shared idle/active/payout reward formula (replaces the removed `IdleMiningCalculatorTests.cs`)                                                                                                                                                                                                                          |
| `IdleMiningSessionTests.cs`    | EditMode | Wall-clock idle session state machine (`Traveling → Mining → ReadyToClaim → Complete`)                                                                                                                                                                                                                                    |
| `ActiveMiningSessionTests.cs`  | EditMode | Tap-timing minigame state machine: hit/miss windows, success/fail thresholds                                                                                                                                                                                                                                              |
| `ActiveMiningMinigameTests.cs` | EditMode | `ActiveMiningMinigame` session lifecycle (begin/tap/clear), replacing the old free-tap stub                                                                                                                                                                                                                               |
| `MiningControllerTests.cs`     | EditMode | `MiningController` idle/active orchestration, drone gating, single-tap claim, respawn scheduling                                                                                                                                                                                                                          |
| `AsteroidSpawnerDistributionTests.cs` | EditMode | `AsteroidSpawner` per-planet field size and stable per-asteroid `SlotId` distribution                                                                                                                                                                                                                              |
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
| `FuelSystemTests.cs`           | EditMode | `FuelSystem.RefreshAsync` applies fuel/maxFuel; `TrySpendAsync` deducts on success and resyncs on failure, short-circuits zero/negative spends without calling the backend; `RefillAsync` updates fuel + wallet on success |
| `TravelServiceTests.cs`       | EditMode | `TravelService.GetFuelCost` (home always free); `TravelToPlanetAsync` succeeds/fails on fuel spend, always succeeds for the home planet regardless of distance, and fails with `FailureReason = "OutOfRange"` for a target outside `TravelRangeMath.IsInRange` |
| `TravelRangeMathTests.cs`     | EditMode | `TravelRangeMath.IsInRange` — true for adjacent or tied `OrbitOrder`, false for distant orbits or the same planet |
| `SkyLockOnMathTests.cs`       | EditMode | `SkyLockOnMath.GetSkyDirection` returns unit-length vectors; `FindClosest` returns the nearest direction/angle, `-1` for an empty set |
| `DroneUpgradeMathTests.cs`     | EditMode | `DroneUpgradeMath` effective-stat and cost curves per upgrade level |
| `DroneRuntimeTests.cs`         | EditMode | `DroneRuntime` effective cargo/yield/speed from base stats + upgrade levels |
| `DroneFleetTests.cs`           | EditMode | `DroneFleet` slots, owned drones, active-drone selection, snapshot round-trip |
| `DroneServiceTests.cs`         | EditMode | `DroneService` acquire/upgrade/unlock-slot/set-active payloads and `DroneActionResult` handling against a fake `IBackendClient` |
| `MineralInventoryTests.cs`     | EditMode | `MineralInventory` add/remove and `MineralInventoryChangedEvent` publication |
| `MineralServiceTests.cs`       | EditMode | `MineralService` grant-on-claim and sell flows (`ValidateMining` / `SellMinerals`) |
| `DroneGarageHandlerTests.cs`   | EditMode | Drone intent events resolve to the right `IDroneService` call |
| `DatabaseRegistryM6Tests.cs`   | EditMode | `GetMineral`/`GetUpgrade`/`GetDrone` lookups across the M6 assets |
| `DroneComparisonTests.cs`      | EditMode | `DroneComparison` stat rows — int/float formatting and deltas |
| `MiningCatalogAlignmentTests.cs` | EditMode | Parses `ServerCode/ValidateMining.js` and checks its planet/mineral/drone catalog and constants against the SO assets (replaced `ValidateMiningCapAlignmentTests`, 2026-09-29). **Reads `ServerCode/` from the repo root**, as do `DroneCatalogAlignmentTests` and `ServerCodeEconomyApiTests` |
| `MiningGrantResultContractTests.cs` | EditMode | Deserializes `ValidateMining` responses the way the Cloud Code SDK does (Newtonsoft, case-insensitive, `MissingMemberHandling.Error`) into `MiningGrantResult` |
| `PlanetSceneFlowTests.cs`      | PlayMode | ✅ **2/2 since 2026-09-29** (Known Issue #7 fixed): idle-claim grants the asteroid's mineral via `ValidateMining`; confirming a tile purchase transfers ownership and debits the wallet. Runs under `PlanetTestRootScope` + `FakeBackendClient`. *Previous note:* Both tests **fail at `SetUp`** with `PlanetSceneScope.Container not initialized` — see Known Issue #7 (pre-existing, unrelated to the mining rework). Intended coverage: (1) idle-mining a claimed asteroid reaches `ReadyToClaim`, a single tap claims it via `MiningController.ClaimIdleSessionAsync`, grants `RemainingYield × CoinsPerUnit` coins, and schedules respawn — this test replaced the old cargo-based `CommitCargoAsync` flow test as part of the mining rework, but remains blocked by the same Known Issue #7 its predecessor was; (2) selecting an available tile fires `TileSelectedEvent` → `TilePurchaseHandler` → `LandPurchaseService`, debits the wallet, and transfers the tile to `OwnedByPlayer` |


**EditMode total: 326/326 passing across 68 EditMode files** — measured 2026-09-17 by a headless
`unity test --mode EditMode` run (Unity 6000.3.12f1) against a scratch copy of
`feature/m6-drones-mining-depth`: 323 `[Test]` + 3 `[TestCase]` cases, 0 failures, 0 skipped.
(Up from 97/97 at M5.) The growth is the Post-M5 feature work — Land Building
(`LandBuildMathTests`, `LandBuildServiceTests`, `HexBoardMathTests`, `BuildFeedbackTests`,
`HexCellVisualTests`, `LandSlotResolverTests`, `PointerGestureTests`, `LandBuildingHandoffTests`,
`LandBuildingThemeResolverTests`), avatars (`AvatarAssignmentTests`, `PlayerStateAvatarTests`,
`DatabaseRegistryAvatarTests`), social polish (`ChatDisplayNameResolverTests`,
`DisplayNameValidatorTests`), audio/settings (`AudioManagerTests`, `AudioSettingsServiceTests`),
onboarding (`ProfileOnboardingTests`), the active-mining redesign (`ActiveMiningHandoffTests`,
`ActiveMiningAsteroidStageTests`, `ActiveMiningTargetPointTests`), travel/resume
(`TravelTripSystemTests`, `FuelRechargeEstimatorTests`, `PlanetResumeResolverTests`,
`PlayerStateTravelTests`, `GyroAttitudeMathTests`), yield/mining estimators
(`YieldEstimateCalculatorTests`, `EconomyServiceMiningTests`, `ValidateMiningCapAlignmentTests`),
and the 9 M6 suites above.

**Update 2026-09-30:** EditMode **405/405** (adds `DroneCatalogAlignmentTests`,
`ServerCodeEconomyApiTests`, `ServerFailureFeedbackTests`, `MineralSaleHandlerTests`,
`BackendRetryPolicyTests`, `AsteroidYieldRollTests`, `MiningCatalogAlignmentTests`,
`MiningGrantResultContractTests`, `CloudCodeResponseContractTests`, `DroneUpgradeTracksTests`,
`MineralContentAlignmentTests`; removes `ValidateMiningCapAlignmentTests` and
`EconomyServiceMiningTests`, which covered the deleted coin-based mining grant).

**PlayMode total: 2/2** (2026-09-29) — Known Issue #7 fixed.

**Two things to know before running the suite:** EditMode tests write to the project's editor
`PlayerPrefs` (music/SFX volume, idle-mining session, asteroid respawn timers) and reset them in
`SetUp`/`TearDown`, and the `ServerCode/` drift tests (`MiningCatalogAlignmentTests`,
`DroneCatalogAlignmentTests`, `ServerCodeEconomyApiTests`) need the repo's `ServerCode/` folder
next to `Assets/`. A headless run also can't share a project folder with an open Editor — run it
on a copy, or close the Editor first.

**Missing tests (high priority):**

- [x] EditMode: a JS/SO drift guard — `DroneCatalogAlignmentTests` (added 2026-09-29)
- [ ] EditMode: `LandmarkService` marks exactly 12 tiles as `IsLandmark = true`
- [ ] EditMode: `DatabaseRegistry` planet/asteroid lookups (`GetPlanet`, `GetAsteroid`) — the M6 assets are covered by `DatabaseRegistryM6Tests`, the older ones are not
- [x] Fix `PlanetSceneFlowTests` PlayMode regression (Known Issue #7) — 2/2 since 2026-09-29. Test 1 also still asserts a **coin** payout, but idle claims now grant minerals, so it needs retargeting after #7 is fixed

---

## Future Tasks (what's left, in rough priority order)

The remaining work is mostly **server fixes, Cloud Code deploy, backend/dashboard configuration
and on-device verification** — very little of it is new client code.

**0. Before any build leaves this machine (security)**

- [ ] Rotate the release keystore — public since 2026-07-02 (Known Issue #14). Reset the upload
      key in Play Console, then `git rm --cached zKeystore/user.keystore` and add `zKeystore/`
      and `*.keystore` to `.gitignore`.
- [ ] Decide whether `Christian-valari/social-universe` should stay public — `ServerCode/`
      publishes the whole economy surface, including the grant functions in Known Issue #16.
- [ ] Stop tracking `social-universe-build-1.0_BackUpThisFolder_ButDontShipItWithYourGame/`
      (824 files) and the Burst `DoNotShip` output; `.gitignore` covers `Build/` and `*.apk`,
      but not these.

**1. ~~Fix the three blockers — Known Issues #10, #11, #12~~** ✅ fixed in source 2026-09-29 (EditMode
340/340). They still have to be deployed — see (2).

**2. Backend bring-up — unblocks M2–M6 + Land Building**

- [ ] Pick the environment and make it deliberate: `Assets/AppConfig.asset` selects
      **Production** (`_environment: 2`), while this file has always said to use `Development`
      for testing.
- [ ] Create/link the UGS project (`Project Settings > Services`).
- [ ] UGS Economy dashboard: define and publish `COINS` + `STARDUST`.
- [ ] UGS Dashboard: enable/configure **Vivox** (text chat) and **Friends**.
- [ ] Configure **Firebase Auth** (email/password + Google) and the UGS OIDC provider
      `oidc-firebase`. `Assets/google-services.json` currently has **no Android OAuth client and
      no SHA-1 fingerprint**, so Google sign-in has to be verified on a device; if testers install
      through Play, register Play's app-signing SHA-1 as well.
- [ ] Deploy **all** `ServerCode/*.js` from source to that environment — M2, M3, M4, M5, Land
      Building, and the 7 M6 functions. Redeploy everything rather than trusting what is live:
      `FuelSystem.cs:20-27` records that the deployed `RefillFuel` already differs from the repo
      copy, and no deploy has ever been ticked off in this file.
- [ ] Do **not** deploy `GrantCoins.js` / `GrantStardust.js` — nothing in the game calls them and
      any signed-in player could (Known Issue #16).
- [ ] Re-check the six functions named in Known Issue #12 against the live SDK once fixed.
- [ ] Decide the fate of `ServerCode/ModerateMessage.js` (wire it in server-side or delete it).

**3. Fix Known Issue #7 — unblocks all PlayMode verification** ✅ 2026-09-29

- [x] `PlanetSceneFlowTests` fail at `SetUp` (`PlanetSceneScope.Container not initialized`)
      because the scene's `PlanetSceneScope` has a production `parentReference` but the test loads
      `Planet.unity` standalone. Fix with a test-only bootstrap that creates `RootLifetimeScope`
      first, or via the `parentReference == null` standalone-mock path. Test 1 also still asserts
      a coin payout, but idle claims now grant minerals, so retarget it at the same time.

**4. Ship an M6 test build**

- [x] Merge `feature/m6-drones-mining-depth` into `main` — done (fast-forward; verified 2026-09-29). Once the
      new `ValidateMining` is deployed, every tester must be on an M6 build (Known Issue #13) —
      retire the older APKs in `Build/` and the repo root.
- [x] Bump `AndroidBundleVersionCode` — now **5** (v4 was used by the 2026-07-31 bundle).
- [ ] Build an **`.aab`**, not an `.apk`: Play rejects APKs over 100 MB and the 2026-08-10 APK was
      340 MB. An app bundle's base module may be up to 500 MB, so no Play Asset Delivery split is
      needed yet (`androidSplitApplicationBinary: 0`).
- [x] Add a minimal failure toast (Known Issue #17) so rejected server calls stop looking like
      dead buttons.
- [ ] Decide what testers should see of the dev surfaces: the HUD chat button opens
      `SocialDebugPanel` (a developer/QA panel) and `CloudTestHarness` is active in
      `Bootstrap.unity`. `_devMode` is off, so builds use the real services.

**5. On-device verification pass** — nothing has been recorded as verified on a device for any
milestone. Once (1)–(3) land, run each milestone's "Manual Play Mode Verification" checklist:
auth (email + Google), persistence, two-client presence/chat, travel/fuel, gyro sky discovery,
land building, and the full M6 mine → sell → upgrade → higher-tier loop.

**6. Editor/scene housekeeping**

- [x] `Assets/Scenes/LoadingScreen.unity` exists and is in Build Settings.
- [x] `ItemDefinition` assets authored and wired — `DatabaseRegistry._items` has 5 entries.
- [x] All 10 `LandBuildingTheme` assets authored and referenced from their `PlanetDefinition`s.
- [ ] Move `Assets/SocialConfig.asset` into `Assets/_Project/ScriptableObjects/` and re-point
      `RootLifetimeScope` / `PlanetSceneScope._socialConfig`.
- [x] Delete the stale root `Planet_TerraPrime.asset` and `Asteroid_Iron.asset` (Known Issue #2).
- [ ] Prune stale branches and the 11 `.claude/worktrees/` copies. The unmerged `worktree-*`
      branches were all superseded by later work: pre-login email verification → Firebase-native
      auth (`db028f19` retired the email Cloud Code), the ChooseName panel → `DisplayNameModal` +
      `ProfileOnboarding`, and the planet-collider fix → smaller collider radii (0.45 or less) in
      every planet prefab.
- [ ] `docs/google-signin-setup-checklist.md` still describes the retired Play Games flow and a
      lower-case package id; the real id is `com.ValariSolutions.SocialUniverse` (case matters).

**7. Resolve Open Decisions still outstanding**

- [ ] **Age policy / content rating** — needed to layer per-age-band behavior over `SocialConfig`'s
      provisional `Strict` default (drives M10 `AgeGateService`).
- [ ] **Land resale model** — confirm coins-only / no real-money cash-out before M8.

**8. Next new-code milestone: M7 — Space Stations & Guilds.** M6 is code-complete on `main`;
M7–M11 remain not started. The M6 follow-ups (Known Issue #15, tier-4/5 asteroids, fleet-slot
purchases) were cleared on 2026-09-30. Open content question: `AsteroidSpawner.DistributeFieldSize`
weights types by `1 − Rarity`, but the shipped rarity values look inverted (Iron `0.7` gets fewer
slots than Iridium `0.08`); Palladium/Helium-3 use `0.85`/`0.92` so they come out scarce under the
current code. Decide the intended meaning before tuning field composition.

**Note:** The temporary `PlayModeVerifier.cs` smoke-test script (and its component on the `PlanetSceneScope` GameObject in `Planet.unity`) has been removed — its checks are now covered by `PlanetSceneFlowTests` under `Assets/_Project/Tests/PlayMode/` (assembly `SocialUniverse.PlayModeTests`). As of M4, both tests fail at `SetUp` — see Known Issue #7.