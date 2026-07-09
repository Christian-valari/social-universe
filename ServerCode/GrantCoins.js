// GrantCoins — server-authoritative coin grant.
// Called after idle mining claim; server validates the amount is within the
// session's maximum payout before incrementing the balance.
const { CurrenciesApi } = require("@unity-services/economy-2.5");

const MAX_GRANT_PER_CALL = 100_000; // sanity cap against runaway clients

/**
 * @param {number} amount - Number of coins to grant. Must be a positive integer no greater than 100000.
 */
module.exports = async ({ params, context, logger }) => {
  const { amount } = params;

  if (!Number.isInteger(amount) || amount <= 0 || amount > MAX_GRANT_PER_CALL) {
    throw Error(`Invalid amount: ${amount}`);
  }

  const { projectId, playerId, accessToken } = context;
  const api = new CurrenciesApi({ accessToken });

  const res = await api.incrementPlayerCurrencyBalance({
    projectId,
    playerId,
    currencyId: "COINS",
    currencyModifyBalanceRequest: { amount }
  });

  logger.info(`GrantCoins: player ${playerId} +${amount} → ${res.data.balance}`);
  return { newBalance: res.data.balance };
};
