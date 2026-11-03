// GetPlayerProfile — returns any player's public profile. Reads the target
// player's "player_profile" Cloud Save record (the same one GetBootstrapState
// returns for the caller) plus a tile count derived from their owned-tiles
// lists. Returns defaults for players who haven't saved a profile yet.
//
// FIX: DataApi's constructor doesn't read a { headers: ... } field, and
// getItems takes positional args (projectId, playerId, keys[]), not an
// options object — same SDK-shape mismatch as Known Issue #6. The old call
// silently failed every time (caught below), so `profile` was always null.
// DataApi(context) authenticates via the service token, which is required
// to read another player's data.
//
// FIX 2: displayName no longer defaults to a synthetic "Pilot {id6}"
// placeholder — it's null when the player hasn't saved a custom display
// name. The only current caller (PlanetSceneScope.HydrateServerStateAsync,
// hydrating the signed-in player's own HUD name) already falls back to the
// UGS auth username when displayName is empty; the placeholder default was
// unconditionally overriding that correct username for every player who
// hadn't explicitly customized their name.
const { DataApi } = require("@unity-services/cloud-save-1.4");

const PROFILE_KEY        = "player_profile";
const OWNED_TILES_PREFIX = "owned_tiles_";
const MAX_PAGES          = 5; // Cloud Save pages hold 20 keys; a player has well under 100

/**
 * @param {string} playerId - Player ID whose public profile to fetch.
 */
module.exports = async ({ params, context, logger }) => {
  await requireActiveSession(context, params.sessionId);
  const targetId = params.playerId;

  if (!targetId) {
    throw new Error("Invalid params: playerId is required");
  }

  const { projectId } = context;
  const saveApi = new DataApi(context);

  let profile = null;
  let tilesOwned = 0;
  try {
    // Cloud Code's service auth may read another player's data by passing
    // their playerId explicitly.
    const res  = await saveApi.getItems(projectId, targetId, [PROFILE_KEY]);
    const item = res.data.results.find(r => r.key === PROFILE_KEY);
    if (item) profile = typeof item.value === "string" ? JSON.parse(item.value) : item.value;

    // owned_tiles_* lists are server-written (Protected class, Known Issue #16). There is one per
    // planet and the planet ids aren't known here, so page through every protected key.
    let after;
    for (let page = 0; page < MAX_PAGES; page++) {
      const pres  = await saveApi.getProtectedItems(projectId, targetId, undefined, after);
      const items = pres.data.results;
      for (const it of items) {
        if (it.key.startsWith(OWNED_TILES_PREFIX) && Array.isArray(it.value)) tilesOwned += it.value.length;
      }
      if (!pres.data.links?.next || items.length === 0) break;
      after = items[items.length - 1].key;
    }
  } catch (err) {
    logger.warn(`GetPlayerProfile: read failed for ${targetId} (${err.message})`);
  }

  return {
    playerId:     targetId,
    displayName:  profile?.displayName ?? null,
    avatarId:     profile?.avatarId ?? null,
    level:        profile?.level ?? 1,
    xp:           profile?.xp ?? 0,
    badges:       profile?.badges ?? [],
    tilesOwned
  };
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
  playerId: "String",
  sessionId: "String"
};
