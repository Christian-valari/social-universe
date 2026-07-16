// SaveEmail — stores the player's email in their "player_profile" Cloud Save
// record and writes a reverse-lookup field used by RequestPasswordReset to
// identify the account from an email address alone.
//
// SETUP REQUIRED: in the UGS dashboard (Cloud Save → Indexes), add an index
// on the "email_lookup" key (Player Data, Default access class). Values saved
// BEFORE the index existed are never indexed (Cloud Save does not backfill) —
// AuthService re-runs this function on every email login to re-save the key
// for such accounts. Without the index,
// RequestPasswordReset/ConfirmPasswordReset's cross-player query fails to
// find any player, regardless of whether the email is registered.
//
// FIX: DataApi's constructor doesn't read a { headers: ... } field, and
// getItems/setItem take positional args (projectId, playerId, keys[])/
// (projectId, playerId, { key, value }), not an options object — same
// SDK-shape mismatch documented as Known Issue #6. The old object-style
// calls sent playerId as undefined to the real positional parameter,
// producing "RequiredError: Required parameter playerId was null or
// undefined when calling setItem". DataApi(context) authenticates as the
// calling player via the service token, matching PurchaseLand/GetFuelState/etc.
const { DataApi } = require("@unity-services/cloud-save-1.4");

const PROFILE_KEY     = "player_profile";
const EMAIL_REGEX     = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

// Fixed key name (not per-email) that RequestPasswordReset/ConfirmPasswordReset
// look up via Cloud Save's cross-player Query API — indexes are configured per
// key name in the UGS dashboard, so the key must be constant across players;
// the email itself is the value being matched. Must be identical in this file
// and in RequestPasswordReset.js/ConfirmPasswordReset.js.
const EMAIL_LOOKUP_KEY = "email_lookup";

/**
 * @param {string} email - The player's email address to persist.
 */
module.exports = async ({ params, context, logger }) => {
  const email = (params.email ?? "").trim().toLowerCase();

  if (!EMAIL_REGEX.test(email)) {
    throw new Error("Invalid email address");
  }

  const { projectId, playerId } = context;
  if (!playerId) {
    throw new Error("Unauthorized: playerId missing from Cloud Code context — player must be authenticated");
  }
  const saveApi = new DataApi(context);

  // Merge email into player_profile
  let profile = {};
  try {
    const res  = await saveApi.getItems(projectId, playerId, [PROFILE_KEY]);
    const item = res.data.results.find(r => r.key === PROFILE_KEY);
    if (item?.value) profile = typeof item.value === "string" ? JSON.parse(item.value) : item.value;
  } catch (_) {}

  profile.email     = email;
  profile.updatedMs = Date.now();
  await saveApi.setItem(projectId, playerId, { key: PROFILE_KEY, value: profile });

  // Write the reverse-lookup field under a fixed key name so
  // RequestPasswordReset/ConfirmPasswordReset can find this player by email
  // via Cloud Save's cross-player Query API (queryDefaultPlayerData) — see
  // SETUP REQUIRED below.
  await saveApi.setItem(projectId, playerId, { key: EMAIL_LOOKUP_KEY, value: email });

  logger.info(`SaveEmail: email stored and index written for ${playerId}`);
  return { success: true };
};
