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

const PROFILE_KEY = "player_profile";

/**
 * @param {string} playerId - Player ID whose public profile to fetch.
 */
module.exports = async ({ params, context, logger }) => {
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
    const res = await saveApi.getItems(projectId, targetId);
    for (const item of res.data.results) {
      if (item.key === PROFILE_KEY) {
        profile = typeof item.value === "string" ? JSON.parse(item.value) : item.value;
      } else if (item.key.startsWith("owned_tiles_") && Array.isArray(item.value)) {
        tilesOwned += item.value.length;
      }
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
