// ClaimYield — computes and grants accrued visitor-driven land income for an
// owned tile, then resets its yield-accrual state in the planet's global land
// registry (Custom Data).
// Formula (constants below must match EconomyConfig's [Header("Yield")] values):
//   elapsedHours = min((now - entry.lastYieldClaimTs) / 3600000, MAX_YIELD_ACCRUAL_HOURS)
//   buildBonus   = entry.buildLevel * BUILD_LEVEL_YIELD_MULTIPLIER
//   visitBonus   = min(entry.visitCount, MAX_VISIT_COUNT) * VISIT_YIELD_BONUS
//   granted      = floor(BASE_YIELD_PER_TILE_PER_HOUR * (1 + buildBonus + visitBonus) * elapsedHours)
// NOTE: the validate -> grant -> registry-write sequence is not transactional;
// same caveat as PurchaseLand.
const { CurrenciesApi }           = require("@unity-services/economy-2.5");
const { DataApi } = require("@unity-services/cloud-save-1.4");

const CURRENCY_ID  = "COINS";
const REGISTRY_KEY = "land_registry";

const BASE_YIELD_PER_TILE_PER_HOUR = 2;    // must match EconomyConfig.BaseYieldPerTilePerHour
const BUILD_LEVEL_YIELD_MULTIPLIER = 0.25; // must match EconomyConfig.BuildLevelYieldMultiplier
const VISIT_YIELD_BONUS            = 0.1;  // must match EconomyConfig.VisitYieldBonus
const MAX_YIELD_ACCRUAL_HOURS      = 24;   // must match EconomyConfig.MaxYieldAccrualHours
const MAX_VISIT_COUNT              = 50;   // must match EconomyConfig.MaxVisitCount

const MS_PER_HOUR = 3600000;

/**
 * @param {string} tileId - ID of the hex tile to claim yield for.
 * @param {string} planetId - ID of the planet the tile belongs to.
 */
module.exports = async ({ params, context, logger }) => {
  const { tileId, planetId } = params;

  if (!tileId || !planetId) {
    throw new Error("Invalid params: tileId and planetId are required");
  }

  const { projectId, playerId, accessToken } = context;
  const authHeader    = { headers: { Authorization: `Bearer ${accessToken}` } };
  const econApi       = new CurrenciesApi(authHeader);
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

    // 2. Compute accrued yield since the last claim.
    const now          = Date.now();
    const elapsedHours = Math.min((now - (entry.lastYieldClaimTs ?? now)) / MS_PER_HOUR, MAX_YIELD_ACCRUAL_HOURS);
    const buildBonus   = (entry.buildLevel ?? 0) * BUILD_LEVEL_YIELD_MULTIPLIER;
    const visitBonus   = Math.min(entry.visitCount ?? 0, MAX_VISIT_COUNT) * VISIT_YIELD_BONUS;
    const granted      = Math.floor(BASE_YIELD_PER_TILE_PER_HOUR * (1 + buildBonus + visitBonus) * elapsedHours);

    let newBalance;
    if (granted > 0) {
      const grantRes = await econApi.incrementPlayerCurrencyBalance({
        projectId, playerId, currencyId: CURRENCY_ID,
        currencyModifyBalanceRequest: { amount: granted }
      });
      newBalance = grantRes.data.balance;
    } else {
      newBalance = (await econApi.getPlayerCurrencyBalance({ projectId, playerId, currencyId: CURRENCY_ID })).data.balance;
    }

    // 3. Reset yield-accrual state and write the registry back.
    entry.lastYieldClaimTs = now;
    entry.visitCount       = 0;
    registry[tileId]       = entry;
    await customDataApi.setCustomItem(projectId, customId, { key: REGISTRY_KEY, value: registry });

    logger.info(`ClaimYield: player ${playerId} claimed tile ${tileId} (${planetId}) — granted ${granted} → ${newBalance}`);
    return { success: true, granted, newBalance };
  } catch (err) {
    logger.error("ClaimYield failed", { "error.message": err.message });
    throw err;
  }
};
