// ValidateMining — grants MINERALS for an idle or active mining claim (M6), server-authoritatively.
// Design: docs/superpowers/specs/2026-09-29-mining-claim-budget-design.md (Known Issue #16).
//
// Request { planetId, mineralId, claimedQty }. Checks, in order:
//   1. planetId == the server's current_planet (a missing/null record accepts the claimed planet)
//   2. mineralId is one of that planet's asteroid minerals
//   3. the saved fleet's active drone tier >= the mineral's asteroid tier
//   4. grant = min(claimedQty, ceil(ceil(baseYield × YIELD_ROLL_MAX) × effectiveYieldMult))
//   5. fewer than FIELD_SIZE claims on this planet in the last RESPAWN_HOURS (mining_claim_log)
// Rejections return { granted: 0, mineralId, reason } so the client can show why, and log one
// warn line (player, planet, mineral, reason) — the only server-side signal for false rejections.
// The budget slot is reserved before minerals are granted and released if the grant fails.
const { DataApi } = require("@unity-services/cloud-save-1.4");

const INVENTORY_KEY      = "mineral_inventory";
const CLAIM_LOG_KEY      = "mining_claim_log";
const PLANET_KEY         = "current_planet";
const FLEET_KEY          = "drone_fleet";
const MAX_WRITE_ATTEMPTS = 3;
const MS_PER_HOUR        = 3600000;

// ---- Catalog: MUST MATCH the SO assets (MiningCatalogAlignmentTests) ----
const FIELD_SIZE            = 6;    // >= every PlanetDefinition._asteroidFieldSize
const RESPAWN_HOURS         = 4;    // EconomyConfig._asteroidRespawnHours
const YIELD_ROLL_MAX        = 1.2;  // EconomyConfig._asteroidYieldRollMax
const YIELD_DELTA_PER_LEVEL = 0.15; // Upgrade_Yield._deltaPerLevel
const STARTER_DRONE_ID      = "scout"; // MUST MATCH the Tier-1, zero-cost DroneDefinition

// PlanetDefinition._planetId -> its _asteroidTypes' mineral ids
const PLANET_MINERALS = {
  earth:   ["iron", "carbon", "silicon"],
  jupiter: ["silicon", "nickel", "platinum"],
  mars:    ["silicon", "nickel"],
  mercury: ["iron", "carbon"],
  moon:    ["iron", "carbon"],
  neptune: ["platinum", "iridium", "palladium"],
  pluto:   ["platinum", "iridium", "palladium", "helium3"],
  saturn:  ["silicon", "nickel", "platinum"],
  uranus:  ["platinum", "iridium", "palladium"],
  venus:   ["iron", "carbon"],
};

// Mineral id -> its AsteroidDefinition's _baseYield and _tier
const MINERALS = {
  iron:     { baseYield: 80, tier: 1 },
  carbon:   { baseYield: 65, tier: 1 },
  silicon:  { baseYield: 50, tier: 2 },
  nickel:   { baseYield: 40, tier: 2 },
  platinum: { baseYield: 25, tier: 3 },
  iridium:  { baseYield: 15, tier: 3 },
  palladium: { baseYield: 12, tier: 4 },
  helium3:  { baseYield: 8, tier: 5 },
};

// DroneDefinition._droneId -> _tier and _yieldMultiplier
const DRONES = {
  scout:     { tier: 1, yieldMult: 1 },
  extractor: { tier: 2, yieldMult: 1.5 },
  excavator: { tier: 4, yieldMult: 1.8 },
  surveyor:  { tier: 4, yieldMult: 2.5 },
  titan:     { tier: 5, yieldMult: 3 },
};

/**
 * @param {string} planetId - Planet the claim was mined on (PlanetDefinition._planetId).
 * @param {string} mineralId - Mineral mined (MineralDefinition._mineralId).
 * @param {number} claimedQty - Units the client computed. Positive integer; clamped server-side.
 */
module.exports = async ({ params, context, logger }) => {
  const { planetId, mineralId, claimedQty } = params;
  const reject = reason => {
    logger.warn(`ValidateMining: rejected ${reason} for player ${context?.playerId} (planet ${planetId}, mineral ${mineralId}, claimed ${claimedQty})`);
    return { granted: 0, mineralId: mineralId ?? null, reason };
  };

  if (!planetId || !mineralId || !Number.isInteger(claimedQty) || claimedQty <= 0) return reject("INVALID_PARAMS");

  const { projectId, playerId } = context;
  const saveApi = new DataApi(context);

  try {
    const records = await loadRecords(saveApi, projectId, playerId, [PLANET_KEY, FLEET_KEY]);

    const serverPlanet = records[PLANET_KEY]?.value?.planetId;
    if (serverPlanet && serverPlanet !== planetId) return reject("WRONG_PLANET");

    const mineral = MINERALS[mineralId];
    if (!mineral || !(PLANET_MINERALS[planetId] ?? []).includes(mineralId)) return reject("MINERAL_NOT_ON_PLANET");

    const drone = activeDrone(records[FLEET_KEY]?.value);
    if (mineral.tier > drone.tier) return reject("TIER_TOO_LOW");

    const cap         = Math.ceil(Math.ceil(mineral.baseYield * YIELD_ROLL_MAX) * drone.yieldMult);
    const grantAmount = Math.min(claimedQty, cap);

    const claimTs = Date.now();
    if (!(await reserveBudget(saveApi, projectId, playerId, planetId, claimTs))) return reject("BUDGET_EXHAUSTED");

    try {
      await addToInventory(saveApi, projectId, playerId, mineralId, grantAmount);
    } catch (err) {
      try { await releaseBudget(saveApi, projectId, playerId, planetId, claimTs); }
      catch (releaseErr) { logger.error(`ValidateMining: budget release FAILED for ${playerId}: ${releaseErr?.message}`); }
      throw err;
    }

    logger.info(`ValidateMining: player ${playerId} +${grantAmount} ${mineralId} on ${planetId} (claimed ${claimedQty}, cap ${cap})`);
    return { granted: grantAmount, mineralId };
  } catch (err) {
    logger.error("ValidateMining failed", { "error.message": err.message });
    throw err;
  }
};

// ---- helpers ----

// UGS Cloud Save's getItems returns an empty `results` array for keys that don't exist yet — it
// does not throw for that. So a thrown error here is a real (e.g. transient network) failure and
// must propagate, not be treated as "record absent": swallowing it would let callers write
// without a writeLock and silently overwrite (or bypass) whatever they were trying to read.
async function loadRecords(saveApi, projectId, playerId, keys) {
  const out = {};
  const res = await saveApi.getItems(projectId, playerId, keys);
  for (const r of res.data.results) out[r.key] = { value: r.value, writeLock: r.writeLock };
  return out;
}

// The fleet's active drone, if owned; otherwise the starter drone (empty fleet, as in Known Issue #11).
function activeDrone(fleet) {
  const owned = Array.isArray(fleet?.drones) ? fleet.drones : [];
  const entry = owned.find(d => d.droneId === fleet?.activeDroneId) ?? { droneId: STARTER_DRONE_ID, upgrades: {} };
  const def   = DRONES[entry.droneId] ?? DRONES[STARTER_DRONE_ID];
  const level = Number.isInteger(entry.upgrades?.Yield) ? entry.upgrades.Yield : 0;
  return { tier: def.tier, yieldMult: def.yieldMult + level * YIELD_DELTA_PER_LEVEL };
}

// The claim log must be a plain object keyed by planet id. Anything else (null, a primitive, or
// an array — whose planet-keyed properties would be dropped on serialize, silently disabling the
// budget) is treated as an empty log.
function asLog(value) {
  return value && typeof value === "object" && !Array.isArray(value) ? value : {};
}

function prune(log, now) {
  const cutoff = now - RESPAWN_HOURS * MS_PER_HOUR;
  for (const id of Object.keys(log)) {
    log[id] = (Array.isArray(log[id]) ? log[id] : []).filter(ts => ts > cutoff);
    if (log[id].length === 0) delete log[id];
  }
  return log;
}

// Appends claimTs to the planet's log under the record's writeLock. False = budget exhausted.
async function reserveBudget(saveApi, projectId, playerId, planetId, claimTs) {
  for (let attempt = 0; attempt < MAX_WRITE_ATTEMPTS; attempt++) {
    const rec = (await loadRecords(saveApi, projectId, playerId, [CLAIM_LOG_KEY]))[CLAIM_LOG_KEY];
    const log = prune(asLog(rec?.value), claimTs);
    const list = log[planetId] ?? [];
    if (list.length >= FIELD_SIZE) return false;
    log[planetId] = [...list, claimTs];
    try {
      await saveApi.setItem(projectId, playerId, withLock(CLAIM_LOG_KEY, log, rec?.writeLock));
      return true;
    } catch (err) {
      if (isConflict(err) && attempt < MAX_WRITE_ATTEMPTS - 1) continue;
      throw err;
    }
  }
}

async function releaseBudget(saveApi, projectId, playerId, planetId, claimTs) {
  for (let attempt = 0; attempt < MAX_WRITE_ATTEMPTS; attempt++) {
    const rec = (await loadRecords(saveApi, projectId, playerId, [CLAIM_LOG_KEY]))[CLAIM_LOG_KEY];
    const log = asLog(rec?.value);
    log[planetId] = (log[planetId] ?? []).filter(ts => ts !== claimTs);
    if (log[planetId].length === 0) delete log[planetId];
    try {
      await saveApi.setItem(projectId, playerId, withLock(CLAIM_LOG_KEY, log, rec?.writeLock));
      return;
    } catch (err) {
      if (isConflict(err) && attempt < MAX_WRITE_ATTEMPTS - 1) continue;
      throw err;
    }
  }
}

// Read-modify-write under the record's writeLock: SellMinerals writes the same record, and an
// unlocked write here could undo a sale that already paid out (Known Issue #16).
async function addToInventory(saveApi, projectId, playerId, mineralId, qty) {
  for (let attempt = 0; attempt < MAX_WRITE_ATTEMPTS; attempt++) {
    const rec = (await loadRecords(saveApi, projectId, playerId, [INVENTORY_KEY]))[INVENTORY_KEY];
    const inventory = typeof rec?.value === "object" && rec.value ? rec.value : {};
    inventory[mineralId] = (inventory[mineralId] || 0) + qty;
    try {
      await saveApi.setItem(projectId, playerId, withLock(INVENTORY_KEY, inventory, rec?.writeLock));
      return;
    } catch (err) {
      if (isConflict(err) && attempt < MAX_WRITE_ATTEMPTS - 1) continue;
      throw err;
    }
  }
}

function withLock(key, value, writeLock) {
  return writeLock ? { key, value, writeLock } : { key, value };
}

function isConflict(err) {
  const status = err?.response?.status ?? err?.status;
  return status === 409;
}
