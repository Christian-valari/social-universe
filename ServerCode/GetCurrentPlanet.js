// GetCurrentPlanet — returns the caller's server-recorded current planet, so
// signing in on a different device resumes on the same planet instead of
// always landing back on Earth. Written by LandTravel.js whenever a real
// trip completes; this is a pure read, mirroring GetFuelState.js's shape.
//
// The current_planet record is created lazily on the player's first landing
// (same "created lazily" precedent as fuel_state) — new players and accounts
// that predate this feature simply get planetId: null until their next trip,
// and the client falls back to its own local/default resume logic.
const { DataApi } = require("@unity-services/cloud-save-1.4");

const SAVE_KEY = "current_planet";

/**
 * No parameters.
 */
module.exports = async ({ context, logger }) => {
  const { projectId, playerId } = context;
  const saveApi = new DataApi(context);

  let planetId = null;
  try {
    const res    = await saveApi.getItems(projectId, playerId, [SAVE_KEY]);
    const record = res.data.results.find(r => r.key === SAVE_KEY);
    if (record?.value?.planetId) planetId = record.value.planetId;
  } catch (_) { /* no record yet — new player or pre-feature account */ }

  logger.info(`GetCurrentPlanet: player ${playerId} -> ${planetId ?? "(none saved)"}`);
  return { planetId };
};
