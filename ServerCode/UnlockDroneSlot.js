// UnlockDroneSlot — scaling slot price; deduct; slots++.
// SLOT_BASE/SLOT_GROWTH/START_SLOTS MUST MATCH EconomyConfig + DroneUpgradeMath.SlotUnlockCost.
const { CurrenciesApi, ConfigurationApi } = require("@unity-services/economy-2.5");
const { DataApi } = require("@unity-services/cloud-save-1.4");

const CURRENCY_ID = "COINS";
const SLOT_BASE   = 500;  // MUST MATCH EconomyConfig._slotUnlockBaseCost
const SLOT_GROWTH = 2;    // MUST MATCH EconomyConfig._slotUnlockCostGrowth
const START_SLOTS = 2;    // MUST MATCH EconomyConfig._startingFleetSlots

module.exports = async ({ params, context, logger }) => {
  const { projectId, playerId, accessToken } = context;
  const econApi = new CurrenciesApi({ accessToken });
  const config  = new ConfigurationApi({ accessToken });
  const saveApi = new DataApi(context);

  const fleet = await loadFleet(saveApi, projectId, playerId);
  const steps = Math.max(0, fleet.slots - START_SLOTS);
  const cost  = Math.round(SLOT_BASE * Math.pow(SLOT_GROWTH, steps));

  let newBalance = await currentBalance(econApi, projectId, playerId);
  if (newBalance < cost) return { success: false, reason: "INSUFFICIENT_FUNDS" };

  const cfg  = await config.getPlayerConfiguration({ projectId, playerId });
  const hash = cfg.data.metadata.configAssignmentHash;
  const res  = await econApi.decrementPlayerCurrencyBalance({
    projectId, playerId, currencyId: CURRENCY_ID, configAssignmentHash: hash,
    currencyModifyBalanceRequest: { currencyId: CURRENCY_ID, amount: cost }
  });
  newBalance = res.data.balance;

  fleet.slots += 1;
  await saveFleet(saveApi, projectId, playerId, fleet);
  logger.info(`UnlockDroneSlot: ${playerId} -> ${fleet.slots} slots for ${cost}`);
  return { success: true, newBalance, fleet };
};

// ---- shared helpers (duplicate this block into each drone function; keep in sync) ----
async function loadFleet(saveApi, projectId, playerId) {
  let fleet = { slots: 2, activeDroneId: "scout", drones: [] }; // MUST MATCH EconomyConfig.StartingFleetSlots
  try {
    const res  = await saveApi.getItems(projectId, playerId, ["drone_fleet"]);
    const item = res.data.results.find(r => r.key === "drone_fleet");
    if (item && item.value && typeof item.value === "object") fleet = item.value;
  } catch (_) { /* none */ }
  if (!Array.isArray(fleet.drones)) fleet.drones = [];
  if (typeof fleet.slots !== "number") fleet.slots = 2;
  return fleet;
}
async function saveFleet(saveApi, projectId, playerId, fleet) {
  await saveApi.setItem(projectId, playerId, { key: "drone_fleet", value: fleet });
}
async function currentBalance(econApi, projectId, playerId) {
  const res = await econApi.getPlayerCurrencies({ projectId, playerId });
  const c   = res.data.results.find(x => x.currencyId === "COINS");
  return c ? c.balance : 0;
}
