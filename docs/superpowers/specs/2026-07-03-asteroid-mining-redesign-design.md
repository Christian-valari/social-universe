# Asteroid Mining Redesign — Idle & Active Modes

**Date:** 2026-07-03
**Status:** Approved for planning
**Milestone context:** Completes a piece of M1 (Core Loop Prototype) that `PROGRESS.md` explicitly flagged as deferred — `ActiveMiningMinigame` was stubbed with a no-op "Active Mine" button ("mini-game coming in a later milestone"). This spec resolves that deferral and reworks the surrounding idle-mining flow at the same time, since both modes need to change together to make sense as a pair.

## 1. Current state (before this change)

The mining loop already exists but is inconsistent and partially stubbed:

- **Automatic offline yield** (`IdleMiningCalculator`): every time a mining session starts, real elapsed time since the last session (capped at `MaxOfflineHours`) is silently converted to cargo, independent of any player choice.
- **Player-directed idle mining** (`IdleMiningSession` / `IdleMiningSessionController`): choosing "Idle Mine" on an asteroid sends the drone traveling to it, mines for a fixed `IdleSessionDuration` (30s), then requires `IdleSessionClaimTaps` (5) taps to claim. Not a true walk-away flow, and does not persist across an app restart — progress is tracked only via in-frame `Tick()`.
- **"Active mining"** is really just an always-available free-tap mode: whenever the drone isn't on an idle session, pressing Space taps whatever asteroid `MiningController` has auto-picked as `CurrentTarget`, yielding `ActiveTapYield` units with a `CritChance` crit. This has nothing to do with the "Active Mine" button in `MiningModePromptView`, whose click handler is a no-op stub.
- Payout for the free-tap mode routes through `DroneRuntime` cargo (`AddCargo`/`EmptyCargo`) and commits when cargo is full or on demand via `CommitCargoAsync`. Idle-session payout bypasses cargo entirely and pays out directly on claim.
- Asteroid respawn (`AsteroidSpawner.ScheduleRespawn`, persisted via `PlayerPrefs`) and per-type spawn counts (derived from `AsteroidDefinition.Rarity` and a flat `_maxPerType`) already work and are not being redesigned, aside from making the total field size explicit per planet.

## 2. Goals

1. Two explicit, mutually exclusive-per-asteroid mining modes, chosen from the existing tap prompt (`MiningModePromptView`):
   - **Idle Mining** — drone travels to the asteroid, mines for a duration, player can leave the app entirely and claim on return with a single tap.
   - **Active Mining** — a tap-timing minigame the player plays immediately, no drone travel, resolves instantly.
2. Both modes pay out the **same total reward** for a given asteroid; active mining is faster, not more lucrative.
3. Failing the active-mining minigame (3 errors) costs the asteroid — no reward, same respawn cooldown as a successful claim.
4. Idle-mining session state survives the app being closed and reopened.
5. Each planet has an explicit, designer-set number of asteroids in its field.
6. Remove the mechanics this replaces (auto offline-yield, free-tap-anytime mode, cargo-based payout) rather than leaving them as dead paths alongside the new ones.

## 3. Config & data model changes

### `EconomyConfig`

Remove:
- `IdleMiningRate`, `MaxOfflineHours` (drove `IdleMiningCalculator`, being removed)
- `IdleSessionClaimTaps` (claim becomes a single tap, no count needed)
- `ActiveTapYield`, `CritChance`, `CritMultiplier` (drove the free-tap mode, being removed)

Add:
- `IdleSecondsPerYieldUnit` (float) — idle duration scales with asteroid size
- `MinIdleSessionSeconds`, `MaxIdleSessionSeconds` (float) — clamp bounds
- `ActiveYieldPerTap` (float) — how much yield one successful tap represents
- `MinActiveTaps`, `MaxActiveTaps` (int) — clamp bounds on required taps
- `ActiveTapWindowSeconds` (float) — time allowed to hit each spawned target point
- `ActiveMaxErrors` (int, default 3)

Keep unchanged: `AsteroidRespawnHours`.

A new `MiningRewardCalculator` (Mining/) is the single source of truth for these derived numbers, given an `Asteroid` and `EconomyConfig`:
```
totalCoins           = asteroid.RemainingYield * asteroid.Definition.CoinsPerUnit
idleDurationSeconds  = clamp(asteroid.RemainingYield * IdleSecondsPerYieldUnit, MinIdleSessionSeconds, MaxIdleSessionSeconds)
activeTapsRequired   = clamp(ceil(asteroid.RemainingYield / ActiveYieldPerTap), MinActiveTaps, MaxActiveTaps)
coinsPerSec          = totalCoins / idleDurationSeconds
```
`coinsPerSec` is computed per-claim from the asteroid's actual `totalCoins` and `idleDurationSeconds` — **not** a fixed per-asteroid-type constant. This matters because `idleDurationSeconds` is clamped: for a very high-yield asteroid, `idleDurationSeconds` may sit at `MaxIdleSessionSeconds` even though `RemainingYield * IdleSecondsPerYieldUnit` would be larger. A fixed `coinsPerSec` (e.g. `CoinsPerUnit / IdleSecondsPerYieldUnit`) would then make `sessionDurationSec * coinsPerSec < totalCoins`, causing the server to under-grant a legitimate full-yield claim. Computing `coinsPerSec = totalCoins / idleDurationSeconds` per claim makes `sessionDurationSec * coinsPerSec` always equal `totalCoins` exactly, so the server cap never clips a correct claim regardless of clamping.

### `PlanetDefinition`

Add `AsteroidFieldSize` (int) — the total number of asteroids simultaneously present on this planet. `AsteroidSpawner.SpawnForPlanet` distributes this total across `AsteroidTypes[]` weighted by `(1 - Rarity)` per type, using largest-remainder rounding so the sum of per-type counts always equals `AsteroidFieldSize` exactly (replacing today's flat `_maxPerType` constant).

## 4. Idle mining flow

State machine shape is unchanged (`Traveling → Mining → ReadyToClaim → Complete`), but:

- Duration comes from the formula in §3 instead of a flat config value.
- Claiming is a single tap: `RegisterClaimTap()` is replaced by a parameterless `Claim()` that transitions `ReadyToClaim → Complete` directly. `ClaimTapsRequired`/`ClaimTapsRemaining` are removed from `IdleMiningSession`.
- **Persistence:** on `BeginIdleMining`, `MiningController` persists `{planetId, asteroidDefinitionId, spawnSlotIndex, startUtc, durationSec}` via `PlayerPrefs`, following the same pattern `AsteroidSpawner` already uses for pending respawns (`SaveKeys.AsteroidRespawns`). On scene load, after `AsteroidSpawner.SpawnForPlanet` runs, `MiningController` checks for a persisted session: if the referenced slot is still present and unclaimed, it resumes the session at the correct stage using `DateTime.UtcNow - startUtc` compared against `durationSec` — no per-frame ticking is required to cover time the app was closed. If the referenced asteroid is no longer resolvable (edge case — planet asset changed, slot data corrupt), the persisted entry is discarded and the drone is freed.
- The drone is occupied for the duration of one idle session at a time, same as today (single drone, single idle session).

## 5. Active mining flow (new)

Choosing "Active Mine" in `MiningModePromptView`:

1. Opens a minigame overlay immediately. No drone travel — the drone is not referenced at all and remains free for a concurrent idle-mining session on a different asteroid.
2. A single target point spawns at a random position on/around the asteroid. Player has `ActiveTapWindowSeconds` to tap it.
   - Hit within the window → that portion of the asteroid is mined, next point spawns.
   - Miss (tap wrong spot) or timeout (window expires) → 1 error.
3. Three errors (`ActiveMaxErrors`) → **fail**: asteroid is consumed with zero payout and scheduled for respawn via the existing `AsteroidSpawner.ScheduleRespawn`, same cooldown as a successful claim.
4. Reaching `activeTapsRequired` successful hits (from `MiningRewardCalculator`, §3) → **success**: asteroid's full `RemainingYield` is mined instantly, coins granted via the same `IEconomyService.GrantMiningRewardAsync` path idle claiming uses (§6), and the asteroid is scheduled for respawn.

New types: an `ActiveMiningSession` (state: current error count, taps remaining, current target point, elapsed-in-window) and an `ActiveMiningMinigame` rewrite (replacing today's free-tap stub) that judges hit/miss and drives point spawning. Neither references `DroneRuntime`.

## 6. Server-authoritative validation

**Correction to an assumption made earlier in this design:** `ServerCode/ValidateMining.js` already exists and computes a sensible cap (`min(claimedCoins, sessionDurationSec × coinsPerSec, ABSOLUTE_COINS_CAP)`), but it is **not currently called by any gameplay code** — it's reachable only from `CloudCodeTestHarness`, a dev-only debug component. The live payout path (`IdleMiningSession` → `MiningController.RegisterIdleClaimTapAsync` → `IEconomyService.GrantCoinsAsync(amount)`) calls a different Cloud Code function, `GrantCoins.js`, which has no session-based validation at all — it only sanity-caps at a flat 100,000 coins per call and otherwise grants whatever `amount` the client sends. This is a real gap against Architecture Rule 1 ("the client never mints currency... it requests; the server validates and commits") that predates this feature.

Since this rework already touches the entire mining payout path, it fixes this by wiring both idle-claim and active-mining-success payouts through `ValidateMining` for real:

- `IEconomyService` gains `Task<int> GrantMiningRewardAsync(int claimedCoins, float sessionDurationSec, float coinsPerSec)`.
- `EconomyService` (real/M2) implements it by calling the Cloud Code function `"ValidateMining"` with those three params (matching `ValidateMining.js`'s existing signature and the param names `CloudCodeTestHarness` already uses), then applies the returned `newBalance` to the local `Wallet` (guarding against the function's `newBalance: null` response when `granted` is `0`).
- `LocalMockEconomy` (M1 mock) implements it by granting `claimedCoins` directly to the wallet with no validation, consistent with how it already implements `GrantCoinsAsync`.
- Both the idle-claim path and the active-mining-success path call `GrantMiningRewardAsync` with `claimedCoins = totalCoins`, `sessionDurationSec = idleDurationSeconds`, `coinsPerSec = coinsPerSec` — all three taken from the same `MiningRewardCalculator` result (§3), so the server always receives a cap basis that exactly matches the intended full payout, regardless of which mode was used or how fast the player finished. This is what makes "same reward regardless of mode" hold under server validation, not just in client-side math.
- The now-superfluous `GrantCoins.js` path is left as-is (still used elsewhere, e.g. land-sale flows are out of scope here) — this spec only changes what mining payouts call.

## 7. Removals

- `MiningPhase` enum, `MiningController.OnPhaseChanged`, `MiningController.Phase`
- `MiningController.Tap()`, `MiningController.CommitCargoAsync()`, `MiningController.PickNextTarget()` (auto-target-picking is no longer meaningful once mining is always asteroid-selection-driven)
- `MiningInputHandler` (spacebar-driven free-tap entry point)
- `IdleMiningCalculator` and its test suite (`IdleMiningCalculatorTests`)
- `DroneRuntime.CargoAmount`, `IsCargoFull`, `AddCargo()`, `EmptyCargo()` — verified via repo-wide search that nothing outside the removed call sites depends on these. `DroneRuntime` continues to exist as a thin wrapper around `DroneDefinition` (still used as the drone identity for idle-mining travel).

## 8. UI changes

- `MiningModePromptView.OnActiveMineClicked` wires to starting an `ActiveMiningSession` instead of logging a no-op.
- New minigame overlay view (`UI/`): renders the current target point on/near the asteroid, an error counter (3 pips), and hit/miss/success/fail feedback.
- `HUDController`'s mining status line drops the `cargo/cap` readout (cargo no longer exists) and shows idle-session state only — it already surfaces "Heading to…", "Mining: NN%", "Tap to claim!" for the idle flow; this continues unchanged. Active mining's overlay is self-contained and doesn't need a HUD status line since it's a modal, synchronous interaction.

## 9. Testing plan

**EditMode:**
- `MiningRewardCalculator`: idle duration formula (`clamp` behavior at both bounds, mid-range scaling), active tap-count formula (same), `coinsPerSec` computation (verify `sessionDurationSec * coinsPerSec == totalCoins` exactly, including at both clamp bounds)
- `ActiveMiningSession` / `ActiveMiningMinigame` state machine: hit advances progress, miss/timeout increments errors, 3rd error fails, reaching required taps succeeds
- Asteroid field-size distribution: total spawned always equals `AsteroidFieldSize`, weighted correctly by rarity, largest-remainder rounding doesn't drop or double-count units
- Idle-session persistence reconciliation: simulate elapsed real time across a save/load boundary and assert correct resumed stage (still mining vs. ready-to-claim vs. session discarded if slot is gone)
- `LocalMockEconomy.GrantMiningRewardAsync`: grants `claimedCoins` directly to the wallet

**Removed:** `IdleMiningCalculatorTests` (class under test no longer exists).

**PlayMode:** extend or replace the existing mining coverage in `PlanetSceneFlowTests` to reflect the new flow (idle claim grants correct coins with no cargo intermediary; active-mining success/fail paths).

## 10. Open items carried forward (not blocking this spec)

- Full server-side session-token anti-cheat for `ValidateMining` remains deferred to M3 per the existing comment in `ValidateMining.js` — this spec only fixes the duration-basis bug in §6, it does not add session tokens.
- Multiple simultaneous drones / drone slots (M6) are out of scope; this spec assumes the single-drone model that exists today.
