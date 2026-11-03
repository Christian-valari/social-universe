// LandTravel — consumes the caller's in-progress trip once it has arrived, so
// it can't be landed twice. The actual scene transition is client-driven (per
// this codebase's convention — scene transitions were never server-authoritative,
// only economy state is); this function's job is just the trip record's
// lifecycle + a final server-side arrival check the client can't bypass.
//
// No deleteItem precedent exists elsewhere in this codebase's Cloud Code
// functions (only in-object `delete` on JS objects inside a single record, see
// SellLand.js), so this overwrites the record with a null sentinel rather than
// removing the key outright — if the installed @unity-services/cloud-save-1.4
// DataApi does expose a delete call, swapping this for a real delete is a safe
// follow-up.
const { DataApi } = require("@unity-services/cloud-save-1.4");

const TRAVEL_KEY  = "travel_state";
const PLANET_KEY  = "current_planet";

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
    return { success: false, reason: "no_trip" };
  }
  if (trip.arrivalTs > Date.now()) {
    logger.warn(`LandTravel: player ${playerId} tried to land before arrival (${new Date(trip.arrivalTs).toISOString()})`);
    return { success: false, reason: "not_arrived" };
  }

  const targetPlanetId = trip.targetPlanetId;

  // Record the new current planet BEFORE consuming the trip. current_planet is both the
  // cross-device resume hint (GetCurrentPlanet.js) and what ValidateMining checks mining claims
  // against, so landing without it would reject every claim on the new planet (WRONG_PLANET).
  // On failure the trip stays intact and the call returns a plain failure (not a throw), so the
  // client's Land button re-enables; landing again is idempotent.
  try {
    await saveApi.setProtectedItem(projectId, playerId, {
      key: PLANET_KEY, value: { planetId: targetPlanetId, updatedTs: Date.now() }
    });
  } catch (err) {
    logger.error(`LandTravel: current_planet write failed for ${playerId}; trip kept: ${err?.message}`);
    return { success: false, reason: "save_failed" };
  }

  await saveApi.setProtectedItem(projectId, playerId, { key: TRAVEL_KEY, value: { targetPlanetId: null } });

  logger.info(`LandTravel: player ${playerId} landed on ${targetPlanetId}`);
  return { success: true, traveling: false, targetPlanetId };
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
