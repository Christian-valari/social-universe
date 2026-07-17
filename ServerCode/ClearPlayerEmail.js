// ClearPlayerEmail — clears the CALLING player's own email keys. The inverse of
// SaveEmail.js: it overwrites the reverse-lookup "email_lookup" index value and
// the "player_profile.email" field with the null sentinel so the account no
// longer matches CheckEmailAvailable / RequestPasswordReset's cross-player
// query.
//
// WHY: UGS AuthenticationService.DeleteAccountAsync() deletes only the
// Authentication account — it does NOT cascade-delete Cloud Save data. Without
// this, a cancelled registration leaves an orphaned email_lookup row that keeps
// reporting the email as taken forever (the login-key identity is gone, so
// sign-up's ENTITY_EXISTS backstop can never fire either). AuthService calls
// this immediately before DeleteAccountAsync, while the session token is still
// valid.
//
// FIX: DataApi's constructor doesn't read a { headers: ... } field, and
// getItems/setItem take positional args (projectId, playerId, keys[])/
// (projectId, playerId, { key, value }), not an options object — same
// SDK-shape mismatch documented as Known Issue #6 (see SaveEmail.js).
// No deleteItem precedent exists in this codebase's Cloud Code functions
// (see ConfirmPasswordReset.js / ConfirmEmailVerificationCode.js) — this
// overwrites the records with a null sentinel rather than assuming a delete
// call exists on this SDK version. DataApi(context) authenticates as the
// calling player via the service token.
const { DataApi } = require("@unity-services/cloud-save-1.4");

const PROFILE_KEY      = "player_profile";
const EMAIL_LOOKUP_KEY = "email_lookup"; // must match SaveEmail.js

/**
 * No parameters — operates on the caller (context.playerId).
 */
module.exports = async ({ context, logger }) => {
  const { projectId, playerId } = context;
  if (!playerId) {
    throw new Error("Unauthorized: playerId missing from Cloud Code context — player must be authenticated");
  }
  const saveApi = new DataApi(context);

  // Clear the cross-player reverse-lookup index value.
  await saveApi.setItem(projectId, playerId, { key: EMAIL_LOOKUP_KEY, value: null });

  // Clear the email fields on the profile, preserving any other profile data.
  let profile = {};
  try {
    const res  = await saveApi.getItems(projectId, playerId, [PROFILE_KEY]);
    const item = res.data.results.find(r => r.key === PROFILE_KEY);
    if (item?.value) profile = typeof item.value === "string" ? JSON.parse(item.value) : item.value;
  } catch (_) { /* no profile yet — nothing to clear */ }

  profile.email         = null;
  profile.emailVerified = false;
  profile.updatedMs     = Date.now();
  await saveApi.setItem(projectId, playerId, { key: PROFILE_KEY, value: profile });

  logger.info(`ClearPlayerEmail: email keys cleared for ${playerId}`);
  return { success: true };
};
