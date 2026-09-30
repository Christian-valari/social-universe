# Mining Claim Budget (server-side mining validation) — Design

## Context

`ServerCode/ValidateMining.js` grants minerals for every idle and active mining claim. Today it
trusts the client completely: the grant is `min(claimedQty, sessionDurationSec × unitsPerSec,
10000)`, and both `sessionDurationSec` and `unitsPerSec` come from the client. A modified client
can claim any mineral, in any quantity up to 10,000, as often as it likes. This is the last
unresolved `ValidateMining` item in Known Issue #16 (Architecture Rule 1: server-authoritative
economy).

What the client does today:

- **Asteroid fields are client-only.** Each planet has `_asteroidFieldSize` asteroids (6 on all 10
  planets) drawn from `PlanetDefinition._asteroidTypes`. Slot ids are `"{mineral}#{index}"`. A
  mined asteroid respawns after `EconomyConfig._asteroidRespawnHours` (4h); the timers live in
  `PlayerPrefs` (`AsteroidSpawner.SavePendingRespawns`).
- **Two mining modes**, both claimed through `IMineralService.GrantMiningAsync`:
  - Idle (`MiningController.ClaimIdleSessionAsync`): 30–1800 s, scaled by the asteroid's yield.
  - Active (`MiningController.CompleteActiveMiningAsync`): a tap minigame in the ActiveMining
    scene. Pass/fail is decided client-side, and a fast player can finish in a few seconds.
- **Payout:** `MiningRewardCalculator` grants `round(RemainingYield × EffectiveYieldMult)`.
  `RemainingYield` is rolled client-side as `BaseYield × Random.Range(0.8, 1.2)` (`Asteroid.cs:25`).
  `EffectiveYieldMult` = drone `YieldMultiplier + YieldLevel × Upgrade_Yield._deltaPerLevel`
  (`DroneUpgradeMath.EffectiveStat`).
- **The server already knows the player's planet:** `LandTravel.js` writes `current_planet`
  (`{ planetId }`) when a trip completes. It is null for players who have not travelled since that
  feature shipped; the client then resumes on its locally saved planet (`PlanetResumeResolver`).

Because the active minigame is client-decided, enforcing a session *duration* cannot bound active
mining. For a legit player, the real limit on mining is asteroid supply: 6 asteroids per planet,
respawning every 4 hours. This design enforces that supply limit on the server without modelling
individual asteroid slots.

## Goal

A modified client that only changes what it *claims* can mine no faster and no more than a legit
player, with a small, bounded exception: it can always pick the best mineral on its current planet
and always get the top yield roll (≤ +20%). Legit players must never be rejected in normal play.
This does not hold against a client that also writes its own Cloud Save records — see
**Dependency** under Error handling.

**Out of scope:** server-side asteroid slots and respawn timers; moving economy records to a
server-only Cloud Save access class (the other open part of Known Issue #16); a `StartMining` call.

## Decisions

| Decision | Choice | Why |
|---|---|---|
| Strictness | Pace + payout; no per-slot cooldowns | Per-slot cooldowns need the server to mirror every field layout and move respawn timers server-side |
| Idle vs active | Both allowed at once (unchanged gameplay) | A single budget covers both modes |
| Approach | Claim budget inside `ValidateMining`; no session record | Same guarantee as Start/Claim sessions once the budget exists, with no extra round trip or change to how mining starts |

## Server contract — `ValidateMining`

**Request:** `{ planetId: string, mineralId: string, claimedQty: int }`.
`sessionDurationSec` / `unitsPerSec` are no longer read.

**Response:** `{ granted: int, mineralId: string, reason?: string }`. Rejections return
`granted: 0` plus a `reason` instead of throwing, so the client can show it.

**Checks, in order:**

| # | Check | Reject reason |
|---|---|---|
| 0 | `planetId`, `mineralId` present; `claimedQty` a positive integer | `INVALID_PARAMS` |
| 1 | `planetId` equals the server's `current_planet.planetId`. **If the record is missing or null, accept the claimed planet.** | `WRONG_PLANET` |
| 2 | `mineralId` is in `PLANET_MINERALS[planetId]` (an unknown planet also fails here) | `MINERAL_NOT_ON_PLANET` |
| 3 | `MINERALS[mineralId].tier` ≤ the tier of the saved fleet's active drone (an empty fleet counts as the starter Scout, as in Known Issue #11) | `TIER_TOO_LOW` |
| 4 | Grant = `min(claimedQty, ceil(ceil(baseYield × YIELD_ROLL_MAX) × effectiveYieldMult))` — **clamped, never rejected**. The inner `ceil` covers the client rounding the roll before multiplying (`round(round(base × roll) × mult)`) | — |
| 5 | Fewer than `FIELD_SIZE` claims for `planetId` in the last `RESPAWN_HOURS` | `BUDGET_EXHAUSTED` |

`effectiveYieldMult = DRONES[activeDroneId].yieldMult + (upgrades.Yield ?? 0) × YIELD_DELTA_PER_LEVEL`.
An unknown `activeDroneId` counts as the starter drone.

**Commit sequence:**

1. Read `mining_claim_log` (with its `writeLock`): `{ [planetId]: number[] }` of claim timestamps
   (ms). Drop timestamps older than `RESPAWN_HOURS` for every planet. Run check 5 against the
   pruned list. Append `Date.now()` and write under the `writeLock`. On 409, re-read and retry
   (max 3 attempts, re-running check 5 each time); the last attempt's conflict is thrown.
2. Add the grant to `mineral_inventory` under its `writeLock` with retry (as today).
3. If step 2 throws, remove the appended timestamp from `mining_claim_log` (best effort, logged on
   failure) and rethrow — a failed grant must not consume budget.

Order matters: the budget slot is reserved before minerals are granted, so two concurrent claims
cannot both take the last slot.

## Server catalog — constants in `ValidateMining.js`

Each block carries a `MUST MATCH` comment and is checked by `MiningCatalogAlignmentTests`.

| Constant | Shape | Source of truth |
|---|---|---|
| `PLANET_MINERALS` | `{ earth: ["iron", …], … }` — all 10 planets | `PlanetDefinition._planetId` → `_asteroidTypes[]` → `AsteroidDefinition._mineral` → `MineralDefinition` id |
| `MINERALS` | `{ iron: { baseYield: 80, tier: 1 }, … }` — 6 minerals | `AsteroidDefinition._baseYield`, `_tier`, keyed by its mineral's id |
| `DRONES` | `{ scout: { tier: 1, yieldMult: 1 }, … }` — 5 drones | `DroneDefinition._droneId`, `_tier`, `_yieldMultiplier` |
| `YIELD_DELTA_PER_LEVEL` | `0.15` | `Upgrade_Yield._deltaPerLevel` |
| `FIELD_SIZE` | `6` | ≥ every `PlanetDefinition._asteroidFieldSize` |
| `RESPAWN_HOURS` | `4` | `EconomyConfig._asteroidRespawnHours` |
| `YIELD_ROLL_MAX` | `1.2` | new `EconomyConfig._asteroidYieldRollMax` (see below) |
| `STARTER_DRONE_ID` | `"scout"` | as in the other drone functions (already tested) |

`ABSOLUTE_SESSION_CAP_SECONDS` and `ABSOLUTE_QTY_CAP` are removed.

**`EconomyConfig` change:** the roll range is a literal in `Asteroid.cs`. Add
`_asteroidYieldRollMin = 0.8f` / `_asteroidYieldRollMax = 1.2f` (with getters) and have
`Asteroid` use them, so the server constant has an asset value to be tested against.
`AsteroidSpawner` gains `[Inject] private EconomyConfig _config;` (it already injects
`DatabaseRegistry` the same way; `EconomyConfig` is registered in `PlanetSceneScope`) and calls
`asteroid.Initialize(def, slotId, _config.AsteroidYieldRollMin, _config.AsteroidYieldRollMax)`.
The yield roll moves into a pure static `Asteroid.RollYield(baseYield, min, max, float random01)`
so it can be unit-tested.

## Client changes

**`Mining/IMineralService.cs`**
- `Task<int> GrantMiningAsync(string mineralId, int qty, float sessionDurationSec, float unitsPerSec)`
  becomes `Task<MiningGrantResult> GrantMiningAsync(string planetId, string mineralId, int qty)`.
- New public DTO `MiningGrantResult { int Granted; string MineralId; string Reason; }` in its own
  file (`Mining/MiningGrantResult.cs`), following the `SellResult` public-DTO pattern. Its shape
  MUST MATCH the `ValidateMining` response.

**`Mining/MineralService.cs`** sends `{ planetId, mineralId, claimedQty }`, adds `Granted` to
`MineralInventory` only when > 0, and returns the result (a null response becomes
`{ Granted = 0, Reason = "Empty response" }`).

**`Mining/LocalMockMineralService.cs`** grants `qty` in full and returns it.

**`Mining/MiningController.cs`** — both `ClaimIdleSessionAsync` and `CompleteActiveMiningAsync`:
- pass `_planet.PlanetId`;
- if `Granted > 0`: behave as today (the idle path publishes `IdleClaimCompletedEvent`);
- else, if `Reason` is set: publish `MiningClaimRejectedEvent { MineralId, Reason }` and skip
  `IdleClaimCompletedEvent`;
- the asteroid is still depleted and put on its respawn timer in every case (unchanged).

**New `Mining/MiningClaimRejectedEvent.cs`** — `{ string MineralId; string Reason; }`.

**New `App/MiningClaimFeedbackHandler.cs`** (`IStartable, IDisposable`, registered with
`RegisterEntryPoint` in `PlanetSceneScope`) — on `MiningClaimRejectedEvent`, logs and publishes
`ServerActionFailedEvent(ServerFailureMessages.For("Mining", reason))`. The existing `ToastView`
(Known Issue #17) shows it.

**`App/ServerFailureMessages.cs`** — new reasons:

| Reason | Text |
|---|---|
| `BUDGET_EXHAUSTED` | "No more asteroids here for now. Try again later." |
| `TIER_TOO_LOW` | "Requires a higher-tier drone" |
| `WRONG_PLANET`, `MINERAL_NOT_ON_PLANET` | "Mining isn't available here right now" |

**Clean-up:** remove `MiningReward.UnitsPerSec` and the "server anti-cheat cap" comment in
`MiningRewardCalculator`.

## Error handling

- **Rejections** (checks 0–3, 5) return `granted: 0` + `reason` → toast; the asteroid is consumed,
  as it is today when a grant fails.
- **Oversized claims** are clamped (check 4), never rejected.
- **Cloud Save conflicts** are retried (3 attempts) for both records; a final failure throws, and
  `MineralService` / `MiningController` handle it as today (logged; asteroid respawns).
- **Grant failure after reserving budget** releases the reservation (commit step 3).

**Rejection logging:** every rejection writes one `logger.warn` line (player, planet, mineral,
reason) — the only server-side signal for false rejections of legit players.

**Client respawn timers are per planet:** `AsteroidSpawner` persists pending respawns as
`{planetId}|{mineralType}|{slotId}|{unixSeconds}` for every planet, but only the loaded planet's
entries reduce, reserve slots in, or respawn into its field, so an asteroid claimed on one planet
never reappears on another (which the server would reject as `MINERAL_NOT_ON_PLANET` or count
against the wrong budget). Legacy 3-part entries are dropped on load (no build writing them
shipped).

**Known edge case (accepted):** respawn timers are local. A player who reinstalls or clears app
data sees a fresh field while the server still counts the earlier claims; mining a 7th asteroid
inside the window returns `BUDGET_EXHAUSTED` and that asteroid's minerals are lost. Fixing this
needs server-side respawn timers (out of scope).

**Dependency (not fixed here):** every record this function reads is still in the
player-writable (default) Cloud Save access class, so a modified client can write them itself
through the Cloud Save SDK:
- `drone_fleet` (checks 3 and 4) — raise its own drone tier and yield multiplier;
- `mining_claim_log` (check 5) — delete or reset it, restoring the full budget at will;
- `current_planet` (checks 1 and 2) — delete or null it; with no record, any `planetId` is
  accepted, so a client can draw on all 10 planets' budgets in parallel. This is also the state of
  every player who has not travelled since `LandTravel` shipped.

`current_planet` and `mining_claim_log` must therefore join `drone_fleet` and `mineral_inventory`
in the access-class migration (Known Issue #16). Until it lands, the planet check, the budget and
the yield cap stop a client that only tampers with its `ValidateMining` requests; they do not stop
a client that also writes its own Cloud Save. (Practically moot for now: such a client can already
write `mineral_inventory` directly.)

## Testing

**EditMode (C#)**
- `MiningCatalogAlignmentTests` (new, replaces `ValidateMiningCapAlignmentTests`): parse
  `ValidateMining.js` and compare `PLANET_MINERALS` (all 10 planets), `MINERALS`, `DRONES`,
  `YIELD_DELTA_PER_LEVEL`, `RESPAWN_HOURS`, `YIELD_ROLL_MAX` with the assets; assert every planet's
  field size ≤ `FIELD_SIZE`.
- `MineralServiceTests`: request carries `planetId`/`mineralId`/`claimedQty` and no duration/rate;
  a rejection returns its reason and leaves `MineralInventory` unchanged.
- `MiningControllerTests`: a rejected claim publishes `MiningClaimRejectedEvent`, not
  `IdleClaimCompletedEvent`, and still schedules the respawn.
- `MiningClaimFeedbackHandlerTests`: event → `ServerActionFailedEvent` with mapped text.
- `ServerFailureFeedbackTests`: new reason cases.
- `AsteroidYieldRollTests` (new): `Asteroid.RollYield` at random01 = 0 and 1 hits the configured
  min/max, so the server's `YIELD_ROLL_MAX` cap always covers a legit roll.

**PlayMode:** `FakeBackendClient`'s `ValidateMining` responder uses the new args; assert the claim
sends the scene's `planetId`.

**Server logic (ad-hoc Node run against a mocked SDK, not committed):** wrong planet; null
`current_planet` accepts the claimed planet; mineral from another planet; tier too low; oversized
claim clamped; 7th claim within 4h rejected and accepted once the oldest ages out; two concurrent
claims racing for the last slot (exactly one wins); budget released when the inventory write fails.

## Rollout

The `ValidateMining` contract changes, so the client build and the `ServerCode/` deploy must ship
together (already required by Known Issue #13). Update `PROGRESS.md`: move Known Issue #16's
`ValidateMining` item to fixed; keep the access-class migration and the reinstall edge case open.
