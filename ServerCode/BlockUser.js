// BlockUser — adds/removes a player on the caller's server-side block list
// (Cloud Save player data, key "blocked_users"). The server is the source of
// truth: other functions (e.g. future messaging paths) consult this list, and
// the full list is returned so the client cache converges every call.
// Chat-provider-level blocking (Vivox) is applied separately by the client's
// ReportService; this record is what moderation/audit relies on.
const { DataApi } = require("@unity-services/cloud-save-1.4");

const BLOCKED_KEY       = "blocked_users";
const MAX_BLOCKED_USERS = 200;

/**
 * @param {string} targetId - Player ID to block or unblock.
 * @param {boolean} blocked - True to block, false to unblock.
 */
module.exports = async ({ params, context, logger }) => {
  const { targetId, blocked } = params;

  if (!targetId || typeof blocked !== "boolean") {
    throw new Error("Invalid params: targetId and blocked are required");
  }

  const { projectId, playerId, accessToken } = context;

  if (targetId === playerId) {
    return { success: false, blockedUsers: [] };
  }

  const saveApi = new DataApi(context);

  let blockedUsers = [];
  try {
    const res  = await saveApi.getItems(projectId, playerId, [BLOCKED_KEY]);
    const item = res.data.results.find(r => r.key === BLOCKED_KEY);
    if (item && Array.isArray(item.value)) blockedUsers = item.value;
  } catch (_) { /* key doesn't exist yet */ }

  if (blocked) {
    if (!blockedUsers.includes(targetId)) blockedUsers.push(targetId);
    if (blockedUsers.length > MAX_BLOCKED_USERS) {
      return { success: false, blockedUsers };
    }
  } else {
    blockedUsers = blockedUsers.filter(id => id !== targetId);
  }

  await saveApi.setItem(projectId, playerId, { key: BLOCKED_KEY, value: blockedUsers });

  logger.info(`BlockUser: ${playerId} ${blocked ? "blocked" : "unblocked"} ${targetId} (${blockedUsers.length} total)`);
  return { success: true, blockedUsers };
};
