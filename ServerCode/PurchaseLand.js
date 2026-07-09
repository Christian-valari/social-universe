// PurchaseLand — validates ownership, deducts coins, records per-tile ownership,
// appends to the player's owned-tiles list so the client can restore state on login,
// and updates the planet's global land registry so other players see the new owner.
// The registry entry is { ownerId, buildLevel, lastYieldClaimTs, lastUpkeepTs, visitCount } —
// see GetLandRegistry, PlaceBuild, ClaimYield, RecordVisit, ApplyUpkeep, SellLand for readers/writers.
// NOTE: the deduct -> record -> registry sequence is not transactional; see the
// design caveat above.
//
// FIX (Known Issue #6): constructors don't accept { headers: { Authorization: ... } }.
// Economy: { accessToken } authenticates as the calling player. Cloud Save:
// DataApi(context) uses the service token (required for both player-scoped
// writes and custom/game data writes). getItems/setItem take positional args
// (projectId, playerId, ...), not an options object. getPlayerCurrencyBalance
// does not exist on CurrenciesApi — the read method is getPlayerCurrencies.
// decrementPlayerCurrencyBalance requires a configAssignmentHash fetched via
// ConfigurationApi before the write.
const { CurrenciesApi, ConfigurationApi } = require("@unity-services/economy-2.5");
const { DataApi: PlayerDataApi }          = require("@unity-services/cloud-save-1.4");

const CURRENCY_ID  = "COINS";
const REGISTRY_KEY = "land_registry";

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
  const econApi       = new CurrenciesApi({ accessToken });
  const config        = new ConfigurationApi({ accessToken });
  const saveApi       = new PlayerDataApi(context);
  const customDataApi = new PlayerDataApi(context); // same instance is fine; kept separate for clarity

  try {
    const ownedKey = `owned_tiles_${planetId.toLowerCase()}`;

    // 1. Load the player's current owned-tiles list for this planet.
    let ownedTiles = [];
    try {
      const saveRes = await saveApi.getItems(projectId, playerId, [ownedKey]);
      const item    = saveRes.data.results.find(r => r.key === ownedKey);
      if (item && Array.isArray(item.value)) ownedTiles = item.value; // already parsed by Cloud Save
    } catch (_) { /* key doesn't exist yet */ }

    if (ownedTiles.includes(tileId)) {
      return { success: false, reason: "ALREADY_OWNED" };
    }

    // 2. Validate balance.
    const balancesRes = await econApi.getPlayerCurrencies({ projectId, playerId });
    const coins       = balancesRes.data.results.find(c => c.currencyId === CURRENCY_ID);
    const balance     = coins ? coins.balance : 0;

    if (balance < price) {
      return { success: false, reason: "INSUFFICIENT_FUNDS" };
    }

    // 3. Deduct coins. configAssignmentHash is required by decrementPlayerCurrencyBalance.
    const cfg = await config.getPlayerConfiguration({ projectId, playerId });
    const configAssignmentHash = cfg.data.metadata.configAssignmentHash;

    const deductRes = await econApi.decrementPlayerCurrencyBalance({
      projectId,
      playerId,
      currencyId: CURRENCY_ID,
      configAssignmentHash,
      currencyModifyBalanceRequest: { currencyId: CURRENCY_ID, amount: price }
    });
    const newBalance = deductRes.data.balance;

    // 4. Record per-tile ownership (for cross-player lookup in M3+).
    await saveApi.setItem(projectId, playerId, { key: `tile_${tileId}_owner`, value: playerId });

    // 5. Append to the player's owned-tiles list so it can be restored on login.
    ownedTiles.push(tileId);
    await saveApi.setItem(projectId, playerId, { key: ownedKey, value: ownedTiles });

    // 6. Update the planet's global land registry (Custom Data, shared across all
    //    players) so other clients render this tile as "owned by other".
    const customId = planetId.toLowerCase();
    let registry = {};
    try {
      const regRes = await customDataApi.getCustomItems(projectId, customId, [REGISTRY_KEY]);
      const item   = regRes.data.results.find(r => r.key === REGISTRY_KEY);
      if (item?.value) registry = item.value;
    } catch (_) { /* registry doesn't exist yet */ }

    const now = Date.now();
    registry[tileId] = {
      ownerId: playerId,
      buildLevel: 0,
      lastYieldClaimTs: now,
      lastUpkeepTs: now,
      visitCount: 0
    };
    await customDataApi.setCustomItem(projectId, customId, { key: REGISTRY_KEY, value: registry });

    logger.info(`PurchaseLand: player ${playerId} purchased tile ${tileId} on ${planetId} for ${price} → ${newBalance}`);
    return { success: true, newBalance };
  } catch (err) {
    logger.error("PurchaseLand failed", { "error.message": err.message });
    throw err;
  }
};
