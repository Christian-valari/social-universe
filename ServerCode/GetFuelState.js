// GetFuelState — returns the caller's current fuel, recharged server-side based
// on elapsed time since the last update.
//
// FIX 1: this is now a PURE READ. The old version wrote to Cloud Save on every
// call, which turned every gauge refresh into a write (cost + rate-limit risk,
// and a read that mutates state). Recharge is fully derived from lastUpdateTs,
// so there is nothing to persist here. The fuel_state record is created lazily
// on the first SpendFuel / RefillFuel call.
// FIX 2: DataApi's constructor doesn't read a { headers: ... } field, and
// getProtectedItems takes positional args (projectId, playerId, keys[]), not an options
// object — same SDK-shape mismatch documented as Known Issue #6 for the old
// PurchaseLand.js. DataApi(context) authenticates as the calling player via
// the service token, which is correct for player-scoped reads.
const { DataApi } = require("@unity-services/cloud-save-1.4");

const SAVE_KEY               = "fuel_state";
const MAX_FUEL               = 100; // keep in sync with EconomyConfig.MaxFuel
const FUEL_RECHARGE_PER_HOUR = 10;  // keep in sync with EconomyConfig.FuelRechargePerHour
const MS_PER_HOUR            = 3600000;

// Returns { fuel, maxFuel } with time-based recharge applied (no mutation).
function rechargedFuel(state, now) {
  if (!state) return { fuel: MAX_FUEL, maxFuel: MAX_FUEL };
  const maxFuel = state.maxFuel ?? MAX_FUEL;
  const last    = state.lastUpdateTs ?? now;
  const elapsedHours = Math.max(0, (now - last) / MS_PER_HOUR);
  const fuel    = Math.min(maxFuel, (state.fuel ?? 0) + elapsedHours * FUEL_RECHARGE_PER_HOUR);
  return { fuel, maxFuel };
}

/**
 * No parameters.
 */
module.exports = async ({ params, context, logger }) => {
  await requireActiveSession(context, params.sessionId);
  const { projectId, playerId } = context;
  const saveApi = new DataApi(context);

  let state = null;
  try {
    const res    = await saveApi.getProtectedItems(projectId, playerId, [SAVE_KEY]);
    const record = res.data.results.find(r => r.key === SAVE_KEY);
    if (record?.value) state = record.value;
  } catch (_) { /* no record yet */ }

  const { fuel, maxFuel } = rechargedFuel(state, Date.now());

  logger.info(`GetFuelState: player ${playerId} fuel ${fuel.toFixed(2)}/${maxFuel}`);
  return { success: true, fuel, maxFuel };
};

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
  sessionId: "String"
};
