// GrantOfflineIncome — validates the claimed offline yield against the server's
// authoritative session-end timestamp, then grants coins.
// The client sends the claimed amount; the server caps it at the theoretical max.
const { CurrenciesApi } = require("@unity-services/economy-2.5");
const { DataApi }       = require("@unity-services/cloud-save-1.4");

const IDLE_RATE_PER_SEC   = 1;   // must match EconomyConfig.IdleMiningRate
const MAX_OFFLINE_SECONDS = 8 * 3600; // 8 hours, must match EconomyConfig.MaxOfflineHours

/**
 * @param {number} claimedAmount - Coins the client claims to have earned while offline. Must be a positive integer; the server caps the actual grant at the theoretical max for the elapsed offline time.
 */
module.exports = async ({ params, context, logger }) => {
  const { claimedAmount } = params;

  if (!Number.isInteger(claimedAmount) || claimedAmount <= 0) {
    throw Error(`Invalid claimedAmount: ${claimedAmount}`);
  }

  const { projectId, playerId, accessToken } = context;
  const econApi    = new CurrenciesApi({ accessToken });
  const saveApi    = new DataApi(context);

  // Load the server-recorded session end time.
  let sessionEndMs = Date.now(); // fallback: assume just now
  try {
    const res    = await saveApi.getItems(projectId, playerId, ["last_session_end"]);
    const record = res.data.results.find(r => r.key === "last_session_end");
    if (record) sessionEndMs = parseInt(record.value, 10);
  } catch (_) {}

  const offlineSeconds   = Math.min((Date.now() - sessionEndMs) / 1000, MAX_OFFLINE_SECONDS);
  const maxGrantable     = Math.floor(offlineSeconds * IDLE_RATE_PER_SEC);
  const grantAmount      = Math.min(claimedAmount, maxGrantable);

  if (grantAmount <= 0) {
    return { granted: 0, newBalance: (await econApi.getPlayerCurrencyBalance({ projectId, playerId, currencyId: "COINS" })).data.balance };
  }

  const res = await econApi.incrementPlayerCurrencyBalance({
    projectId, playerId, currencyId: "COINS",
    currencyModifyBalanceRequest: { amount: grantAmount }
  });

  // Reset session end to now.
  await saveApi.setItem(projectId, playerId, { key: "last_session_end", value: String(Date.now()) });

  logger.info(`GrantOfflineIncome: player ${playerId} claimed ${claimedAmount}, granted ${grantAmount} → ${res.data.balance}`);
  return { granted: grantAmount, newBalance: res.data.balance };
};
