// GetLandRegistry — returns the global tile-ownership map for a planet so a
// client can render other players' tiles as "owned by other".
// Reads from Cloud Save Custom Data (shared across all players, keyed by
// planet rather than by player) — written by PurchaseLand (and future
// SellLand). Returns an empty map if the registry doesn't exist yet (404 or no item). Any other
// read failure throws: answering {} would show every tile on the planet as unowned, and the client
// now retries a failed load instead of opening the planet with it.
const { DataApi } = require("@unity-services/cloud-save-1.4");

const REGISTRY_KEY = "land_registry";

/**
 * @param {string} planetId - ID of the planet to fetch the land registry for.
 */
module.exports = async ({ params, context, logger }) => {
  await requireActiveSession(context, params.sessionId);
  const { planetId } = params;

  if (!planetId) {
    throw new Error("Invalid params: planetId is required");
  }

  const { projectId } = context;
  const customDataApi = new DataApi(context);
  const customId = planetId.toLowerCase();

  try {
    const res  = await customDataApi.getCustomItems(projectId, customId, [REGISTRY_KEY]);
    const item = res.data.results.find(r => r.key === REGISTRY_KEY);
    return { tiles: item?.value ?? {} };
  } catch (err) {
    if ((err?.response?.status ?? err?.status) !== 404) throw err;
    logger.warn(`GetLandRegistry: no registry yet for ${planetId} (${err.message})`);
    return { tiles: {} };
  }
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
  planetId: "String",
  sessionId: "String"
};
