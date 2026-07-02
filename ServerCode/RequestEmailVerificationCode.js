// RequestEmailVerificationCode — generates a 6-digit OTP to prove a
// registration email address is reachable, before the account is created.
// There's no player yet to scope a Cloud Save key to, so the pending code is
// stored in its own per-email Custom Data key (not a single shared map) —
// this avoids one item growing unbounded as registrations pile up, and
// means concurrent requests for two different emails never contend for the
// same key. Emailed via Resend, same as RequestPasswordReset.js.
//
// SETUP REQUIRED: reuses the same Cloud Code secrets as RequestPasswordReset:
//   RESEND_API_KEY   — your Resend API key
//   RESET_FROM_EMAIL — verified sender address (e.g. noreply@yourgame.com)
const { DataApi } = require("@unity-services/cloud-save-1.4");

const CUSTOM_ID   = "email_verification";
const OTP_TTL_MS  = 15 * 60 * 1000; // 15 minutes, matches RequestPasswordReset
const COOLDOWN_MS = 60 * 1000;      // 60 seconds between sends to the same email —
                                     // there's no existing-account check to bound abuse
                                     // here (unlike RequestPasswordReset), so this bounds
                                     // email-bombing/Resend-quota abuse against arbitrary
                                     // third-party addresses instead.
const EMAIL_REGEX = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

function generateOtp() {
  return String(Math.floor(100000 + Math.random() * 900000));
}

function emailToKey(email) {
  // Deterministic short key from the email without storing PII as a Cloud
  // Save key. Simple djb2 hash — no crypto module needed. Duplicated from
  // SaveEmail.js/RequestPasswordReset.js (Cloud Code modules deploy as
  // standalone files, so small helpers are duplicated rather than shared).
  let h = 5381;
  for (let i = 0; i < email.length; i++) h = ((h << 5) + h) ^ email.charCodeAt(i);
  return (h >>> 0).toString(16).padStart(8, "0");
}

async function sendVerificationEmail(email, otp, apiKey, fromEmail) {
  const res = await fetch("https://api.resend.com/emails", {
    method:  "POST",
    headers: {
      "Authorization": `Bearer ${apiKey}`,
      "Content-Type":  "application/json"
    },
    body: JSON.stringify({
      from:    fromEmail,
      to:      [email],
      subject: "Social Universe — Verify Your Email",
      text:    `Your verification code is: ${otp}\n\nEnter this code to finish creating your account. This code expires in 15 minutes.`
    })
  });

  if (!res.ok) throw new Error(`Resend error: ${res.status}`);
}

/**
 * @param {string} email - The email address to verify.
 */
module.exports = async ({ params, context, logger, secrets }) => {
  const email = (params.email ?? "").trim().toLowerCase();

  if (!EMAIL_REGEX.test(email)) {
    throw new Error("Invalid email address");
  }

  const { projectId } = context;
  const customDataApi = new DataApi(context);
  const codeKey = "code_" + emailToKey(email);

  let existing = null;
  try {
    const res  = await customDataApi.getCustomItems(projectId, CUSTOM_ID, [codeKey]);
    const item = res.data.results.find(r => r.key === codeKey);
    if (item?.value) existing = item.value;
  } catch (_) { /* nothing pending yet */ }

  if (existing && Date.now() - existing.requestedAt < COOLDOWN_MS) {
    throw new Error("Please wait a moment before requesting another code");
  }

  const otp = generateOtp();
  await customDataApi.setCustomItem(projectId, CUSTOM_ID, {
    key: codeKey,
    value: { otp, expiresAt: Date.now() + OTP_TTL_MS, requestedAt: Date.now() }
  });

  // Unlike RequestPasswordReset, don't swallow a send failure — there's no
  // enumeration risk pre-registration, and a silent failure would strand the
  // player with no code and no way to know why.
  await sendVerificationEmail(email, otp, secrets.RESEND_API_KEY, secrets.RESET_FROM_EMAIL);

  logger.info(`RequestEmailVerificationCode: code sent to ${email}`);
  return { success: true };
};
