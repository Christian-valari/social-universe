// GrantStardust — server-authoritative stardust grant (premium currency).
const { CurrenciesApi } = require("@unity-services/economy-2.5");

const MAX_GRANT_PER_CALL = 10_000;

/**
 * @param {number} amount - Number of stardust to grant. Must be a positive integer no greater than 10000.
 */
module.exports = async ({ params, context, logger }) => {
  const { amount } = params;

  if (!Number.isInteger(amount) || amount <= 0 || amount > MAX_GRANT_PER_CALL) {
    throw Error(`Invalid amount: ${amount}`);
  }

  const { projectId, playerId, accessToken } = context;
  const api = new CurrenciesApi({ headers: { Authorization: `Bearer ${accessToken}` } });

  const res = await api.incrementPlayerCurrencyBalance({
    projectId,
    playerId,
    currencyId: "STARDUST",
    currencyModifyBalanceRequest: { amount }
  });

  logger.info(`GrantStardust: player ${playerId} +${amount} → ${res.data.balance}`);
  return { newBalance: res.data.balance };
};
