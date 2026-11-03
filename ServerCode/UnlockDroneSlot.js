// UnlockDroneSlot — scaling slot price; deduct; slots++.
// SLOT_BASE/SLOT_GROWTH/START_SLOTS MUST MATCH EconomyConfig + DroneUpgradeMath.SlotUnlockCost.
const { CurrenciesApi, ConfigurationApi } = require("@unity-services/economy-2.5");
const { DataApi } = require("@unity-services/cloud-save-1.4");

const CURRENCY_ID = "COINS";
const SLOT_BASE   = 500;  // MUST MATCH EconomyConfig._slotUnlockBaseCost
const SLOT_GROWTH = 2;    // MUST MATCH EconomyConfig._slotUnlockCostGrowth
const STARTER_DRONE_ID = "scout"; // MUST MATCH the Tier-1, zero-cost DroneDefinition
const START_SLOTS = 2;    // MUST MATCH EconomyConfig._startingFleetSlots

module.exports = async ({ params, context, logger }) => {
  await requireActiveSession(context, params.sessionId);
  const { projectId, playerId, accessToken } = context;
  const econApi = new CurrenciesApi({ accessToken });
  const config  = new ConfigurationApi({ accessToken });
  const saveApi = new DataApi(context);

  const { slots } = (await loadFleet(saveApi, projectId, playerId)).fleet;
  const steps = Math.max(0, slots - START_SLOTS);
  const cost  = Math.round(SLOT_BASE * Math.pow(SLOT_GROWTH, steps));

  let newBalance = await currentBalance(econApi, projectId, playerId);
  if (newBalance < cost) return { success: false, reason: "INSUFFICIENT_FUNDS" };

  // The price is for this slot count: if another unlock landed first, stop rather than sell the next slot at it.
  const op = fleet => {
    if (fleet.slots !== slots) return "CONFLICT";
    fleet.slots += 1;
  };

  if (cost > 0) newBalance = await charge(econApi, config, projectId, playerId, cost);
  let result;
  try {
    result = await updateFleet(saveApi, projectId, playerId, op, logger);
  } catch (err) {
    if (cost > 0) await refund(econApi, projectId, playerId, cost, logger);
    throw err;
  }
  if (result.reason) {
    if (cost > 0) await refund(econApi, projectId, playerId, cost, logger);
    logger.warn(`UnlockDroneSlot: ${playerId} not applied (${result.reason}); refunded ${cost}`);
    return { success: false, reason: result.reason };
  }

  logger.info(`UnlockDroneSlot: ${playerId} -> ${slots + 1} slots for ${cost}`);
  return { success: true, newBalance, fleet: result.fleet };
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

// Coins are charged before the fleet changes: a refund is an increment, which can't fail for lack of
// coins, so an op that doesn't land (its fleet write conflicts, stops or fails) is always paid back.
async function charge(econApi, config, projectId, playerId, cost) {
  const cfg  = await config.getPlayerConfiguration({ projectId, playerId });
  const hash = cfg.data.metadata.configAssignmentHash;
  const res  = await econApi.decrementPlayerCurrencyBalance({
    projectId, playerId, currencyId: CURRENCY_ID, configAssignmentHash: hash,
    currencyModifyBalanceRequest: { currencyId: CURRENCY_ID, amount: cost }
  });
  return res.data.balance;
}
async function refund(econApi, projectId, playerId, cost, logger) {
  try {
    await econApi.incrementPlayerCurrencyBalance({
      projectId, playerId, currencyId: CURRENCY_ID, currencyModifyBalanceRequest: { amount: cost }
    });
    logger.info(`refunded ${cost} coins to ${playerId}`);
  } catch (err) {
    logger.error(`REFUND FAILED: ${cost} coins for ${playerId} — manual fix needed. ${err?.message}`);
  }
}
async function currentBalance(econApi, projectId, playerId) {
  const res = await econApi.getPlayerCurrencies({ projectId, playerId });
  const c   = res.data.results.find(x => x.currencyId === "COINS");
  return c ? c.balance : 0;
}

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
