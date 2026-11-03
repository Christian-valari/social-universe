// SellLand — validates ownership, removes the tile from the planet's global land registry (so it
// reverts to Available for everyone) under the registry's writeLock, grants the refund, and
// removes the tile from the player's owned-tiles list.
//
// Known Issue #19: the refund used to come from the client (`refund`, any non-negative integer),
// which minted unlimited COINS. It is now computed here as round(LAND_PRICES × RESALE_RATE) —
// half the planet's current purchase price, the same formula LandSaleService shows the player.
// The client may still send `refund`; it is only compared and logged when it differs.
// The registry entry is removed under its writeLock before the refund is granted, so two
// concurrent sales of one tile can't both pay: the second write gets a 409, re-reads and answers
// NOT_OWNER. If the grant fails the entry is put back. Every registry writer uses the same locked
// read-modify-write (updateRegistry below), so none of them can re-add a sold tile.
const { CurrenciesApi }            = require("@unity-services/economy-2.5");
const { DataApi: PlayerDataApi }   = require("@unity-services/cloud-save-1.4");

const CURRENCY_ID  = "COINS";
const REGISTRY_KEY = "land_registry";

// MUST MATCH PurchaseLand.js LAND_PRICES, i.e. round(EconomyConfig._baseLandPrice ×
// PlanetDefinition._landPriceMultiplier), keyed by the planet asset's name lowercased
// (LandPriceAlignmentTests).
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
const RESALE_RATE = 0.5; // MUST MATCH EconomyConfig._landResaleRate (Open Decision: coins-only resale)

/**
 * @param {string} tileId - ID of the hex tile being sold.
 * @param {string} planetId - PlanetDefinition.name of the planet the tile belongs to.
 * @param {number} [refund] - The client's displayed refund. Informational only: never granted.
 */
module.exports = async ({ params, context, logger }) => {
  await requireActiveSession(context, params.sessionId);
  const { tileId, planetId, refund: clientRefund } = params;

  if (!tileId || !planetId) {
    throw new Error("Invalid params: tileId and planetId are required");
  }

  const customId = planetId.toLowerCase();
  const price    = LAND_PRICES[customId];
  if (price === undefined) {
    logger.warn(`SellLand: no land price for planet ${planetId}`);
    return { success: false, reason: "UNKNOWN_PLANET" };
  }
  const refund = Math.round(price * RESALE_RATE);
  if (clientRefund !== undefined && clientRefund !== refund) {
    logger.warn(`SellLand: client refund ${clientRefund} for ${planetId} differs from server refund ${refund}`);
  }

  const { projectId, playerId, accessToken } = context;
  const econApi       = new CurrenciesApi({ accessToken });
  const saveApi       = new PlayerDataApi(context);
  const customDataApi = new PlayerDataApi(context);

  try {
    // 1. Validate ownership and remove the tile from the global registry — one locked write. A
    //    concurrent sale (or any write) since the read is a 409 and a fresh read, so the tile is
    //    paid out at most once.
    const step = await updateRegistry(customDataApi, projectId, customId, registry => {
      const entry = registry[tileId];
      if (!entry || entry.ownerId !== playerId) return { write: false, reason: "NOT_OWNER" };
      delete registry[tileId];
      return { write: true, entry };
    });
    if (step.conflict) return { success: false, reason: "CONFLICT" };
    if (!step.write)   return { success: false, reason: step.reason };
    const { entry } = step;

    // 2. Grant the refund. If it fails, put the tile back.
    let newBalance;
    try {
      newBalance = await currentBalance(econApi, projectId, playerId);
      if (refund > 0) {
        const grantRes = await econApi.incrementPlayerCurrencyBalance({
          projectId, playerId, currencyId: CURRENCY_ID,
          currencyModifyBalanceRequest: { amount: refund }
        });
        newBalance = grantRes.data.balance;
      }
    } catch (err) {
      logger.error(`SellLand: grant failed for ${playerId}; restoring tile ${tileId} on ${planetId}. ${err?.message}`);
      await restoreEntry(customDataApi, projectId, customId, tileId, entry, logger);
      throw err;
    }

    // 3. Remove the tile from the player's owned-tiles list. The sale is paid and the registry (the
    //    authority) no longer lists the tile, so a failure here is logged, not thrown.
    const ownedKey = `owned_tiles_${customId}`;
    try {
      await updateOwnedTiles(saveApi, projectId, playerId, ownedKey, tileId, false, logger);
    } catch (err) {
      logger.error(`SellLand: owned-tiles update failed for ${playerId} (${tileId} on ${planetId}): ${err?.message}`);
    }

    logger.info(`SellLand: player ${playerId} sold tile ${tileId} on ${planetId} for ${refund} → ${newBalance}`);
    return { success: true, newBalance };
  } catch (err) {
    logger.error("SellLand failed", { "error.message": err.message });
    throw err;
  }
};

// CurrenciesApi has no getPlayerCurrencyBalance (Known Issue #12) — read all balances and pick one.
async function currentBalance(econApi, projectId, playerId) {
  const res = await econApi.getPlayerCurrencies({ projectId, playerId });
  const c   = res.data.results.find(x => x.currencyId === "COINS");
  return c ? c.balance : 0;
}

// Puts the seller's entry back (re-read under the lock: other tiles may have changed), unless
// someone has claimed the tile meanwhile.
async function restoreEntry(api, projectId, customId, tileId, entry, logger) {
  try {
    const step = await updateRegistry(api, projectId, customId, registry => {
      if (registry[tileId]) return { write: false, claimed: true };
      registry[tileId] = entry;
      return { write: true };
    });
    if (step.conflict) throw new Error(`${MAX_ATTEMPTS} write conflicts`);
    if (step.claimed)  throw new Error("the tile was claimed meanwhile");
  } catch (err) {
    logger.error(`SellLand: RESTORE FAILED for tile ${tileId} on ${customId} (player ${entry.ownerId}) — manual reconciliation needed: ${err?.message}`);
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
  refund: "Numeric",
  sessionId: "String"
};
