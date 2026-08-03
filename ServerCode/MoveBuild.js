// MoveBuild — moves an item from fromSlot to an empty toSlot on an owned tile.
// Free; buildLevel unchanged (filled count is invariant under a move).
const { DataApi } = require("@unity-services/cloud-save-1.4");

const REGISTRY_KEY = "land_registry";
const SLOT_COUNT   = 8; // must match EconomyConfig.PlotSlotCount

/**
 * @param {string} tileId
 * @param {string} planetId
 * @param {number} fromSlot
 * @param {number} toSlot
 */
module.exports = async ({ params, context, logger }) => {
  const { tileId, planetId, fromSlot, toSlot } = params;

  const inRange = i => Number.isInteger(i) && i >= 0 && i < SLOT_COUNT;
  if (!tileId || !planetId || !inRange(fromSlot) || !inRange(toSlot) || fromSlot === toSlot) {
    throw new Error("Invalid params: tileId, planetId, distinct in-range fromSlot/toSlot required");
  }

  const { projectId, playerId } = context;
  const customDataApi = new DataApi(context);
  const customId      = planetId.toLowerCase();

  try {
    let registry = {};
    try {
      const regRes = await customDataApi.getCustomItems(projectId, customId, [REGISTRY_KEY]);
      const item   = regRes.data.results.find(r => r.key === REGISTRY_KEY);
      if (item?.value) registry = item.value;
    } catch (_) { /* none yet */ }

    const entry = registry[tileId];
    if (!entry || entry.ownerId !== playerId) {
      return { success: false, reason: "NOT_OWNER" };
    }

    const slots = entry.slots;
    if (!Array.isArray(slots) || !slots[fromSlot] || slots[toSlot]) {
      return { success: false, reason: "INVALID_MOVE" };
    }

    slots[toSlot]   = slots[fromSlot];
    slots[fromSlot] = null;
    registry[tileId] = entry;
    await customDataApi.setCustomItem(projectId, customId, { key: REGISTRY_KEY, value: registry });

    logger.info(`MoveBuild: ${playerId} moved slot ${fromSlot}->${toSlot} on ${tileId} (${planetId})`);
    return { success: true };
  } catch (err) {
    logger.error("MoveBuild failed", { "error.message": err.message });
    throw err;
  }
};
