// SellLand — validates ownership, grants the (client-computed) refund, removes
// the tile from the player's owned-tiles list, and removes the tile from the
// planet's global land registry so it reverts to Available for everyone.
// `refund` is computed client-side from EconomyConfig.BaseLandPrice *
// PlanetDefinition.LandPriceMultiplier * EconomyConfig.LandResaleRate — the
// server doesn't have planet pricing data, but still gates the payout on
// ownership, same trust model as PurchaseLand's `price` param.
// NOTE: the validate -> grant -> cleanup sequence is not transactional; same
// caveat as PurchaseLand.
const { CurrenciesApi }            = require("@unity-services/economy-2.5");
const { DataApi: PlayerDataApi }   = require("@unity-services/cloud-save-1.4");

const CURRENCY_ID  = "COINS";
const REGISTRY_KEY = "land_registry";

/**
 * @param {string} tileId - ID of the hex tile being sold.
 * @param {string} planetId - ID of the planet the tile belongs to.
 * @param {number} refund - Coins to refund the seller. Must be a non-negative integer.
 */
module.exports = async ({ params, context, logger }) => {
  const { tileId, planetId, refund } = params;

  if (!tileId || !planetId || !Number.isInteger(refund) || refund < 0) {
    throw new Error("Invalid params: tileId, planetId, and refund are required");
  }

  const { projectId, playerId, accessToken } = context;
  const econApi       = new CurrenciesApi({ accessToken });
  const saveApi       = new PlayerDataApi(context);
  const customDataApi = new PlayerDataApi(context);
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

    // 2. Grant the refund.
    let newBalance = (await econApi.getPlayerCurrencyBalance({ projectId, playerId, currencyId: CURRENCY_ID })).data.balance;
    if (refund > 0) {
      const grantRes = await econApi.incrementPlayerCurrencyBalance({
        projectId, playerId, currencyId: CURRENCY_ID,
        currencyModifyBalanceRequest: { amount: refund }
      });
      newBalance = grantRes.data.balance;
    }

    // 3. Remove the tile from the player's owned-tiles list.
    const ownedKey = `owned_tiles_${planetId.toLowerCase()}`;
    try {
      const saveRes = await saveApi.getItems(projectId, playerId, [ownedKey]);
      const item    = saveRes.data.results.find(r => r.key === ownedKey);
      if (item && Array.isArray(item.value)) {
        const ownedTiles = item.value.filter(id => id !== tileId);
        await saveApi.setItem(projectId, playerId, { key: ownedKey, value: ownedTiles });
      }
    } catch (_) { /* key doesn't exist yet */ }

    // 4. Remove the tile from the global land registry.
    delete registry[tileId];
    await customDataApi.setCustomItem(projectId, customId, { key: REGISTRY_KEY, value: registry });

    logger.info(`SellLand: player ${playerId} sold tile ${tileId} on ${planetId} for ${refund} → ${newBalance}`);
    return { success: true, newBalance };
  } catch (err) {
    logger.error("SellLand failed", { "error.message": err.message });
    throw err;
  }
};
