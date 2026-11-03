// RecordVisit — increments a tile's visitCount in the planet's global land
// registry (Custom Data) when another player visits it.
// This is the M3 stand-in for real presence-based visit detection — the
// client calls this when a player selects a tile they don't own (see
// VisitorTrackingController). Feeds into ClaimYield's visit bonus. No economy
// mutation. The registry write carries its writeLock and retries on a conflict (Known Issue #19).
const { DataApi } = require("@unity-services/cloud-save-1.4");

const REGISTRY_KEY    = "land_registry";
const MAX_VISIT_COUNT = 50; // must match EconomyConfig.MaxVisitCount

/**
 * @param {string} tileId - ID of the hex tile being visited.
 * @param {string} planetId - ID of the planet the tile belongs to.
 */
module.exports = async ({ params, context, logger }) => {
  await requireActiveSession(context, params.sessionId);
  const { tileId, planetId } = params;

  if (!tileId || !planetId) {
    throw new Error("Invalid params: tileId and planetId are required");
  }

  const { projectId, playerId } = context;
  const customDataApi = new DataApi(context);
  const customId      = planetId.toLowerCase();

  try {
    const step = await updateRegistry(customDataApi, projectId, customId, registry => {
      const entry = registry[tileId];
      if (!entry || entry.ownerId === playerId) return { write: false, visitCount: entry?.visitCount ?? 0 };
      // Already capped: report the visit without a write (every write bumps the shared lock).
      if ((entry.visitCount ?? 0) >= MAX_VISIT_COUNT) return { write: false, recorded: true, visitCount: MAX_VISIT_COUNT };

      entry.visitCount = (entry.visitCount ?? 0) + 1;
      return { write: true, visitCount: entry.visitCount };
    });
    if (step.conflict) logger.warn(`RecordVisit: gave up on tile ${tileId} (${planetId}) after ${MAX_ATTEMPTS} write conflicts`);

    // RecordVisitResult has only Success and VisitCount (Known Issue #18) — no reason field.
    return { success: Boolean(step.write || step.recorded), visitCount: step.visitCount ?? 0 };
  } catch (err) {
    logger.error("RecordVisit failed", { "error.message": err.message });
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
  sessionId: "String"
};
