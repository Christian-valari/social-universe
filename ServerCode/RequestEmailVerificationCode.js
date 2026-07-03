// RequestEmailVerificationCode — generates a 6-digit OTP proving the calling
// player's own email address (already on file from registration) is
// reachable. Verification now happens post-login against an
// already-authenticated real player, so this reads player_profile.email via
// context.playerId rather than trusting a client-supplied address — no
// Custom Data, no email-hash keys, no shared-item concurrency concerns.
// Mirrors SaveEmail.js's player-scoped Cloud Save pattern exactly.
//
// SETUP REQUIRED: reuses the same Cloud Code secrets as RequestPasswordReset:
//   RESEND_API_KEY   — your Resend API key
//   RESET_FROM_EMAIL — verified sender address (e.g. noreply@yourgame.com)
const { DataApi } = require("@unity-services/cloud-save-1.4");
const axios       = require("axios-1.6");

const PROFILE_KEY = "player_profile";
const OTP_KEY      = "email_verify_otp";
const OTP_TTL_MS   = 15 * 60 * 1000; // 15 minutes, matches RequestPasswordReset
const COOLDOWN_MS  = 60 * 1000;      // 60 seconds — purely a cost/accidental-double-click
                                      // guard now, not an abuse mitigant, since the caller
                                      // is an identified account.

function generateOtp() {
  return String(Math.floor(100000 + Math.random() * 900000));
}

function buildVerificationEmailHtml(otp) {
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
                  Enter this code to verify your email address:
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
                  This code expires in 15 minutes. If you didn't request this, you can safely ignore this email.
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

async function sendVerificationEmail(email, otp, apiKey, fromEmail) {
  // Cloud Code's JS runtime has no global `fetch` — outbound HTTP goes through
  // the whitelisted `axios-1.6` library instead (see Unity's Cloud Code
  // "Available libraries" reference). Axios throws on non-2xx responses.
  try {
    await axios.post("https://api.resend.com/emails", {
      from:    fromEmail,
      to:      [email],
      subject: "Social Universe — Verify Your Email",
      html:    buildVerificationEmailHtml(otp),
      text:    `Your verification code is: ${otp}\n\nEnter this code to verify your email. This code expires in 15 minutes.`
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

module.exports = async ({ context, logger, secretManager }) => {
  const { projectId, playerId } = context;
  const saveApi = new DataApi(context);

  let profile = {};
  let existing = null;
  try {
    const res = await saveApi.getItems(projectId, playerId, [PROFILE_KEY, OTP_KEY]);
    for (const item of res.data.results) {
      if (item.key === PROFILE_KEY && item.value) {
        profile = typeof item.value === "string" ? JSON.parse(item.value) : item.value;
      } else if (item.key === OTP_KEY && item.value) {
        existing = item.value;
      }
    }
  } catch (_) { /* no profile/pending code yet */ }

  const email = profile.email;
  if (!email) {
    throw new Error("No email on file for this account");
  }

  if (existing && Date.now() - existing.requestedAt < COOLDOWN_MS) {
    throw new Error("Please wait a moment before requesting another code");
  }

  const otp = generateOtp();
  await saveApi.setItem(projectId, playerId, {
    key: OTP_KEY,
    value: { otp, expiresAt: Date.now() + OTP_TTL_MS, requestedAt: Date.now() }
  });

  const resend_api = await secretManager.getSecret("RESEND_API_KEY");
  const reset_email = await secretManager.getSecret("RESET_FROM_EMAIL");

  // Unlike RequestPasswordReset, don't swallow a send failure — the caller is
  // already an identified account, so there's no enumeration risk, and a
  // silent failure would strand the player with no code and no way to know why.
  await sendVerificationEmail(email, otp, resend_api, reset_email);

  logger.info(`RequestEmailVerificationCode: code sent to player ${playerId}`);
  return { success: true };
};
