// PurchaseLand — prices the tile server-side (LAND_PRICES), refuses a tile the planet's land
// registry already assigns to anyone, claims the registry entry under its writeLock, deducts
// coins, records per-tile ownership and appends to the player's owned-tiles list so the client
// can restore state on login. The registry is what other players see as the owner.
// The registry entry is { ownerId, buildLevel, lastYieldClaimTs, lastUpkeepTs, visitCount } —
// see GetLandRegistry, PlaceBuild, ClaimYield, RecordVisit, ApplyUpkeep, SellLand for readers/writers.
//
// Known Issue #19: the price used to come from the client (`price`, any positive integer), so a
// modified client bought land for 1 coin and SellLand refunded it at full rate. The client may
// still send `price`; it is only compared against LAND_PRICES and logged when they differ.
// The registry is claimed under its writeLock before coins move, so two buyers racing for one
// tile can't both pay: the second write gets a 409, re-reads and answers OWNED_BY_OTHER. If the
// deduction then fails the claim is released. Every registry writer uses the same locked
// read-modify-write (updateRegistry below).
//
// FIX (Known Issue #6): constructors don't accept { headers: { Authorization: ... } }.
// Economy: { accessToken } authenticates as the calling player. Cloud Save:
// DataApi(context) uses the service token (required for both player-scoped
// writes and custom/game data writes). getItems/setItem take positional args
// (projectId, playerId, ...), not an options object. getPlayerCurrencyBalance
// does not exist on CurrenciesApi — the read method is getPlayerCurrencies.
// decrementPlayerCurrencyBalance requires a configAssignmentHash fetched via
// ConfigurationApi before the write.
const { CurrenciesApi, ConfigurationApi } = require("@unity-services/economy-2.5");
const { DataApi: PlayerDataApi }          = require("@unity-services/cloud-save-1.4");

const CURRENCY_ID  = "COINS";
const REGISTRY_KEY = "land_registry";

// MUST MATCH round(EconomyConfig._baseLandPrice × PlanetDefinition._landPriceMultiplier) for every
// PlanetDefinition (LandPriceAlignmentTests). Keyed by the planet asset's name lowercased — the
// client sends PlanetDefinition.name as `planetId` to every land function, and the registry's
// customId is that same lowercased name. SellLand.js keeps an identical copy.
const LAND_PRICES = {
  planet_earth:   150,
  planet_jupiter: 200,
  planet_mars:    100,
  planet_mercury: 80,
  planet_moon:    120,
  planet_neptune: 300,
  planet_pluto:   500,
  planet_saturn:  180,
  planet_uranus:  250,
  planet_venus:   100,
};

/**
 * @param {string} tileId - ID of the hex tile being purchased.
 * @param {string} planetId - PlanetDefinition.name of the planet the tile belongs to.
 * @param {number} [price] - The client's displayed price. Informational only: never charged.
 */
module.exports = async ({ params, context, logger }) => {
  await requireActiveSession(context, params.sessionId);
  const { tileId, planetId, price: clientPrice } = params;

  if (!tileId || !planetId) {
    throw new Error("Invalid params: tileId and planetId are required");
  }

  const customId = planetId.toLowerCase();
  const price    = LAND_PRICES[customId];
  if (price === undefined) {
    logger.warn(`PurchaseLand: no land price for planet ${planetId}`);
    return { success: false, reason: "UNKNOWN_PLANET" };
  }
  if (clientPrice !== undefined && clientPrice !== price) {
    logger.warn(`PurchaseLand: client price ${clientPrice} for ${planetId} differs from server price ${price}`);
  }

  const { projectId, playerId, accessToken } = context;
  const econApi       = new CurrenciesApi({ accessToken });
  const config        = new ConfigurationApi({ accessToken });
  const saveApi       = new PlayerDataApi(context);
  const customDataApi = new PlayerDataApi(context); // same instance is fine; kept separate for clarity

  try {
    const ownedKey = `owned_tiles_${customId}`;

    // 1. Load the player's current owned-tiles list for this planet.
    let ownedTiles = [];
    try {
      const saveRes = await saveApi.getProtectedItems(projectId, playerId, [ownedKey]);
      const item    = saveRes.data.results.find(r => r.key === ownedKey);
      if (item && Array.isArray(item.value)) ownedTiles = item.value; // already parsed by Cloud Save
    } catch (_) { /* key doesn't exist yet */ }

    if (ownedTiles.includes(tileId)) {
      return { success: false, reason: "ALREADY_OWNED" };
    }

    // 2. Claim the tile in the planet's registry (Custom Data, shared across all players) so other
    //    clients render it as "owned by other". The registry is the authority on who owns a tile:
    //    any entry means it is taken — including one naming this player (e.g. an owned-tiles list
    //    reset by the access-class migration), since buying it again would charge twice and wipe
    //    its buildLevel. Balance is checked against the same read, before anything is written.
    const now  = Date.now();
    const step = await updateRegistry(customDataApi, projectId, customId, async registry => {
      const existing = registry[tileId];
      if (existing) {
        return { write: false, reason: existing.ownerId === playerId ? "ALREADY_OWNED" : "OWNED_BY_OTHER" };
      }

      const balancesRes = await econApi.getPlayerCurrencies({ projectId, playerId });
      const coins       = balancesRes.data.results.find(c => c.currencyId === CURRENCY_ID);
      if ((coins ? coins.balance : 0) < price) {
        return { write: false, reason: "INSUFFICIENT_FUNDS" };
      }

      registry[tileId] = {
        ownerId: playerId,
        buildLevel: 0,
        lastYieldClaimTs: now,
        lastUpkeepTs: now,
        visitCount: 0
      };
      return { write: true };
    });
    if (step.conflict) return { success: false, reason: "CONFLICT" };
    if (!step.write)   return { success: false, reason: step.reason };

    // 3. Deduct coins. configAssignmentHash is required by decrementPlayerCurrencyBalance.
    //    If it fails (e.g. the balance dropped since step 2), release the claim.
    let newBalance;
    try {
      const cfg = await config.getPlayerConfiguration({ projectId, playerId });
      const configAssignmentHash = cfg.data.metadata.configAssignmentHash;

      const deductRes = await econApi.decrementPlayerCurrencyBalance({
        projectId,
        playerId,
        currencyId: CURRENCY_ID,
        configAssignmentHash,
        currencyModifyBalanceRequest: { currencyId: CURRENCY_ID, amount: price }
      });
      newBalance = deductRes.data.balance;
    } catch (err) {
      logger.error(`PurchaseLand: charge failed for ${playerId}; releasing tile ${tileId} on ${planetId}. ${err?.message}`);
      await releaseClaim(customDataApi, projectId, customId, tileId, playerId, logger);
      throw err;
    }

    // 4. Record per-tile ownership (for cross-player lookup in M3+).
    await saveApi.setItem(projectId, playerId, { key: `tile_${tileId}_owner`, value: playerId });

    // 5. Add the tile to the player's owned-tiles list so it can be restored on login. Protected class:
    //    the client reads it but can't write it (GetPlayerProfile counts it — Known Issue #16).
    await updateOwnedTiles(saveApi, projectId, playerId, ownedKey, tileId, true, logger);

    logger.info(`PurchaseLand: player ${playerId} purchased tile ${tileId} on ${planetId} for ${price} → ${newBalance}`);
    return { success: true, newBalance };
  } catch (err) {
    logger.error("PurchaseLand failed", { "error.message": err.message });
    throw err;
  }
};

// Removes this player's claim on tileId (re-read under the lock: other tiles may have changed).
async function releaseClaim(api, projectId, customId, tileId, playerId, logger) {
  try {
    const step = await updateRegistry(api, projectId, customId, registry => {
      if (registry[tileId]?.ownerId !== playerId) return { write: false };
      delete registry[tileId];
      return { write: true };
    });
    if (step.conflict) throw new Error(`${MAX_ATTEMPTS} write conflicts`);
  } catch (err) {
    logger.error(`PurchaseLand: RELEASE FAILED for tile ${tileId} on ${customId} (player ${playerId}) — manual cleanup needed: ${err?.message}`);
  }
}

// ---- land_registry access (Known Issue #19) — identical in every script that writes the registry,
// pinned by LandRegistryLockTests. The registry is one Custom Data item holding every tile on the
// planet, so each write is a read-modify-write of the whole map: it must carry the writeLock it
// read, or it silently undoes a concurrent write (e.g. puts back a tile SellLand just sold).
const MAX_ATTEMPTS = 3;

// Reads the registry and its writeLock. Only a missing registry reads as {}: any other failure
// throws, so a transient read error can never turn into a write that wipes every tile.
async function loadRegistry(api, projectId, customId) {
  try {
    const res  = await api.getCustomItems(projectId, customId, [REGISTRY_KEY]);
    const item = res.data.results.find(r => r.key === REGISTRY_KEY);
    return item?.value ? { registry: item.value, writeLock: item.writeLock } : { registry: {}, writeLock: undefined };
  } catch (err) {
    if (statusOf(err) === 404) return { registry: {}, writeLock: undefined };
    throw err;
  }
}

// Read-modify-write under the writeLock. `mutate(registry)` validates against a fresh read and
// either edits the registry and returns { write: true, ... } or returns { write: false, ... } to stop.
// A 409 (someone wrote since the read) re-runs mutate on a fresh read, up to MAX_ATTEMPTS; after
// that the result is { write: false, conflict: true }. Returns whatever mutate returned.
async function updateRegistry(api, projectId, customId, mutate) {
  for (let attempt = 1; attempt <= MAX_ATTEMPTS; attempt++) {
    const { registry, writeLock } = await loadRegistry(api, projectId, customId);
    const step = await mutate(registry);
    if (!step.write) return step;
    try {
      await api.setCustomItem(projectId, customId,
        writeLock ? { key: REGISTRY_KEY, value: registry, writeLock } : { key: REGISTRY_KEY, value: registry });
      return step;
    } catch (err) {
      if (statusOf(err) !== 409) throw err;
    }
  }
  return { write: false, conflict: true };
}

function statusOf(err) {
  return err?.response?.status ?? err?.status;
}
// ---- end land_registry access

// ---- owned_tiles access (copy verbatim into PurchaseLand and SellLand; PlayerRecordLockTests keeps them identical) ----
// owned_tiles_<planet> is the player's index of their land on a planet (land_registry is the
// authority). Two purchases or sales on one planet each rewrite the whole list, so the write carries
// the writeLock it read and a 409 re-applies the change to a fresh read — no tile is dropped. A failed
// read throws rather than rewriting the list from nothing.
async function updateOwnedTiles(saveApi, projectId, playerId, ownedKey, tileId, owned, logger) {
  for (let attempt = 0; attempt < 3; attempt++) {
    let tiles = [], writeLock;
    try {
      const res  = await saveApi.getProtectedItems(projectId, playerId, [ownedKey]);
      const item = res.data.results.find(r => r.key === ownedKey);
      if (item) { writeLock = item.writeLock; if (Array.isArray(item.value)) tiles = item.value; }
    } catch (err) {
      if ((err?.response?.status ?? err?.status) !== 404) throw err;
    }
    if (tiles.includes(tileId) === owned) return;
    const next = owned ? [...tiles, tileId] : tiles.filter(id => id !== tileId);
    try {
      await saveApi.setProtectedItem(projectId, playerId,
        writeLock ? { key: ownedKey, value: next, writeLock } : { key: ownedKey, value: next });
      return;
    } catch (err) {
      if ((err?.response?.status ?? err?.status) !== 409) throw err;
      logger.warn(`${ownedKey}: write conflict for ${playerId} (attempt ${attempt + 1}), re-reading`);
    }
  }
  logger.error(`${ownedKey}: gave up ${owned ? "adding" : "removing"} ${tileId} for ${playerId} after 3 write conflicts`);
}
// ---- end owned_tiles access

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
  tileId: "String",
  planetId: "String",
  price: "Numeric",
  sessionId: "String"
};
