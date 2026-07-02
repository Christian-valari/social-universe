// ConfirmEmailVerificationCode — validates the OTP sent by
// RequestEmailVerificationCode. The client calls this immediately before
// RegisterAsync (AuthenticationService.SignUpWithUsernamePasswordAsync) —
// see AuthScreen.OnRegisterClicked. Single-use: the code is cleared (null
// sentinel — no deleteItem precedent, see LandTravel.js) from its per-email
// Custom Data key on success.
const { DataApi } = require("@unity-services/cloud-save-1.4");

const CUSTOM_ID   = "email_verification";
const EMAIL_REGEX = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

function emailToKey(email) {
  let h = 5381;
  for (let i = 0; i < email.length; i++) h = ((h << 5) + h) ^ email.charCodeAt(i);
  return (h >>> 0).toString(16).padStart(8, "0");
}

/**
 * @param {string} email - The email address being verified.
 * @param {string} code  - The 6-digit OTP from the verification email.
 */
module.exports = async ({ params, context, logger }) => {
  const email = (params.email ?? "").trim().toLowerCase();
  const code  = (params.code  ?? "").trim();

  if (!EMAIL_REGEX.test(email)) throw new Error("Invalid email address");
  if (code.length !== 6)        throw new Error("Verification code must be 6 digits");

  const { projectId } = context;
  const customDataApi = new DataApi(context);
  const codeKey = "code_" + emailToKey(email);

  let entry = null;
  try {
    const res  = await customDataApi.getCustomItems(projectId, CUSTOM_ID, [codeKey]);
    const item = res.data.results.find(r => r.key === codeKey);
    if (item?.value) entry = item.value;
  } catch (_) { /* nothing pending */ }

  if (!entry)                       throw new Error("No verification code requested for this email");
  if (Date.now() > entry.expiresAt) throw new Error("Verification code has expired — request a new one");
  if (code !== entry.otp)           throw new Error("Invalid verification code");

  await customDataApi.setCustomItem(projectId, CUSTOM_ID, { key: codeKey, value: null });

  logger.info(`ConfirmEmailVerificationCode: verified ${email}`);
  return { success: true };
};
