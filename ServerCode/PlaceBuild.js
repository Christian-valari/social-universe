// PlaceBuild — validates tile ownership, deducts the item's coin cost, and
// increments the tile's buildLevel in the planet's global land registry
// (Custom Data, shared across all players) so TileExtrusionView reflects the
// new build level for everyone.
// NOTE: the validate -> deduct -> registry-write sequence is not transactional;
// same caveat as PurchaseLand.
const { CurrenciesApi }           = require("@unity-services/economy-2.5");
const { DataApi } = require("@unity-services/cloud-save-1.4");

const CURRENCY_ID  = "COINS";
const REGISTRY_KEY = "land_registry";

/**
 * @param {string} tileId - ID of the hex tile being built on.
 * @param {string} planetId - ID of the planet the tile belongs to.
 * @param {string} itemId - ID of the ItemDefinition being placed.
 * @param {number} cost - Coin cost of the item. Must be a positive integer.
 */
module.exports = async ({ params, context, logger }) => {
  const { tileId, planetId, itemId, cost } = params;

  if (!tileId || !planetId || !itemId || !Number.isInteger(cost) || cost <= 0) {
    throw new Error("Invalid params: tileId, planetId, itemId, and cost are required");
  }

  const { projectId, playerId, accessToken } = context;
  const econApi       = new CurrenciesApi({ accessToken });
  const customDataApi = new DataApi(context);
  const customId      = planetId.toLowerCase();

  try {
    // 1. Load the planet's land registry and validate ownership.
    let registry = {};
    try {
      const regRes = await customDataApi.getCustomItems(projectId, customId, [REGISTRY_KEY]);
      const item    = regRes.data.results.find(r => r.key === REGISTRY_KEY);
      if (item?.value) registry = item.value;
    } catch (_) { /* registry doesn't exist yet */ }

    const entry = registry[tileId];
    if (!entry || entry.ownerId !== playerId) {
      return { success: false, reason: "NOT_OWNER" };
    }

    // 2. Validate balance and deduct the item's cost.
    const balanceRes = await econApi.getPlayerCurrencyBalance({ projectId, playerId, currencyId: CURRENCY_ID });
    if (balanceRes.data.balance < cost) {
      return { success: false, reason: "INSUFFICIENT_FUNDS" };
    }

    const deductRes = await econApi.decrementPlayerCurrencyBalance({
      projectId,
      playerId,
      currencyId: CURRENCY_ID,
      currencyModifyBalanceRequest: { amount: cost }
    });
    const newBalance = deductRes.data.balance;

    // 3. Increment the tile's build level and write the registry back.
    entry.buildLevel = (entry.buildLevel ?? 0) + 1;
    registry[tileId] = entry;
    await customDataApi.setCustomItem(projectId, customId, { key: REGISTRY_KEY, value: registry });

    logger.info(`PlaceBuild: player ${playerId} placed ${itemId} on tile ${tileId} (${planetId}) for ${cost} → ${newBalance}, buildLevel ${entry.buildLevel}`);
    return { success: true, newBalance, buildLevel: entry.buildLevel };
  } catch (err) {
    logger.error("PlaceBuild failed", { "error.message": err.message });
    throw err;
  }
};
