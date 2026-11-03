# Server-tracked idle mining — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** an idle mining run lives in a Protected Cloud Save record, so any device the player signs in on shows it and can claim it.

**Architecture:** two new Cloud Code scripts (`StartIdleMining`, `GetIdleMining`) and an optional `idleSlotId` on `ValidateMining` that ends the run with the claim; a shared locked helper block writes the record. On the client a new `IIdleMiningService` (Mining) is called by `MiningController` to start runs, and by `PlanetSceneBootstrapper` (App) to hydrate them; `AsteroidSpawner.EnsureSlot` puts the run's asteroid back if this device has it waiting to respawn.

**Tech Stack:** Unity 6000.3.12f1, C# (VContainer, NUnit EditMode/PlayMode), UGS Cloud Code JavaScript (`@unity-services/cloud-save-1.4`), Node 22 for a throwaway server simulation.

**Spec:** `docs/superpowers/plans/2026-10-15-server-tracked-idle-mining.md` (approved 2026-10-15).

## Global Constraints

- Every deployed script's handler starts with `await requireActiveSession(context, params.sessionId);`, carries the session block copied verbatim from `ClaimSession.js`, and declares `sessionId: "String"` (`ActiveSessionGuardTests`).
- Every script declares its params in `module.exports.params = {...};` after `module.exports = ...` (`ServerCodeDeployTests`).
- Every read-modify-write passes the `writeLock` it read, re-applies its change on a 409 (3 attempts), and throws on a failed read (404 = missing).
- Server-trusted keys use `getProtectedItems`/`setProtectedItem` only (`ServerCodeAccessClassTests`).
- Every field a script returns must exist on the C# result type; Cloud Code deserializes with `MissingMemberHandling.Error`.
- Namespaces mirror folders: `SocialUniverse.Mining` for `Scripts/Mining/`, `SocialUniverse.App` for `Scripts/App/`. Mining must not reference `Net` or `ICloudSave`.
- No deploy. **No commits** — the working tree holds ~170 uncommitted changes from other sessions; the maintainer commits. Each task ends with a test run instead of a commit.
- Headless Unity runs use a scratch copy on a short path (CLAUDE.md "Running Tests"): copy `Assets/`, `Packages/`, `ProjectSettings/`, `ServerCode/`; drop `com.coplaydev.unity-mcp` from the copy's manifest with `sed`; set the copy's `productName` to `Social Universe Tests`; always pass `-logFile`. Copy the `.meta` files Unity generates for new scripts back into the repo.

## Review Focus

1. **A run on a slot this device has waiting to respawn** — the asteroid must come back, not be silently dropped (Task 4 `EnsureSlot` pending-slot test; Task 5 restore test).
2. **The claim throws after the server accepted the start** — the run must survive on the server and reappear (Task 2 simulation scenario "a throwing grant leaves the run").
3. **A start whose reply arrives after the player left the scene** — no session is created on a destroyed asteroid (Task 5 guard `if (asteroid == null) return false;`, reviewed by reading).
4. **Two starts in flight** — the second must be refused locally (Task 5 in-flight test) and resolved by the lock on the server (Task 2 concurrent-start scenario).
5. **Old build's PlayerPrefs session plus a server run** — the server run wins and the key is deleted (Task 5 test).

---

### Task 1: Server scripts and their drift tests

**Files:**
- Create: `ServerCode/StartIdleMining.js`, `ServerCode/GetIdleMining.js`
- Modify: `ServerCode/ValidateMining.js`, `ServerCode/deploy-list.txt`, `ServerCode/CLOUD_CODE_FUNCTIONS.md`
- Test: `Assets/_Project/Tests/EditMode/Net/PlayerRecordLockTests.cs`, `Assets/_Project/Tests/EditMode/Economy/ServerCodeAccessClassTests.cs`

**Interfaces:**
- Produces: Cloud Code `StartIdleMining { planetId, slotId, mineralId, durationSec, sessionId }` → `{ success, reason?, planetId, slotId, mineralId, elapsedSeconds, durationSeconds }`; `GetIdleMining { sessionId }` → same shape, `{ success: true }` when no run; `ValidateMining` accepts optional `idleSlotId: String`.

- [ ] **Step 1: Failing drift tests.** In `PlayerRecordLockTests` add
  ```csharp
  private static readonly string[] IdleRunWriters = { "StartIdleMining", "ValidateMining" };

  [Test]
  public void Idle_mining_session_is_written_only_through_the_shared_locked_helper() =>
      AssertSharedLockedBlock(IdleRunWriters, "idle_mining_session", @"\.setProtectedItem", @"IDLE_SESSION_KEY|idle_mining_session");
  ```
  and in `ServerCodeAccessClassTests.ProtectedKeys` add `"idle_mining_session"`.
- [ ] **Step 2: Shared helper block** (identical in both writers):
  ```js
  // ---- idle_mining_session access (copy verbatim into every script that writes it; PlayerRecordLockTests keeps the copies identical) ----
  // The player's idle mining run in progress — { planetId, slotId, mineralId, startTs, durationSec } —
  // or { slotId: null, endedTs } once it has ended. Only StartIdleMining and ValidateMining write it.
  // change(run) gets the run in progress (null if none) and returns { value, result }: value is
  // written (undefined = write nothing) and result returned. The write carries the writeLock it read;
  // a 409 re-runs change on a fresh read, 3 attempts, then IDLE_CONFLICT. A missing record (404)
  // reads as no run; any other read failure throws.
  const IDLE_SESSION_KEY = "idle_mining_session";
  const IDLE_CONFLICT    = { conflict: true };

  async function updateIdleSession(saveApi, projectId, playerId, change) {
    for (let attempt = 0; attempt < 3; attempt++) {
      let rec;
      try {
        rec = (await saveApi.getProtectedItems(projectId, playerId, [IDLE_SESSION_KEY])).data.results
          .find(r => r.key === IDLE_SESSION_KEY);
      } catch (err) {
        if ((err?.response?.status ?? err?.status) !== 404) throw err;
      }
      const { value, result } = change(rec?.value?.slotId ? rec.value : null);
      if (value === undefined) return result;
      const item = rec?.writeLock
        ? { key: IDLE_SESSION_KEY, value, writeLock: rec.writeLock }
        : { key: IDLE_SESSION_KEY, value };
      try {
        await saveApi.setProtectedItem(projectId, playerId, item);
        return result;
      } catch (err) {
        if ((err?.response?.status ?? err?.status) !== 409) throw err;
      }
    }
    return IDLE_CONFLICT;
  }
  // ---- end idle_mining_session access
  ```
- [ ] **Step 3: `StartIdleMining.js`** — validates params (`INVALID_PARAMS`: non-empty strings ≤ 64 chars; `durationSec` finite in (0, 86400]); reads `current_planet` (404 = none) → `WRONG_PLANET` if recorded and different; `updateIdleSession` with `run && run.planetId === planetId ? { result: { taken: false, run } } : { value: newRun, result: { taken: true, run: newRun, replaced: run } }`; `IDLE_CONFLICT` → `CONFLICT`; not taken → `{ success: false, reason: "ALREADY_MINING", ...describe(run) }`; taken → `{ success: true, ...describe(newRun) }`. `describe(run)` = `{ planetId, slotId, mineralId, elapsedSeconds: max(0, (Date.now() − startTs) / 1000), durationSeconds: durationSec }`. Logs: `info` on start and replace, `warn` on rejections and give-up. Params: `planetId, slotId, mineralId: "String"`, `durationSec: "Numeric"`, `sessionId: "String"`.
- [ ] **Step 4: `GetIdleMining.js`** — guard; `getProtectedItems(["idle_mining_session"])` (404 = none, other errors throw); a value with `slotId` → `{ success: true, ...describe(run) }`, else `{ success: true }`. Params: `sessionId`.
- [ ] **Step 5: `ValidateMining.js`** — move the existing body into `async function claim({ planetId, mineralId, claimedQty }, context, logger)`; the handler becomes guard → `const result = await claim(params, context, logger);` → if `params.idleSlotId` is a non-empty string, `await endIdleRun(context, logger, params.planetId, params.idleSlotId)` → `return result`. `endIdleRun` uses the helper to write `{ slotId: null, endedTs }` only when the run names that planet and slot; it catches every error and logs `error` (the claim is already answered); `info` when it ends a run. Declare `idleSlotId: "String"`. Header comment documents the new param.
- [ ] **Step 6: Deploy list and reference doc.** Add `GetIdleMining.js` and `StartIdleMining.js` (alphabetical) to `deploy-list.txt`. Add `## StartIdleMining` and `## GetIdleMining` sections to `CLOUD_CODE_FUNCTIONS.md` (after `ReleaseSession`) and an `idleSlotId` row to the `ValidateMining` parameter table.
- [ ] **Step 7: Syntax check** — `node --check` each changed script (the module wrapper is CommonJS, so `node --check` parses it as-is).

### Task 2: Throwaway Node simulation of the server scripts

**Files:** Create (scratchpad only, not in the repo): `<scratchpad>/idle-sim/idle-sim.js`

**Interfaces:** Consumes Task 1's scripts by loading `ServerCode/*.js` with fake `@unity-services/cloud-save-1.4` (protected items with `writeLock`/409, failure injection, 404 injection), a controllable clock and `ClaimSession` for session ids (pattern: the 2026-10-15 consistency simulation).

- [ ] **Step 1: Write scenarios** — (1) start on A → B signs in, `GetIdleMining` after +10 min reports elapsed ≈ 600 s → B claims with `idleSlotId` → granted, run ended → A' (new sign-in) reads none; (2) a second start on the same planet → `ALREADY_MINING` with the first run, record unchanged; (3) a start on another planet replaces the run; (4) `WRONG_PLANET` when `current_planet` differs; accepted and **not pinned** when there is none; (5) `INVALID_PARAMS` for missing slot, zero/negative/non-number/oversized duration; (6) two concurrent starts on one planet with a seeded record → one success, one `ALREADY_MINING`, one run stored; (7) a claim naming an old slot leaves a newer run (another planet) alone; (8) a rejected claim (`BUDGET_EXHAUSTED`) still ends the run; (9) an inventory write failure (500) makes the claim throw, leaves the run and releases the budget slot; (10) the run's end write failing after a grant: claim still returns the grant, run remains, an `error` is logged; (11) `GetIdleMining`: 404 → none, 500 → throws; (12) `StartIdleMining` with a failing run read (500) throws and writes nothing; (13) a claim without `idleSlotId` leaves the run alone; (14) every new script rejects a stale session id.
- [ ] **Step 2: Run** `node idle-sim.js <repo>/ServerCode` — expect all pass.
- [ ] **Step 3: Mutation check** — on a copy of `ServerCode/`, drop the `writeLock` from the helper's write; scenario 6 must fail.

### Task 3: Client service layer

**Files:**
- Create: `Assets/_Project/Scripts/Mining/IdleMiningSessionResult.cs`, `IIdleMiningService.cs`, `IdleMiningService.cs`, `LocalMockIdleMiningService.cs`
- Modify: `Mining/IMineralService.cs`, `Mining/MineralService.cs`, `Mining/LocalMockMineralService.cs`, `App/ServerFailureMessages.cs`
- Modify (test doubles): `Tests/EditMode/Mining/MiningControllerTests.cs` (`CapturingMineralService`), `Tests/EditMode/App/MineralSaleHandlerTests.cs`, `Tests/EditMode/App/ServerFailureFeedbackTests.cs`
- Test: `Tests/EditMode/Mining/IdleMiningServiceTests.cs` (new), `Tests/EditMode/Mining/IdleMiningSessionResultContractTests.cs` (new), `Tests/EditMode/Mining/MineralServiceTests.cs`, `Tests/EditMode/App/ServerFailureFeedbackTests.cs`

**Interfaces:**
- Produces:
  ```csharp
  public class IdleMiningSessionResult { public bool Success; public string Reason; public string PlanetId; public string SlotId;
      public string MineralId; public float ElapsedSeconds; public float DurationSeconds; public bool HasSession => !string.IsNullOrEmpty(SlotId); }
  public interface IIdleMiningService {
      Task<IdleMiningSessionResult> StartAsync(string planetId, string slotId, string mineralId, float durationSeconds); // never throws
      Task<IdleMiningSessionResult> GetAsync(); } // throws on network failure; null reply = no run
  // IMineralService:
  Task<MiningGrantResult> GrantMiningAsync(string planetId, string mineralId, int qty, string idleSlotId = null);
  ```

- [ ] **Step 1: Failing tests.**
  - `IdleMiningServiceTests`: Start sends `StartIdleMining` with `planetId/slotId/mineralId/durationSec` and returns the reply; a null reply → `Success=false, Reason="Empty response"`; a throw → `Reason="Network error"`; Get calls `GetIdleMining` and a null reply → `Success=true, HasSession=false`; Get rethrows.
  - `IdleMiningSessionResultContractTests` (Newtonsoft, `MissingMemberHandling.Error`): `{"success":true,"planetId":"earth","slotId":"Iron#0","mineralId":"iron","elapsedSeconds":0,"durationSeconds":240}`; `{"success":false,"reason":"ALREADY_MINING","planetId":"earth","slotId":"Iron#1","mineralId":"iron","elapsedSeconds":612.345,"durationSeconds":240}`; `{"success":false,"reason":"WRONG_PLANET"}`; `{"success":true}`.
  - `MineralServiceTests`: `idleSlotId` is in the `ValidateMining` args only when given.
  - `ServerFailureFeedbackTests`: `[TestCase("ALREADY_MINING", "Your drone is already mining here")]` in the Mining cases.
- [ ] **Step 2: Implement** the four new files, the optional `idleSlotId` (sent as `"idleSlotId"` only when non-empty), the three test doubles' signatures, and the `ALREADY_MINING` message. `IdleMiningService.StartAsync` logs `SULog.Warn` on a throw.
- [ ] **Step 3: Run** these test classes on the scratch copy (Task 7 setup) or defer to the full run.

### Task 4: `AsteroidSpawner.EnsureSlot`

**Files:** Modify `Assets/_Project/Scripts/Mining/AsteroidSpawner.cs`; Test `Assets/_Project/Tests/EditMode/Mining/AsteroidSpawnerEnsureSlotTests.cs` (new)

**Interfaces:** Produces `public Asteroid EnsureSlot(string slotId)` — the live asteroid in `slotId`; otherwise, if the slot's type (`slotId` prefix before `#` = `AsteroidDefinition.MineralType`) is one of the current planet's `AsteroidTypes`, drops this planet's pending respawn for that slot, spawns it and saves the timers; otherwise null. `SpawnForPlanet` records `planet.AsteroidTypes`; `SpawnOne` returns the asteroid it spawned.

- [ ] **Step 1: Failing tests** (planet with one `Iron` type, field size 2, `_config` with roll 1/1 injected by reflection, `SaveKeys.AsteroidRespawns` cleared in SetUp/TearDown): a live slot returns the same instance and spawns nothing; a slot claimed with `ScheduleRespawn` comes back from `EnsureSlot("Iron#1")` with `NextRespawnUtc == null` and the persisted timers no longer naming `Iron#1`; `EnsureSlot("Gold#0")` and `EnsureSlot("nonsense")` return null.
- [ ] **Step 2: Implement** as specified.

### Task 5: `MiningController`, `IdleMiningSession`, prompt view and scope wiring

**Files:**
- Modify: `Mining/IdleMiningSession.cs`, `Mining/MiningController.cs`, `UI/MiningModePromptView.cs`, `App/PlanetSceneScope.cs`
- Test: `Tests/EditMode/Mining/MiningControllerTests.cs`

**Interfaces:**
- Consumes: Task 3 (`IIdleMiningService`, `IdleMiningSessionResult`, `GrantMiningAsync(..., idleSlotId)`), Task 4 (`EnsureSlot`).
- Produces: `IdleMiningSession(Asteroid, DateTime startUtc, float durationSeconds, bool restored = false, bool serverTracked = false)` with `bool IsServerTracked`; `MiningController(..., DroneFleet fleet, IIdleMiningService idle)`; `Task<bool> BeginIdleMiningAsync(Asteroid)` (replaces `BeginIdleMining`); `void Initialize(IdleMiningSessionResult serverRun = null)`.

- [ ] **Step 1: Failing tests** (a `FakeIdleMiningService` echoing starts, with optional canned reply and a pending `TaskCompletionSource`; `CapturingMineralService.LastIdleSlotId`):
  - start sends planet, slot, mineral and duration and yields a server-tracked session; the PlayerPrefs key is not written;
  - a rejected start (`WRONG_PLANET`) creates no session and publishes `MiningClaimRejectedEvent` with that reason;
  - `ALREADY_MINING` naming another live slot here takes that run over (server-anchored: elapsed 600 s of 60 s → `ReadyToClaim`), no rejection event, returns false;
  - while a start is in flight a second start and `BeginActiveMining` are refused and only one server call is made; completing it starts the session;
  - `Initialize(serverRun here)` restores it (`WasRestored`, `IsServerTracked`, stage from server elapsed: 600/60 → ready; 10/60 → not ready);
  - a server run here wins over a legacy PlayerPrefs session, which is deleted;
  - a server run on another planet is ignored and a legacy session here restores (not server-tracked);
  - a server run here whose slot can't be placed shows nothing and does not fall back to the legacy session;
  - claiming a server-tracked run sends `idleSlotId`; claiming a legacy one sends none.
  - Existing tests move to `await BeginIdleMiningAsync(...)`.
- [ ] **Step 2: Implement.** `MiningController`: `_startingIdle` flag (set around the awaited start; `BeginActiveMining` also refuses while set); success → `SetSession(new IdleMiningSession(asteroid, UtcNow − elapsed, serverDuration > 0 ? serverDuration : localDuration, serverTracked: true))`; after the await, `if (asteroid == null) return false;`; `ALREADY_MINING` with a run here → `AdoptServerRun(res, restored: false)`; anything else → `MiningClaimRejectedEvent`. `Initialize` → `TryRestoreServerRun(serverRun)` (run on this planet: delete the legacy key, `AdoptServerRun(restored: true)`, warn if unplaceable, return true) else legacy restore (unchanged parsing; `serverTracked: false`). Remove `PersistIdleSession`. Claim passes `session.IsServerTracked ? asteroid.SlotId : null`.
  `MiningModePromptView.OnIdleMineClicked` → `_ = _mining.BeginIdleMiningAsync(_pendingAsteroid);`.
  `PlanetSceneScope`: register `LocalMockIdleMiningService` (standalone) / `IdleMiningService`; `PlanetSceneBootstrapper` injects `IIdleMiningService`, fetches the run after `HydrateServerStateAsync` (warn and pass null on failure, `info` line naming the run) and calls `_miningController.Initialize(run)`.

### Task 6: PlayMode

**Files:** Modify `Assets/_Project/Tests/PlayMode/PlanetSceneFlowTests.cs`

- [ ] **Step 1:** In SetUp script `StartIdleMining` to echo the request as a successful run (JsonUtility field names: `Success`, `PlanetId`, `SlotId`, `MineralId`, `ElapsedSeconds`, `DurationSeconds`; format the float with `CultureInfo.InvariantCulture`). `GetIdleMining` stays unscripted (null → no run).
- [ ] **Step 2:** Replace each `Assert.IsTrue(_mining.BeginIdleMining(x), msg)` with `yield return StartIdle(x);`, a coroutine that waits for `BeginIdleMiningAsync` and asserts its result. The idle-claim test also asserts `call.Args["idleSlotId"] == asteroid.SlotId`.

### Task 7: Full verification and docs

- [ ] **Step 1:** Scratch copy (see Global Constraints), import, run EditMode then PlayMode headless with `-logFile`. Expected: all pass (baseline before this change: EditMode 504, PlayMode 12).
- [ ] **Step 2:** Mutation checks: a claim that never sends `idleSlotId` must fail the controller claim test; the server's helper without the lock must fail Task 2 scenario 6.
- [ ] **Step 3:** Copy generated `.meta` files for new `.cs` files back into the repo.
- [ ] **Step 4:** Update `PROGRESS.md` (Known Issue #21 "still open" note, the `MiningController` row, the deploy item in Future Tasks, test totals, header) and the spec's status/results.
