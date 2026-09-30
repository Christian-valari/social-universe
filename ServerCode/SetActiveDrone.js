// SetActiveDrone — validate ownership; set activeDroneId. No currency change.
const { DataApi } = require("@unity-services/cloud-save-1.4");

const STARTER_DRONE_ID = "scout"; // MUST MATCH the Tier-1, zero-cost DroneDefinition
const START_SLOTS      = 2;       // MUST MATCH EconomyConfig._startingFleetSlots

module.exports = async ({ params, context, logger }) => {
  const { droneId } = params;
  const { projectId, playerId } = context;
  const saveApi = new DataApi(context);

  const fleet = await loadFleet(saveApi, projectId, playerId);
  if (!fleet.drones.some(d => d.droneId === droneId)) return { success: false, reason: "NOT_OWNED" };

  fleet.activeDroneId = droneId;
  await saveFleet(saveApi, projectId, playerId, fleet);
  logger.info(`SetActiveDrone: ${playerId} active=${droneId}`);
  return { success: true, newBalance: -1, fleet };
};

// ---- shared helpers (duplicate this block into each drone function; keep in sync) ----
// An empty fleet gets the free starter drone, same as GetBootstrapState and the client's
// PlanetSceneScope fallback — otherwise the Scout the client shows is NOT_OWNED here.
async function loadFleet(saveApi, projectId, playerId) {
  let fleet = { slots: START_SLOTS, activeDroneId: STARTER_DRONE_ID, drones: [] };
  try {
    const res  = await saveApi.getItems(projectId, playerId, ["drone_fleet"]);
    const item = res.data.results.find(r => r.key === "drone_fleet");
    if (item && item.value && typeof item.value === "object") fleet = item.value;
  } catch (_) { /* none */ }
  if (!Array.isArray(fleet.drones)) fleet.drones = [];
  if (typeof fleet.slots !== "number") fleet.slots = START_SLOTS;
  if (fleet.drones.length === 0) {
    fleet.drones.push({ droneId: STARTER_DRONE_ID, upgrades: { Cargo: 0, Yield: 0, Speed: 0 } });
    fleet.activeDroneId = STARTER_DRONE_ID;
  }
  return fleet;
}
async function saveFleet(saveApi, projectId, playerId, fleet) {
  await saveApi.setItem(projectId, playerId, { key: "drone_fleet", value: fleet });
}
