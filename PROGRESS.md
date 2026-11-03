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
>   **Update 2026-10-01:** EditMode **408/408**, PlayMode **2/2** after fixing #7, #10–#12, #15, #17, #18,
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
| 16  | ⚠️ Partly fixed | **Economy hardening gaps** (all Architecture Rule 1). `GrantCoins.js:6` / `GrantStardust.js:4` accept any client-supplied amount up to 100k/10k and are callable by any signed-in player. `ValidateMining` (before 2026-09-29) trusted the client's `unitsPerSec`. `drone_fleet`, `mineral_inventory`, `current_planet`, `mining_claim_log`, fuel and travel records are written with `DataApi.setItem`, i.e. the player-writable default access class. `SellMinerals.js` read-modify-writes without `writeLock` and `MineralSaleHandler.cs:17-19` has no in-flight guard, so a double-tap can pay twice. `BackendClient.cs:51` retries on `Unknown`, so a timed-out purchase can be charged twice | **Partly fixed 2026-09-29:** `SellMinerals` and `ValidateMining` now write `mineral_inventory` under its `writeLock` (409 → `CONFLICT` / retry), and `SellMinerals` restores the inventory if the coin grant fails; `MineralSaleHandler` drops taps while a sale is in flight; `BackendClient` retries `Unknown` only for read-only `Get*` functions (`BackendRetryPolicy`); `ValidateMining` no longer reads `unitsPerSec` — it checks planet (`current_planet`), mineral-on-planet, drone tier and a claim budget of 6 per planet per rolling 4h (`mining_claim_log`), clamps the grant to the best legit roll, and logs every rejection — see `docs/superpowers/specs/2026-09-29-mining-claim-budget-design.md`. **Access classes fixed 2026-10-13 (deployed to `development` 2026-10-13, v2):** `drone_fleet`, `mineral_inventory`, `current_planet`, `mining_claim_log`, `fuel_state`, `travel_state` and `owned_tiles_<planet>` are read and written with `getProtectedItems`/`setProtectedItem` (player-readable, Cloud Code-writable only) in all 16 scripts that touch them; the client reads them through `ICloudSave.LoadProtectedAsync`. A player with no `current_planet` (never landed via `LandTravel`) has it pinned by their first valid `ValidateMining` claim, so they can no longer spend every planet's budget. `ServerCodeAccessClassTests` fails if a protected key goes back through `getItems`/`setItem` or the client loads one with `LoadAsync`. **Deploy note:** records already saved in the default class are not migrated — after the redeploy every `development` account starts with a fresh fleet (Scout), no minerals, a full tank, no trip in progress and an empty owned-tiles list. Ownership itself lives in the planet's `land_registry` and still paints those tiles as owned, but the client's own land list (`LandRegistryService.Hydrate`) won't include tiles bought before the redeploy — reset `development` land rather than carry it over. **Still open:** leave `GrantCoins`/`GrantStardust` undeployed; a reinstall resets the client's local respawn timers but not the server budget (rare lost claim); two first claims on different planets sent at the same instant can both pass the pin once (no create-only write in Cloud Save) |
| 17  | ✅ Fixed (2026-09-29) | **Server rejections are invisible to players.** `DroneGarageHandler.cs:39-40` and `MineralSaleHandler.cs:21` only `SULog.Warn` on failure, and the project has no toast/feedback service, so a rejected purchase, upgrade or sale looks like a dead button. With #10–#12 unfixed this is how testers will experience every M6 action | `DroneGarageHandler` / `MineralSaleHandler` publish `ServerActionFailedEvent` with text from `ServerFailureMessages`; `ToastView` (self-built overlay canvas, created by `HUDController.Start`) shows it above every panel. Covered by `ServerFailureFeedbackTests`. Not yet seen on a device |
| 18  | ✅ Fixed (2026-09-30) | **Cloud Code response contracts.** The UGS Cloud Code SDK deserializes responses with `MissingMemberHandling.Error` (`JsonObject.GetAs<T>`), so any field a function returns that its C# result type lacks makes the call throw after the server has already committed. An audit of all 31 called functions (34 call sites) found: every `RefillFuel` failure (`already_full`, `insufficient_funds`, `write_failed`) returned `reason`, which `FuelStateResult` lacked → threw; and `StartTravel`'s `already_traveling` reply was applied as trip state, wiping the client's in-progress trip and resume hint. Uncalled server functions: `GetBootstrapState`, `GrantOfflineIncome` (test harness only), `ModerateMessage` (none — deleted 2026-09-30) | Added `FuelStateResult.Reason`; `TravelTripSystem.StartTravelAsync` applies only fuel on failure and resyncs via `GetTravelState` on `already_traveling`. `CloudCodeResponseContractTests` and `MiningGrantResultContractTests` deserialize each at-risk response shape with the SDK's settings. **Rule for new/changed functions:** every returned field must exist on the C# type, and int fields must be sent as integers (`1.0` into an int also throws) |
| 19  | ⚠️ Mostly fixed (deployed to `development` 2026-10-13, v2) | **Land purchase/sale minted COINS.** `PurchaseLand.js` charged the client-supplied `price` (any positive integer) and `SellLand.js` paid the client-supplied `refund` (any non-negative integer, no upper bound). Both are deployed to `development`, so a modified client could buy a tile for 1 coin and sell it back for any amount — an unlimited mint. `PurchaseLand` also never checked the planet's `land_registry`, so it overwrote another player's tile. Two concurrent `SellLand` calls on one tile both paid out | Both scripts price land from a `LAND_PRICES` map keyed by `PlanetDefinition.name` lowercased (the `planetId` every land function receives); `SellLand` grants `Math.round(price × RESALE_RATE)`, with `RESALE_RATE` matching `EconomyConfig.LandResaleRate` (resale semantics unchanged — coins-only is still an Open Decision). The client still sends `price`/`refund`, but only so the server can log a mismatch; they are never charged or granted, so the new client works against both the old and the new server. Unknown planets get `UNKNOWN_PLANET`. `PurchaseLand` rejects any tile with a registry entry: `OWNED_BY_OTHER`, or `ALREADY_OWNED` if the entry names the caller (buying it again would charge twice and wipe its `buildLevel`). **Registry locking (same day):** `land_registry` is one Custom Data item per planet holding every tile, and every writer rewrote the whole map without a `writeLock` — so `ClaimYield` (or `RecordVisit`, `PlaceBuild`, …) racing a `SellLand` put the sold tile back, and the owner could sell it again (a repeatable mint); concurrent claims, upkeep runs, placements and unlocks could also each charge or pay twice. All nine writers (`PurchaseLand`, `SellLand`, `ClaimYield`, `RecordVisit`, `ApplyUpkeep`, `PlaceBuild`, `MoveBuild`, `RemoveBuild`, `PurchaseHexatile`) now go through one shared `loadRegistry`/`updateRegistry` helper block: the write carries the lock it read, a 409 re-runs the validation on a fresh read (3 attempts, then `CONFLICT` — `RecordVisit`/`ApplyUpkeep`, whose C# types have no `Reason`, return a no-op result instead), and the registry change is written *before* any coin call, with a rollback if the coin call fails (`PurchaseLand` releases its claim, `SellLand` restores the entry, `ClaimYield` restores accrual, `ApplyUpkeep` undoes advances and now charges once for all tiles, `PlaceBuild` clears the slot, `PurchaseHexatile` relocks). A failed registry read other than 404 now throws instead of reading as `{}`, which could have wiped every tile on the planet. `LandPurchaseService` maps `OWNED_BY_OTHER`/`CONFLICT` to player text. Every response still uses only fields on its C# result type (#18). `LandRegistryLockTests` fails if any registry writer reads or writes it outside the helper or the copies drift; `LandPriceAlignmentTests` parses both maps and `RESALE_RATE` and checks them against `EconomyConfig` and every `PlanetDefinition` asset, including the refund rounding. **Still open:** a planet with no registry yet has no lock, so two first writes on a fresh planet can still overwrite each other once (Cloud Save has no create-only write). `PlaceBuild` still charges the client's `cost` (any positive integer) — same Rule 1 gap, now the cheapest way to raise `buildLevel` and so yield. `ApplyUpkeep` reverts tiles without removing them from `owned_tiles_<planet>`, so `PurchaseLand` answers `ALREADY_OWNED` if that player tries to rebuy one. The `writeLock` on Custom Data items is unverified against the live SDK (see `docs/deploy-runbook.md` §5). **Deployed** 2026-10-13 (all 30 scripts, v2) — not yet smoke-tested (runbook §4 step 7) |
| 20  | ✅ Fixed (deployed to `development` 2026-10-15 — not yet smoke-tested) | **One account could be played on several devices at once.** Nothing tied a UGS session to a device: each sign-in minted its own token, UGS has no server-side session revocation, and every Cloud Code function accepted any signed-in caller — so two devices could mine, buy and sell on the same account concurrently | Newest sign-in wins, enforced in Cloud Code. `ClaimSession` writes the account's session id into the Protected `active_session` item on every fresh sign-in (one unconditional write, so simultaneous sign-ins leave exactly one valid); every other deployed script starts with the shared `requireActiveSession` block and throws `SESSION_REPLACED` for a missing or stale `sessionId` (a failed read throws too); `ReleaseSession` clears it on logout under the record's `writeLock`. Client: `BackendClient` sends `IBackendClient.SessionId` with every call and publishes `SessionReplacedEvent` on that error; `AuthService` claims inside `BridgeToUgsAsync` and raises `OnSignedIn` only after (so no game call runs without a session), claims a fresh session on every sign-in including relaunch (newest launch wins — 2026-10-17), and releases on sign-out; `ActiveSessionController` re-checks every 60 s in game and on focus regain, and a replaced device leaves chat and shows `QuitDialogView` without being signed out (layout: the editable prefab `Assets/_Project/Prefabs/UI/Resources/QuitDialog.prefab`, guarded by `QuitDialogViewTests`; "Your account was signed in on another device.", one Quit button that closes the app); reopening the app signs in again and takes the account back. Tests: `ActiveSessionGuardTests`, `ActiveSessionControllerTests`, `AuthServiceTests`. Design + limits: `docs/superpowers/plans/2026-10-15-single-active-session.md`. **Deploy note:** client and server ship as a pair — once deployed, every older build gets every Cloud Code call rejected. **Still open:** Vivox, Friends and direct Cloud Save writes bypass Cloud Code, so a modified client that ignores the sign-out keeps chat/friends/profile access until it signs out (needs UGS Access Control + server-issued Vivox tokens); not seen live — the `SESSION_REPLACED` match in `CloudCodeException.Message` and `context.correlationId` are unverified |
| 21  | ✅ Fixed (deployed to `development` 2026-10-15 — not yet smoke-tested) | **Player data could diverge or be lost across devices.** (a) Where to resume was decided from a device-local PlayerPrefs flag (`travel_target_planet_id`, not even per player): the server's trip was only read inside the Travel scene and `current_planet` changes only on landing, so a trip to Mars started on device A left device B resuming on Earth (able to mine there), and if the trip had arrived but wasn't landed, `StartTravel` (which blocked only trips still in flight) let B overwrite it — fuel spent, landing lost; `StartTravel` also took its origin from B's stale scene, and `AuthState` ignored the flag entirely. (b) Read-modify-writes without the record's `writeLock` lost updates under overlapping requests: `drone_fleet` (coins charged first, so a paid-for drone, upgrade or slot could vanish, or two upgrades charge twice for one level), `owned_tiles_<planet>` (two purchases dropped a tile; a sale + purchase resurrected the sold one), `travel_state` (two departures, fuel spent twice), `player_profile` and `blocked_users`. (c) Those scripts read a failed Cloud Save read as "no record" — so a transient error could wipe a profile or block list, reset a fleet, or refill the fuel tank | Server is the source of truth; the `writeLock` is each record's revision. Every read-modify-write passes the lock it read, re-applies its change to a fresh read on a 409 (3 attempts, then `CONFLICT`) and throws on a failed read. `drone_fleet`: shared `drone_fleet access` block in `AcquireDrone`/`UpgradeDrone`/`UnlockDroneSlot`/`SetActiveDrone`; coins are charged first and refunded if the change doesn't land (a refund is an increment, which can't fail for lack of coins), and an upgrade/unlock whose price changed meanwhile answers `CONFLICT`. `owned_tiles`: shared `owned_tiles access` block in `PurchaseLand`/`SellLand`. `UpdateProfile`/`BlockUser`: locked field/id change. `StartTravel`: refuses while any trip is unlanded (`already_traveling`, which the client already resyncs on), writes the trip under the lock it read (a concurrent start refunds its fuel under the fuel lock), takes the origin from `current_planet`. `GetCurrentPlanet` also returns `travelTargetId`; `BootState` and `AuthState` route on the server's answer (pending trip → Travel, else the server planet) and rewrite the local hints to mirror it — the hints are read only if that call fails (`PlanetResumeResolver.ResolveTravelTarget`). Client caches were already scene-scoped and rehydrated from the server on every scene load, and the client never writes Cloud Save; with #20 a device that falls behind is signed out and rehydrates on its next sign-in. Tests: `PlayerRecordLockTests`, `PlanetResumeResolverTests`, `CloudCodeResponseContractTests` (GetCurrentPlanet). Design: `docs/superpowers/plans/2026-10-15-cross-device-data-consistency.md`. **Deploy note:** ship with #20 and a client from the same commit; no data migration — an arrived-but-unlanded trip now has to be landed before the next departure (the client routes there). **Still open:** Cloud Save has no create-only write, so a record's very first write carries no lock (two first-ever writes can still race once, same as #19); the idle mining session is now server-tracked (#22); `GetBootstrapState` (uncalled) still seeds `drone_fleet` unlocked |
| 22  | ✅ Fixed (deployed to `development` 2026-10-15 — not yet smoke-tested) | **An idle mining run lived only on the device that started it.** The session was a device-global PlayerPrefs value (`idle_mining_session`, written by `MiningController`), so a run started on device A was invisible on device B, was lost if A never came back, and was silently dropped by A itself on any trip (the restore discarded a session for another planet) | The run is a Protected Cloud Save record `idle_mining_session` = `{ planetId, slotId, mineralId, startTs, durationSec }`, written only by Cloud Code. **`StartIdleMining`** (new) records it under the record's `writeLock` (a run on another planet is replaced; one on the same planet answers `ALREADY_MINING` with that run, which the client takes over; `WRONG_PLANET` against a recorded `current_planet`, which it never pins); **`GetIdleMining`** (new) returns it with `elapsedSeconds` on the server clock; **`ValidateMining`** takes an optional `idleSlotId` and, once the claim has an answer (grant or rejection), ends the run if it still names that planet and slot — a claim that throws leaves the run, so the haul reappears ready on the next load. Both writers share the `idle_mining_session access` helper block. Readiness is not enforced server-side: the claim budget (#16) stays the reward bound. Client: `IIdleMiningService` (`IdleMiningService` / `LocalMockIdleMiningService`); `MiningController.BeginIdleMiningAsync` awaits the server start (an in-flight start also blocks active mining) and anchors progress to the server's elapsed time; `PlanetSceneBootstrapper` fetches the run at every Planet load and `MiningController.Initialize(serverRun)` restores it, using `AsteroidSpawner.EnsureSlot` to bring back a slot this device has waiting to respawn; claims of server-tracked runs send `idleSlotId`, and no start is sent while a claim is in flight; a same-planet run this device can't place (its asteroid type left the planet) is replaced through `StartIdleMining`'s `replaceSlotId` instead of locking idle mining there. **Backward compatible:** new builds never write the PlayerPrefs key; a session an older build left there is restored and claimed locally as before unless the server has a run on this planet, which wins (the key is deleted). Tests: `MiningControllerTests` (rewritten for the async start + 21 new cases), `AsteroidTierPromptTests`, `TaskExtensionsTests`, `IdleMiningServiceTests`, `IdleMiningSessionResultContractTests`, `AsteroidSpawnerEnsureSlotTests`, `PlayerRecordLockTests` (idle helper), `ServerCodeAccessClassTests`, `MineralServiceTests`, `ServerFailureFeedbackTests`, `PlanetSceneFlowTests`. Design + limits: `docs/superpowers/plans/2026-10-15-server-tracked-idle-mining.md`. **Deploy note:** ship `StartIdleMining`, `GetIdleMining` and the new `ValidateMining` with #20/#21 and a client from the same commit (`deploy-list.txt` now has 34 scripts). **Still open:** yields still re-roll per load, so the same run can pay up to ±20 % differently on another device (server clamp unchanged); respawn timers stay device-local; dev mode (`_devMode`, whose mock backend answers null) can no longer start idle runs; the first-ever write of the record carries no lock (same as #19/#21); not seen live. Review minors fixed the same day: the mining prompt shows the drone as busy while a start or a claim is with the server (`MiningController.StartingAsteroid`, `AsteroidTierPrompt` "bringing back your …"); the run is fetched alongside the rest of Planet hydration; fire-and-forget start/claim calls log failures (`Core/TaskExtensions.Forget`); a server-tracked claim worth nothing still reaches `ValidateMining`, so the server ends the run |
| 23  | ✅ Fixed (deployed to `development` 2026-10-18 — not yet smoke-tested, not committed) | **Planet data was partly device-only, and a failed load opened the game with defaults.** (a) Asteroid respawn timers lived only in a device-global PlayerPrefs key (`asteroid_respawns`, shared by every account on the device): a second device showed every asteroid fresh while the server's claim budget rejected the claims, and the next account on a device inherited the last one's empty slots. (b) Every read at scene load was caught and replaced by a default — 0 coins, a Scout-only fleet, no minerals, no owned tiles, every asteroid back — and `CloudSaveService` itself turned any failure into "no record"; `GetLandRegistry` answered `{}` (every tile unowned) on any read failure. (c) The block list (`blocked_users`, Cloud Save) was never read after sign-in, and the app-wide `ReportService` kept the previous account's blocks and mutes after sign-out | **A:** `ValidateMining` takes an optional `slotId` (else `idleSlotId`) and logs each budget entry as `{ ts, slotId, mineralId }` (old bare timestamps still count and prune); new read-only **`GetAsteroidRespawns { planetId }`** returns `{ success, respawns: [{ slotId, mineralId, secondsLeft }] }` on the server clock; `AsteroidSpawner.SpawnForPlanet(planet, respawns)` builds the field from it after the data pull and never touches PlayerPrefs (the old key is deleted); `MiningController` sends the asteroid's slot with idle and active claims, and a failed active-mining minigame forfeits its slot (`ValidateMining` with `forfeit: true`: planet and mineral checked, one of the planet's claims used, nothing granted, `reason: "FORFEITED"`; the drone tier isn't checked and the planet isn't pinned), so the lost asteroid stays empty on every device. **B:** `PlayerDataPull` runs a scene's reads at once and, with the real backend (`PlayerDataSyncPolicy`, `!_devMode`), retries failed ones behind `SyncRetryDialogView` ("Couldn't load your game data — Retry", sorting 5500) until all load — Planet (wallet, fuel, minerals, fleet, profile, owned tiles, land registry, idle run, asteroid respawns, block list), Travel (trip) and SolarSystem (fuel, wallet), and `GetCurrentPlanet` in `BootState`/`AuthState` (so the device's resume hints are only read in dev mode); the dialog brings its own EventSystem when no loaded scene has one; dev mode, standalone scenes and PlayMode tests keep the old per-step fallbacks. `CloudSaveService` throws on anything but not-found; `GetLandRegistry` throws on a non-404 failure. **C:** `BlockListSync` reads `blocked_users` into `ReportService.SetBlockList` on `PlayerReadyEvent` (the Planet pull retries it if that failed); `PlayerLoggedOutEvent` calls `ReportService.Reset`. Tests: `AsteroidSpawnerDistributionTests` (rewritten for server timers), `AsteroidRespawnServiceTests`, `AsteroidRespawnsResultContractTests`, `MiningCatalogAlignmentTests` (GetAsteroidRespawns `RESPAWN_HOURS`), `MineralServiceTests`, `MiningControllerTests`, `MiningGrantResultContractTests` (FORFEITED), `PlanetSceneFlowTests`, `PlayerDataPullTests`, `SyncRetryDialogViewTests`, `ReportServiceTests`, `BlockListSyncTests`, `SocialServicesInitializerTests`; the three server scripts were exercised with Node against a fake Cloud Save (not checked in). Design: `docs/superpowers/plans/2026-10-18-cloud-synced-planet-data.md`. **Deploy note:** server first — a new client against the old server can't load `GetAsteroidRespawns` and would sit on the retry dialog. **Deployed 2026-10-18:** all 35 scripts on `deploy-list.txt` (`GetAsteroidRespawns` v1 created; `ValidateMining` v4, `GetLandRegistry` v3 and `ClaimSession` v2 — its undeployed 2026-10-17 edit — got new versions; the rest were unchanged), verified with `ugs cloud-code scripts get`: live code and params identical to the repo, newest version active. **Behaviour change:** a failed minigame now uses one of the planet's claims (it used to cost nothing on the server). **Still open:** a rejected claim, a grant that threw, or a forfeit the server didn't record now empties the slot only until the next Planet load (it used to be 4 h on that device); Vivox-level blocks are not re-applied from the pulled list (the local filter already drops those messages); not seen live |
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
| `MiningController`            | `Mining/` | Orchestrate a mining session (idle + active); gate sessions so they don't fight over drone  | ✅      | Idle runs are server-tracked (`StartIdleMining` / `GetIdleMining`, Known Issue #22) and survive app restarts and device switches; PlayerPrefs is only read for a legacy session; exposes `CurrentIdleSession`, `BeginIdleMiningAsync`, single-tap `ClaimIdleSessionAsync`; on claim calls `ScheduleRespawn`                                         |
| `IdleMiningSession`           | `Mining/` | State machine: `Traveling → Mining → ReadyToClaim → Complete`, driven by wall-clock `DateTime.UtcNow` deltas rather than per-frame ticks so progress survives app close/background | ✅      |                                                                                                                                                        |
| `IdleMiningSessionController` | `Mining/` | Drive the session: send drone, spawn mining VFX, tick timer, single tap on the ready asteroid claims via `MiningController.ClaimIdleSessionAsync`          | ✅      |                                                                                                                                                        |
| `MiningRewardCalculator`      | `Mining/` | Shared idle/active/payout formula (yield, duration, coins/sec); replaces the removed `IdleMiningCalculator` and is used by both idle and active mining payout paths | ✅      | `IdleMiningCalculator` removed                                                                                                                        |
| `ActiveMiningSession`         | `Mining/` | Closing-ring minigame run: one ring is live at a time, and a tap before it closes banks a share by how early it landed (Perfect 100%, Good 80%, Late 60%); a ring that closes untapped or a tap on empty space is a miss; the tier's miss limit ends the run; `BankedFraction` is the share earned | ✅ (Phase 1) | Per-tier numbers (targets, ring time, grade cut-offs, miss limit) come from `EconomyConfig.ActiveRunSettings(tier)`, five tiers. Replaced the old tap-window `ActiveMiningMinigame` (2026-11). Phase 1 has not been played on a device yet — see "Active Mining Difficulty" below |
| `ActiveMiningSessionRunner`   | `Mining/` | Creates the session when Start is pressed in the ActiveMining scene and ticks it; writes the outcome into `ActiveMiningHandoff` and keeps the run's local marker up to date | ✅ (Phase 1) | Replaced `ActiveMiningSessionController`. In the Editor and Development Builds each finished run also logs one `ActiveMining run: tier=… result=…` line (`ActiveMiningRunLog`) |


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
  client-supplied `price`. **Superseded 2026-10-13 (Known Issue #19):** both amounts are now
  computed server-side from `LAND_PRICES`/`RESALE_RATE`; the client's `price`/`refund` are
  informational only.
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
| `ModerateMessage`                       | `ServerCode/` | Standalone moderation function | 🗑️ **Deleted 2026-09-30** — nothing called it; `UpdateProfile.js` moderates display names itself |
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
  `ServerCode/UpdateProfile.js` — same pattern as
  the M3 yield-formula constants in `ClaimYield.js`. If the word list or limits change, update
  both places.
- **`ModerateMessage.js` was orphaned** and was deleted on 2026-09-30 (nothing called it).
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
      (`ModerateMessage` was deleted 2026-09-30 — nothing to deploy for it).
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
| `MiningController` + `MiningRewardCalculator`                                | `Mining/`         | Mineral payout + tier gate on the active drone     | ✅      | The asteroid tap prompt shows the tier ("Mine Nickel? · Tier 2") and whether the active drone can mine it, disabling Idle/Active when it can't, and pops in/out like the transaction popup (`AsteroidTierPrompt`, 2026-10-02; tests `AsteroidTierPromptTests`, `MiningModePromptSceneWiringTests`, PlayMode `PlanetSceneFlowTests`). While an idle session runs, the drone is busy: the prompt says "Your drone is currently mining Iron" (or "…done — claim your Iron first") and disables both buttons, tapping the asteroid being mined toasts "Your drone is currently mining this asteroid", and `BeginActiveMining` now refuses too — reversing the July rule that active mining could run alongside an idle session (2026-10-05). Publishes `MiningBlockedEvent` → HUD message                                                                   |
| `DroneController` + `IdleMiningSessionController` + `DroneModelView`         | `Mining/`         | Swap the spawned model to the active drone         | ✅      | 4 new drone model prefabs                                                                                      |
| `DroneGarageView` + `DroneRowView` + `DroneComparison` + `DroneBuyButton`    | `UI/`             | Garage carousel, upgrade pips, stat comparison     | ✅      | Bottom buy button follows the centred card (2026-10-02): the drone's own price while a slot is free, "Slot <price>" when the fleet is full, hidden on owned drones. Was a fixed slot-unlock button that read as "every drone costs 500". Locked cards no longer carry their own "Unlock" button (removed from `DroneRow.prefab`), so the bottom button is the one way to buy. The open garage also re-prices on `Wallet.OnCoinsChanged`: `DroneService` applies a reply's fleet before its balance, so after a slot purchase the buttons used to show affordability against the old coins. Tests: `DroneBuyButtonTests`, `DroneGarageSceneWiringTests`, PlayMode `DroneGarageFlowTests`. Drone and slot purchases go through the shared `TransactionModal` popup (2026-10-02; palette: background #22164A, border #583E9F, buttons #7F5AC2, Cancel as a #22164A outline button; opens/closes with a pop & fade, `PopupTransition` + `PopupMotion` — since 2026-10-05 every popup uses it: Claimed! reward, land purchase, tile info, display name/avatar, email verification, Settings + logout confirm, Fuel, land-build purchase/remove; `PopupWindowWiringTests`): confirm → "Buying…" → success screen or the failure reason, in one popup; their failures no longer toast (upgrade/Select failures still do). Tests: `DronePurchasePromptTests`, `TransactionModalPrefabTests`, PlayMode `DroneGarageFlowTests`. **Visual pass (2026-10-23, uncommitted)** with existing art only — Ultimate Clean GUI Pack SciFi shapes/icons (and the same coin as `PlayerTopBar`), the Bootstrap nebula (`Art/UI/Raw/NewBG2.png`), `Art/UI/Raw/Icons/icon/MineIcon.png`: the garage panel has a header (coin balance, close ✕, "DRONE GARAGE", "FLEET n/m") because the full-screen panel covers the HUD; neighbouring cards peek in at the edges (Fade min 0.35, Scale min 0.82) and the arrow buttons are visible chevron discs that hide at either end of the carousel; the buy button reads "BUY / UNLOCK SLOT · coin · price" (`DroneBuyButton.Caption`; `_buyLabel` is now the price alone) and hangs under the card. Each card is a chamfered panel with a state-tinted frame, a tier hexagon ("T1"…"T5", tier-coloured), a state chip (Active / Owned / Locked + lock), a shine and holo pad behind the drone (locked drones are dimmed), and Yield/Speed rows with a pickaxe/bolt icon, tinted chamfer pips and a coin + gold price on the upgrade button (coin hidden at MAX). The price no longer sits in a locked card's title. All card and garage text is TextMeshPro (`DroneRowView._label` and `StatMeter.NameLabel` were legacy `Text`). Tests: `DroneRowPrefabTests` (state chip, tier badge, lock, MAX coin, pip tint), `DroneBuyButtonTests` (caption), PlayMode `DroneGarageFlowTests` (header follows wallet and fleet; arrows hide at the ends); EditMode 605/605, PlayMode 15/15 headless. Checked from headless renders at 1080×2340 and 1080×1920 only — not yet seen on a device, and touch feel (swipe, peeking cards, arrow taps) is untested |
| `MineralInventoryView` + `MineralRowView`                                    | `UI/`             | Mineral list + sell actions                        | ✅      | Opened from the Planet HUD. Sales (a row's Sell, Sell all) go through the shared `TransactionModal` popup (2026-10-02): confirm → "Selling…" → "Sold …" or the failure reason; sale failures no longer toast. Tests: `MineralSalePromptTests`, `MineralInventorySceneWiringTests`, PlayMode `MineralSaleFlowTests`. **Visual pass (2026-10-23, uncommitted)**, matching the Drone Garage's and with existing art only: nebula backdrop, the garage's header (coin balance — `MineralInventoryView` now injects `Wallet` — close ✕, "MINERALS", "N MINERALS HELD"), rows in a vertical `ScrollRect` (the list could not scroll before), and "SELL ALL · coin · total" at the bottom. Each row is a chamfered panel: the rock icon on a dark tile with a tier hexagon, name over "coin N each", "×qty", and "SELL · coin · value"; a mineral the player holds none of dims (`CanvasGroup`) with Sell disabled. `MineralRowView.Bind` now takes name, tier, qty and unit value instead of a composed label; all text is TextMeshPro. Tier colours come from `UI/TierColors` (new), shared with the drone cards — a tier-N drone mines tier-N asteroids. Fixed on the way: the old "N minerals · Total" line sat behind CLOSE on tall phones (its total is now on Sell all), and the row prefab's `Border` was a 40×1920 sliver that drew a purple line down the right screen edge. Tests: `MineralRowPrefabTests` (new), PlayMode `MineralSaleFlowTests.Header_and_sell_all_show_the_hold_and_the_balance`; EditMode 608/608, PlayMode 16/16 headless. Checked from headless renders at 1080×2340 and 1080×1920 only — not yet seen on a device, and list scrolling by touch is untested |
| Confirmation / message popups (shared style)                                 | `UI/`             | One look for every popup                           | ✅      | **Visual pass (2026-10-23, uncommitted)**, matching the Drone Garage and Mineral Inventory, existing art only. Covers `TransactionModal` (prefab), the mining prompt, "Claimed!" reward, land purchase, tile info, display name / avatar, email verification, Settings + logout confirm, Fuel, LandBuilding's purchase/remove `HexBuildPopup`, `QuitDialogView`, `SyncRetryDialogView` and `ToastView`; the Auth screens are out of scope. Each window is a chamfered #22164A panel with a 4px #583E9F border, uppercase title with a short #7F5AC2 rule, the garage's ✕ icon (was a text "X"), filled #7F5AC2 primary / #2B1E61 outlined secondary buttons with uppercase labels, palette sliders and fuel bar (were #C629FF magenta) and input fields (were cyan), a dim behind the popup where its root was an invisible tap-catcher, a shine behind the reward and purchase icons (`TransactionModal._iconGlow`, hidden with a missing icon), gold prices, and Tile info's cyan accent in lilac. Sizes are on-screen: several popups were sized by transform scale (roots at 1.44), and they ranged from 54% (mining prompt, reward) through 56% (Settings, logout, land purchase, Fuel) to 63% (the two dialogs) of the canvas width — each centred window is now brought to ~80% of its own canvas (LandBuilding's is 800 wide, Planet's 1080). Fixed on the way: the email popup's third button carried a stray 0.69 scale, and the pack's input field forced 14 pt text. **Code:** `MiningModePromptView`'s title/tier labels are `TMP_Text` (were legacy `Text`); `SyncRetryDialogView` and `ToastView` load their layouts from `Prefabs/UI/Resources/SyncRetryDialog.prefab` / `Toast.prefab` (were built in code with the legacy font, like `QuitDialog` before them), and the retry dialog pops in with `PopupTransition`. Tests: `PopupStyleTests` (new: no legacy `Text` in any popup; centred windows 60–100% of their canvas on screen), `ToastViewTests` (new), `SyncRetryDialogViewTests` (prefab wiring), PlayMode `DroneGarageFlowTests` (icon glow), `PlanetSceneFlowTests` (toast and mining-prompt labels read as `TMP_Text`); EditMode 627/627, PlayMode 16/16 headless. Checked from headless edit-mode renders only (no runtime data in them) — not yet seen on a device |
| `MiningClaimRewardModal` + `IdleClaimCompletedEvent`                         | `UI/`, `Mining/`  | Idle-claim reward popup                            | ⚠️     | Reads `def.Icon` without a null check                                                                          |

### Server (`ServerCode/`) — written, **none confirmed deployed**

| Function                     | Status | Notes                                                                                  |
| ---------------------------- | ------ | -------------------------------------------------------------------------------------- |
| `ValidateMining` (rewritten) | ⚠️     | Server-side planet/mineral/tier checks + per-planet claim budget (2026-09-29). New request shape `{ planetId, mineralId, claimedQty }` — client and server must deploy together (Known Issue #13) |
| `SellMinerals`               | ⚠️     | Read-modify-write without `writeLock` (Known Issue #16)                                |
| `AcquireDrone`               | 🚨     | Drone catalog does not match the shipped assets (Known Issue #10)                      |
| `UpgradeDrone`               | 🚨     | Returns `NOT_OWNED` for Scout (Known Issue #11)                                        |
| `SetActiveDrone`             | 🚨     | Same root cause as above                                                                |
| `UnlockDroneSlot`            | ⚠️     | Logic fine; reached from the garage buy button when the centred drone has no free slot |
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

## Active Mining Difficulty — Phase 1 🚧 CODE COMPLETE (branch `feature/active-mining-difficulty`)

Spec: `docs/superpowers/specs/2026-11-03-active-mining-difficulty-design.md` · plan:
`docs/superpowers/plans/2026-11-03-active-mining-difficulty.md`. Goal: an active run can be lost,
playing better pays more, and higher tiers are harder.

**Status (2026-11-04):** Phase 1 — the closing-ring core loop, plan tasks 1–8 — is written and tested on
the branch and is not merged to `main`. **Phase 1 has not been played on a device yet**; every number
below is a starting value for the first play session, not tuned balance. **Phase 2 (tap-based
challenges: decoys, two live targets, faster spin) and Phase 3 (hold-and-drag veins) are not built**, and
the plan holds them until the owner has played Phase 1 and said to continue. Tests on 2026-11-04 (scratch
copy, Unity 6000.3.12f1): EditMode **740/740**, PlayMode **52/52**.

| Piece                                  | Where                                                                                          | What it does                                                                                                                                                                                                                                                                                      |
| -------------------------------------- | ---------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Per-tier settings                      | `EconomyConfig`, `ActiveMiningTierSettings`, `ActiveMiningRunSettings` (`Config/`)             | Five tier rows: 8 / 10 / 12 / 14 / 16 targets, ring 1.6 / 1.3 / 1.1 / 1.0 / 0.9 s, Perfect by 0.8 / 0.75 / 0.7 / 0.65 / 0.6 s, Good by 1.2 / 1.05 / 0.9 / 0.85 / 0.75 s, 3 misses at every tier. Shared: shares 100% / 80% / 60%, 0.25 s between targets, 0.2 s after a hit when an off-target tap is ignored. `ActiveRunSettings(tier)` resolves a tier |
| Closing-ring run                       | `ActiveMiningSession`, `ActiveMiningTarget`, `ActiveMiningGrade`, `ActiveMiningTargetKind` (`Mining/`) | One ring live at a time; the earlier the tap, the better the grade (Perfect, Good, Late); a ring that closes or a tap on empty space is a miss; the tier's miss limit fails the run; every target is worth an equal share                                                                          |
| Banked payout                          | `ActiveMiningPayout`, `ActiveMiningHandoff` (`Core/`)                                          | A run pays its banked share of the full quantity pinned when it began, rather than a figure recomputed after Planet reloads. A run that banked nothing pays nothing, and the asteroid is used up whether the run cleared or failed                                                                |
| Idle pays 70%                          | `EconomyConfig` (`_idleYieldFraction`), `MiningRewardCalculator`                               | Idle runs pay 70% of the full quantity; their durations are unchanged                                                                                                                                                                                                                              |
| ActiveMining scene                     | `ActiveMiningMinigameView`, `ActiveMiningAsteroidStage`, `ActiveMiningTargetView`, `ActiveMiningRulesText` | Closing rings with a banked counter and a miss counter, grade labels, a shake and sounds on a miss and on a failed run (`SfxId.ActiveMiningMiss`, `ActiveMiningFail`, at the end of the enum), targets placed on the rock and kept on screen, a rules line on the pre-game panel, and an `AudioListener` on the scene camera (the scene had none) |
| Mining prompt                          | `AsteroidTierPrompt.PayLines`, `MiningModePromptView`                                          | Under the tier status, when the drone can mine the asteroid: "Idle: safe, pays 70%" and "Active: up to 100%, depends on how you play"                                                                                                                                                             |
| Quitting mid-run                       | `ActiveMiningRunStore`, `ActiveMiningRunRecord`, `SaveKeys.ActiveMiningRun`, `MiningController` | A local marker is written when Start is pressed and refreshed on pause, quit and run end. If Planet loads with a marker and no result, the run settles like a failed one (pays what was banked, asteroid used up). The marker is cleared once the claim is answered                                  |
| Dev tools (Editor and Development Builds only) | `ActiveMiningDevTierPicker`, `ActiveMiningRunLog`, `ActiveMiningHandoff.DevSettingsForTier` / `UseSettings` | Tier 1–5 buttons across the top of the pre-game panel, so any tier can be tried on any asteroid; a pick changes only the minigame's difficulty for that run, not the asteroid or what it pays, and the picker goes when Start is pressed. Each finished run logs one line: `[SU:Mining] ActiveMining run: tier=3 perfect=1 good=1 late=0 misses=0 banked=0.90 result=Success`. All of it sits inside `#if UNITY_EDITOR || DEVELOPMENT_BUILD`; a release compile of the player scripts has none of it |

The play-session steps (dev mode on, a Development Build to a phone, Tiers 1 and 2 through the picker)
are in the plan's "STOP: Phase 1 play session". What to look for: Tier 1 clears nearly every time and
banks at least 60% even when played casually, and a lost run feels like the player's mistake. This is
the first time M6-era code runs on a device, so unrelated device problems may also show up.

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
| `ActiveMiningSessionTests.cs`  | EditMode | Closing-ring run state machine: grading by tap time, banked share, miss limit, spawn gap and miss grace, tick clamp |
| `ActiveMiningConfigTests.cs`, `ActiveMiningPayoutTests.cs`, `ActiveMiningRunStoreTests.cs`, `ActiveMiningSessionRunnerTests.cs`, `ActiveMiningRunLogTests.cs` | EditMode | Active-mining difficulty (Phase 1): per-tier settings from `EconomyConfig`, banked-share payout, the local run marker, the runner's result hand-off, and the dev run-log line. `ActiveMiningSceneFlowTests` (PlayMode) plays the real ActiveMining scene, including the dev tier picker |
| `MiningControllerTests.cs`     | EditMode | `MiningController` idle/active orchestration, drone gating, single-tap claim, respawn scheduling; server-tracked idle runs (Known Issue #22): the awaited start, a refused start, `ALREADY_MINING` takeover, the in-flight guard, restore of the server's run (server-anchored progress, server wins over a legacy PlayerPrefs session, other planet ignored, unplaceable slot), and `idleSlotId` on claims |
| `IdleMiningServiceTests.cs`    | EditMode | `IdleMiningService` start/get requests and reply mapping (null reply, network failure), and the standalone `LocalMockIdleMiningService` |
| `IdleMiningSessionResultContractTests.cs` | EditMode | Deserializes every `StartIdleMining` / `GetIdleMining` reply shape the way the Cloud Code SDK does into `IdleMiningSessionResult` |
| `AsteroidSpawnerEnsureSlotTests.cs` | EditMode | `AsteroidSpawner.EnsureSlot`: a live slot is returned as is; a slot this device has waiting to respawn is spawned now and its timer dropped; a slot not on this planet returns null |
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

**Update 2026-10-01:** EditMode **408/408** (adds `DroneCatalogAlignmentTests`,
`ServerCodeEconomyApiTests`, `ServerFailureFeedbackTests`, `MineralSaleHandlerTests`,
`BackendRetryPolicyTests`, `AsteroidYieldRollTests`, `MiningCatalogAlignmentTests`,
`MiningGrantResultContractTests`, `CloudCodeResponseContractTests`, `DroneUpgradeTracksTests`,
`MineralContentAlignmentTests`, `ServerCodeDeployTests`; removes `ValidateMiningCapAlignmentTests` and
`EconomyServiceMiningTests`, which covered the deleted coin-based mining grant).

**PlayMode total: 2/2** (2026-09-29) — Known Issue #7 fixed.

**Update 2026-10-15 (server-tracked idle mining, Known Issue #22):** EditMode **556/556** (504 before; 52 new test cases in
`MiningControllerTests`, `IdleMiningServiceTests`, `IdleMiningSessionResultContractTests`, `AsteroidSpawnerEnsureSlotTests`,
`PlayerRecordLockTests`, `ServerCodeAccessClassTests`, `MineralServiceTests`, `ServerFailureFeedbackTests`, `AsteroidTierPromptTests`,
`TaskExtensionsTests`), PlayMode **13/13** (adds the parallel idle-run fetch check) — headless
Unity 6000.3.12f1 on a scratch copy. Eleven deliberate breakages each failed exactly their intended tests.

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

Follow **`docs/deploy-runbook.md`** (added 2026-10-01): dashboard + Firebase setup, then
`./ServerCode/deploy.ps1` deploys every Cloud Code script on `ServerCode/deploy-list.txt` with the
UGS CLI. **First confirmed deploy: 2026-10-01** — all 30 listed scripts created and published (v1)
in `development`, parameters registered (`ugs cc scripts list/get`). UGS CLI 2.0.0 has no Economy
support, so the currencies are still a dashboard step. Every script now declares its parameters in
`module.exports.params` (the only place the CLI reads them); `ServerCodeDeployTests` pins the list,
the declarations and the currency values.

- [x] Pick the environment: `Assets/AppConfig.asset` now selects **Development**
      (`_environment: 1`, 2026-09-30) — testers use the UGS `development` environment; set up
      and deploy everything below there. Switch to Production only for launch.
- [ ] Create/link the UGS project (`Project Settings > Services`).
- [ ] Economy (dashboard, `development`): create and publish `COINS` (initial 500) + `STARDUST`
      (initial 10) — see `ServerCode/ECONOMY_CONFIG.md`. Not deployable with UGS CLI 2.0.0.
- [ ] UGS Dashboard: enable/configure **Vivox** (text chat) and **Friends**.
- [ ] Configure **Firebase Auth** (email/password + Google) and the UGS OIDC provider
      `oidc-firebase`. `Assets/google-services.json` currently has **no Android OAuth client and
      no SHA-1 fingerprint**, so Google sign-in has to be verified on a device; if testers install
      through Play, register Play's app-signing SHA-1 as well.
- [x] `./ServerCode/deploy.ps1` run against `development` (2026-10-01, 30 scripts). Still to do:
      the runbook's smoke test once Economy/OIDC/Vivox/Friends are set up. Redeploy everything
      rather than trusting what is live:
      `FuelSystem.cs:20-27` records that the deployed `RefillFuel` already differs from the repo
      copy, and no deploy has ever been ticked off in this file.
- [x] `GrantCoins`, `GrantStardust` and `GrantOfflineIncome` are excluded from
      `deploy-list.txt` (any signed-in player could call them — Known Issue #16), and the client's
      unused `IEconomyService.GrantCoinsAsync`/`GrantStardustAsync` were removed (2026-10-01).
- [ ] Re-check the six functions named in Known Issue #12 against the live SDK once fixed.
- [x] Deploy the single-active-session, cross-device consistency and server-tracked idle mining changes (Known Issues #20–#22)
      — **deployed to `development` 2026-10-15**: all 34 scripts on `deploy-list.txt` (`ClaimSession`, `ReleaseSession`,
      `StartIdleMining`, `GetIdleMining` created at v1; the other 30 updated), verified with `ugs cloud-code scripts get` (live code
      and parameters identical to the repo, newest version active). From now on every client build older than this working
      tree gets every Cloud Code call rejected (`SESSION_REPLACED`).
- [x] Deploy the cloud-synced planet data change (Known Issue #23): `ValidateMining` (slot + forfeit), `GetLandRegistry` and the new
      `GetAsteroidRespawns` — **deployed to `development` 2026-10-18** (all 35 scripts, verified identical with `scripts get`).
- [ ] Give testers a client built from the same commit as that deploy (the #20–#22 client changes are still uncommitted), then
      run the runbook's smoke steps 10–15 with two clients — and start an idle run on one device, then sign in on the other and
      claim it there.
- [x] `ServerCode/ModerateMessage.js` deleted (2026-09-30) — nothing called it; `UpdateProfile.js`
      moderates display names itself, and chat is filtered client-side + by Vivox.

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
- [x] Dev surfaces checked (2026-09-30): nothing needs hiding for testers. `SocialDebugPanel`
      is the game's **only chat UI** (channel toggle, presence, message log, input, send) until
      M11's ChatScreen, so it stays. `CloudCodeTestHarness` exposes only Inspector buttons, which
      don't exist in a player build. `ChatSendProbe` (Planet.unity, a TEMP diagnostic that
      auto-sends "diagnostic ping") is inactive and unwired — delete its GameObject and script
      in the Editor when convenient. `_devMode` is off, so builds use the real services.

**5. On-device verification pass** — nothing has been recorded as verified on a device for any
milestone. Once (1)–(3) land, run each milestone's "Manual Play Mode Verification" checklist:
auth (email + Google), persistence, two-client presence/chat, travel/fuel, gyro sky discovery,
land building, and the full M6 mine → sell → upgrade → higher-tier loop.

**5a. Active Mining Phase 1 — weigh after the first play session** (see "Active Mining Difficulty" above;
Phases 2 and 3 are not built and wait for the owner's go-ahead)

- [ ] Play Phase 1 on a device (Development Build, dev tier picker, Tiers 1 and 2 first) and tune the
      `EconomyConfig` tier rows if Tier 1 doesn't clear nearly every time or banks under 60%.
- [ ] The mining prompt's pay lines render small — the status text auto-sizes to about 17 where it
      normally uses 26 — and the prompt window has about 84 units of spare room below the button row.
      Decide whether to use that room for larger text.
- [ ] Targets sit nearer the centre of elongated asteroids: they are placed at 75% of the rock's
      inscribed radius (`ActiveMiningMinigameView.PlacementInset`) so every one lands on the rock. Relax it
      if the targets feel bunched.
- [ ] Check on a device that a run left by closing the app settles when Planet next loads.

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
purchases) were cleared on 2026-09-30, and asteroid `_rarity` now means scarcity (0 = common,
1 = rare) with values rising by tier — Iron 0.1 … Helium-3 0.92 (`MineralContentAlignmentTests`).

**Note:** The temporary `PlayModeVerifier.cs` smoke-test script (and its component on the `PlanetSceneScope` GameObject in `Planet.unity`) has been removed — its checks are now covered by `PlanetSceneFlowTests` under `Assets/_Project/Tests/PlayMode/` (assembly `SocialUniverse.PlayModeTests`). As of M4, both tests fail at `SetUp` — see Known Issue #7.