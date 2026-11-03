// PurchaseHexatile — unlocks hexatile[hexIndex] on an owned tile if it is adjacent
// to an already-unlocked hexatile. Price is computed SERVER-SIDE from the current
// unlocked count (base + step*(unlocked - FREE)); never trusts a client price.
// Board geometry (radius, free count, spiral order, neighbors) MIRRORS
// Assets/_Project/Scripts/Economy/HexBoardMath.cs — keep in sync.
// Known Issue #19: the unlock is written under the registry's writeLock *before* coins are
// deducted, so two concurrent unlocks can't both charge or price off the same count (the second
// re-reads). If the deduction fails, the hex is locked again.
const { CurrenciesApi, ConfigurationApi } = require("@unity-services/economy-2.5");
const { DataApi }                         = require("@unity-services/cloud-save-1.4");

const CURRENCY_ID  = "COINS";
const REGISTRY_KEY = "land_registry";
const RADIUS = 2, FREE_COUNT = 5, BASE_PRICE = 200, PRICE_STEP = 100;
const DIRS = [[1,0],[1,-1],[0,-1],[-1,0],[-1,1],[0,1]];

function cells(radius) {
  const out = [[0,0]];
  for (let k = 1; k <= radius; k++) {
    let hex = [DIRS[4][0]*k, DIRS[4][1]*k];
    for (let side = 0; side < 6; side++)
      for (let step = 0; step < k; step++) { out.push([hex[0], hex[1]]); hex = [hex[0]+DIRS[side][0], hex[1]+DIRS[side][1]]; }
  }
  return out;
}
function neighbors(radius) {
  const c = cells(radius), key = (a) => `${a[0]},${a[1]}`, idx = {};
  c.forEach((a, i) => idx[key(a)] = i);
  return c.map(a => DIRS.map(d => idx[key([a[0]+d[0], a[1]+d[1]])]).filter(n => n !== undefined));
}
function hexCount(radius) { return 3*radius*radius + 3*radius + 1; }

module.exports = async ({ params, context, logger }) => {
  await requireActiveSession(context, params.sessionId);
  const { tileId, planetId, hexIndex } = params;
  const N = hexCount(RADIUS);
  if (!tileId || !planetId || !Number.isInteger(hexIndex) || hexIndex < 0 || hexIndex >= N) {
    throw new Error("Invalid params: tileId, planetId, hexIndex required");
  }

  const { projectId, playerId, accessToken } = context;
  const econApi = new CurrenciesApi({ accessToken });
  const config  = new ConfigurationApi({ accessToken });
  const dataApi = new DataApi(context);
  const customId = planetId.toLowerCase();

  try {
    // 1. Validate, price and unlock — one locked write.
    const step = await updateRegistry(dataApi, projectId, customId, async registry => {
      const entry = registry[tileId];
      if (!entry || entry.ownerId !== playerId) return { write: false, reason: "NOT_OWNER" };

      // Normalize unlocked mask: free indices default true.
      let unlocked = Array.isArray(entry.unlocked) ? entry.unlocked.slice(0, N) : [];
      while (unlocked.length < N) unlocked.push(false);
      if (!Array.isArray(entry.unlocked)) for (let i = 0; i < FREE_COUNT; i++) unlocked[i] = true;

      if (unlocked[hexIndex]) return { write: false, reason: "ALREADY_UNLOCKED" };

      const nb = neighbors(RADIUS);
      const adjacent = nb[hexIndex].some(n => unlocked[n]);
      if (!adjacent) return { write: false, reason: "NOT_ADJACENT" };

      const unlockedCount = unlocked.filter(Boolean).length;
      const price = BASE_PRICE + PRICE_STEP * (unlockedCount - FREE_COUNT);

      const balances = await econApi.getPlayerCurrencies({ projectId, playerId });
      const coins = balances.data.results.find(c => c.currencyId === CURRENCY_ID);
      if ((coins ? coins.balance : 0) < price) return { write: false, reason: "INSUFFICIENT_FUNDS" };

      unlocked[hexIndex] = true;
      entry.unlocked = unlocked;
      return { write: true, price, unlockedCount: unlockedCount + 1 };
    });

    if (step.conflict) return { success: false, reason: "CONFLICT" };
    if (!step.write)   return { success: false, reason: step.reason };

    // 2. Deduct. If it fails, lock the hex again.
    let deduct;
    try {
      const cfg = await config.getPlayerConfiguration({ projectId, playerId });
      const configAssignmentHash = cfg.data.metadata.configAssignmentHash;
      deduct = await econApi.decrementPlayerCurrencyBalance({
        projectId, playerId, currencyId: CURRENCY_ID, configAssignmentHash,
        currencyModifyBalanceRequest: { currencyId: CURRENCY_ID, amount: step.price }
      });
    } catch (err) {
      logger.error(`PurchaseHexatile: charge failed for ${playerId}; relocking hex ${hexIndex} of ${tileId} (${planetId}). ${err?.message}`);
      await relock(dataApi, projectId, customId, tileId, playerId, hexIndex, logger);
      throw err;
    }

    logger.info(`PurchaseHexatile: ${playerId} unlocked hex ${hexIndex} of ${tileId} (${planetId}) for ${step.price}`);
    return { success: true, newBalance: deduct.data.balance, unlockedCount: step.unlockedCount };
  } catch (err) {
    logger.error("PurchaseHexatile failed", { "error.message": err.message });
    throw err;
  }
};

// Undoes step 1 if the hex is still unlocked and empty on this player's tile.
async function relock(api, projectId, customId, tileId, playerId, hexIndex, logger) {
  try {
    const step = await updateRegistry(api, projectId, customId, registry => {
      const entry = registry[tileId];
      if (!entry || entry.ownerId !== playerId || !Array.isArray(entry.unlocked) || !entry.unlocked[hexIndex]) return { write: false };
      if (Array.isArray(entry.slots) && entry.slots[hexIndex]) return { write: false, occupied: true };
      entry.unlocked[hexIndex] = false;
      return { write: true };
    });
    if (step.conflict) throw new Error(`${MAX_ATTEMPTS} write conflicts`);
    if (step.occupied) throw new Error("an item was placed on it meanwhile");
  } catch (err) {
    logger.error(`PurchaseHexatile: RELOCK FAILED for hex ${hexIndex} of ${tileId} on ${customId} (player ${playerId}) — hex unlocked unpaid: ${err?.message}`);
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
  tileId: "String",
  planetId: "String",
  hexIndex: "Numeric",
  sessionId: "String"
};
