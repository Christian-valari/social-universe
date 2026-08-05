// PlaceBuild — validates tile ownership, that the target slot is empty, and the
// player's balance, then deducts the item's coin cost, writes itemId into
// slots[slotIndex] in the planet's shared land registry, and sets buildLevel to
// the number of filled slots.
// NOTE: the validate -> deduct -> registry-write sequence is not transactional;
// same caveat as PurchaseLand.
// NOTE (same as PurchaseLand): CurrenciesApi has no getPlayerCurrencyBalance — read via
// getPlayerCurrencies and find the currency. decrementPlayerCurrencyBalance requires a
// configAssignmentHash fetched from ConfigurationApi and a request body carrying currencyId.
const { CurrenciesApi, ConfigurationApi } = require("@unity-services/economy-2.5");
const { DataApi }                         = require("@unity-services/cloud-save-1.4");

const CURRENCY_ID  = "COINS";
const REGISTRY_KEY = "land_registry";
const SLOT_COUNT   = 8; // must match EconomyConfig.PlotSlotCount

function filledCount(slots) {
  if (!Array.isArray(slots)) return 0;
  return slots.filter(s => s !== null && s !== undefined && s !== "").length;
}

/**
 * @param {string} tileId
 * @param {string} planetId
 * @param {number} slotIndex - target slot, integer in [0, SLOT_COUNT).
 * @param {string} itemId - ItemDefinition id being placed.
 * @param {number} cost - coin cost, positive integer.
 */
module.exports = async ({ params, context, logger }) => {
  const { tileId, planetId, slotIndex, itemId, cost } = params;

  if (!tileId || !planetId || !itemId ||
      !Number.isInteger(slotIndex) || slotIndex < 0 || slotIndex >= SLOT_COUNT ||
      !Number.isInteger(cost) || cost <= 0) {
    throw new Error("Invalid params: tileId, planetId, slotIndex, itemId, cost required");
  }

  const { projectId, playerId, accessToken } = context;
  const econApi       = new CurrenciesApi({ accessToken });
  const config        = new ConfigurationApi({ accessToken });
  const customDataApi = new DataApi(context);
  const customId      = planetId.toLowerCase();

  try {
    let registry = {};
    try {
      const regRes = await customDataApi.getCustomItems(projectId, customId, [REGISTRY_KEY]);
      const item   = regRes.data.results.find(r => r.key === REGISTRY_KEY);
      if (item?.value) registry = item.value;
    } catch (_) { /* registry doesn't exist yet */ }

    const entry = registry[tileId];
    if (!entry || entry.ownerId !== playerId) {
      return { success: false, reason: "NOT_OWNER" };
    }

    if (!Array.isArray(entry.slots)) entry.slots = new Array(SLOT_COUNT).fill(null);
    if (entry.slots[slotIndex]) {
      return { success: false, reason: "SLOT_OCCUPIED" };
    }

    const balancesRes = await econApi.getPlayerCurrencies({ projectId, playerId });
    const coins       = balancesRes.data.results.find(c => c.currencyId === CURRENCY_ID);
    const balance     = coins ? coins.balance : 0;
    if (balance < cost) {
      return { success: false, reason: "INSUFFICIENT_FUNDS" };
    }

    // decrementPlayerCurrencyBalance requires a configAssignmentHash fetched via ConfigurationApi.
    const cfg = await config.getPlayerConfiguration({ projectId, playerId });
    const configAssignmentHash = cfg.data.metadata.configAssignmentHash;

    const deductRes = await econApi.decrementPlayerCurrencyBalance({
      projectId, playerId, currencyId: CURRENCY_ID, configAssignmentHash,
      currencyModifyBalanceRequest: { currencyId: CURRENCY_ID, amount: cost }
    });
    const newBalance = deductRes.data.balance;

    entry.slots[slotIndex] = itemId;
    entry.buildLevel = filledCount(entry.slots);
    registry[tileId] = entry;
    await customDataApi.setCustomItem(projectId, customId, { key: REGISTRY_KEY, value: registry });

    logger.info(`PlaceBuild: ${playerId} placed ${itemId} in slot ${slotIndex} of ${tileId} (${planetId}) for ${cost} -> ${newBalance}, buildLevel ${entry.buildLevel}`);
    return { success: true, newBalance, buildLevel: entry.buildLevel };
  } catch (err) {
    logger.error("PlaceBuild failed", { "error.message": err.message });
    throw err;
  }
};
