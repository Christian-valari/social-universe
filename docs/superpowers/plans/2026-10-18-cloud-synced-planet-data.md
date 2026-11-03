# Cloud-synced planet data and a mandatory post-login pull

**Goal:** everything the game shows about the player comes from the cloud, and it is all loaded
after sign-in before the player can play — no device-only planet state, and no scene that opens
with defaults because a read failed.

**Status (2026-10-18):** implemented and tested locally (Known Issue #23). EditMode and PlayMode
suites green on a scratch copy; server scripts covered by Node behaviour tests (not checked in).
**Deployed to `development` 2026-10-18** (35 scripts, verified identical with `ugs cloud-code scripts get`); **not smoke-tested, not committed.**

**Scope:** cross-device follow-up to [cross-device data consistency](2026-10-15-cross-device-data-consistency.md)
and [server-tracked idle mining](2026-10-15-server-tracked-idle-mining.md) — not M7 work. It touches
no open decision (age policy, land resale).

## What was local or silently defaulted (audit, 2026-10-18)

| Data | Before | Now |
|---|---|---|
| Land registry, owned tiles, builds, current planet, trip, idle run, wallet, fuel, fleet, minerals, profile | Cloud | Cloud (unchanged) |
| **Asteroid respawn timers** | Device-global PlayerPrefs `asteroid_respawns` — shared by every account on the device, invisible to other devices | Server claim log (`mining_claim_log`), read through `GetAsteroidRespawns` |
| **Failed reads at scene load** | Each read was caught and the scene opened with defaults (0 coins, Scout only, no minerals, no tiles, every asteroid back). `CloudSaveService` itself turned every failure into "no record", and `GetLandRegistry` turned any read failure into "no owners" | A failed read is retried behind a "Couldn't load your game data — Retry" dialog until it loads |
| **Block list** | In Cloud Save (`blocked_users`) but never read after sign-in; the app-wide `ReportService` cache also survived sign-out into the next account | Pulled at sign-in (and by the Planet load if that failed); cleared on sign-out with mutes |

## A — Respawn timers from the server

- `ValidateMining` takes an optional `slotId` (an idle claim's `idleSlotId` is used when it is
  absent) and writes each budget entry as `{ ts, slotId, mineralId }`. A missing/malformed slot
  (`^[A-Za-z0-9_]{1,32}#[0-9]{1,3}$`) is budgeted with `slotId: null`. Entries written before this
  change are bare timestamps; they still count and prune (`entryTs`).
- New read-only `GetAsteroidRespawns { planetId }` → `{ success, respawns: [{ slotId, mineralId,
  secondsLeft }] }`. `secondsLeft` is an integer on the server clock (same reasoning as
  `GetIdleMining.elapsedSeconds`). Slotless entries are left out. A 404 means none; any other read
  failure throws. `RESPAWN_HOURS` is pinned to `EconomyConfig` by `MiningCatalogAlignmentTests`.
- Client: `IAsteroidRespawnService` (`AsteroidRespawnService` / `LocalMockAsteroidRespawnService`).
  `AsteroidSpawner.SpawnForPlanet(planet, respawns)` keeps those slots empty, dedupes a slot reported
  twice (later timer wins) and skips slots the planet can't hold. Nothing is written to the device;
  the old PlayerPrefs key is deleted on load. The field is now spawned after the data pull.
  `MiningController` sends the asteroid's slot with idle and active claims.

**Failed minigames forfeit their slot (same day).** `MiningController.FailActiveMining` empties the
slot at once and calls `IMineralService.ForfeitMiningAsync`: `ValidateMining { planetId, mineralId,
slotId, forfeit: true }` checks the planet and the mineral like a claim, logs `{ ts, slotId,
mineralId }` against the budget and answers `{ granted: 0, mineralId, reason: "FORFEITED" }`. It
needs a usable slot (else `INVALID_PARAMS`), skips the drone-tier check and never pins the planet —
the player gains nothing either way. The client never adds minerals for a forfeit and shows no toast
whatever the answer (the player already saw the run fail); a failure is only logged.

**Behaviour change:** a failed minigame now uses one of the planet's claims (before: it cost nothing
on the server). Depletions the server still never logs — a rejected claim, a grant that threw, a
forfeit that wasn't recorded — last only until the next Planet load (before: 4 h on that device). The
server's claim budget is still the reward bound.

## B — The post-login pull is mandatory

- `Core/PlayerDataPull.RunAsync(steps, required, askToRetry, onProgress)`: runs every step at once;
  when `required`, failed steps are retried (only those) after the player taps Retry, until all load.
  When not required, a failed step is logged and skipped (old behaviour).
- `Core/PlayerDataSyncPolicy { Required, AskToRetry }`: `RootLifetimeScope` registers
  `required: !_devMode` with `SyncRetryDialogView.ShowAsync` (Core can't reference UI); standalone
  scene scopes and `PlanetTestRootScope` register `false` (their mocks answer with nothing).
- `UI/SyncRetryDialogView`: code-built overlay (sorting 5500 — above the loading screen and
  `ToastView`, below `QuitDialogView`), survives scene loads, and brings its own EventSystem when no
  loaded scene has one (boot, and between the Auth scene unloading and the next scene loading).
- `BootState` / `AuthState`: `GetCurrentPlanet` (where to resume after sign-in) goes through the same
  gate, so the device's last-planet hints are only read in dev mode.
- `PlanetSceneBootstrapper` now pulls first (wallet, fuel, minerals, fleet, profile, owned tiles,
  land registry, idle run, asteroid respawns, block list) and applies after, so prompts and backfills
  run once. `TravelSceneBootstrapper` (trip) and `SolarSystemBootstrapper` (fuel, wallet) use the same
  gate — the Travel scene is where a player with an unlanded trip lands after sign-in.
- `CloudSaveService.Load*` throws on any failure except not-found. `GetLandRegistry` throws on a
  non-404 read failure. `LandRegistrySyncController.RefreshAsync` throws; its poll loop still catches.

## C — Block list

- `ReportService.SetBlockList / HasBlockList / Reset`. `App/BlockListSync` reads `blocked_users`
  (default access class, written only by `BlockUser`). `SocialServicesInitializer` starts the read on
  `PlayerReadyEvent` and calls `Reset` on `PlayerLoggedOutEvent`. Vivox-level blocks are not
  re-applied from the pulled list (the local filter drops those messages already).

## Deploy

Ship `ValidateMining` (slot + forfeit), the new `GetAsteroidRespawns` and `GetLandRegistry` together with a client
from the same commit (`deploy-list.txt` now has 35 scripts). Mixed versions stay safe: an old client
sends no `slotId` (entries get `slotId: null`, so no respawn timers until the new client's claims)
and no forfeits; an old server answers a new client's forfeit with `INVALID_PARAMS` (no `claimedQty`),
which the client only logs;
the new client against the old server gets "unknown function" for `GetAsteroidRespawns`, which the
retry gate would keep asking about — so deploy the server first.
