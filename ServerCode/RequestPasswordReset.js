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
const axios       = require("axios-1.6");

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
  // Cloud Code's JS runtime has no global `fetch` — outbound HTTP goes through
  // the whitelisted `axios-1.6` library instead (see Unity's Cloud Code
  // "Available libraries" reference).
  const emailKey = "idx_email_" + emailToKey(email);
  try {
    const res = await axios.get(
      `https://cloud-save.services.api.unity.com/v1/data/projects/${projectId}/items?key=${encodeURIComponent(emailKey)}`,
      { headers: { Authorization: `Bearer ${accessToken}` } }
    );
    return res.data.results?.[0]?.value?.playerId ?? null;
  } catch (_) {
    return null;
  }
}

function emailToKey(email) {
  // Produce a short deterministic key fragment from the email without storing
  // PII as a Cloud Save key. Simple djb2 hash — no crypto module needed.
  let h = 5381;
  for (let i = 0; i < email.length; i++) h = ((h << 5) + h) ^ email.charCodeAt(i);
  return (h >>> 0).toString(16).padStart(8, "0");
}

function buildResetEmailHtml(otp) {
  return `<!DOCTYPE html>
<html>
  <body style="margin:0; padding:0; background-color:#0b0e1a; font-family:-apple-system,Segoe UI,Roboto,Helvetica,Arial,sans-serif;">
    <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="background-color:#0b0e1a; padding:32px 0;">
      <tr>
        <td align="center">
          <table role="presentation" width="480" cellpadding="0" cellspacing="0" style="background-color:#151a2e; border-radius:12px; overflow:hidden;">
            <tr>
              <td style="padding:32px 40px 8px 40px; text-align:center;">
                <div style="font-size:20px; font-weight:600; color:#ffffff; letter-spacing:0.5px;">Social Universe</div>
              </td>
            </tr>
            <tr>
              <td style="padding:8px 40px 0 40px; text-align:center;">
                <p style="margin:0; font-size:15px; line-height:22px; color:#c3c8dc;">
                  Use this code to reset your password:
                </p>
              </td>
            </tr>
            <tr>
              <td style="padding:24px 40px; text-align:center;">
                <div style="display:inline-block; padding:16px 28px; background-color:#0b0e1a; border:1px solid #2b3252; border-radius:8px; font-size:32px; font-weight:700; letter-spacing:8px; color:#7dd3fc; font-family:'Courier New', monospace;">
                  ${otp}
                </div>
              </td>
            </tr>
            <tr>
              <td style="padding:0 40px 32px 40px; text-align:center;">
                <p style="margin:0; font-size:13px; line-height:20px; color:#8b91ab;">
                  This code expires in 15 minutes. If you didn't request a password reset, you can safely ignore this email.
                </p>
              </td>
            </tr>
          </table>
          <p style="margin:16px 0 0 0; font-size:12px; color:#5b6180;">&copy; Social Universe</p>
        </td>
      </tr>
    </table>
  </body>
</html>`;
}

async function sendResetEmail(email, otp, apiKey, fromEmail) {
  // Cloud Code's JS runtime has no global `fetch` — outbound HTTP goes through
  // the whitelisted `axios-1.6` library instead. Axios throws on non-2xx responses.
  try {
    await axios.post("https://api.resend.com/emails", {
      from:    fromEmail,
      to:      [email],
      subject: "Social Universe — Password Reset Code",
      html:    buildResetEmailHtml(otp),
      text:    `Your password reset code is: ${otp}\n\nThis code expires in 15 minutes. If you did not request a reset, ignore this email.`
    }, {
      headers: {
        "Authorization": `Bearer ${apiKey}`,
        "Content-Type":  "application/json"
      }
    });
  } catch (err) {
    throw new Error(`Resend error: ${err.response?.status ?? err.message}`);
  }
}

module.exports = async ({ params, context, logger, secretManager }) => {
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
    const resend_api = await secretManager.getSecret("RESEND_API_KEY");
    const reset_email = await secretManager.getSecret("RESET_FROM_EMAIL");
    await sendResetEmail(email, otp, resend_api, reset_email);
    logger.info(`RequestPasswordReset: reset email sent to player ${targetPlayerId}`);
  } catch (err) {
    logger.error(`RequestPasswordReset: email dispatch failed — ${err.message}`);
  }

  return { success: true };
};
