// RemoveBuild — clears slots[slotIndex] for an owned tile and recomputes buildLevel.
// No coin refund (prevents place/remove refund-farming). Idempotent on an already-empty slot.
const { DataApi } = require("@unity-services/cloud-save-1.4");

const REGISTRY_KEY = "land_registry";
const SLOT_COUNT   = 8; // must match EconomyConfig.PlotSlotCount

function filledCount(slots) {
  if (!Array.isArray(slots)) return 0;
  return slots.filter(s => s !== null && s !== undefined && s !== "").length;
}

/**
 * @param {string} tileId
 * @param {string} planetId
 * @param {number} slotIndex - slot to clear, integer in [0, SLOT_COUNT).
 */
module.exports = async ({ params, context, logger }) => {
  const { tileId, planetId, slotIndex } = params;

  if (!tileId || !planetId ||
      !Number.isInteger(slotIndex) || slotIndex < 0 || slotIndex >= SLOT_COUNT) {
    throw new Error("Invalid params: tileId, planetId, slotIndex required");
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

    if (!Array.isArray(entry.slots) || !entry.slots[slotIndex]) {
      // already empty — nothing to do, report current level
      return { success: true, buildLevel: filledCount(entry.slots) };
    }

    entry.slots[slotIndex] = null;
    entry.buildLevel = filledCount(entry.slots);
    registry[tileId] = entry;
    await customDataApi.setCustomItem(projectId, customId, { key: REGISTRY_KEY, value: registry });

    logger.info(`RemoveBuild: ${playerId} cleared slot ${slotIndex} of ${tileId} (${planetId}), buildLevel ${entry.buildLevel}`);
    return { success: true, buildLevel: entry.buildLevel };
  } catch (err) {
    logger.error("RemoveBuild failed", { "error.message": err.message });
    throw err;
  }
};
