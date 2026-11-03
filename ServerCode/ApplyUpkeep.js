// ApplyUpkeep — charges recurring per-tile upkeep for all of the caller's
// tiles on a planet. For each owned tile where at least one full day has
// elapsed since lastUpkeepTs:
//   cost = UPKEEP_PER_TILE_PER_DAY * daysElapsed
//   if balance >= cost: deduct cost, advance lastUpkeepTs by daysElapsed days (chargedTiles)
//   else: remove the tile from the registry — it reverts to Available for everyone (revertedTiles)
// Known Issue #19: the registry changes are written under its writeLock *before* the coins are
// deducted (as one charge), so two concurrent calls can't both charge the same days — the second
// re-reads and finds nothing due. If the charge fails, the advanced lastUpkeepTs values are put
// back (reverted tiles stay reverted: the player could not afford them at the time).
const { CurrenciesApi }           = require("@unity-services/economy-2.5");
const { DataApi } = require("@unity-services/cloud-save-1.4");

const CURRENCY_ID  = "COINS";
const REGISTRY_KEY = "land_registry";

const UPKEEP_PER_TILE_PER_DAY = 5; // must match EconomyConfig.UpkeepPerTilePerDay
const MS_PER_DAY = 86400000;

/**
 * @param {string} planetId - ID of the planet to apply upkeep for.
 */
module.exports = async ({ params, context, logger }) => {
  await requireActiveSession(context, params.sessionId);
  const { planetId } = params;

  if (!planetId) {
    throw new Error("Invalid params: planetId is required");
  }

  const { projectId, playerId, accessToken } = context;
  const econApi       = new CurrenciesApi({ accessToken });
  const customDataApi = new DataApi(context);
  const customId      = planetId.toLowerCase();

  try {
    const now = Date.now();

    // 1. Plan against a fresh read of the registry and balance, then write it under the lock.
    const step = await updateRegistry(customDataApi, projectId, customId, async registry => {
      let balance = await currentBalance(econApi, projectId, playerId);
      const plan  = { write: false, balance, cost: 0, chargedTiles: [], revertedTiles: [], advanced: {} };

      for (const [tileId, entry] of Object.entries(registry)) {
        if (entry.ownerId !== playerId) continue;

        const daysElapsed = Math.floor((now - (entry.lastUpkeepTs ?? now)) / MS_PER_DAY);
        if (daysElapsed < 1) continue;

        const cost = UPKEEP_PER_TILE_PER_DAY * daysElapsed;
        if (balance >= cost) {
          balance   -= cost;
          plan.cost += cost;
          // daysElapsed >= 1 implies lastUpkeepTs is set.
          const from = entry.lastUpkeepTs;
          entry.lastUpkeepTs = from + daysElapsed * MS_PER_DAY;
          plan.advanced[tileId] = { from, to: entry.lastUpkeepTs };
          plan.chargedTiles.push(tileId);
        } else {
          delete registry[tileId];
          plan.revertedTiles.push(tileId);
        }
        plan.write = true;
      }
      return plan;
    });

    if (step.conflict) {
      // Busy planet: charge nothing now; the client polls again (EconomyConfig.UpkeepPollIntervalSec).
      logger.warn(`ApplyUpkeep: gave up on ${planetId} for ${playerId} after ${MAX_ATTEMPTS} write conflicts`);
      const balance = await currentBalance(econApi, projectId, playerId);
      return { newBalance: balance, chargedTiles: [], revertedTiles: [] };
    }

    // 2. One deduction for every charged tile. If it fails, undo the lastUpkeepTs advances.
    let newBalance = step.balance;
    if (step.cost > 0) {
      try {
        const deductRes = await econApi.decrementPlayerCurrencyBalance({
          projectId, playerId, currencyId: CURRENCY_ID,
          currencyModifyBalanceRequest: { amount: step.cost }
        });
        newBalance = deductRes.data.balance;
      } catch (err) {
        logger.error(`ApplyUpkeep: charge of ${step.cost} failed for ${playerId} on ${planetId}; undoing ${step.chargedTiles.length} advances. ${err?.message}`);
        await undoAdvances(customDataApi, projectId, customId, playerId, step.advanced, logger);
        throw err;
      }
    }

    logger.info(`ApplyUpkeep: player ${playerId} on ${planetId} — charged ${step.chargedTiles.length}, reverted ${step.revertedTiles.length} → balance ${newBalance}`);
    return { newBalance, chargedTiles: step.chargedTiles, revertedTiles: step.revertedTiles };
  } catch (err) {
    logger.error("ApplyUpkeep failed", { "error.message": err.message });
    throw err;
  }
};

// CurrenciesApi has no getPlayerCurrencyBalance (Known Issue #12) — read all balances and pick one.
async function currentBalance(econApi, projectId, playerId) {
  const res = await econApi.getPlayerCurrencies({ projectId, playerId });
  const c   = res.data.results.find(x => x.currencyId === "COINS");
  return c ? c.balance : 0;
}

// Puts each charged tile's lastUpkeepTs back where this call found it, if nothing has moved it since.
async function undoAdvances(api, projectId, customId, playerId, advanced, logger) {
  try {
    const step = await updateRegistry(api, projectId, customId, registry => {
      let write = false;
      for (const [tileId, { from, to }] of Object.entries(advanced)) {
        const entry = registry[tileId];
        if (!entry || entry.ownerId !== playerId || entry.lastUpkeepTs !== to) continue;
        entry.lastUpkeepTs = from;
        write = true;
      }
      return { write };
    });
    if (step.conflict) throw new Error(`${MAX_ATTEMPTS} write conflicts`);
  } catch (err) {
    logger.error(`ApplyUpkeep: UNDO FAILED on ${customId} (player ${playerId}) — upkeep days given away: ${err?.message}`);
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
  sessionId: "String"
};
