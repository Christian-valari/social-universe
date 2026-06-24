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
const { DataApi } = require("@unity-services/cloud-save-1.4");

const FUEL_KEY               = "fuel_state";
const TRAVEL_KEY              = "travel_state";
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
    const res    = await saveApi.getItems(projectId, playerId, [key]);
    const record = res.data.results.find(r => r.key === key);
    return record?.value ? { value: record.value, writeLock: record.writeLock } : { value: null, writeLock: undefined };
  } catch (_) {
    return { value: null, writeLock: undefined };
  }
}

/**
 * @param {string} targetPlanetId - Planet id to travel to. Must be a known id.
 * @param {string} originPlanetId - Planet id the player is traveling from. The
 *   server has no other notion of "where the player currently is", so this
 *   drives the origin/destination-specific duration lookup (TRAVEL_SECONDS)
 *   the same way the client's TravelService.GetTravelDuration does.
 */
module.exports = async ({ params, context, logger }) => {
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

  // 1. Reject if a trip is already in progress (must Land first).
  const existingTrip = await readRecord(saveApi, projectId, playerId, TRAVEL_KEY);
  if (existingTrip.value && existingTrip.value.arrivalTs > now) {
    logger.warn(`StartTravel: player ${playerId} already traveling to ${existingTrip.value.targetPlanetId}`);
    return { success: false, reason: "already_traveling" };
  }

  const cost     = TRAVEL_FUEL_COST[targetPlanetId];
  const duration = travelSecondsFor(originPlanetId, targetPlanetId);

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
      await saveApi.setItem(projectId, playerId, body);
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

  // 3. Commit the trip. If this write fails, refund the fuel we just spent.
  const trip = { targetPlanetId, startTs: now, arrivalTs: now + duration * 1000, fuelSpent: cost };
  try {
    await saveApi.setItem(projectId, playerId, { key: TRAVEL_KEY, value: trip });
  } catch (err) {
    logger.error(`StartTravel: travel_state write failed for ${playerId}; refunding ${cost} fuel. ${err?.message}`);
    try {
      const refunded = { ...fuelState, fuel: Math.min(fuelState.maxFuel, fuelState.fuel + cost) };
      await saveApi.setItem(projectId, playerId, { key: FUEL_KEY, value: refunded });
      fuelState = refunded;
    } catch (refundErr) {
      logger.error(`StartTravel: REFUND FAILED for ${playerId} — manual reconciliation needed: ${refundErr?.message}`);
    }
    return { success: false, reason: "write_failed", fuel: fuelState.fuel, maxFuel: fuelState.maxFuel };
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
