// GetBootstrapState — returns wallet balances and server time in a single call,
// minimizing round-trips on app launch.
const { CurrenciesApi } = require("@unity-services/economy-2.5");
const { DataApi }       = require("@unity-services/cloud-save-1.4");

/**
 * No parameters.
 */
module.exports = async ({ params, context, logger }) => {
  const { projectId, playerId, accessToken } = context;
  const authHeader = { headers: { Authorization: `Bearer ${accessToken}` } };

  const economyApi   = new CurrenciesApi(authHeader);
  const cloudSaveApi = new DataApi(authHeader);

  // Fetch balances + saved profile in parallel.
  const [balancesRes, profileRes] = await Promise.all([
    economyApi.getPlayerCurrencies({ projectId, playerId }),
    cloudSaveApi.getItems({ projectId, playerId, key: ["player_profile"] }).catch(() => ({ data: { results: [] } }))
  ]);

  const balances = {};
  for (const currency of balancesRes.data.results) {
    balances[currency.currencyId] = currency.balance;
  }

  const profileItem = profileRes.data.results.find(r => r.key === "player_profile");
  const profile     = profileItem ? JSON.parse(profileItem.value) : null;

  return {
    serverTimeMs: Date.now(),
    balances,
    profile
  };
};
