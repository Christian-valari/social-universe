// AcquireDrone — validate coins >= unlockCost, fleet not full, not already owned; deduct; append.
// UNLOCK_COSTS MUST MATCH each DroneDefinition._unlockCost.
const { CurrenciesApi, ConfigurationApi } = require("@unity-services/economy-2.5");
const { DataApi } = require("@unity-services/cloud-save-1.4");

const CURRENCY_ID = "COINS";
const FLEET_KEY   = "drone_fleet";
// MUST MATCH DroneDefinition assets.
const UNLOCK_COSTS = { scout: 0, hauler: 300, prospector: 1200 };
const DRONE_TIERS  = { scout: 1, hauler: 2, prospector: 3 };

module.exports = async ({ params, context, logger }) => {
  const { droneId } = params;
  if (!droneId || UNLOCK_COSTS[droneId] === undefined) return { success: false, reason: "UNKNOWN_DRONE" };

  const { projectId, playerId, accessToken } = context;
  const econApi = new CurrenciesApi({ accessToken });
  const config  = new ConfigurationApi({ accessToken });
  const saveApi = new DataApi(context);

  const fleet = await loadFleet(saveApi, projectId, playerId);
  if (fleet.drones.some(d => d.droneId === droneId)) return { success: false, reason: "ALREADY_OWNED" };
  if (fleet.drones.length >= fleet.slots)             return { success: false, reason: "SLOTS_FULL" };

  const cost = UNLOCK_COSTS[droneId];
  let newBalance = await currentBalance(econApi, projectId, playerId);
  if (newBalance < cost) return { success: false, reason: "INSUFFICIENT_FUNDS" };

  if (cost > 0) {
    const cfg  = await config.getPlayerConfiguration({ projectId, playerId });
    const hash = cfg.data.metadata.configAssignmentHash;
    const res  = await econApi.decrementPlayerCurrencyBalance({
      projectId, playerId, currencyId: CURRENCY_ID, configAssignmentHash: hash,
      currencyModifyBalanceRequest: { currencyId: CURRENCY_ID, amount: cost }
    });
    newBalance = res.data.balance;
  }

  fleet.drones.push({ droneId, upgrades: { Cargo: 0, Yield: 0, Speed: 0 } });
  await saveFleet(saveApi, projectId, playerId, fleet);

  logger.info(`AcquireDrone: ${playerId} bought ${droneId} for ${cost} -> ${newBalance}`);
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
