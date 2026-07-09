// ApplyUpkeep — charges recurring per-tile upkeep for all of the caller's
// tiles on a planet. For each owned tile where at least one full day has
// elapsed since lastUpkeepTs:
//   cost = UPKEEP_PER_TILE_PER_DAY * daysElapsed
//   if balance >= cost: deduct cost, advance lastUpkeepTs by daysElapsed days (chargedTiles)
//   else: remove the tile from the registry — it reverts to Available for everyone (revertedTiles)
// NOTE: the read -> deduct -> registry-write sequence is not transactional;
// same caveat as PurchaseLand.
const { CurrenciesApi }           = require("@unity-services/economy-2.5");
const { DataApi } = require("@unity-services/cloud-save-1.4");

const CURRENCY_ID  = "COINS";
const REGISTRY_KEY = "land_registry";

const UPKEEP_PER_TILE_PER_DAY = 5; // must match EconomyConfig.UpkeepPerTilePerDay
const MS_PER_DAY = 86400000;

/**
 * @param {string} planetId - ID of the planet to apply upkeep for.
 */
module.exports = async ({ params, context, logger }) => {
  const { planetId } = params;

  if (!planetId) {
    throw new Error("Invalid params: planetId is required");
  }

  const { projectId, playerId, accessToken } = context;
  const econApi       = new CurrenciesApi({ accessToken });
  const customDataApi = new DataApi(context);
  const customId      = planetId.toLowerCase();

  try {
    let registry = {};
    try {
      const regRes = await customDataApi.getCustomItems(projectId, customId, [REGISTRY_KEY]);
      const item    = regRes.data.results.find(r => r.key === REGISTRY_KEY);
      if (item?.value) registry = item.value;
    } catch (_) { /* registry doesn't exist yet */ }

    const now = Date.now();
    let balance = (await econApi.getPlayerCurrencyBalance({ projectId, playerId, currencyId: CURRENCY_ID })).data.balance;

    const chargedTiles  = [];
    const revertedTiles = [];
    let registryDirty   = false;

    for (const [tileId, entry] of Object.entries(registry)) {
      if (entry.ownerId !== playerId) continue;

      const daysElapsed = Math.floor((now - (entry.lastUpkeepTs ?? now)) / MS_PER_DAY);
      if (daysElapsed < 1) continue;

      const cost = UPKEEP_PER_TILE_PER_DAY * daysElapsed;
      if (balance >= cost) {
        const deductRes = await econApi.decrementPlayerCurrencyBalance({
          projectId, playerId, currencyId: CURRENCY_ID,
          currencyModifyBalanceRequest: { amount: cost }
        });
        balance = deductRes.data.balance;
        entry.lastUpkeepTs = (entry.lastUpkeepTs ?? now) + daysElapsed * MS_PER_DAY;
        chargedTiles.push(tileId);
      } else {
        delete registry[tileId];
        revertedTiles.push(tileId);
      }
      registryDirty = true;
    }

    if (registryDirty) {
      await customDataApi.setCustomItem(projectId, customId, { key: REGISTRY_KEY, value: registry });
    }

    logger.info(`ApplyUpkeep: player ${playerId} on ${planetId} — charged ${chargedTiles.length}, reverted ${revertedTiles.length} → balance ${balance}`);
    return { newBalance: balance, chargedTiles, revertedTiles };
  } catch (err) {
    logger.error("ApplyUpkeep failed", { "error.message": err.message });
    throw err;
  }
};
