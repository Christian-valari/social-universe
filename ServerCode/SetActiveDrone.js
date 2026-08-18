// SetActiveDrone — validate ownership; set activeDroneId. No currency change.
const { DataApi } = require("@unity-services/cloud-save-1.4");

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
