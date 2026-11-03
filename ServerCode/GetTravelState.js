// GetTravelState — pure read of the caller's in-progress trip, if any. Called
// on Hub/SolarSystem entry so the Traveling panel can resume correctly even if
// the player backgrounded the app or returned to Hub from elsewhere.
const { DataApi } = require("@unity-services/cloud-save-1.4");

const TRAVEL_KEY = "travel_state";

/**
 * No parameters.
 */
module.exports = async ({ params, context, logger }) => {
  await requireActiveSession(context, params.sessionId);
  const { projectId, playerId } = context;
  const saveApi = new DataApi(context);

  let trip = null;
  try {
    const res    = await saveApi.getProtectedItems(projectId, playerId, [TRAVEL_KEY]);
    const record = res.data.results.find(r => r.key === TRAVEL_KEY);
    if (record?.value) trip = record.value;
  } catch (_) { /* no record */ }

  if (!trip || trip.targetPlanetId == null) {
    return { success: true, traveling: false };
  }

  logger.info(`GetTravelState: player ${playerId} traveling to ${trip.targetPlanetId}, arrives ${new Date(trip.arrivalTs).toISOString()}`);
  return {
    success: true,
    traveling: true,
    targetPlanetId: trip.targetPlanetId,
    arrivalTs: trip.arrivalTs,
  };
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

// Parameter declarations for the UGS CLI / Cloud Code (types only: each script validates its own
// inputs and returns reasons the client understands). Must stay after `module.exports = ...`,
// which would otherwise overwrite it. Pinned by ServerCodeDeployTests.
module.exports.params = {
  sessionId: "String"
};
