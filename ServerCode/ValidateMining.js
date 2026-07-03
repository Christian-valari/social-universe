// ValidateMining — validates an idle mining session payout and grants coins.
// The client sends claimed coins and the session parameters; the server caps the
// grant at (sessionDurationSec * coinsPerSec) to prevent inflated claims.
// Full anti-cheat with a server-stored session token is scheduled for M3.
const { CurrenciesApi } = require("@unity-services/economy-2.5");

const ABSOLUTE_SESSION_CAP_SECONDS = 1800; // Must be >= EconomyConfig.MaxIdleSessionSeconds (client's clamp ceiling) or legitimate long-duration idle/active claims get under-granted here.
const ABSOLUTE_COINS_CAP           = 10000; // hard upper bound per call

/**
 * @param {number} claimedCoins - Coins the client claims to have mined this session. Must be a positive integer.
 * @param {number} [sessionDurationSec] - Session length in seconds. Optional, defaults to 30 and is capped at 1800.
 * @param {number} [coinsPerSec] - Coin yield rate per second for the session. Optional, defaults to 1.
 */
module.exports = async ({ params, context, logger }) => {
  const { claimedCoins, sessionDurationSec, coinsPerSec } = params;

  if (!Number.isInteger(claimedCoins) || claimedCoins <= 0) {
    throw new Error(`Invalid claimedCoins: ${claimedCoins}`);
  }

  const cappedDuration = Math.min(sessionDurationSec ?? 30, ABSOLUTE_SESSION_CAP_SECONDS);
  const maxByRate      = Math.floor(cappedDuration * (coinsPerSec ?? 1));
  const grantAmount    = Math.min(claimedCoins, maxByRate, ABSOLUTE_COINS_CAP);

  if (grantAmount <= 0) {
    return { granted: 0, newBalance: null };
  }

  const { projectId, playerId, accessToken } = context;
  const authHeader = { headers: { Authorization: `Bearer ${accessToken}` } };
  const econApi    = new CurrenciesApi(authHeader);

  const res = await econApi.incrementPlayerCurrencyBalance({
    projectId,
    playerId,
    currencyId: "COINS",
    currencyModifyBalanceRequest: { amount: grantAmount }
  });

  logger.info(`ValidateMining: player ${playerId} claimed ${claimedCoins}, granted ${grantAmount} → balance ${res.data.balance}`);
  return { granted: grantAmount, newBalance: res.data.balance };
};
