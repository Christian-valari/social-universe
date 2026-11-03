// UpdateProfile — validates and commits the caller's display name and/or
// avatar into their "player_profile" Cloud Save record (merging with any
// existing profile fields such as level/xp/badges). Both params are
// independently optional: a call may update just the name, just the
// avatar, or both. The name is re-moderated server-side: the client's
// ChatModerationFilter check is only fast feedback.
// BLOCKED_WORDS / CHAR_MAP / MAX_DISPLAY_NAME_LENGTH must match
// SocialConfig.BlockedWords / ChatModerationFilter / MaxDisplayNameLength —
// same "must match" pattern as the other ServerCode catalogs (Cloud Code modules deploy
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
  await requireActiveSession(context, params.sessionId);
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

  // Only the fields sent change, on a fresh read, under the record's writeLock: a name change and an
  // avatar change racing each other (two devices, or two quick saves) both survive. A failed read
  // throws — a profile rebuilt from nothing would wipe the fields not sent.
  let profile = null;
  for (let attempt = 0; attempt < 3 && !profile; attempt++) {
    let current = {}, writeLock;
    try {
      const res  = await saveApi.getItems(projectId, playerId, [PROFILE_KEY]);
      const item = res.data.results.find(r => r.key === PROFILE_KEY);
      if (item) writeLock = item.writeLock;
      if (item?.value) current = typeof item.value === "string" ? JSON.parse(item.value) : item.value;
    } catch (err) {
      if ((err?.response?.status ?? err?.status) !== 404) throw err;
    }

    if (hasDisplayName) current.displayName = displayName;
    if (hasAvatarId)    current.avatarId    = avatarId;
    current.updatedMs = Date.now();

    try {
      await saveApi.setItem(projectId, playerId,
        writeLock ? { key: PROFILE_KEY, value: current, writeLock } : { key: PROFILE_KEY, value: current });
      profile = current;
    } catch (err) {
      if ((err?.response?.status ?? err?.status) !== 409) throw err;
      logger.warn(`UpdateProfile: write conflict for ${playerId} (attempt ${attempt + 1}), re-reading`);
    }
  }
  if (!profile) {
    logger.warn(`UpdateProfile: gave up for ${playerId} after 3 write conflicts`);
    return { success: false, reason: "CONFLICT", displayName: null, avatarId: null };
  }

  logger.info(`UpdateProfile: ${playerId} → displayName=${profile.displayName ?? "(unchanged)"} avatarId=${profile.avatarId ?? "(unchanged)"}`);
  return {
    success: true,
    reason: null,
    displayName: profile.displayName ?? null,
    avatarId: profile.avatarId ?? null
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
  displayName: "String",
  avatarId: "String",
  sessionId: "String"
};
