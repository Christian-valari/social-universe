// UpdateProfile — validates and commits the caller's display name and/or
// avatar into their "player_profile" Cloud Save record (merging with any
// existing profile fields such as level/xp/badges). Both params are
// independently optional: a call may update just the name, just the
// avatar, or both. The name is re-moderated server-side: the client's
// ChatModerationFilter check is only fast feedback.
// BLOCKED_WORDS / CHAR_MAP / MAX_DISPLAY_NAME_LENGTH must match
// SocialConfig.BlockedWords / ChatModerationFilter / MaxDisplayNameLength —
// same "must match" pattern as ModerateMessage.js (Cloud Code modules deploy
// as standalone files, so the filter is duplicated rather than required).
// AVATAR_IDS must match the 25 AvatarDefinition assets registered on
// DatabaseRegistry (see docs/superpowers/plans/2026-07-06-avatar-selection.md).
//
// FIX: DataApi's constructor doesn't read a { headers: ... } field, and
// getItems/setItem take positional args (projectId, playerId, ...), not an
// options object — same SDK-shape mismatch as GetPlayerProfile.js / GetFuelState.js
// / SpendFuel.js. The old setItem call silently dropped playerId, causing a
// 422 RequiredError; getItems had the same bug but it was masked by the
// surrounding try/catch.
const { DataApi } = require("@unity-services/cloud-save-1.4");

const PROFILE_KEY = "player_profile";
const MAX_DISPLAY_NAME_LENGTH = 20; // must match SocialConfig.MaxDisplayNameLength

const BLOCKED_WORDS = [
  "fuck", "shit", "bitch", "asshole", "cunt", "dick", "faggot",
  "nigger", "nigga", "whore", "slut", "retard", "kys"
];
const CHAR_MAP = { "@": "a", "4": "a", "1": "i", "!": "i", "0": "o", "3": "e", "$": "s", "5": "s", "7": "t" };

const AVATAR_IDS = [
  "avatar_alien_blue", "avatar_alien_green", "avatar_boy1", "avatar_boy_1_dark",
  "avatar_boy_2", "avatar_boy_3", "avatar_boy_4", "avatar_boy_5", "avatar_boy_6",
  "avatar_boy_6_light", "avatar_boy_7", "avatar_boy_8", "avatar_boy_9", "avatar_boy_10",
  "avatar_dark", "avatar_girl_1", "avatar_girl_2", "avatar_girl_2_dark", "avatar_girl_3",
  "avatar_girl_4", "avatar_girl_5", "avatar_girl_6", "avatar_girl_7", "avatar_girl_8",
  "avatar_wizard"
];

function isClean(text) {
  let normalized = "";
  for (const ch of text.toLowerCase()) normalized += CHAR_MAP[ch] ?? ch;
  return !BLOCKED_WORDS.some(word => normalized.includes(word));
}

/**
 * @param {string} [params.displayName] - New display name, 1-20 chars, must pass moderation.
 * @param {string} [params.avatarId] - New avatar id, must be one of AVATAR_IDS.
 */
module.exports = async ({ params, context, logger }) => {
  const hasDisplayName = params.displayName !== undefined && params.displayName !== null;
  const hasAvatarId    = params.avatarId    !== undefined && params.avatarId    !== null;

  let displayName = null;
  if (hasDisplayName) {
    displayName = params.displayName.trim();

    if (displayName.length === 0) {
      return { success: false, reason: "NAME_EMPTY", displayName: null, avatarId: null };
    }
    if (displayName.length > MAX_DISPLAY_NAME_LENGTH) {
      return { success: false, reason: "NAME_TOO_LONG", displayName: null, avatarId: null };
    }
    if (!isClean(displayName)) {
      logger.info(`UpdateProfile: rejected display name from ${context.playerId}`);
      return { success: false, reason: "NAME_REJECTED", displayName: null, avatarId: null };
    }
  }

  let avatarId = null;
  if (hasAvatarId) {
    avatarId = params.avatarId;
    if (!AVATAR_IDS.includes(avatarId)) {
      logger.info(`UpdateProfile: rejected unknown avatarId "${avatarId}" from ${context.playerId}`);
      return { success: false, reason: "AVATAR_INVALID", displayName: null, avatarId: null };
    }
  }

  const { projectId, playerId } = context;
  const saveApi = new DataApi(context);

  let profile = {};
  try {
    const res  = await saveApi.getItems(projectId, playerId, [PROFILE_KEY]);
    const item = res.data.results.find(r => r.key === PROFILE_KEY);
    if (item?.value) profile = typeof item.value === "string" ? JSON.parse(item.value) : item.value;
  } catch (_) { /* no profile yet */ }

  if (hasDisplayName) profile.displayName = displayName;
  if (hasAvatarId)    profile.avatarId    = avatarId;
  profile.updatedMs = Date.now();

  await saveApi.setItem(projectId, playerId, { key: PROFILE_KEY, value: profile });

  logger.info(`UpdateProfile: ${playerId} → displayName=${profile.displayName ?? "(unchanged)"} avatarId=${profile.avatarId ?? "(unchanged)"}`);
  return {
    success: true,
    reason: null,
    displayName: profile.displayName ?? null,
    avatarId: profile.avatarId ?? null
  };
};
