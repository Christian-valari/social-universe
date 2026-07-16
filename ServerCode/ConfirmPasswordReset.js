// ConfirmPasswordReset — validates the OTP submitted by the client and calls
// the UGS Authentication Admin API to update the player's password.
//
// SETUP REQUIRED:
//   1. In the UGS dashboard (Cloud Code → Secrets), add:
//        UGS_SERVICE_ACCOUNT_KEY    — service account key ID with Auth admin scope
//        UGS_SERVICE_ACCOUNT_SECRET — service account secret
//   2. In the UGS dashboard (Cloud Save → Indexes), add an index on the
//      "email_lookup" key (Player Data, Default access class) — see
//      SaveEmail.js. findPlayerByEmail's query fails to match anyone without it.
//   3. Deploy this function via the UGS CLI.
//
// Throws on invalid/expired OTP; always clears the OTP record on success.
//
// FIX: DataApi's constructor doesn't read a { headers: ... } field, and
// getItems takes positional args (projectId, playerId, keys[]), not an
// options object — same SDK-shape mismatch as Known Issue #6 (see
// SaveEmail.js). DataApi(context) authenticates via the service token. Also,
// no deleteItem precedent exists elsewhere in this codebase's Cloud Code
// functions (see LandTravel.js) — this now overwrites the OTP record with a
// null sentinel rather than assuming a delete call exists on this SDK version.
//
// FIX: findPlayerByEmail previously hit a raw `GET .../items?key=...` REST
// endpoint using the *caller's own* player access token, which can only ever
// see the token owner's own Cloud Save items — it always returned null, so
// this function always threw "No account found" (see RequestPasswordReset.js
// for the matching fix). Cross-player lookups require Cloud Save's Query API
// (queryDefaultPlayerData) via the elevated Cloud Code DataApi, matched
// against the fixed "email_lookup" key SaveEmail.js writes.
const { DataApi } = require("@unity-services/cloud-save-1.4");
const axios       = require("axios-1.6");

const EMAIL_LOOKUP_KEY = "email_lookup"; // must match SaveEmail.js
const RESET_KEY   = "auth_reset_otp";
const EMAIL_REGEX = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

async function findPlayerByEmail(saveApi, projectId, email, logger) {
  // A query error (e.g. the index missing or not yet READY) is logged loudly
  // instead of being folded into "no match" — see RequestPasswordReset.js.
  try {
    const res = await saveApi.queryDefaultPlayerData(projectId, {
      // asc is mandatory on every query field (400 "asc must be specified"
      // without it), even though sort order is irrelevant for an EQ match.
      fields: [{ key: EMAIL_LOOKUP_KEY, op: "EQ", value: email, asc: true }],
    });
    const results = res.data.results ?? [];
    logger.info(`findPlayerByEmail v2: query returned ${results.length} match(es)`);
    const match = results[0];
    return match?.id ?? match?.playerId ?? null;
  } catch (err) {
    const detail = err.response?.data ? JSON.stringify(err.response.data) : err.message;
    logger.error(`findPlayerByEmail v2: query FAILED (not a no-match): ${detail}`);
    return null;
  }
}

async function getAdminToken(serviceKey, serviceSecret) {
  try {
    const res = await axios.post("https://services.api.unity.com/auth/v1/genesis-token-exchange/unity", {
      scopes: ["authentication.admin"], keyId: serviceKey, secretKey: serviceSecret
    }, {
      headers: { "Content-Type": "application/json" }
    });
    return res.data.accessToken;
  } catch (err) {
    throw new Error(`Service account token exchange failed: ${err.response?.status ?? err.message}`);
  }
}

async function resetPasswordViaAdminApi(projectId, playerId, newPassword, serviceKey, serviceSecret) {
  const adminToken = await getAdminToken(serviceKey, serviceSecret);
  try {
    await axios.patch(
      `https://player-auth.services.api.unity.com/v1/authentication/players/${playerId}/password`,
      { password: newPassword },
      {
        headers: {
          "Authorization": `Bearer ${adminToken}`,
          "Content-Type":  "application/json"
        }
      }
    );
  } catch (err) {
    throw new Error(`Admin password reset failed: ${err.response?.status ?? err.message}`);
  }
}

/**
 * @param {string} email       - The account email address.
 * @param {string} code        - The 6-digit OTP from the reset email.
 * @param {string} newPassword - The desired new password (min 6 chars).
 */
module.exports = async ({ params, context, logger, secretManager }) => {
  const email       = (params.email       ?? "").trim().toLowerCase();
  const code        = (params.code        ?? "").trim();
  const newPassword = (params.newPassword ?? "");

  if (!EMAIL_REGEX.test(email)) throw new Error("Invalid email address");
  if (code.length !== 6)        throw new Error("Reset code must be 6 digits");
  if (newPassword.length < 6)   throw new Error("Password must be at least 6 characters");

  const { projectId } = context;
  const saveApi = new DataApi(context);

  const targetPlayerId = await findPlayerByEmail(saveApi, projectId, email, logger);
  if (!targetPlayerId) throw new Error("No account found for this email address");

  // Load and validate OTP record
  let resetRecord = null;
  try {
    const res  = await saveApi.getItems(projectId, targetPlayerId, [RESET_KEY]);
    const item = res.data.results.find(r => r.key === RESET_KEY);
    if (item?.value) resetRecord = typeof item.value === "string" ? JSON.parse(item.value) : item.value;
  } catch (_) {}

  if (!resetRecord)                      throw new Error("No password reset is pending for this email");
  if (Date.now() > resetRecord.expiresAt) throw new Error("Reset code has expired — request a new one");
  if (code !== resetRecord.otp)          throw new Error("Invalid reset code");

  // Reset the password via the UGS Admin API
  const serviceKey    = await secretManager.getSecret("UGS_SERVICE_ACCOUNT_KEY");
  const serviceSecret = await secretManager.getSecret("UGS_SERVICE_ACCOUNT_SECRET");
  await resetPasswordViaAdminApi(
    projectId,
    targetPlayerId,
    newPassword,
    serviceKey,
    serviceSecret
  );

  // Clear the OTP so it cannot be reused
  await saveApi.setItem(projectId, targetPlayerId, { key: RESET_KEY, value: null });

  logger.info(`ConfirmPasswordReset: password reset confirmed for player ${targetPlayerId}`);
  return { success: true };
};
