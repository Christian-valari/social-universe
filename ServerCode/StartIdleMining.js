// StartIdleMining — records the caller's idle mining run on the server, so any device the player
// signs in on can show it (GetIdleMining) and claim it (ValidateMining with idleSlotId ends it).
// Design: docs/superpowers/plans/2026-10-15-server-tracked-idle-mining.md.
//
// Request { planetId, slotId, mineralId, durationSec }. One run per player, in the Protected
// idle_mining_session item:
//   - planetId must equal current_planet when one is recorded. No record: accepted and NOT pinned
//     (ValidateMining's first valid claim pins it, as before).
//   - no run, or a run on another planet → this run replaces it (the old one is abandoned);
//   - a run on this planet → ALREADY_MINING with that run, so the client takes it over — unless
//     replaceSlotId names it: a client that can't place that run's asteroid (e.g. its type left the
//     planet) replaces it instead of being locked out of idle mining here.
// The server clock stamps the start. Readiness is not enforced anywhere: ValidateMining's claim
// budget is the reward bound, and active mining has no start call at all.
// Response: { success, reason?, planetId, slotId, mineralId, elapsedSeconds, durationSeconds } —
// every field MUST exist on IdleMiningSessionResult (Cloud Code deserializes strictly).
const { DataApi } = require("@unity-services/cloud-save-1.4");

const PLANET_KEY       = "current_planet";
const MAX_NAME_LENGTH  = 64;
const MAX_DURATION_SEC = 86400; // sanity bound on the client's duration, not a game rule

/**
 * @param {string} planetId - Planet the asteroid is on (PlanetDefinition._planetId).
 * @param {string} slotId - The asteroid's slot ("{MineralType}#{index}", AsteroidSpawner).
 * @param {string} mineralId - The asteroid's mineral (MineralDefinition._mineralId).
 * @param {number} durationSec - Run length the client computed (MiningRewardCalculator).
 * @param {string} [replaceSlotId] - Slot of a same-planet run the client can't place, to replace.
 */
module.exports = async ({ params, context, logger }) => {
  await requireActiveSession(context, params.sessionId);
  const { planetId, slotId, mineralId, durationSec, replaceSlotId } = params;
  const { projectId, playerId } = context;

  if (!isName(planetId) || !isName(slotId) || !isName(mineralId)
      || typeof durationSec !== "number" || !Number.isFinite(durationSec)
      || durationSec <= 0 || durationSec > MAX_DURATION_SEC
      || (replaceSlotId !== undefined && replaceSlotId !== null && !isName(replaceSlotId))) {
    logger.warn(`StartIdleMining: rejected INVALID_PARAMS for player ${playerId}`);
    return { success: false, reason: "INVALID_PARAMS" };
  }

  const saveApi = new DataApi(context);

  const serverPlanet = await readCurrentPlanet(saveApi, projectId, playerId);
  if (serverPlanet && serverPlanet !== planetId) {
    logger.warn(`StartIdleMining: rejected WRONG_PLANET for player ${playerId} (asked ${planetId}, server ${serverPlanet})`);
    return { success: false, reason: "WRONG_PLANET" };
  }

  const outcome = await updateIdleSession(saveApi, projectId, playerId, current => {
    if (current && current.planetId === planetId && current.slotId !== replaceSlotId)
      return { result: { taken: false, run: current } };
    const run = { planetId, slotId, mineralId, startTs: Date.now(), durationSec };
    return { value: run, result: { taken: true, run, replaced: current } };
  });

  if (outcome === IDLE_CONFLICT) {
    logger.warn(`StartIdleMining: gave up after repeated write conflicts for player ${playerId}`);
    return { success: false, reason: "CONFLICT" };
  }
  if (!outcome.taken) {
    logger.info(`StartIdleMining: player ${playerId} is already mining ${outcome.run.slotId} on ${planetId}`);
    return { success: false, reason: "ALREADY_MINING", ...describe(outcome.run) };
  }
  if (outcome.replaced)
    logger.info(`StartIdleMining: player ${playerId} abandoned ${outcome.replaced.slotId} on ${outcome.replaced.planetId}`);
  logger.info(`StartIdleMining: player ${playerId} mining ${slotId} (${mineralId}) on ${planetId} for ${durationSec}s`);
  return { success: true, ...describe(outcome.run) };
};

// ---- helpers ----

function isName(value) {
  return typeof value === "string" && value.length > 0 && value.length <= MAX_NAME_LENGTH;
}

// The player's recorded planet, or null. A missing record (404, or no result) means none; any other
// read failure throws rather than letting a start through on a planet the server never confirmed.
async function readCurrentPlanet(saveApi, projectId, playerId) {
  try {
    const res = await saveApi.getProtectedItems(projectId, playerId, [PLANET_KEY]);
    return res.data.results.find(r => r.key === PLANET_KEY)?.value?.planetId ?? null;
  } catch (err) {
    if ((err?.response?.status ?? err?.status) !== 404) throw err;
    return null;
  }
}

// The client-facing view of a run. elapsedSeconds is measured on the server clock, so every device
// shows the same progress whatever its own clock says.
function describe(run) {
  return {
    planetId: run.planetId,
    slotId: run.slotId,
    mineralId: run.mineralId,
    elapsedSeconds: Math.max(0, (Date.now() - run.startTs) / 1000),
    durationSeconds: run.durationSec,
  };
}

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

// ---- active session check (copy verbatim into every deployed script; ActiveSessionGuardTests keeps the copies identical) ----
// ClaimSession records the latest sign-in's session in the Protected active_session item. A call
// naming any other session, or none, comes from a replaced or signed-out device. A failed read
// throws rather than letting the call through.
async function requireActiveSession(context, sessionId) {
  const { DataApi } = require("@unity-services/cloud-save-1.4");
  const { projectId, playerId } = context;
  let current;
  try {
    const res = await new DataApi(context).getProtectedItems(projectId, playerId, ["active_session"]);
    current = res.data.results.find(r => r.key === "active_session")?.value?.sessionId;
  } catch (e) {
    if (e?.response?.status !== 404) throw e;
  }
  if (!sessionId || sessionId !== current) throw Error("SESSION_REPLACED");
}
// ---- end active session check

// Parameter declarations for the UGS CLI / Cloud Code (types only: each script validates its own
// inputs and returns reasons the client understands). Must stay after `module.exports = ...`,
// which would otherwise overwrite it. Pinned by ServerCodeDeployTests.
module.exports.params = {
  planetId: "String",
  slotId: "String",
  mineralId: "String",
  durationSec: "Numeric",
  replaceSlotId: "String",
  sessionId: "String"
};
