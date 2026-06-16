// SubmitReport — logs a player report and queues it for moderation review.
// Reports are appended to a shared Custom Data list (customId "moderation",
// key "reports", capped) that a future moderation dashboard/pipeline consumes.
// The client cannot self-moderate — it only files the report.
const { DataApi } = require("@unity-services/cloud-save-1.4");

const MODERATION_CUSTOM_ID = "moderation";
const REPORTS_KEY          = "reports";
const MAX_QUEUED_REPORTS   = 500; // oldest entries are dropped beyond this
const MAX_REASON_LENGTH    = 64;
const MAX_CONTEXT_LENGTH   = 500; // e.g. the offending chat line

/**
 * @param {string} targetId - Player ID being reported.
 * @param {string} reason - Short reason code/category (e.g. "harassment", "spam").
 * @param {string} [context] - Optional free-text context such as the offending message. Truncated server-side.
 */
module.exports = async ({ params, context, logger }) => {
  const { targetId, reason } = params;
  const reportContext = params.context ?? "";

  if (!targetId || !reason) {
    throw new Error("Invalid params: targetId and reason are required");
  }

  const { projectId, playerId } = context;

  if (targetId === playerId) {
    return { success: false, reportId: null };
  }

  const customDataApi = new DataApi(context);

  let reports = [];
  try {
    const res  = await customDataApi.getCustomItems(projectId, MODERATION_CUSTOM_ID, [REPORTS_KEY]);
    const item = res.data.results.find(r => r.key === REPORTS_KEY);
    if (item && Array.isArray(item.value)) reports = item.value;
  } catch (_) { /* no reports queued yet */ }

  const reportId = `${Date.now()}_${playerId.slice(0, 8)}`;
  reports.push({
    reportId,
    reporterId: playerId,
    targetId,
    reason:  String(reason).slice(0, MAX_REASON_LENGTH),
    context: String(reportContext).slice(0, MAX_CONTEXT_LENGTH),
    createdMs: Date.now(),
    status: "open"
  });

  if (reports.length > MAX_QUEUED_REPORTS) {
    reports = reports.slice(reports.length - MAX_QUEUED_REPORTS);
  }

  await customDataApi.setCustomItem(projectId, MODERATION_CUSTOM_ID, { key: REPORTS_KEY, value: reports });

  logger.info(`SubmitReport: ${playerId} reported ${targetId} (${reason}) → ${reportId}`);
  return { success: true, reportId };
};
