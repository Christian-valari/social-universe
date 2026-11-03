// GetAsteroidRespawns — which of a planet's asteroid slots the caller emptied in the last
// RESPAWN_HOURS, on any device, so every device shows the same asteroid field. A pure read of the
// Protected mining_claim_log that ValidateMining writes (one { ts, slotId, mineralId } entry per
// granted claim). Design: docs/superpowers/plans/2026-10-18-cloud-synced-planet-data.md.
//
// Response: { success: true, respawns: [{ slotId, mineralId, secondsLeft }] }. Every field MUST exist
// on AsteroidRespawnsResult / AsteroidRespawn. secondsLeft is an integer measured on the server
// clock, so a device whose clock is off still brings the asteroid back on time. Entries without a
// slot (malformed, or bare timestamps logged before slots were recorded) still count toward
// ValidateMining's budget but name no asteroid, so they are left out.
// A missing record (404, or no result) means nothing emptied. Any other read failure throws: the
// client then retries instead of showing slots the server still counts as emptied.
const { DataApi } = require("@unity-services/cloud-save-1.4");

const CLAIM_LOG_KEY = "mining_claim_log";
const RESPAWN_HOURS = 4;    // MUST MATCH ValidateMining.js RESPAWN_HOURS and EconomyConfig._asteroidRespawnHours
const MS_PER_HOUR   = 3600000;

/**
 * @param {string} planetId - Planet whose asteroid field is being loaded (PlanetDefinition._planetId).
 */
module.exports = async ({ params, context, logger }) => {
  await requireActiveSession(context, params.sessionId);
  const { projectId, playerId } = context;

  let log = null;
  try {
    const res = await new DataApi(context).getProtectedItems(projectId, playerId, [CLAIM_LOG_KEY]);
    log = res.data.results.find(r => r.key === CLAIM_LOG_KEY)?.value ?? null;
  } catch (err) {
    if ((err?.response?.status ?? err?.status) !== 404) throw err;
  }

  const entries = log && typeof log === "object" && Array.isArray(log[params.planetId]) ? log[params.planetId] : [];
  const now = Date.now();
  const respawns = entries
    .filter(e => e && typeof e === "object" && typeof e.slotId === "string" && Number.isFinite(e.ts))
    .map(e => ({ slotId: e.slotId, mineralId: e.mineralId ?? null, secondsLeft: Math.ceil((e.ts + RESPAWN_HOURS * MS_PER_HOUR - now) / 1000) }))
    .filter(r => r.secondsLeft > 0);

  logger.info(`GetAsteroidRespawns: player ${playerId} has ${respawns.length} slot(s) respawning on ${params.planetId}`);
  return { success: true, respawns };
};

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

// Parameter declarations for the UGS CLI / Cloud Code (types only). Must stay after
// `module.exports = ...`, which would otherwise overwrite it. Pinned by ServerCodeDeployTests.
module.exports.params = {
  planetId: "String",
  sessionId: "String"
};
