// CheckEmailAvailable — registration pre-check: returns whether an email is
// free to register, by querying the cross-player email_lookup index (written
// by SaveEmail.js) through the elevated Cloud Code DataApi. Same setup
// prerequisites as RequestPasswordReset.js: the "email_lookup" Cloud Save
// index (Player Data, Default access class) must exist, and values saved
// before the index was created are never matched (no backfill).
//
// Unlike RequestPasswordReset, this endpoint intentionally reveals whether an
// email is registered — that is its purpose (pre-registration duplicate
// check), and the same fact already leaks through sign-up's ENTITY_EXISTS.
//
// Fails OPEN ({ available: true }) on query errors: sign-up's ENTITY_EXISTS
// remains the duplicate backstop, and a broken index shouldn't block all
// registrations.
const { DataApi } = require("@unity-services/cloud-save-1.4");

const EMAIL_LOOKUP_KEY = "email_lookup"; // must match SaveEmail.js
const EMAIL_REGEX = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

/**
 * @param {string} email - The address the player wants to register with.
 */
module.exports = async ({ params, context, logger }) => {
  const email = (params.email ?? "").trim().toLowerCase();
  if (!EMAIL_REGEX.test(email)) throw new Error("Invalid email address");

  const { projectId } = context;
  const saveApi = new DataApi(context);

  try {
    const res = await saveApi.queryDefaultPlayerData(projectId, {
      // asc is mandatory on every query field (400 "asc must be specified"
      // without it), even though sort order is irrelevant for an EQ match.
      fields: [{ key: EMAIL_LOOKUP_KEY, op: "EQ", value: email, asc: true }],
    });
    const matches = res.data.results ?? [];
    logger.info(`CheckEmailAvailable: ${matches.length} match(es)`);
    return { available: matches.length === 0 };
  } catch (err) {
    const detail = err.response?.data ? JSON.stringify(err.response.data) : err.message;
    logger.error(`CheckEmailAvailable: query FAILED (treating as available): ${detail}`);
    return { available: true };
  }
};
