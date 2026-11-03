// SpendCoins — validates the player can afford the spend, then decrements.
// Returns { success, newBalance }.
const { CurrenciesApi } = require("@unity-services/economy-2.5");

/**
 * @param {number} amount - Number of coins to spend. Must be a positive integer not exceeding the player's current balance.
 */
module.exports = async ({ params, context, logger }) => {
  await requireActiveSession(context, params.sessionId);
  const { amount } = params;

  if (!Number.isInteger(amount) || amount <= 0) {
    throw Error(`Invalid amount: ${amount}`);
  }

  const { projectId, playerId, accessToken } = context;
  const api = new CurrenciesApi({ accessToken });

  // Fetch current balance to validate afford-ability server-side.
  const current = await currentBalance(api, projectId, playerId);

  if (current < amount) {
    logger.warn(`SpendCoins: insufficient balance (have ${current}, need ${amount})`);
    return { success: false, newBalance: current };
  }

  const res = await api.decrementPlayerCurrencyBalance({
    projectId,
    playerId,
    currencyId: "COINS",
    currencyModifyBalanceRequest: { amount }
  });

  logger.info(`SpendCoins: player ${playerId} -${amount} → ${res.data.balance}`);
  return { success: true, newBalance: res.data.balance };
};

// CurrenciesApi has no getPlayerCurrencyBalance (Known Issue #12) — read all balances and pick one.
async function currentBalance(econApi, projectId, playerId) {
  const res = await econApi.getPlayerCurrencies({ projectId, playerId });
  const c   = res.data.results.find(x => x.currencyId === "COINS");
  return c ? c.balance : 0;
}

// ---- active session check (copy verbatim into every deployed script; ActiveSessionGuardTests keeps the copies identical) ----
// ClaimSession records the latest sign-in's session in the Protected active_session item. A call
// naming any other session, or none, comes from a replaced or signed-out device. A failed read
// throws rather than letting the call through.
async function requireActiveSession(context, sessionId) {
  const { DataApi } = require("@unity-services/cloud-save-1.4");
  const { projectId, playerId } = context;
  let current;
  try {
    const res = await new DataApi(context).getProtectedItems(projectId, playerId, ["active_session"]);
    current = res.data.results.find(r => r.key === "active_session")?.value?.sessionId;
  } catch (e) {
    if (e?.response?.status !== 404) throw e;
  }
  if (!sessionId || sessionId !== current) throw Error("SESSION_REPLACED");
}
// ---- end active session check

// Parameter declarations for the UGS CLI / Cloud Code (types only: each script validates its own
// inputs and returns reasons the client understands). Must stay after `module.exports = ...`,
// which would otherwise overwrite it. Pinned by ServerCodeDeployTests.
module.exports.params = {
  amount: "Numeric",
  sessionId: "String"
};
