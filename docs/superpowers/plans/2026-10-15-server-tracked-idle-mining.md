# Server-tracked idle mining sessions

**Goal:** an idle mining run started on one device shows up on any other device the player signs
in on (same planet, same asteroid, correct progress) and can be claimed there. A run is no longer
lost when the device that started it never comes back.

**Status (2026-10-15):** design approved (approach A); implemented and tested locally (Known Issue
#22, plan: [implementation plan](2026-10-15-server-tracked-idle-mining-implementation.md)). **Deployed to `development` 2026-10-15 (not yet smoke-tested); not committed.** This is the follow-up named in
[cross-device data consistency](2026-10-15-cross-device-data-consistency.md) (Known Issue #21) and
builds on [single active session](2026-10-15-single-active-session.md) (Known Issue #20); all three
ship together.

**Scope:** M6 (drones & mining depth) is complete; this is a cross-device follow-up, not M7 work. It
touches no open decision (age policy, land resale).

## Current flow (what exists today)

- **Start** (`MiningController.BeginIdleMining`): client-only. Checks the asteroid and drone tier,
  computes the duration with `MiningRewardCalculator` (30–1800 s, shortened by drone speed), and
  writes `planetId|slotId|startUtc|duration` to the device-global PlayerPrefs key
  `idle_mining_session`. No server call.
- **Restore** (`MiningController.Initialize`, after `HydrateServerStateAsync`): reads that key. A
  session for another planet, or whose slot no longer resolves, is deleted. **So travelling
  silently drops the run**, and any other device never sees it.
- **Claim** (`ClaimIdleSessionAsync`): tears the session down locally, depletes the asteroid,
  calls `ValidateMining { planetId, mineralId, claimedQty }`, and schedules the respawn whatever the
  outcome (on a throw the haul is lost).
- **Asteroid fields are rebuilt on every Planet load.** Slot ids (`{MineralType}#{index}`, e.g.
  `Iron#0`) come from a deterministic distribution, so every device has the same ones. But yields
  are re-rolled per load (`Asteroid.RollYield`), and respawn timers are per device (PlayerPrefs
  `AsteroidRespawns`). So device B may have the run's slot waiting to respawn.
- **Reward bounds:** `ValidateMining` checks planet, mineral, drone tier and a budget of 6 claims
  per planet per rolling 4 h, and clamps the grant. The
  [claim-budget design](../specs/2026-09-29-mining-claim-budget-design.md) decided that a session
  record adds no security over the budget: active mining is decided on the client and has no start
  call. That still holds, so this change is about **state**, not anti-cheat.

## Approaches considered

| | Approach | Verdict |
|---|---|---|
| **A** | Protected record + `StartIdleMining` + `GetIdleMining`; `ValidateMining` gains an optional `idleSlotId` that ends the run with the claim. Progress is anchored to the server clock. The budget stays the only reward bound. | **Chosen** |
| B | Same record, but the client reads it straight from Cloud Save and computes progress from the device clock. | One fewer function, but cross-device progress depends on both devices' clocks |
| C | Fully server-run: the server computes duration and quantity, enforces readiness, and every idle claim needs a run. | Needs the idle tunables mirrored in JS and a "not ready" path that keeps the asteroid alive; adds no security while active claims bypass it |

## Design

### Server record

Protected Cloud Save item **`idle_mining_session`**, one per player, written only by Cloud Code:

```
{ planetId, slotId, mineralId, startTs, durationSec }   // run in progress (startTs = server ms)
{ slotId: null, endedTs }                              // no run (ended or replaced)
```

- An ended run is overwritten, not deleted (no delete precedent; same as `travel_state`).
- **No drone id.** Both the client's quantity and the server's cap use whichever drone is active at
  claim time, so nothing would read it.
- **Not readiness-enforced.** `startTs` and `durationSec` drive what every device shows; the claim
  does not check them (approach C's cost, no security gain).

### Server functions

All three start with `await requireActiveSession(context, params.sessionId);`, carry the shared
session block and declare `sessionId` (`ActiveSessionGuardTests`).

**`StartIdleMining { planetId, slotId, mineralId, durationSec }`** (new)

| Step | Outcome |
|---|---|
| `planetId`, `slotId`, `mineralId` non-empty strings; `durationSec` a finite number in (0, 86400] (a sanity bound, not a game rule) | else `INVALID_PARAMS` |
| `current_planet` recorded and ≠ `planetId` | `WRONG_PLANET`. No record → accepted; the planet is **not** pinned here (the claim pins it, as today) |
| Read the run (with its `writeLock`): none, or one on **another planet** | Write the new run under the lock. The old one is abandoned and logged (`info`). Travelling no longer drops a run; starting another one elsewhere does |
| A run on the **same planet** | `ALREADY_MINING`, returning that run's fields so the client takes it over. Nothing is written |
| …unless optional `replaceSlotId` names that run's slot | The run is replaced. A client sends it only when it can't place that run's asteroid (its type isn't on the planet any more), which would otherwise answer `ALREADY_MINING` forever and lock idle mining on that planet (added after the final review) |
| Write → 409 | Re-read and decide again; 3 attempts, then `CONFLICT` |

Response (every field on `IdleMiningSessionResult`):
`{ success, reason?, planetId, slotId, mineralId, elapsedSeconds, durationSeconds }`. Success
returns `elapsedSeconds: 0`.

**`GetIdleMining {}`** (new, read-only): `{ success: true, planetId, slotId, mineralId,
elapsedSeconds, durationSeconds }` for a run in progress, `{ success: true }` for none.
`elapsedSeconds = (Date.now() − startTs) / 1000` on the server clock. A 404 means none; any other
read failure throws. Named `Get*`, so `BackendRetryPolicy` retries it on an ambiguous failure.

**`ValidateMining`** gains an optional **`idleSlotId`** (declared `String`). Its checks, budget and
grant are unchanged. After the claim **returns** (a grant or any rejection) and `idleSlotId` is set,
it ends the run if the run still names this `planetId` and `idleSlotId` — under the record's
`writeLock`, re-checked on a 409. Then:

- **Claim throws** (Cloud Save or Economy failure): the run is left in place, so it reappears ready
  on the next Planet load and the haul can be claimed again. Today it is simply lost.
- **Ending fails after a grant**: logged `error`, the claim still succeeds. Worst case the run shows
  again and can be claimed once more, still inside the budget.
- **A newer run** (another planet) is never touched by an old claim.

**Shared helper:** `StartIdleMining` and `ValidateMining` read and write the record only through an
`idle_mining_session access` block copied verbatim into both. It has exactly one write, which
carries the lock it read and retries on a 409, and a failed read other than 404 throws.
`PlayerRecordLockTests` gains a case for it, like `drone_fleet` and `owned_tiles`.

### Interactions

- **Claim budget:** unchanged. A start reserves nothing; the budget is consumed only at claim time,
  for idle and active claims alike.
- **`current_planet`:** a start must match it when it is recorded; claims check it as today, and the
  first valid claim still pins it. `GetCurrentPlanet` routing (#21) already puts a signing-in device
  on the server's planet, which is normally the run's planet.
- **Travel:** neither ends nor blocks a run. On another planet the run is ignored (not shown);
  back on its planet it is restored. `StartTravel`/`LandTravel` don't change.
- **Single active session (#20):** only one device can call these at a time, so cross-device races
  need a takeover in the middle of a request; the locks cover that.

### Client

All in `SocialUniverse.Mining` unless noted.

- **`IIdleMiningService`** — `StartAsync(planetId, slotId, mineralId, durationSeconds)` and
  `GetAsync()`, both returning `IdleMiningSessionResult` (public DTO in its own file:
  `Success, Reason, PlanetId, SlotId, MineralId, ElapsedSeconds, DurationSeconds`, plus a computed
  `HasSession`).
  - `IdleMiningService` (real): `IBackendClient`, like `MineralService`. `StartAsync` turns an
    exception into `Reason = "Network error"` and a null reply into `"Empty response"`; `GetAsync`
    lets exceptions through (the caller logs them) and treats null as "no run".
  - `LocalMockIdleMiningService` (standalone Planet scene only): `StartAsync` always succeeds,
    `GetAsync` returns no run. Runs don't survive a reload there.
- **`IdleMiningSession`** gains `IsServerTracked`.
- **`MiningController`** (takes `IIdleMiningService`):
  - `BeginIdleMining` → **`Task<bool> BeginIdleMiningAsync`**. The local checks are unchanged. Then
    it **awaits** the server start (not optimistic); an in-flight flag refuses a second start and
    `BeginActiveMining` meanwhile.
  - Success → a server-tracked session with `StartUtc = DateTime.UtcNow − ElapsedSeconds` and the
    server's duration (anchored on arrival, so device clock offsets don't matter).
  - `ALREADY_MINING` with a run on this planet → take over that run (see restore), even if it is on
    another asteroid. If that run can't be placed, start again once with `replaceSlotId` = its slot.
  - A start is refused while a claim is in flight (`ClaimingAsteroid`): until `ValidateMining`
    answers, the server still holds the run being claimed.
  - Any other failure → no session; `MiningClaimRejectedEvent { MineralId, Reason }`, which
    `MiningClaimFeedbackHandler` already turns into a toast (`CONFLICT`, `WRONG_PLANET` and network
    errors already have text).
  - **`Initialize(IdleMiningSessionResult serverRun = null)`**:
    0. Active-mining finalize first (it may consume the asteroid the run is on, which step 1 then
       brings back; possible only after a failed `GetIdleMining`).
    1. A server run on this planet → `AsteroidSpawner.EnsureSlot(slotId)` → a restored
       (`WasRestored`, the drone snaps there), server-tracked session anchored as above. The
       legacy PlayerPrefs key is deleted (the server wins). If the slot can't be resolved (its
       asteroid type isn't on this planet), log a warning and show nothing — no legacy fallback;
       the run stays on the server until the next start replaces it (`replaceSlotId`).
    2. Otherwise (no run, a run on another planet, or the read failed) → the legacy PlayerPrefs
       restore below.
  - **Claim:** unchanged flow; a server-tracked session sends `idleSlotId = asteroid.SlotId`.
- **`AsteroidSpawner.EnsureSlot(slotId)`** — returns the live asteroid in that slot; otherwise, if
  the slot's asteroid type belongs to the current planet, drops this device's pending respawn for
  that slot (if any), spawns it now and saves the timers. Returns null for a type not on this
  planet. The field size stays exact: a pending slot becomes live.
- **`IMineralService.GrantMiningAsync`** gains an optional `idleSlotId` (sent only when set).
- **`MiningModePromptView`** starts the run fire-and-forget (`_ = BeginIdleMiningAsync(...)`).
- **`App/PlanetSceneScope`**: registers the service (mock when standalone, as for minerals), and
  `HydrateServerStateAsync` calls `GetAsync()` (a failure is logged, not fatal) and passes the
  result to `MiningController.Initialize`.

### Backward compatibility (sessions already in PlayerPrefs)

- New builds **never write** the PlayerPrefs key; they only read and delete it.
- With no server run on this planet (or a failed read), a legacy `planet|slot|start|duration`
  value is restored exactly as today, as a **local** session (`IsServerTracked = false`). It is
  claimed the plain way (no `idleSlotId`) and the key is deleted. Such a run stays single-device,
  as today.
- With a server run on this planet, the server's run wins and the key is deleted.
- A legacy run for another planet is deleted, as today.
- No server migration: the record is created by the first start.

## Behaviour matrix

| Case | Result |
|---|---|
| Run started on A; the player signs in on B | B lands on the server planet (#21), loads the run, the drone sits at the asteroid with server-anchored progress; B claims it. A is signed out (#20) |
| B has that slot waiting to respawn | `EnsureSlot` spawns it now and drops B's stale timer |
| A never comes back | The run stays on the server until claimed or replaced |
| Claimed on B, A signs in later | The server has no run; nothing is shown on A |
| Player travels with a run in progress | The run stays; ignored on other planets, restored on its own. Starting a run elsewhere replaces it |
| The hydration read fails | No run shown; tapping Idle Mine returns `ALREADY_MINING` with the run, which is taken over |
| The claim throws | The run stays and reappears ready on the next load |
| The claim is rejected (`BUDGET_EXHAUSTED`, …) | The run ends; the asteroid is consumed and a toast shows, as today |
| Two starts at once | One writes; the other re-reads: `ALREADY_MINING` (same planet) or replaces (other planet) |
| Old build's PlayerPrefs session | Restored and claimed locally on that device; a server run wins |

## Diagnostics

- Server: `info` on start, replace and end; `warn` on rejected starts and when a start gives up
  after write conflicts (the shared helper retries silently); `error` when ending a run fails or
  gives up after a grant.
- Client: one `info` line at Planet load naming the server run (or none / legacy / read failure)
  and how it was attached (live slot or respawned).

## Limitations / follow-ups (not in this change)

- **Readiness isn't enforced server-side**, by design (see approaches).
- **Yields still re-roll per load** (unchanged), so the same run can pay up to ±20 % differently on
  another device; the server clamp is unchanged.
- **Respawn timers stay device-local**; `EnsureSlot` overrides one only for the slot the server run
  is on.
- **Cloud Save has no create-only write**, so a player's very first run write carries no lock (same
  as #19/#21).
- **Dev mode** (`RootLifetimeScope._devMode`, whose `LocalMockBackendClient` answers null to every
  call): idle starts now fail with "Couldn't reach the server", like every other server action
  there (claims already did).
- **Cost:** one extra Cloud Code call per Planet load and per start.
- Follow-ups: a budget check at start, so a 30-minute run can't end in `BUDGET_EXHAUSTED`; a "drone
  busy on Earth" hint on other planets; server-side respawn timers.

## Testing plan

- **EditMode (C#)**
  - `MiningControllerTests`: start sends planet, slot, mineral and duration and creates a
    server-tracked session only on success; a rejected or failed start creates none and publishes
    `MiningClaimRejectedEvent`; `ALREADY_MINING` takes over the returned run; a start in flight
    refuses another start and active mining; `Initialize` with a run here restores it (stage from
    server elapsed) and deletes the legacy key; a run on another planet is ignored; a run on a slot
    waiting to respawn spawns it; a legacy session restores when the server has none and is claimed
    without `idleSlotId`; a server-tracked claim sends `idleSlotId`. Existing tests move to
    `BeginIdleMiningAsync`.
  - `IdleMiningServiceTests` (new): request arguments; reply mapping; network error and null reply.
  - `AsteroidSpawnerEnsureSlotTests` (new): live slot returned; pending slot respawned and its timer
    dropped; other planet's type refused.
  - `MineralServiceTests`: `idleSlotId` sent only when set.
  - Response contracts for both functions' reply shapes (`MissingMemberHandling.Error`).
  - Drift: `PlayerRecordLockTests` (shared `idle_mining_session access` block, one locked write);
    `ServerCodeAccessClassTests` (`idle_mining_session` is Protected). `ServerCodeDeployTests` and
    `ActiveSessionGuardTests` cover deploy list, params and the session guard automatically.
- **PlayMode:** `FakeBackendClient` gains `StartIdleMining`/`GetIdleMining` responders;
  `PlanetSceneFlowTests` moves to the async start.
- **Server scripts:** a throwaway Node simulation in the session scratchpad (real scripts against a
  fake Cloud Save with `writeLock`/409 and a controllable clock): start on A → read on B (elapsed) →
  claim on B ends it → A reads none; replace on another planet; `ALREADY_MINING`; concurrent starts;
  a stale claim leaves a newer run; a throwing grant leaves the run; ending fails after a grant; a
  failed read throws.
- Then the full EditMode and PlayMode suites on a scratch copy.

## Deploy

- `StartIdleMining.js` and `GetIdleMining.js` join `deploy-list.txt` (32 → 34 scripts);
  `ValidateMining.js` changes. Ship them with Known Issues #20/#21 and a client built from the same
  commit. Older clients don't call the new functions, and they already fail under #20.
- No data migration.

## Checklist

- [x] Map today's idle flow, asteroid field identity and the claim path.
- [x] Choose the server record, functions and conflict policy (approach A).
- [x] Server: `StartIdleMining`, `GetIdleMining`, `ValidateMining` `idleSlotId`, shared helper, deploy list.
- [x] Client: service + DTO + mock, `IdleMiningSession.IsServerTracked`, `MiningController` start/restore/claim, `AsteroidSpawner.EnsureSlot`, `GrantMiningAsync` `idleSlotId`, prompt view, scope registration and hydration.
- [x] Legacy PlayerPrefs sessions restored and claimed locally; server wins.
- [x] EditMode tests, PlayMode responders, Node simulation.
- [x] Full EditMode + PlayMode on a scratch copy.
- [x] `PROGRESS.md` updated (Known Issue #22).

## Results (2026-10-15)

- **Node simulation** (throwaway, in the session scratchpad): the real `StartIdleMining`,
  `GetIdleMining`, `ValidateMining` and `ClaimSession` against a fake Cloud Save with
  `writeLock`/409 behaviour, failure injection and a controllable clock. **18/18 scenarios pass**
  (stable over 40 runs). They cover:
  - a run started on A, read on B with server-clock elapsed time, claimed on B, and gone for A;
  - `ALREADY_MINING`, replacement on another planet, and `WRONG_PLANET` (a start never pins);
  - `INVALID_PARAMS`, concurrent starts, a stale claim leaving a newer run alone;
  - a rejected claim ending the run, a throwing claim leaving it (budget released);
  - a failed end after a grant, 404/500 reads, the session guard, 409 retry and give-up;
  - `replaceSlotId` replacing a same-planet run only when it names that run's slot.

  Every reply's keys are checked against its C# type. Deliberate breakages are caught: an unlocked
  helper write, a claim that ends any run, and an extra reply field.
- **Unity 6000.3.12f1 headless** on a scratch copy: EditMode **544/544** (504 before, 40 new),
  PlayMode **12/12**. Five deliberate client breakages each fail exactly their intended tests: a
  claim without `idleSlotId`, `EnsureSlot` keeping the stale timer, adopting another planet's run,
  active mining during an in-flight start, and a renamed start argument.
- **Final review** (fresh reviewer): "with fixes", 0 critical. Fixed, each with a test seen failing
  first: a start sent while a claim is in flight is now refused (it could collide with the run being
  claimed); a same-planet run this device can't place is replaced (`replaceSlotId`) instead of
  locking idle mining there; active mining is finalized before the idle run is restored, so the run
  never points at an asteroid that result consumed. The review's minors were fixed the same day,
  each with a test seen failing first (or a mutation check for behaviour that already held):
  - the prompt shows the drone as busy while a start or claim is with the server;
  - the run is fetched alongside the rest of Planet hydration;
  - fire-and-forget start and claim calls log failures (`TaskExtensions.Forget`);
  - a server-tracked claim worth nothing still reaches `ValidateMining`, so the server ends the run;
  - controller tests cover the scene being left mid-start, a run on a slot waiting to respawn, and a
    failed replace.

  Final totals: EditMode **556/556**, PlayMode **13/13**, simulation 19/19.
- **Not covered by automated tests:** the start reply arriving after the Planet scene unloaded
  (guarded by a destroyed-asteroid check, reviewed by reading), anything on a device, and the live
  UGS service (`writeLock` on Protected items is used the same way by #16/#21 but unverified live).
