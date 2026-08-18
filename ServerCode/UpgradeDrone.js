// UpgradeDrone — validate owned + level < maxLevel + coins >= NextCost; deduct; level++.
// UPGRADES MUST MATCH UpgradeDefinition assets + DroneUpgradeMath.NextCost.
const { CurrenciesApi, ConfigurationApi } = require("@unity-services/economy-2.5");
const { DataApi } = require("@unity-services/cloud-save-1.4");

const CURRENCY_ID = "COINS";
// MUST MATCH the three UpgradeDefinition assets.
const UPGRADES = {
  Cargo: { baseCost: 50,  growth: 1.5, maxLevel: 10 },
  Yield: { baseCost: 80,  growth: 1.6, maxLevel: 10 },
  Speed: { baseCost: 40,  growth: 1.4, maxLevel: 10 }
};

module.exports = async ({ params, context, logger }) => {
  const { droneId, stat } = params;
  const cfg = UPGRADES[stat];
  if (!droneId || !cfg) return { success: false, reason: "INVALID_PARAMS" };

  const { projectId, playerId, accessToken } = context;
  const econApi = new CurrenciesApi({ accessToken });
  const config  = new ConfigurationApi({ accessToken });
  const saveApi = new DataApi(context);

  const fleet = await loadFleet(saveApi, projectId, playerId);
  const drone = fleet.drones.find(d => d.droneId === droneId);
  if (!drone) return { success: false, reason: "NOT_OWNED" };
  drone.upgrades = drone.upgrades || { Cargo: 0, Yield: 0, Speed: 0 };
  const level = drone.upgrades[stat] || 0;
  if (level >= cfg.maxLevel) return { success: false, reason: "MAX_LEVEL" };

  const cost = Math.round(cfg.baseCost * Math.pow(cfg.growth, level));
  let newBalance = await currentBalance(econApi, projectId, playerId);
  if (newBalance < cost) return { success: false, reason: "INSUFFICIENT_FUNDS" };

  const pcfg = await config.getPlayerConfiguration({ projectId, playerId });
  const hash = pcfg.data.metadata.configAssignmentHash;
  const res  = await econApi.decrementPlayerCurrencyBalance({
    projectId, playerId, currencyId: CURRENCY_ID, configAssignmentHash: hash,
    currencyModifyBalanceRequest: { currencyId: CURRENCY_ID, amount: cost }
  });
  newBalance = res.data.balance;

  drone.upgrades[stat] = level + 1;
  await saveFleet(saveApi, projectId, playerId, fleet);
  logger.info(`UpgradeDrone: ${playerId} ${droneId}.${stat} -> ${level + 1} for ${cost}`);
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
