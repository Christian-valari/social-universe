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

**Formulas:**
```
idleDurationSeconds = clamp(asteroid.RemainingYield * IdleSecondsPerYieldUnit,
                             MinIdleSessionSeconds, MaxIdleSessionSeconds)

activeTapsRequired = clamp(ceil(asteroid.RemainingYield / ActiveYieldPerTap),
                            MinActiveTaps, MaxActiveTaps)
```

Both derive from the same `RemainingYield`, so a given asteroid's idle duration and active tap count are two views of the same underlying size — this is what keeps total reward equal between modes.

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
4. Reaching `activeTapsRequired` successful hits (formula in §3) → **success**: asteroid's full `RemainingYield` is mined instantly, coins granted via the same `IEconomyService.GrantCoinsAsync` path idle claiming already uses, and the asteroid is scheduled for respawn.

New types: an `ActiveMiningSession` (state: current error count, taps remaining, current target point, elapsed-in-window) and an `ActiveMiningMinigame` rewrite (replacing today's free-tap stub) that judges hit/miss and drives point spawning. Neither references `DroneRuntime`.

## 6. Server-authoritative validation

`ServerCode/ValidateMining.js` currently caps the granted payout at `sessionDurationSec × coinsPerSec`, using the session's *actual elapsed wall-clock time* as an anti-cheat bound. This assumption breaks for active mining, which is deliberately near-instant — a naive call would have the server clamp the payout down to almost nothing.

**Fix:** for both idle claims and active-mining success, the client sends the asteroid's *equivalent idle duration* (the same `idleDurationSeconds` formula from §3, computed from the asteroid's known `RemainingYield`/tier) as `sessionDurationSec`, not the actual real-time elapsed. The server's existing cap logic (`min(claimedCoins, sessionDurationSec * coinsPerSec, ABSOLUTE_COINS_CAP)`) is unchanged — only what the client reports as the duration basis changes, and it reports the game-design-intended duration rather than literal clock time. This keeps the server as the source of truth on payout (Architecture Rule 1) while honoring "same reward regardless of mode, active is just faster."

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
- Idle duration formula (`clamp` behavior at both bounds, mid-range scaling)
- Active tap-count formula (same)
- `ActiveMiningSession` / `ActiveMiningMinigame` state machine: hit advances progress, miss/timeout increments errors, 3rd error fails, reaching required taps succeeds
- Asteroid field-size distribution: total spawned always equals `AsteroidFieldSize`, weighted correctly by rarity, largest-remainder rounding doesn't drop or double-count units
- Idle-session persistence reconciliation: simulate elapsed real time across a save/load boundary and assert correct resumed stage (still mining vs. ready-to-claim vs. session discarded if slot is gone)

**Removed:** `IdleMiningCalculatorTests` (class under test no longer exists).

**PlayMode:** extend or replace the existing mining coverage in `PlanetSceneFlowTests` to reflect the new flow (idle claim grants correct coins with no cargo intermediary; active-mining success/fail paths).

## 10. Open items carried forward (not blocking this spec)

- Full server-side session-token anti-cheat for `ValidateMining` remains deferred to M3 per the existing comment in `ValidateMining.js` — this spec only fixes the duration-basis bug in §6, it does not add session tokens.
- Multiple simultaneous drones / drone slots (M6) are out of scope; this spec assumes the single-drone model that exists today.
