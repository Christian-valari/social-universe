// SpendFuel — recharges fuel up to now, then validates and deducts the requested
// amount. Returns the post-spend state either way so the client can resync.
//
// FIX 1: DataApi's constructor doesn't read a { headers: ... } field, and
// getItems/setItem take positional args, not an options object — same
// SDK-shape mismatch documented as Known Issue #6 for the old PurchaseLand.js.
// DataApi(context) authenticates as the calling player via the service token.
// FIX 2 (important): the old version did a plain read-modify-write, so two
// concurrent calls could each read a full tank and both succeed — letting a
// player spend the same fuel twice (e.g. travel twice on one tank). This version
// commits under a Cloud Save write-lock (optimistic concurrency) and retries on
// conflict, so concurrent spends can't double-spend.
// FIX 3: amount validation used typeof === "number", which lets NaN/Infinity
// through (NaN < 0 is false). Now uses Number.isFinite.
const { DataApi } = require("@unity-services/cloud-save-1.4");

const SAVE_KEY               = "fuel_state";
const MAX_FUEL               = 100; // keep in sync with EconomyConfig.MaxFuel
const FUEL_RECHARGE_PER_HOUR = 10;  // keep in sync with EconomyConfig.FuelRechargePerHour
const MS_PER_HOUR            = 3600000;
const MAX_RETRIES            = 3;

function recharge(state, now) {
  if (!state) return { fuel: MAX_FUEL, maxFuel: MAX_FUEL, lastUpdateTs: now };
  const maxFuel = state.maxFuel ?? MAX_FUEL;
  const last    = state.lastUpdateTs ?? now;
  const elapsedHours = Math.max(0, (now - last) / MS_PER_HOUR);
  const fuel    = Math.min(maxFuel, (state.fuel ?? 0) + elapsedHours * FUEL_RECHARGE_PER_HOUR);
  return { fuel, maxFuel, lastUpdateTs: now };
}

function isConflict(err) {
  const status = err?.response?.status ?? err?.status;
  return status === 409; // Cloud Save write-lock mismatch
}

/**
 * @param {number} amount - Fuel units to spend. Must be a finite, non-negative number.
 */
module.exports = async ({ params, context, logger }) => {
  const { amount } = params;
  if (!Number.isFinite(amount) || amount < 0) {
    throw new Error(`Invalid amount: ${amount}`);
  }

  const { projectId, playerId } = context;
  const saveApi = new DataApi(context);

  for (let attempt = 0; attempt < MAX_RETRIES; attempt++) {
    // Read current record + its writeLock.
    let raw = null, writeLock;
    try {
      const res    = await saveApi.getItems(projectId, playerId, [SAVE_KEY]);
      const record = res.data.results.find(r => r.key === SAVE_KEY);
      if (record?.value) { raw = record.value; writeLock = record.writeLock; }
    } catch (_) { /* no record yet */ }

    const state = recharge(raw, Date.now());

    let success = false;
    if (state.fuel >= amount) { state.fuel -= amount; success = true; }

    // Conditional write: include writeLock when we had a record. If your Cloud
    // Save SDK version doesn't accept writeLock here, drop it (you lose the
    // double-spend guard but keep the rest of the logic).
    const body = writeLock ? { key: SAVE_KEY, value: state, writeLock } : { key: SAVE_KEY, value: state };
    try {
      await saveApi.setItem(projectId, playerId, body);
    } catch (err) {
      if (isConflict(err) && attempt < MAX_RETRIES - 1) continue; // someone else wrote; retry
      throw err;
    }

    if (success) logger.info(`SpendFuel: player ${playerId} -${amount} → ${state.fuel.toFixed(2)}/${state.maxFuel}`);
    else         logger.warn(`SpendFuel: insufficient fuel for ${playerId} (have ${state.fuel.toFixed(2)}, need ${amount})`);
    return { success, fuel: state.fuel, maxFuel: state.maxFuel };
  }

  logger.error(`SpendFuel: write-lock retries exhausted for ${playerId}`);
  throw new Error("SpendFuel: concurrent update conflict, please retry.");
};
