// SaveEmail — stores the player's email in their "player_profile" Cloud Save
// record and writes a reverse-lookup index used by RequestPasswordReset to
// identify the account from an email address alone.
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

const PROFILE_KEY = "player_profile";
const EMAIL_REGEX = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

function emailToKey(email) {
  // Deterministic short key from the email without storing PII as a key name.
  // djb2 hash — no crypto module needed (not available in UGS Cloud Code).
  let h = 5381;
  for (let i = 0; i < email.length; i++) h = ((h << 5) + h) ^ email.charCodeAt(i);
  return (h >>> 0).toString(16).padStart(8, "0");
}

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

  // Write the reverse-lookup index: idx_email_<hash> → { playerId, email }
  // Stored under the calling player's own record so no admin token is needed.
  // RequestPasswordReset reads this via the UGS Cloud Save admin query API.
  const indexKey = "idx_email_" + emailToKey(email);
  await saveApi.setItem(projectId, playerId, { key: indexKey, value: { playerId, email } });

  logger.info(`SaveEmail: email stored and index written for ${playerId}`);
  return { success: true };
};
