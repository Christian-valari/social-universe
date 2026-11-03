// SetActiveDrone — validate ownership; set activeDroneId. No currency change.
const { DataApi } = require("@unity-services/cloud-save-1.4");

const STARTER_DRONE_ID = "scout"; // MUST MATCH the Tier-1, zero-cost DroneDefinition
const START_SLOTS      = 2;       // MUST MATCH EconomyConfig._startingFleetSlots

module.exports = async ({ params, context, logger }) => {
  await requireActiveSession(context, params.sessionId);
  const { droneId } = params;
  const { projectId, playerId } = context;
  const saveApi = new DataApi(context);

  const result = await updateFleet(saveApi, projectId, playerId, fleet => {
    if (!fleet.drones.some(d => d.droneId === droneId)) return "NOT_OWNED";
    fleet.activeDroneId = droneId;
  }, logger);
  if (result.reason) return { success: false, reason: result.reason };

  logger.info(`SetActiveDrone: ${playerId} active=${droneId}`);
  return { success: true, newBalance: -1, fleet: result.fleet };
};

// ---- drone_fleet access (copy verbatim into each drone function; PlayerRecordLockTests keeps them identical) ----
// An empty fleet gets the free starter drone, same as GetBootstrapState and the client's
// PlanetSceneScope fallback — otherwise the Scout the client shows is NOT_OWNED here.
// drone_fleet is written only through updateFleet: `apply` changes a fresh copy of the fleet and
// returns a reason to stop. The write carries the writeLock it read, so an overlapping drone call
// (another device, or a double tap) gets a 409 and `apply` re-runs on a fresh read — up to 3 times,
// then "CONFLICT". A failed read throws: a default fleet read from an error would overwrite the real one.
async function loadFleet(saveApi, projectId, playerId) {
  let fleet = { slots: START_SLOTS, activeDroneId: STARTER_DRONE_ID, drones: [] }, writeLock;
  try {
    const res  = await saveApi.getProtectedItems(projectId, playerId, ["drone_fleet"]);
    const item = res.data.results.find(r => r.key === "drone_fleet");
    if (item) writeLock = item.writeLock;
    if (item && item.value && typeof item.value === "object") fleet = item.value;
  } catch (err) {
    if ((err?.response?.status ?? err?.status) !== 404) throw err;
  }
  if (!Array.isArray(fleet.drones)) fleet.drones = [];
  if (typeof fleet.slots !== "number") fleet.slots = START_SLOTS;
  if (fleet.drones.length === 0) {
    fleet.drones.push({ droneId: STARTER_DRONE_ID, upgrades: { Cargo: 0, Yield: 0, Speed: 0 } });
    fleet.activeDroneId = STARTER_DRONE_ID;
  }
  return { fleet, writeLock };
}
async function updateFleet(saveApi, projectId, playerId, apply, logger) {
  for (let attempt = 0; attempt < 3; attempt++) {
    const { fleet, writeLock } = await loadFleet(saveApi, projectId, playerId);
    const reason = apply(fleet);
    if (reason) return { reason };
    try {
      await saveApi.setProtectedItem(projectId, playerId,
        writeLock ? { key: "drone_fleet", value: fleet, writeLock } : { key: "drone_fleet", value: fleet });
      return { fleet };
    } catch (err) {
      if ((err?.response?.status ?? err?.status) !== 409) throw err;
      logger.warn(`drone_fleet: write conflict for ${playerId} (attempt ${attempt + 1}), re-reading`);
    }
  }
  logger.warn(`drone_fleet: gave up for ${playerId} after 3 write conflicts`);
  return { reason: "CONFLICT" };
}
// ---- end drone_fleet access

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
  droneId: "String",
  sessionId: "String"
};
