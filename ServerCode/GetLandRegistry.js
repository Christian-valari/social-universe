// GetLandRegistry — returns the global tile-ownership map for a planet so a
// client can render other players' tiles as "owned by other".
// Reads from Cloud Save Custom Data (shared across all players, keyed by
// planet rather than by player) — written by PurchaseLand (and future
// SellLand). Returns an empty map if the registry doesn't exist yet.
const { DataApi } = require("@unity-services/cloud-save-1.4");

const REGISTRY_KEY = "land_registry";

/**
 * @param {string} planetId - ID of the planet to fetch the land registry for.
 */
module.exports = async ({ params, context, logger }) => {
  const { planetId } = params;

  if (!planetId) {
    throw new Error("Invalid params: planetId is required");
  }

  const { projectId } = context;
  const customDataApi = new DataApi(context);
  const customId = planetId.toLowerCase();

  try {
    const res  = await customDataApi.getCustomItems(projectId, customId, [REGISTRY_KEY]);
    const item = res.data.results.find(r => r.key === REGISTRY_KEY);
    return { tiles: item?.value ?? {} };
  } catch (err) {
    logger.warn(`GetLandRegistry: no registry yet for ${planetId} (${err.message})`);
    return { tiles: {} };
  }
};
