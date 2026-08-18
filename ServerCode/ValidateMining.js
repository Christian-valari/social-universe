// ValidateMining — validates a mining session payout and grants MINERALS (M6).
// The client sends the mined mineralId + claimed quantity + session params; the server
// caps the grant at floor(sessionDurationSec * unitsPerSec) to prevent inflated claims,
// then increments the player's mineral_inventory Cloud Save record.
// FIX (Known Issue #6/#8): DataApi(context) uses the service token; getItems/setItem are positional.
const { DataApi } = require("@unity-services/cloud-save-1.4");

const ABSOLUTE_SESSION_CAP_SECONDS = 1800; // MUST be >= EconomyConfig.MaxIdleSessionSeconds
const ABSOLUTE_QTY_CAP             = 10000; // hard upper bound per call
const INVENTORY_KEY                = "mineral_inventory";

/**
 * @param {string} mineralId - Id of the mineral mined this session.
 * @param {number} claimedQty - Units the client claims to have mined. Positive integer.
 * @param {number} [sessionDurationSec] - Session length; defaults 30, capped at 1800.
 * @param {number} [unitsPerSec] - Mineral yield rate/sec; defaults 1.
 */
module.exports = async ({ params, context, logger }) => {
  const { mineralId, claimedQty, sessionDurationSec, unitsPerSec } = params;

  if (!mineralId || !Number.isInteger(claimedQty) || claimedQty <= 0) {
    throw new Error(`Invalid params: mineralId + positive claimedQty required (got ${mineralId}, ${claimedQty})`);
  }

  const cappedDuration = Math.min(sessionDurationSec ?? 30, ABSOLUTE_SESSION_CAP_SECONDS);
  const maxByRate      = Math.floor(cappedDuration * (unitsPerSec ?? 1));
  const grantAmount    = Math.min(claimedQty, maxByRate, ABSOLUTE_QTY_CAP);

  if (grantAmount <= 0) {
    return { granted: 0, mineralId };
  }

  const { projectId, playerId } = context;
  const saveApi = new DataApi(context);

  let inventory = {};
  try {
    const res  = await saveApi.getItems(projectId, playerId, [INVENTORY_KEY]);
    const item = res.data.results.find(r => r.key === INVENTORY_KEY);
    if (item && item.value && typeof item.value === "object") inventory = item.value;
  } catch (_) { /* record doesn't exist yet */ }

  inventory[mineralId] = (inventory[mineralId] || 0) + grantAmount;
  await saveApi.setItem(projectId, playerId, { key: INVENTORY_KEY, value: inventory });

  logger.info(`ValidateMining: player ${playerId} +${grantAmount} ${mineralId}`);
  return { granted: grantAmount, mineralId };
};
