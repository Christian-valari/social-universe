// GetPlayerProfile — returns any player's public profile. Reads the target
// player's "player_profile" Cloud Save record (the same one GetBootstrapState
// returns for the caller) plus a tile count derived from their owned-tiles
// lists. Returns defaults for players who haven't saved a profile yet.
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

  const { projectId, accessToken } = context;
  const saveApi = new DataApi({ headers: { Authorization: `Bearer ${accessToken}` } });

  let profile = null;
  let tilesOwned = 0;
  try {
    // Cloud Code's service auth may read another player's data by passing
    // their playerId explicitly.
    const res = await saveApi.getItems({ projectId, playerId: targetId });
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
    playerId:    targetId,
    displayName: profile?.displayName ?? `Pilot ${targetId.slice(0, 6)}`,
    level:       profile?.level ?? 1,
    xp:          profile?.xp ?? 0,
    badges:      profile?.badges ?? [],
    tilesOwned
  };
};
