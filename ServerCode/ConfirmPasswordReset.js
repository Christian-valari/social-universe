// ConfirmPasswordReset — validates the OTP submitted by the client and calls
// the UGS Authentication Admin API to update the player's password.
//
// SETUP REQUIRED:
//   1. In the UGS dashboard (Cloud Code → Secrets), add:
//        UGS_SERVICE_ACCOUNT_KEY    — service account key ID with Auth admin scope
//        UGS_SERVICE_ACCOUNT_SECRET — service account secret
//   2. Deploy this function via the UGS CLI.
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
const { DataApi } = require("@unity-services/cloud-save-1.4");

const RESET_KEY   = "auth_reset_otp";
const EMAIL_REGEX = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

async function findPlayerByEmail(projectId, accessToken, email) {
  const emailKey = "idx_email_" + emailToKey(email);
  const res = await fetch(
    `https://cloud-save.services.api.unity.com/v1/data/projects/${projectId}/items?key=${encodeURIComponent(emailKey)}`,
    { headers: { Authorization: `Bearer ${accessToken}` } }
  );
  if (!res.ok) return null;
  const data = await res.json();
  return data.results?.[0]?.value?.playerId ?? null;
}

function emailToKey(email) {
  let h = 5381;
  for (let i = 0; i < email.length; i++) h = ((h << 5) + h) ^ email.charCodeAt(i);
  return (h >>> 0).toString(16).padStart(8, "0");
}

async function getAdminToken(serviceKey, serviceSecret) {
  const res = await fetch("https://services.api.unity.com/auth/v1/genesis-token-exchange/unity", {
    method:  "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ scopes: ["authentication.admin"], keyId: serviceKey, secretKey: serviceSecret })
  });
  if (!res.ok) throw new Error(`Service account token exchange failed: ${res.status}`);
  const data = await res.json();
  return data.accessToken;
}

async function resetPasswordViaAdminApi(projectId, playerId, newPassword, serviceKey, serviceSecret) {
  const adminToken = await getAdminToken(serviceKey, serviceSecret);
  const res = await fetch(
    `https://player-auth.services.api.unity.com/v1/authentication/players/${playerId}/password`,
    {
      method:  "PATCH",
      headers: {
        "Authorization": `Bearer ${adminToken}`,
        "Content-Type":  "application/json"
      },
      body: JSON.stringify({ password: newPassword })
    }
  );
  if (!res.ok) throw new Error(`Admin password reset failed: ${res.status}`);
}

/**
 * @param {string} email       - The account email address.
 * @param {string} code        - The 6-digit OTP from the reset email.
 * @param {string} newPassword - The desired new password (min 6 chars).
 */
module.exports = async ({ params, context, logger, secrets }) => {
  const email       = (params.email       ?? "").trim().toLowerCase();
  const code        = (params.code        ?? "").trim();
  const newPassword = (params.newPassword ?? "");

  if (!EMAIL_REGEX.test(email)) throw new Error("Invalid email address");
  if (code.length !== 6)        throw new Error("Reset code must be 6 digits");
  if (newPassword.length < 6)   throw new Error("Password must be at least 6 characters");

  const { projectId, accessToken } = context;
  const saveApi = new DataApi(context);

  const targetPlayerId = await findPlayerByEmail(projectId, accessToken, email);
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
  await resetPasswordViaAdminApi(
    projectId,
    targetPlayerId,
    newPassword,
    secrets.UGS_SERVICE_ACCOUNT_KEY,
    secrets.UGS_SERVICE_ACCOUNT_SECRET
  );

  // Clear the OTP so it cannot be reused
  await saveApi.setItem(projectId, targetPlayerId, { key: RESET_KEY, value: null });

  logger.info(`ConfirmPasswordReset: password reset confirmed for player ${targetPlayerId}`);
  return { success: true };
};
