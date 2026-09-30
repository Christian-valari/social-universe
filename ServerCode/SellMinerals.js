// SellMinerals — sells minerals from the player's mineral_inventory to the house at a fixed
// per-mineral value, granting COINS. Accepts { mineralId, qty } or { all: true }.
// SELL_VALUES MUST MATCH each MineralDefinition._sellValue (SocialUniverse/Config/MineralDefinition).
// The inventory write uses the record's writeLock, so two concurrent sales can't both pay out
// against the same minerals: the second write gets a 409 and returns CONFLICT (Known Issue #16).
// If the coin grant then fails, the previous inventory is restored.
const { CurrenciesApi } = require("@unity-services/economy-2.5");
const { DataApi }       = require("@unity-services/cloud-save-1.4");

const CURRENCY_ID   = "COINS";
const INVENTORY_KEY = "mineral_inventory";
// MUST MATCH MineralDefinition assets (iron, carbon, silicon, nickel, platinum, iridium).
const SELL_VALUES = { iron: 2, carbon: 3, silicon: 5, nickel: 8, platinum: 20, iridium: 40 };

module.exports = async ({ params, context, logger }) => {
  const { mineralId, qty, all } = params;
  const { projectId, playerId, accessToken } = context;
  const econApi = new CurrenciesApi({ accessToken });
  const saveApi = new DataApi(context);

  // Load inventory + its writeLock.
  let inventory = {}, writeLock;
  try {
    const res  = await saveApi.getItems(projectId, playerId, [INVENTORY_KEY]);
    const item = res.data.results.find(r => r.key === INVENTORY_KEY);
    if (item && item.value && typeof item.value === "object") { inventory = item.value; writeLock = item.writeLock; }
  } catch (_) { /* none */ }
  const previous = { ...inventory };

  // Determine payout + resulting inventory.
  let payout = 0;
  if (all) {
    for (const [id, held] of Object.entries(inventory)) {
      payout += (SELL_VALUES[id] || 0) * held;
    }
    inventory = {};
  } else {
    if (!mineralId || !Number.isInteger(qty) || qty <= 0) {
      return { success: false, reason: "INVALID_PARAMS" };
    }
    const held = inventory[mineralId] || 0;
    if (held < qty) return { success: false, reason: "INSUFFICIENT_QTY" };
    payout = (SELL_VALUES[mineralId] || 0) * qty;
    const remaining = held - qty;
    if (remaining <= 0) delete inventory[mineralId];
    else                inventory[mineralId] = remaining;
  }

  if (payout <= 0) {
    return { success: true, newBalance: -1, remainingInventory: inventory };
  }

  let newLock;
  try {
    const body = writeLock ? { key: INVENTORY_KEY, value: inventory, writeLock } : { key: INVENTORY_KEY, value: inventory };
    newLock = (await saveApi.setItem(projectId, playerId, body)).data?.writeLock;
  } catch (err) {
    if (isConflict(err)) return { success: false, reason: "CONFLICT" };
    throw err;
  }

  let res;
  try {
    res = await econApi.incrementPlayerCurrencyBalance({
      projectId, playerId, currencyId: CURRENCY_ID,
      currencyModifyBalanceRequest: { amount: payout }
    });
  } catch (err) {
    logger.error(`SellMinerals: grant failed for ${playerId}; restoring inventory. ${err?.message}`);
    try {
      const body = newLock ? { key: INVENTORY_KEY, value: previous, writeLock: newLock } : { key: INVENTORY_KEY, value: previous };
      await saveApi.setItem(projectId, playerId, body);
    } catch (restoreErr) {
      logger.error(`SellMinerals: RESTORE FAILED for ${playerId} — manual reconciliation needed: ${restoreErr?.message}`);
    }
    throw err;
  }

  logger.info(`SellMinerals: player ${playerId} sold for ${payout} -> ${res.data.balance}`);
  return { success: true, newBalance: res.data.balance, remainingInventory: inventory };
};

function isConflict(err) {
  const status = err?.response?.status ?? err?.status;
  return status === 409;
}
