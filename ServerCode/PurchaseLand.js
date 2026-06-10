// PurchaseLand — validates ownership, deducts coins, records per-tile ownership,
// and appends to the player's owned-tiles list so the client can restore state on login.
// NOTE: the deduct -> record sequence is not transactional; see the design caveat above.
const { CurrenciesApi }          = require("@unity-services/economy-2.5");
const { DataApi: PlayerDataApi } = require("@unity-services/cloud-save-1.4");

const CURRENCY_ID = "COINS";

/**
 * @param {string} tileId - ID of the hex tile being purchased.
 * @param {string} planetId - ID of the planet the tile belongs to.
 * @param {number} price - Price of the tile in coins. Must be a positive integer.
 */
module.exports = async ({ params, context, logger }) => {
  const { tileId, planetId, price } = params;

  if (!tileId || !planetId || !Number.isInteger(price) || price <= 0) {
    throw new Error("Invalid params: tileId, planetId, and price are required");
  }

  const { projectId, playerId, accessToken } = context;
  const authHeader = { headers: { Authorization: `Bearer ${accessToken}` } };
  const econApi     = new CurrenciesApi(authHeader);
  const saveApi     = new PlayerDataApi(authHeader);

  try {
    const ownedKey = `owned_tiles_${planetId.toLowerCase()}`;

    // 1. Load the player's current owned-tiles list for this planet.
    let ownedTiles = [];
    try {
      const saveRes = await saveApi.getItems({ projectId, playerId, key: [ownedKey] });
      const item    = saveRes.data.results.find(r => r.key === ownedKey);
      if (item && Array.isArray(item.value)) ownedTiles = item.value; // already parsed by Cloud Save
    } catch (_) { /* key doesn't exist yet */ }

    if (ownedTiles.includes(tileId)) {
      return { success: false, reason: "ALREADY_OWNED" };
    }

    // 2. Validate balance and deduct coins.
    const balanceRes = await econApi.getPlayerCurrencyBalance({ projectId, playerId, currencyId: CURRENCY_ID });
    if (balanceRes.data.balance < price) {
      return { success: false, reason: "INSUFFICIENT_FUNDS" };
    }

    const deductRes = await econApi.decrementPlayerCurrencyBalance({
      projectId,
      playerId,
      currencyId: CURRENCY_ID,
      currencyModifyBalanceRequest: { amount: price }
    });
    const newBalance = deductRes.data.balance;

    // 3. Record per-tile ownership (for cross-player lookup in M3+).
    await saveApi.setItem({
      projectId, playerId,
      key:  `tile_${tileId}_owner`,
      body: { value: playerId }
    });

    // 4. Append to the player's owned-tiles list so it can be restored on login.
    ownedTiles.push(tileId);
    await saveApi.setItem({
      projectId, playerId,
      key:  ownedKey,
      body: { value: ownedTiles }
    });

    logger.info(`PurchaseLand: player ${playerId} purchased tile ${tileId} on ${planetId} for ${price} → ${newBalance}`);
    return { success: true, newBalance };
  } catch (err) {
    logger.error("PurchaseLand failed", { "error.message": err.message });
    throw err;
  }
};
