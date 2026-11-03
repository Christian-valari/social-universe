// GetCurrentPlanet — where the caller should resume at sign-in, so a different
// device picks up exactly where the last one left off: the server-recorded
// current planet (written by LandTravel.js on every landing) and the target of
// any trip not landed yet (travel_state, from StartTravel.js on any device —
// in flight or arrived). A pure read.
//
// Missing records are normal (created lazily: new players and accounts that
// predate the feature get nulls). A failed read throws instead of answering
// "no trip": the client then falls back to its local hints, rather than
// clearing them on the strength of an answer the server never gave.
const { DataApi } = require("@unity-services/cloud-save-1.4");

const SAVE_KEY   = "current_planet";
const TRAVEL_KEY = "travel_state";

/**
 * No parameters.
 */
module.exports = async ({ params, context, logger }) => {
  await requireActiveSession(context, params.sessionId);
  const { projectId, playerId } = context;
  const saveApi = new DataApi(context);

  let records = [];
  try {
    records = (await saveApi.getProtectedItems(projectId, playerId, [SAVE_KEY, TRAVEL_KEY])).data.results;
  } catch (err) {
    if ((err?.response?.status ?? err?.status) !== 404) throw err;
  }
  const planetId       = records.find(r => r.key === SAVE_KEY)?.value?.planetId ?? null;
  const travelTargetId = records.find(r => r.key === TRAVEL_KEY)?.value?.targetPlanetId ?? null;

  logger.info(`GetCurrentPlanet: player ${playerId} -> ${planetId ?? "(none saved)"}, trip ${travelTargetId ?? "(none)"}`);
  return { planetId, travelTargetId };
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
