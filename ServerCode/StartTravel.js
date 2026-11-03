// StartTravel — atomically spends fuel and commits a new trip for the caller,
// given a target planet id. The trip's duration and fuel cost are looked up
// server-side (never trusted from the client) so a tampered client can't fake
// a near-instant ETA or a free trip.
//
// Mirrors SpendFuel.js's write-lock + retry-on-409 pattern for the fuel_state
// write. There is no cross-key transaction in this Cloud Save SDK, so the two
// writes (fuel_state, then travel_state) happen in sequence; if the second
// write fails after the first succeeded, the fuel is refunded (same
// compensating-action pattern as RefillFuel.js).
//
// One unlanded trip at a time, whichever device started it: a trip that has
// arrived but not landed still blocks a new one (GetTravelState reports it as
// traveling too), and the trip is written under the writeLock read with it, so
// two simultaneous departures commit one trip. The origin is the server's
// current_planet — a device that missed a landing elsewhere still shows the old
// planet. Reads that fail throw rather than reading as "no record".
const { DataApi } = require("@unity-services/cloud-save-1.4");

const FUEL_KEY               = "fuel_state";
const TRAVEL_KEY              = "travel_state";
const PLANET_KEY             = "current_planet";
const MAX_FUEL               = 100; // keep in sync with EconomyConfig.MaxFuel
const FUEL_RECHARGE_PER_HOUR = 10;  // keep in sync with EconomyConfig.FuelRechargePerHour
const MS_PER_HOUR            = 3600000;
const MAX_RETRIES            = 3;

// keep in sync with each Planet_*.asset's _travelFuelCost
const TRAVEL_FUEL_COST = {
  earth: 0, moon: 5, venus: 15, mercury: 25, mars: 20,
  jupiter: 45, saturn: 55, uranus: 70, neptune: 85, pluto: 100,
};

// Origin/destination-specific durations — keep in sync with
// Data/Travel_Times.csv (and the TravelTimeTable.asset transcribed from it).
// Pluto isn't in that source data; PLUTO_FALLBACK_SEC keeps in sync with
// Planet_Pluto.asset's _travelDurationSeconds instead.
const TRAVEL_SECONDS = {
  mercury: { venus: 175, earth: 220, moon: 220, mars: 310, jupiter: 930, saturn: 1665, uranus: 3280, neptune: 5105 },
  venus:   { mercury: 175, earth: 165, moon: 165, mars: 255, jupiter: 875, saturn: 1610, uranus: 3225, neptune: 5045 },
  earth:   { mercury: 220, venus: 165, moon: 60, mars: 205, jupiter: 825, saturn: 1560, uranus: 3180, neptune: 5000 },
  moon:    { mercury: 220, venus: 165, earth: 60, mars: 205, jupiter: 825, saturn: 1560, uranus: 3180, neptune: 5000 },
  mars:    { mercury: 310, venus: 255, earth: 205, moon: 205, jupiter: 740, saturn: 1475, uranus: 3090, neptune: 4915 },
  jupiter: { mercury: 930, venus: 875, earth: 825, moon: 825, mars: 740, saturn: 855, uranus: 2470, neptune: 4295 },
  saturn:  { mercury: 1665, venus: 1610, earth: 1560, moon: 1560, mars: 1475, jupiter: 855, uranus: 1735, neptune: 3560 },
  uranus:  { mercury: 3280, venus: 3225, earth: 3180, moon: 3180, mars: 3090, jupiter: 2470, saturn: 1735, neptune: 1945 },
  neptune: { mercury: 5105, venus: 5045, earth: 5000, moon: 5000, mars: 4915, jupiter: 4295, saturn: 3560, uranus: 1945 },
};
const PLUTO_FALLBACK_SEC = 120;

function travelSecondsFor(originPlanetId, targetPlanetId) {
  return TRAVEL_SECONDS[originPlanetId]?.[targetPlanetId] ?? PLUTO_FALLBACK_SEC;
}

function rechargeFuel(state, now) {
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

async function readRecord(saveApi, projectId, playerId, key) {
  try {
    const res    = await saveApi.getProtectedItems(projectId, playerId, [key]);
    const record = res.data.results.find(r => r.key === key);
    return { value: record?.value ?? null, writeLock: record?.writeLock };
  } catch (err) {
    if ((err?.response?.status ?? err?.status) === 404) return { value: null, writeLock: undefined };
    throw err; // read as "no record", a failed fuel read would refill the tank
  }
}

// Gives back fuel spent on a trip that was not committed: re-read under the lock and add it, so
// any fuel change since the spend is kept. Returns the refunded state, or null if it failed.
async function refundFuel(saveApi, projectId, playerId, cost, logger) {
  for (let attempt = 0; attempt < MAX_RETRIES; attempt++) {
    try {
      const raw   = await readRecord(saveApi, projectId, playerId, FUEL_KEY);
      const state = rechargeFuel(raw.value, Date.now());
      state.fuel  = Math.min(state.maxFuel, state.fuel + cost);
      const body  = raw.writeLock ? { key: FUEL_KEY, value: state, writeLock: raw.writeLock } : { key: FUEL_KEY, value: state };
      await saveApi.setProtectedItem(projectId, playerId, body);
      return state;
    } catch (err) {
      if (isConflict(err) && attempt < MAX_RETRIES - 1) continue;
      logger.error(`StartTravel: REFUND FAILED for ${playerId} (${cost} fuel) — manual reconciliation needed: ${err?.message}`);
      return null;
    }
  }
  return null;
}

/**
 * @param {string} targetPlanetId - Planet id to travel to. Must be a known id.
 * @param {string} originPlanetId - Planet id the client thinks it is traveling
 *   from. Used for the duration lookup (TRAVEL_SECONDS) only when the server has
 *   no current_planet record for the player yet.
 */
module.exports = async ({ params, context, logger }) => {
  await requireActiveSession(context, params.sessionId);
  const { targetPlanetId, originPlanetId } = params;
  if (typeof targetPlanetId !== "string" || !(targetPlanetId in TRAVEL_FUEL_COST)) {
    throw new Error(`Invalid targetPlanetId: ${targetPlanetId}`);
  }
  if (typeof originPlanetId !== "string") {
    throw new Error(`Invalid originPlanetId: ${originPlanetId}`);
  }

  const { projectId, playerId } = context;
  const saveApi = new DataApi(context);
  const now     = Date.now();

  // 1. Reject if any trip is not landed yet — in flight or arrived, started on any device. The
  //    client resyncs on already_traveling and goes to the Travel scene to land it.
  const existingTrip = await readRecord(saveApi, projectId, playerId, TRAVEL_KEY);
  if (existingTrip.value?.targetPlanetId != null) {
    logger.warn(`StartTravel: player ${playerId} already has an unlanded trip to ${existingTrip.value.targetPlanetId}`);
    return { success: false, reason: "already_traveling" };
  }

  const serverOrigin = (await readRecord(saveApi, projectId, playerId, PLANET_KEY)).value?.planetId;
  if (serverOrigin && serverOrigin !== originPlanetId) {
    logger.warn(`StartTravel: client origin ${originPlanetId} differs from current_planet ${serverOrigin} for ${playerId}; using the server's`);
  }
  const cost     = TRAVEL_FUEL_COST[targetPlanetId];
  const duration = travelSecondsFor(serverOrigin ?? originPlanetId, targetPlanetId);

  // 2. Recharge + spend fuel under write-lock, retrying on conflict.
  let fuelState, fuelWriteLock;
  for (let attempt = 0; attempt < MAX_RETRIES; attempt++) {
    const raw = await readRecord(saveApi, projectId, playerId, FUEL_KEY);
    const state = rechargeFuel(raw.value, now);

    if (state.fuel < cost) {
      logger.warn(`StartTravel: insufficient fuel for ${playerId} (have ${state.fuel.toFixed(2)}, need ${cost})`);
      return { success: false, reason: "insufficient_fuel", fuel: state.fuel, maxFuel: state.maxFuel };
    }
    state.fuel -= cost;

    const body = raw.writeLock ? { key: FUEL_KEY, value: state, writeLock: raw.writeLock } : { key: FUEL_KEY, value: state };
    try {
      await saveApi.setProtectedItem(projectId, playerId, body);
      fuelState = state;
      break;
    } catch (err) {
      if (isConflict(err) && attempt < MAX_RETRIES - 1) continue;
      throw err;
    }
  }
  if (!fuelState) {
    logger.error(`StartTravel: fuel write-lock retries exhausted for ${playerId}`);
    throw new Error("StartTravel: concurrent update conflict, please retry.");
  }

  // 3. Commit the trip under the writeLock read in step 1: a departure committed since then (another
  //    device, or a double tap) makes this a 409, so only one trip is ever committed. A trip that
  //    isn't committed gets its fuel back.
  const trip     = { targetPlanetId, startTs: now, arrivalTs: now + duration * 1000, fuelSpent: cost };
  const tripBody = existingTrip.writeLock ? { key: TRAVEL_KEY, value: trip, writeLock: existingTrip.writeLock } : { key: TRAVEL_KEY, value: trip };
  try {
    await saveApi.setProtectedItem(projectId, playerId, tripBody);
  } catch (err) {
    const conflict = isConflict(err);
    if (conflict) logger.warn(`StartTravel: another departure committed first for ${playerId}; refunding ${cost} fuel`);
    else          logger.error(`StartTravel: travel_state write failed for ${playerId}; refunding ${cost} fuel. ${err?.message}`);
    fuelState = (await refundFuel(saveApi, projectId, playerId, cost, logger)) ?? fuelState;
    return { success: false, reason: conflict ? "already_traveling" : "write_failed", fuel: fuelState.fuel, maxFuel: fuelState.maxFuel };
  }

  logger.info(`StartTravel: player ${playerId} -> ${targetPlanetId}, arrives ${new Date(trip.arrivalTs).toISOString()}, spent ${cost} fuel`);
  return {
    success: true,
    traveling: true,
    targetPlanetId,
    arrivalTs: trip.arrivalTs,
    fuel: fuelState.fuel,
    maxFuel: fuelState.maxFuel,
  };
};

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
  targetPlanetId: "String",
  originPlanetId: "String",
  sessionId: "String"
};
