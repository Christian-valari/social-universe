// RequestPasswordReset — generates a 6-digit OTP for password recovery and
// delivers it to the player's registered email via an HTTP POST to Resend.
//
// SETUP REQUIRED:
//   1. In the UGS dashboard (Cloud Code → Secrets), add:
//        RESEND_API_KEY    — your Resend API key
//        RESET_FROM_EMAIL  — verified sender address (e.g. noreply@yourgame.com)
//   2. Deploy this function via the UGS CLI.
//
// The OTP is stored in the player's Cloud Save under a private key with a
// 15-minute TTL. The client submits it via ConfirmPasswordReset.
//
// This function always returns { success: true } regardless of whether the
// email is registered — prevents email enumeration.
//
// FIX: DataApi's constructor doesn't read a { headers: ... } field, and
// setItem takes positional args (projectId, playerId, { key, value }), not
// an options object — same SDK-shape mismatch as Known Issue #6 (see
// SaveEmail.js). DataApi(context) authenticates via the service token.
const { DataApi } = require("@unity-services/cloud-save-1.4");

const RESET_KEY   = "auth_reset_otp";
const OTP_TTL_MS  = 15 * 60 * 1000; // 15 minutes
const EMAIL_REGEX = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

function generateOtp() {
  // Math.random is sufficient here — the OTP is single-use, short-lived (15 min),
  // and stored server-side in Cloud Save which the client cannot read directly.
  return String(Math.floor(100000 + Math.random() * 900000));
}

async function findPlayerByEmail(projectId, accessToken, email) {
  // Looks up the email→playerId index written by SaveEmail.js.
  // Uses the UGS Cloud Save Admin API to query across players.
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
  // Produce a short deterministic key fragment from the email without storing
  // PII as a Cloud Save key. Simple djb2 hash — no crypto module needed.
  let h = 5381;
  for (let i = 0; i < email.length; i++) h = ((h << 5) + h) ^ email.charCodeAt(i);
  return (h >>> 0).toString(16).padStart(8, "0");
}

async function sendResetEmail(email, otp, apiKey, fromEmail) {
  const res = await fetch("https://api.resend.com/emails", {
    method:  "POST",
    headers: {
      "Authorization": `Bearer ${apiKey}`,
      "Content-Type":  "application/json"
    },
    body: JSON.stringify({
      from:    fromEmail,
      to:      [email],
      subject: "Social Universe — Password Reset Code",
      text:    `Your password reset code is: ${otp}\n\nThis code expires in 15 minutes. If you did not request a reset, ignore this email.`
    })
  });

  if (!res.ok) throw new Error(`Resend error: ${res.status}`);
}

module.exports = async ({ params, context, logger, secrets }) => {
  const email = (params.email ?? "").trim().toLowerCase();

  if (!EMAIL_REGEX.test(email)) {
    return { success: true }; // don't reveal validation errors externally
  }

  const { projectId, accessToken } = context;
  const saveApi = new DataApi(context);

  const targetPlayerId = await findPlayerByEmail(projectId, accessToken, email);
  if (!targetPlayerId) {
    logger.info("RequestPasswordReset: no player found for submitted email");
    return { success: true };
  }

  const otp = generateOtp();
  const resetRecord = {
    otp,
    expiresAt: Date.now() + OTP_TTL_MS,
  };

  await saveApi.setItem(projectId, targetPlayerId, { key: RESET_KEY, value: resetRecord });

  try {
    await sendResetEmail(email, otp, secrets.RESEND_API_KEY, secrets.RESET_FROM_EMAIL);
    logger.info(`RequestPasswordReset: reset email sent to player ${targetPlayerId}`);
  } catch (err) {
    logger.error(`RequestPasswordReset: email dispatch failed — ${err.message}`);
  }

  return { success: true };
};
