// RefillFuel — instantly tops the caller's fuel up to maxFuel in exchange for
// coins.
//
// FIX 1: DataApi's constructor doesn't read a { headers: ... } field, and
// getItems/setItem take positional args, not an options object — same
// SDK-shape mismatch documented as Known Issue #6 for the old PurchaseLand.js.
// DataApi(context) authenticates as the calling player via the service token.
// FIX 2: rejects with no charge if the tank is already full (old version
//        happily took 50 coins for 0 fuel).
// FIX 3: treats a failed coin decrement as "insufficient funds" instead of
//        relying on a racy pre-read + letting the decrement throw uncaught.
// FIX 4: closes the "charged but not granted" window — if the fuel write can't
//        commit after coins were deducted, it refunds the coins (compensating
//        action).
// FIX 5: writes fuel under a Cloud Save write-lock (optimistic concurrency).
// FIX 6: consistent return shape on every path.
const { CurrenciesApi } = require("@unity-services/economy-2.5");
const { DataApi }       = require("@unity-services/cloud-save-1.4");

const SAVE_KEY               = "fuel_state";
const CURRENCY_ID            = "COINS";
const MAX_FUEL               = 100; // keep in sync with EconomyConfig.MaxFuel
const FUEL_RECHARGE_PER_HOUR = 10;  // keep in sync with EconomyConfig.FuelRechargePerHour
const MS_PER_HOUR            = 3600000;
const REFILL_COST            = 50;  // keep in sync with EconomyConfig.FuelRefillCost
const MAX_RETRIES            = 3;

function recharge(state, now) {
  if (!state) return { fuel: MAX_FUEL, maxFuel: MAX_FUEL };
  const maxFuel = state.maxFuel ?? MAX_FUEL;
  const last    = state.lastUpdateTs ?? now;
  const elapsedHours = Math.max(0, (now - last) / MS_PER_HOUR);
  const fuel    = Math.min(maxFuel, (state.fuel ?? 0) + elapsedHours * FUEL_RECHARGE_PER_HOUR);
  return { fuel, maxFuel };
}

function isConflict(err) {
  const status = err?.response?.status ?? err?.status;
  return status === 409;
}

async function readBalance(econApi, projectId, playerId) {
  try {
    const res = await econApi.getPlayerCurrencyBalance({ projectId, playerId, currencyId: CURRENCY_ID });
    return res.data.balance;
  } catch (_) { return undefined; }
}

/**
 * No parameters.
 */
module.exports = async ({ context, logger }) => {
  const { projectId, playerId, accessToken } = context;
  const econApi = new CurrenciesApi({ accessToken });
  const saveApi = new DataApi(context);

  // 1. Read current fuel (recharged) + writeLock.
  let raw = null, writeLock;
  try {
    const res    = await saveApi.getItems(projectId, playerId, [SAVE_KEY]);
    const record = res.data.results.find(r => r.key === SAVE_KEY);
    if (record?.value) { raw = record.value; writeLock = record.writeLock; }
  } catch (_) { /* no record yet */ }

  const now     = Date.now();
  const current = recharge(raw, now);

  // 2. Already full → don't charge.
  if (current.fuel >= current.maxFuel) {
    return {
      success: false, reason: "already_full",
      fuel: current.fuel, maxFuel: current.maxFuel,
      newBalance: await readBalance(econApi, projectId, playerId)
    };
  }

  // 3. Charge coins. Economy is atomic & authoritative; a throw = can't afford.
  let newBalance;
  try {
    const deduct = await econApi.decrementPlayerCurrencyBalance({
      projectId, playerId, currencyId: CURRENCY_ID,
      currencyModifyBalanceRequest: { amount: REFILL_COST }
    });
    newBalance = deduct.data.balance;
  } catch (err) {
    logger.warn(`RefillFuel: charge failed for ${playerId} (insufficient or error): ${err?.message}`);
    return {
      success: false, reason: "insufficient_funds",
      fuel: current.fuel, maxFuel: current.maxFuel,
      newBalance: await readBalance(econApi, projectId, playerId)
    };
  }

  // 4. Grant fuel (full tank) under write-lock, with retry; refund on failure.
  const full = { fuel: current.maxFuel, maxFuel: current.maxFuel, lastUpdateTs: now };
  let lock = writeLock;
  for (let attempt = 0; attempt < MAX_RETRIES; attempt++) {
    const body = lock ? { key: SAVE_KEY, value: full, writeLock: lock } : { key: SAVE_KEY, value: full };
    try {
      await saveApi.setItem(projectId, playerId, body);
      logger.info(`RefillFuel: player ${playerId} refilled to ${full.fuel}/${full.maxFuel} for ${REFILL_COST} → ${newBalance}`);
      return { success: true, fuel: full.fuel, maxFuel: full.maxFuel, newBalance };
    } catch (err) {
      if (isConflict(err) && attempt < MAX_RETRIES - 1) {
        // Re-read the latest writeLock and retry (value is always a full tank).
        try {
          const res    = await saveApi.getItems(projectId, playerId, [SAVE_KEY]);
          const record = res.data.results.find(r => r.key === SAVE_KEY);
          lock = record?.writeLock;
        } catch (_) { lock = undefined; }
        continue;
      }
      // Couldn't grant fuel after charging → refund the coins.
      logger.error(`RefillFuel: fuel write failed after charge for ${playerId}; refunding ${REFILL_COST}. ${err?.message}`);
      try {
        const refund = await econApi.incrementPlayerCurrencyBalance({
          projectId, playerId, currencyId: CURRENCY_ID,
          currencyModifyBalanceRequest: { amount: REFILL_COST }
        });
        newBalance = refund.data.balance;
      } catch (refundErr) {
        logger.error(`RefillFuel: REFUND FAILED for ${playerId} — manual reconciliation needed: ${refundErr?.message}`);
      }
      return { success: false, reason: "write_failed", fuel: current.fuel, maxFuel: current.maxFuel, newBalance };
    }
  }
};
