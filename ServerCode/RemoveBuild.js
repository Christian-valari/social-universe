// RemoveBuild — clears slots[hexIndex] for an owned tile and recomputes buildLevel.
// No coin refund (prevents place/remove refund-farming). Idempotent on an already-empty slot.
const { DataApi } = require("@unity-services/cloud-save-1.4");

const REGISTRY_KEY = "land_registry";
const HEX_COUNT    = 19; // must match HexBoardMath.HexCount(2) / EconomyConfig.HexCount

function filledCount(slots) {
  if (!Array.isArray(slots)) return 0;
  return slots.filter(s => s !== null && s !== undefined && s !== "").length;
}

/**
 * @param {string} tileId
 * @param {string} planetId
 * @param {number} hexIndex - hexatile to clear, integer in [0, HEX_COUNT).
 */
module.exports = async ({ params, context, logger }) => {
  const { tileId, planetId, hexIndex } = params;

  if (!tileId || !planetId ||
      !Number.isInteger(hexIndex) || hexIndex < 0 || hexIndex >= HEX_COUNT) {
    throw new Error("Invalid params: tileId, planetId, hexIndex required");
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

    if (!Array.isArray(entry.slots) || !entry.slots[hexIndex]) {
      // already empty — nothing to do, report current level
      return { success: true, buildLevel: filledCount(entry.slots) };
    }

    entry.slots[hexIndex] = null;
    entry.buildLevel = filledCount(entry.slots);
    registry[tileId] = entry;
    await customDataApi.setCustomItem(projectId, customId, { key: REGISTRY_KEY, value: registry });

    logger.info(`RemoveBuild: ${playerId} cleared hex ${hexIndex} of ${tileId} (${planetId}), buildLevel ${entry.buildLevel}`);
    return { success: true, buildLevel: entry.buildLevel };
  } catch (err) {
    logger.error("RemoveBuild failed", { "error.message": err.message });
    throw err;
  }
};
