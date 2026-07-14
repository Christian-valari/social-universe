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
module.exports = async ({ context, logger }) => {
  const { projectId, playerId } = context;
  const saveApi = new DataApi(context);

  let trip = null;
  try {
    const res    = await saveApi.getItems(projectId, playerId, [TRAVEL_KEY]);
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
  await saveApi.setItem(projectId, playerId, { key: TRAVEL_KEY, value: { targetPlanetId: null } });

  // Cross-device resume hint (see GetCurrentPlanet.js) — best-effort. The trip
  // itself already completed successfully above; a failure here only means
  // GetCurrentPlanet keeps returning the previous value until this player's
  // next real landing, so it must not fail the whole call.
  try {
    await saveApi.setItem(projectId, playerId, {
      key: PLANET_KEY, value: { planetId: targetPlanetId, updatedTs: Date.now() }
    });
  } catch (err) {
    logger.warn(`LandTravel: current_planet write failed for ${playerId}: ${err?.message}`);
  }

  logger.info(`LandTravel: player ${playerId} landed on ${targetPlanetId}`);
  return { success: true, traveling: false, targetPlanetId };
};
