// TestFindPlayerByEmail — TEST-ONLY diagnostic for the email_lookup query used
// by RequestPasswordReset/ConfirmPasswordReset. Runs the exact same
// queryDefaultPlayerData call but returns everything instead of hiding
// failures behind an anti-enumeration { success: true }:
//   - whether the query itself succeeded or threw (and the raw error detail)
//   - how many players matched and the raw results array
//   - the resolved playerId (what findPlayerByEmail would return)
//   - the CALLING player's own stored email_lookup value, so a stored-vs-
//     submitted mismatch (case, quotes, old idx_email_* format) is visible
//     side by side when you test as the account being looked up.
//
// DO NOT ship this in production: it reveals whether an email is registered
// and which playerId owns it (enumeration risk). Deploy to a dev environment,
// run from the Cloud Code tester, delete when done.
//
// Prerequisite (same as the real functions): Cloud Save → Indexes must have a
// READY index on "email_lookup" (Player Data, Default access class), and the
// value must have been saved AFTER the index was created — indexes do not
// backfill pre-existing data.
const { DataApi } = require("@unity-services/cloud-save-1.4");

const EMAIL_LOOKUP_KEY = "email_lookup"; // must match SaveEmail.js

/**
 * @param {string} email - The email address to look up.
 */
module.exports = async ({ params, context, logger }) => {
  const submittedRaw = params.email ?? "";
  const email = submittedRaw.trim().toLowerCase(); // same normalization as the real functions

  const { projectId, playerId } = context;
  const saveApi = new DataApi(context);

  const report = {
    submittedRaw,
    queriedValue: email,
    querySucceeded: false,
    matchCount: 0,
    resolvedPlayerId: null,
    results: null,
    queryError: null,
    callerPlayerId: playerId,
    callerStoredEmailLookup: "(read failed or key absent)",
  };

  // The exact query the real functions run.
  try {
    const res = await saveApi.queryDefaultPlayerData(projectId, {
      fields: [{ key: EMAIL_LOOKUP_KEY, op: "EQ", value: email, asc: true }],
      returnKeys: [EMAIL_LOOKUP_KEY], // include the stored value in results for comparison
    });
    const results = res.data.results ?? [];
    report.querySucceeded = true;
    report.matchCount = results.length;
    report.results = results;
    const match = results[0];
    report.resolvedPlayerId = match?.id ?? match?.playerId ?? null;
  } catch (err) {
    report.queryError = err.response?.data ?? err.message;
  }

  // What the calling player actually has stored under email_lookup — compare
  // against queriedValue to spot case/format mismatches or a pre-index value.
  try {
    const own = await saveApi.getItems(projectId, playerId, [EMAIL_LOOKUP_KEY]);
    const item = own.data.results?.find(i => i.key === EMAIL_LOOKUP_KEY);
    if (item !== undefined) report.callerStoredEmailLookup = item.value;
  } catch (_) { /* keep the placeholder */ }

  logger.info(`TestFindPlayerByEmail: ${JSON.stringify(report)}`);
  return report;
};
