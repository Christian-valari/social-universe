// MoveBuild — moves an item from fromHex to an empty toHex on an owned tile.
// Free; buildLevel unchanged (filled count is invariant under a move).
// The registry write carries its writeLock and retries on a conflict (Known Issue #19).
const { DataApi } = require("@unity-services/cloud-save-1.4");

const REGISTRY_KEY = "land_registry";
const HEX_COUNT    = 19; // must match HexBoardMath.HexCount(2) / EconomyConfig.HexCount
const FREE_COUNT   = 5;  // free hexatiles default unlocked

/**
 * @param {string} tileId
 * @param {string} planetId
 * @param {number} fromHex
 * @param {number} toHex
 */
module.exports = async ({ params, context, logger }) => {
  await requireActiveSession(context, params.sessionId);
  const { tileId, planetId, fromHex, toHex } = params;

  const inRange = i => Number.isInteger(i) && i >= 0 && i < HEX_COUNT;
  if (!tileId || !planetId || !inRange(fromHex) || !inRange(toHex) || fromHex === toHex) {
    throw new Error("Invalid params: tileId, planetId, distinct in-range fromHex/toHex required");
  }

  const { projectId, playerId } = context;
  const customDataApi = new DataApi(context);
  const customId      = planetId.toLowerCase();

  try {
    const step = await updateRegistry(customDataApi, projectId, customId, registry => {
      const entry = registry[tileId];
      if (!entry || entry.ownerId !== playerId) {
        return { write: false, reason: "NOT_OWNER" };
      }

      // Normalize unlocked mask (free indices default true) and require the destination unlocked.
      let unlocked = Array.isArray(entry.unlocked) ? entry.unlocked.slice(0, HEX_COUNT) : [];
      while (unlocked.length < HEX_COUNT) unlocked.push(false);
      if (!Array.isArray(entry.unlocked)) for (let i = 0; i < FREE_COUNT; i++) unlocked[i] = true;
      if (!unlocked[toHex]) {
        return { write: false, reason: "TILE_LOCKED" };
      }

      const slots = entry.slots;
      if (!Array.isArray(slots) || !slots[fromHex] || slots[toHex]) {
        return { write: false, reason: "INVALID_MOVE" };
      }

      entry.unlocked = unlocked;
      slots[toHex]   = slots[fromHex];
      slots[fromHex] = null;
      return { write: true };
    });

    if (step.conflict) return { success: false, reason: "CONFLICT" };
    if (!step.write)   return { success: false, reason: step.reason };

    logger.info(`MoveBuild: ${playerId} moved hex ${fromHex}->${toHex} on ${tileId} (${planetId})`);
    return { success: true };
  } catch (err) {
    logger.error("MoveBuild failed", { "error.message": err.message });
    throw err;
  }
};

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
  fromHex: "Numeric",
  toHex: "Numeric",
  sessionId: "String"
};
