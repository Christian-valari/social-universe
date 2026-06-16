// RecordVisit — increments a tile's visitCount in the planet's global land
// registry (Custom Data) when another player visits it.
// This is the M3 stand-in for real presence-based visit detection — the
// client calls this when a player selects a tile they don't own (see
// VisitorTrackingController). Feeds into ClaimYield's visit bonus. No economy
// mutation.
const { DataApi } = require("@unity-services/cloud-save-1.4");

const REGISTRY_KEY    = "land_registry";
const MAX_VISIT_COUNT = 50; // must match EconomyConfig.MaxVisitCount

/**
 * @param {string} tileId - ID of the hex tile being visited.
 * @param {string} planetId - ID of the planet the tile belongs to.
 */
module.exports = async ({ params, context, logger }) => {
  const { tileId, planetId } = params;

  if (!tileId || !planetId) {
    throw new Error("Invalid params: tileId and planetId are required");
  }

  const { projectId, playerId } = context;
  const customDataApi = new DataApi(context);
  const customId      = planetId.toLowerCase();

  try {
    let registry = {};
    try {
      const regRes = await customDataApi.getCustomItems(projectId, customId, [REGISTRY_KEY]);
      const item    = regRes.data.results.find(r => r.key === REGISTRY_KEY);
      if (item?.value) registry = item.value;
    } catch (_) { /* registry doesn't exist yet */ }

    const entry = registry[tileId];
    if (!entry || entry.ownerId === playerId) {
      return { success: false, visitCount: entry?.visitCount ?? 0 };
    }

    entry.visitCount = Math.min((entry.visitCount ?? 0) + 1, MAX_VISIT_COUNT);
    registry[tileId] = entry;
    await customDataApi.setCustomItem(projectId, customId, { key: REGISTRY_KEY, value: registry });

    return { success: true, visitCount: entry.visitCount };
  } catch (err) {
    logger.error("RecordVisit failed", { "error.message": err.message });
    throw err;
  }
};
