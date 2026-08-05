// MoveBuild — moves an item from fromHex to an empty toHex on an owned tile.
// Free; buildLevel unchanged (filled count is invariant under a move).
const { DataApi } = require("@unity-services/cloud-save-1.4");

const REGISTRY_KEY = "land_registry";
const HEX_COUNT    = 19; // must match HexBoardMath.HexCount(2) / EconomyConfig.HexCount
const FREE_COUNT   = 5;  // free hexatiles default unlocked

/**
 * @param {string} tileId
 * @param {string} planetId
 * @param {number} fromHex
 * @param {number} toHex
 */
module.exports = async ({ params, context, logger }) => {
  const { tileId, planetId, fromHex, toHex } = params;

  const inRange = i => Number.isInteger(i) && i >= 0 && i < HEX_COUNT;
  if (!tileId || !planetId || !inRange(fromHex) || !inRange(toHex) || fromHex === toHex) {
    throw new Error("Invalid params: tileId, planetId, distinct in-range fromHex/toHex required");
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

    // Normalize unlocked mask (free indices default true) and require the destination unlocked.
    let unlocked = Array.isArray(entry.unlocked) ? entry.unlocked.slice(0, HEX_COUNT) : [];
    while (unlocked.length < HEX_COUNT) unlocked.push(false);
    if (!Array.isArray(entry.unlocked)) for (let i = 0; i < FREE_COUNT; i++) unlocked[i] = true;
    entry.unlocked = unlocked;
    if (!unlocked[toHex]) {
      return { success: false, reason: "TILE_LOCKED" };
    }

    const slots = entry.slots;
    if (!Array.isArray(slots) || !slots[fromHex] || slots[toHex]) {
      return { success: false, reason: "INVALID_MOVE" };
    }

    slots[toHex]   = slots[fromHex];
    slots[fromHex] = null;
    registry[tileId] = entry;
    await customDataApi.setCustomItem(projectId, customId, { key: REGISTRY_KEY, value: registry });

    logger.info(`MoveBuild: ${playerId} moved hex ${fromHex}->${toHex} on ${tileId} (${planetId})`);
    return { success: true };
  } catch (err) {
    logger.error("MoveBuild failed", { "error.message": err.message });
    throw err;
  }
};
