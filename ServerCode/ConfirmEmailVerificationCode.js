// ConfirmEmailVerificationCode — validates the OTP sent by
// RequestEmailVerificationCode against the calling player's own
// player-scoped Cloud Save record (no email param — the player is already
// authenticated). On success, marks player_profile.emailVerified = true and
// clears the pending code via the null-sentinel pattern (no deleteItem
// precedent, see LandTravel.js). Mirrors SaveEmail.js's player-scoped
// Cloud Save pattern exactly.
const { DataApi } = require("@unity-services/cloud-save-1.4");

const PROFILE_KEY = "player_profile";
const OTP_KEY      = "email_verify_otp";

/**
 * @param {string} code - The 6-digit OTP from the verification email.
 */
module.exports = async ({ params, context, logger }) => {
  const code = (params.code ?? "").trim();

  if (code.length !== 6) throw new Error("Verification code must be 6 digits");

  const { projectId, playerId } = context;
  const saveApi = new DataApi(context);

  let profile = {};
  let entry = null;
  try {
    const res = await saveApi.getItems(projectId, playerId, [PROFILE_KEY, OTP_KEY]);
    for (const item of res.data.results) {
      if (item.key === PROFILE_KEY && item.value) {
        profile = typeof item.value === "string" ? JSON.parse(item.value) : item.value;
      } else if (item.key === OTP_KEY && item.value) {
        entry = item.value;
      }
    }
  } catch (_) { /* nothing pending */ }

  if (!entry)                       throw new Error("No verification code requested");
  if (Date.now() > entry.expiresAt) throw new Error("Verification code has expired — request a new one");
  if (code !== entry.otp)           throw new Error("Invalid verification code");

  profile.emailVerified = true;
  profile.updatedMs     = Date.now();
  await saveApi.setItem(projectId, playerId, { key: PROFILE_KEY, value: profile });
  await saveApi.setItem(projectId, playerId, { key: OTP_KEY, value: null });

  logger.info(`ConfirmEmailVerificationCode: verified player ${playerId}`);
  return { success: true };
};
