// UpdateProfile — validates and commits the caller's display name into their
// "player_profile" Cloud Save record (merging with any existing profile
// fields such as level/xp/badges). The name is re-moderated server-side: the
// client's ChatModerationFilter check is only fast feedback.
// BLOCKED_WORDS / CHAR_MAP / MAX_DISPLAY_NAME_LENGTH must match
// SocialConfig.BlockedWords / ChatModerationFilter / MaxDisplayNameLength —
// same "must match" pattern as ModerateMessage.js (Cloud Code modules deploy
// as standalone files, so the filter is duplicated rather than required).
const { DataApi } = require("@unity-services/cloud-save-1.4");

const PROFILE_KEY = "player_profile";
const MAX_DISPLAY_NAME_LENGTH = 20; // must match SocialConfig.MaxDisplayNameLength

const BLOCKED_WORDS = [
  "fuck", "shit", "bitch", "asshole", "cunt", "dick", "faggot",
  "nigger", "nigga", "whore", "slut", "retard", "kys"
];
const CHAR_MAP = { "@": "a", "4": "a", "1": "i", "!": "i", "0": "o", "3": "e", "$": "s", "5": "s", "7": "t" };

function isClean(text) {
  let normalized = "";
  for (const ch of text.toLowerCase()) normalized += CHAR_MAP[ch] ?? ch;
  return !BLOCKED_WORDS.some(word => normalized.includes(word));
}

/**
 * @param {string} displayName - The new display name. 1–20 characters, must pass moderation.
 */
module.exports = async ({ params, context, logger }) => {
  const displayName = (params.displayName ?? "").trim();

  if (displayName.length === 0) {
    return { success: false, reason: "NAME_EMPTY", displayName: null };
  }
  if (displayName.length > MAX_DISPLAY_NAME_LENGTH) {
    return { success: false, reason: "NAME_TOO_LONG", displayName: null };
  }
  if (!isClean(displayName)) {
    logger.info(`UpdateProfile: rejected display name from ${context.playerId}`);
    return { success: false, reason: "NAME_REJECTED", displayName: null };
  }

  const { projectId, playerId, accessToken } = context;
  const saveApi = new DataApi({ headers: { Authorization: `Bearer ${accessToken}` } });

  let profile = {};
  try {
    const res  = await saveApi.getItems({ projectId, playerId, key: [PROFILE_KEY] });
    const item = res.data.results.find(r => r.key === PROFILE_KEY);
    if (item?.value) profile = typeof item.value === "string" ? JSON.parse(item.value) : item.value;
  } catch (_) { /* no profile yet */ }

  profile.displayName = displayName;
  profile.updatedMs   = Date.now();

  await saveApi.setItem({ projectId, playerId, key: PROFILE_KEY, body: { value: profile } });

  logger.info(`UpdateProfile: ${playerId} → "${displayName}"`);
  return { success: true, reason: null, displayName };
};
