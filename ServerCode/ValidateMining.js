// ValidateMining — grants MINERALS for an idle or active mining claim (M6), server-authoritatively.
// Design: docs/superpowers/specs/2026-09-29-mining-claim-budget-design.md (Known Issue #16).
//
// Request { planetId, mineralId, claimedQty, slotId?, idleSlotId? } (a forfeit: see below). Checks, in order:
//   1. planetId == the server's current_planet. A player with no record has never landed via
//      LandTravel, so they are still on their first planet: the first valid claim pins it.
//   2. mineralId is one of that planet's asteroid minerals
//   3. the saved fleet's active drone tier >= the mineral's asteroid tier
//   4. grant = min(claimedQty, ceil(ceil(baseYield × YIELD_ROLL_MAX) × effectiveYieldMult))
//   5. fewer than FIELD_SIZE claims on this planet in the last RESPAWN_HOURS (mining_claim_log)
// Rejections return { granted: 0, mineralId, reason } so the client can show why, and log one
// warn line (player, planet, mineral, reason) — the only server-side signal for false rejections.
// The budget slot is reserved before minerals are granted and released if the grant fails.
// Every record here is in the Protected access class (player-readable, server-writable only):
// a default-class record could be rewritten by the client to reset the budget or raise the tier.
//
// Optional slotId (docs/superpowers/plans/2026-10-18-cloud-synced-planet-data.md): the asteroid slot
// the claim emptied ("Iron#1"; an idle claim's idleSlotId is used when slotId is absent). Each budget
// entry is { ts, slotId, mineralId }, so GetAsteroidRespawns can tell every device which slots are
// waiting to respawn. A missing or malformed slot is budgeted with slotId null. Entries written before
// slots were recorded are bare timestamps; they still count toward the budget until they expire.
//
// forfeit: true (a failed active-mining minigame, which loses its asteroid): { planetId, mineralId,
// slotId } is checked for planet and mineral like a claim, then logged against the budget like one,
// so the slot stays empty on every device until it respawns — but nothing is granted, the drone tier
// doesn't matter and the planet is never pinned. Answers { granted: 0, mineralId, reason: "FORFEITED" };
// a forfeit without a usable slot is INVALID_PARAMS (it would use a claim and empty nothing).
//
// Optional idleSlotId (server-tracked idle runs, docs/superpowers/plans/2026-10-15-server-tracked-idle-mining.md):
// once the claim has an answer (a grant or any rejection — the client tears its run down either
// way), the run StartIdleMining recorded is ended if it still names this planet and slot. A claim
// that throws leaves the run, so it reappears ready on the next load and can be claimed again.
const { DataApi } = require("@unity-services/cloud-save-1.4");

const INVENTORY_KEY      = "mineral_inventory";
const CLAIM_LOG_KEY      = "mining_claim_log";
const PLANET_KEY         = "current_planet";
const FLEET_KEY          = "drone_fleet";
const MAX_WRITE_ATTEMPTS = 3;
const MS_PER_HOUR        = 3600000;
const SLOT_ID_PATTERN    = /^[A-Za-z0-9_]{1,32}#[0-9]{1,3}$/; // "{AsteroidDefinition._mineralType}#{index}"

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
 * @param {string} [slotId] - Asteroid slot the claim emptied ("Iron#1"), recorded in the claim log.
 * @param {string} [idleSlotId] - Slot of the server-tracked idle run this claim collects, if any.
 * @param {boolean} [forfeit] - True for a failed active-mining minigame: log slotId, grant nothing.
 */
module.exports = async ({ params, context, logger }) => {
  await requireActiveSession(context, params.sessionId);
  const result = params.forfeit === true
    ? await forfeitSlot(params, context, logger)
    : await claim(params, context, logger);
  if (typeof params.idleSlotId === "string" && params.idleSlotId)
    await endIdleRun(context, logger, params.planetId, params.idleSlotId);
  return result;
};

async function claim({ planetId, mineralId, claimedQty, slotId, idleSlotId }, context, logger) {
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

    // Without the pin, a player who never travelled could spend every planet's budget in parallel.
    if (!serverPlanet) {
      const pinned = await pinPlanet(saveApi, projectId, playerId, planetId, records[PLANET_KEY]?.writeLock);
      if (pinned !== planetId) return reject("WRONG_PLANET");
    }

    const cap         = Math.ceil(Math.ceil(mineral.baseYield * YIELD_ROLL_MAX) * drone.yieldMult);
    const grantAmount = Math.min(claimedQty, cap);

    const claimTs = Date.now();
    const entry   = { ts: claimTs, slotId: validSlot(slotId) ?? validSlot(idleSlotId), mineralId };
    if (!(await reserveBudget(saveApi, projectId, playerId, planetId, entry))) return reject("BUDGET_EXHAUSTED");

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
}

// A failed active-mining minigame: the asteroid is lost, so its slot uses one of the planet's claims
// like a granted claim would (GetAsteroidRespawns then keeps it empty on every device), but nothing is
// granted. The drone tier isn't checked and the planet isn't pinned: the player gains nothing either way.
async function forfeitSlot({ planetId, mineralId, slotId }, context, logger) {
  const reject = reason => {
    logger.warn(`ValidateMining: forfeit rejected ${reason} for player ${context?.playerId} (planet ${planetId}, mineral ${mineralId}, slot ${slotId})`);
    return { granted: 0, mineralId: mineralId ?? null, reason };
  };

  const slot = validSlot(slotId);
  if (!planetId || !mineralId || !slot) return reject("INVALID_PARAMS");

  const { projectId, playerId } = context;
  const saveApi = new DataApi(context);

  try {
    const serverPlanet = (await loadRecords(saveApi, projectId, playerId, [PLANET_KEY]))[PLANET_KEY]?.value?.planetId;
    if (serverPlanet && serverPlanet !== planetId) return reject("WRONG_PLANET");
    if (!MINERALS[mineralId] || !(PLANET_MINERALS[planetId] ?? []).includes(mineralId)) return reject("MINERAL_NOT_ON_PLANET");

    if (!(await reserveBudget(saveApi, projectId, playerId, planetId, { ts: Date.now(), slotId: slot, mineralId })))
      return reject("BUDGET_EXHAUSTED");

    logger.info(`ValidateMining: player ${playerId} forfeited ${slot} (${mineralId}) on ${planetId}`);
    return { granted: 0, mineralId, reason: "FORFEITED" };
  } catch (err) {
    logger.error("ValidateMining forfeit failed", { "error.message": err.message });
    throw err;
  }
}

// ---- helpers ----

// Ends the caller's idle run if it is still the one this claim collects (same planet and slot), so
// a newer run started elsewhere is never touched. Best effort: the claim already has its answer, so
// a failure is logged, not thrown — worst case the run shows again and can be claimed once more,
// still inside the claim budget.
async function endIdleRun(context, logger, planetId, slotId) {
  const { projectId, playerId } = context;
  try {
    const ended = await updateIdleSession(new DataApi(context), projectId, playerId, run =>
      run && run.planetId === planetId && run.slotId === slotId
        ? { value: { slotId: null, endedTs: Date.now() }, result: true }
        : { result: false });
    if (ended === IDLE_CONFLICT) logger.error(`ValidateMining: idle run end gave up after write conflicts for ${playerId} (${slotId} on ${planetId})`);
    else if (ended) logger.info(`ValidateMining: ended idle run ${slotId} on ${planetId} for ${playerId}`);
  } catch (err) {
    logger.error(`ValidateMining: idle run end FAILED for ${playerId} (${slotId} on ${planetId}): ${err?.message}`);
  }
}

// UGS Cloud Save's getProtectedItems returns an empty `results` array for keys that don't exist yet — it
// does not throw for that. So a thrown error here is a real (e.g. transient network) failure and
// must propagate, not be treated as "record absent": swallowing it would let callers write
// without a writeLock and silently overwrite (or bypass) whatever they were trying to read.
async function loadRecords(saveApi, projectId, playerId, keys) {
  const out = {};
  const res = await saveApi.getProtectedItems(projectId, playerId, keys);
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

// A budget entry is { ts, slotId, mineralId }, or a bare timestamp if written before slots were kept.
function entryTs(entry) {
  return typeof entry === "number" ? entry : entry?.ts;
}

function validSlot(slotId) {
  return typeof slotId === "string" && SLOT_ID_PATTERN.test(slotId) ? slotId : null;
}

function prune(log, now) {
  const cutoff = now - RESPAWN_HOURS * MS_PER_HOUR;
  for (const id of Object.keys(log)) {
    log[id] = (Array.isArray(log[id]) ? log[id] : []).filter(e => entryTs(e) > cutoff);
    if (log[id].length === 0) delete log[id];
  }
  return log;
}

// Appends the claim's entry to the planet's log under the record's writeLock. False = budget exhausted.
async function reserveBudget(saveApi, projectId, playerId, planetId, entry) {
  for (let attempt = 0; attempt < MAX_WRITE_ATTEMPTS; attempt++) {
    const rec = (await loadRecords(saveApi, projectId, playerId, [CLAIM_LOG_KEY]))[CLAIM_LOG_KEY];
    const log = prune(asLog(rec?.value), entry.ts);
    const list = log[planetId] ?? [];
    if (list.length >= FIELD_SIZE) return false;
    log[planetId] = [...list, entry];
    try {
      await saveApi.setProtectedItem(projectId, playerId, withLock(CLAIM_LOG_KEY, log, rec?.writeLock));
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
    log[planetId] = (log[planetId] ?? []).filter(e => entryTs(e) !== claimTs);
    if (log[planetId].length === 0) delete log[planetId];
    try {
      await saveApi.setProtectedItem(projectId, playerId, withLock(CLAIM_LOG_KEY, log, rec?.writeLock));
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
      await saveApi.setProtectedItem(projectId, playerId, withLock(INVENTORY_KEY, inventory, rec?.writeLock));
      return;
    } catch (err) {
      if (isConflict(err) && attempt < MAX_WRITE_ATTEMPTS - 1) continue;
      throw err;
    }
  }
}

// Records the claimed planet as current_planet. On a write conflict another call pinned first;
// returns whichever planet won. LandTravel is the only other writer.
async function pinPlanet(saveApi, projectId, playerId, planetId, writeLock) {
  try {
    await saveApi.setProtectedItem(projectId, playerId,
      withLock(PLANET_KEY, { planetId, updatedTs: Date.now() }, writeLock));
    return planetId;
  } catch (err) {
    if (!isConflict(err)) throw err;
    const rec = (await loadRecords(saveApi, projectId, playerId, [PLANET_KEY]))[PLANET_KEY];
    return rec?.value?.planetId ?? null;
  }
}

function withLock(key, value, writeLock) {
  return writeLock ? { key, value, writeLock } : { key, value };
}

function isConflict(err) {
  const status = err?.response?.status ?? err?.status;
  return status === 409;
}

// ---- idle_mining_session access (copy verbatim into every script that writes it; PlayerRecordLockTests keeps the copies identical) ----
// The player's idle mining run in progress — { planetId, slotId, mineralId, startTs, durationSec } —
// or { slotId: null, endedTs } once it has ended. Only StartIdleMining and ValidateMining write it.
// change(run) gets the run in progress (null if none) and returns { value, result }: value is
// written (undefined = write nothing) and result returned. The write carries the writeLock it read;
// a 409 re-runs change on a fresh read, 3 attempts, then IDLE_CONFLICT. A missing record (404)
// reads as no run; any other read failure throws.
const IDLE_SESSION_KEY = "idle_mining_session";
const IDLE_CONFLICT    = { conflict: true };

async function updateIdleSession(saveApi, projectId, playerId, change) {
  for (let attempt = 0; attempt < 3; attempt++) {
    let rec;
    try {
      rec = (await saveApi.getProtectedItems(projectId, playerId, [IDLE_SESSION_KEY])).data.results
        .find(r => r.key === IDLE_SESSION_KEY);
    } catch (err) {
      if ((err?.response?.status ?? err?.status) !== 404) throw err;
    }
    const { value, result } = change(rec?.value?.slotId ? rec.value : null);
    if (value === undefined) return result;
    const item = rec?.writeLock
      ? { key: IDLE_SESSION_KEY, value, writeLock: rec.writeLock }
      : { key: IDLE_SESSION_KEY, value };
    try {
      await saveApi.setProtectedItem(projectId, playerId, item);
      return result;
    } catch (err) {
      if ((err?.response?.status ?? err?.status) !== 409) throw err;
    }
  }
  return IDLE_CONFLICT;
}
// ---- end idle_mining_session access

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
  planetId: "String",
  mineralId: "String",
  claimedQty: "Numeric",
  slotId: "String",
  idleSlotId: "String",
  forfeit: "Boolean",
  sessionId: "String"
};
