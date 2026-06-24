// GetTravelState — pure read of the caller's in-progress trip, if any. Called
// on Hub/SolarSystem entry so the Traveling panel can resume correctly even if
// the player backgrounded the app or returned to Hub from elsewhere.
const { DataApi } = require("@unity-services/cloud-save-1.4");

const TRAVEL_KEY = "travel_state";

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
