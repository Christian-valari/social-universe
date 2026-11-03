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
  await requireActiveSession(context, params.sessionId);
  const { targetId, blocked } = params;

  if (!targetId || typeof blocked !== "boolean") {
    throw new Error("Invalid params: targetId and blocked are required");
  }

  const { projectId, playerId, accessToken } = context;

  if (targetId === playerId) {
    return { success: false, blockedUsers: [] };
  }

  const saveApi = new DataApi(context);

  // The change is applied to a fresh read under the record's writeLock, so two blocks racing each
  // other (two devices, or two quick taps) both stay on the list. A failed read throws — a list
  // rebuilt from nothing would unblock everyone else.
  let stored = [];
  for (let attempt = 0; attempt < 3; attempt++) {
    let writeLock;
    stored = [];
    try {
      const res  = await saveApi.getItems(projectId, playerId, [BLOCKED_KEY]);
      const item = res.data.results.find(r => r.key === BLOCKED_KEY);
      if (item) writeLock = item.writeLock;
      if (item && Array.isArray(item.value)) stored = item.value;
    } catch (err) {
      if ((err?.response?.status ?? err?.status) !== 404) throw err;
    }

    let blockedUsers = [...stored];
    if (blocked) {
      if (!blockedUsers.includes(targetId)) blockedUsers.push(targetId);
      if (blockedUsers.length > MAX_BLOCKED_USERS) {
        return { success: false, blockedUsers };
      }
    } else {
      blockedUsers = blockedUsers.filter(id => id !== targetId);
    }

    try {
      await saveApi.setItem(projectId, playerId,
        writeLock ? { key: BLOCKED_KEY, value: blockedUsers, writeLock } : { key: BLOCKED_KEY, value: blockedUsers });
      logger.info(`BlockUser: ${playerId} ${blocked ? "blocked" : "unblocked"} ${targetId} (${blockedUsers.length} total)`);
      return { success: true, blockedUsers };
    } catch (err) {
      if ((err?.response?.status ?? err?.status) !== 409) throw err;
      logger.warn(`BlockUser: write conflict for ${playerId} (attempt ${attempt + 1}), re-reading`);
    }
  }
  logger.warn(`BlockUser: gave up for ${playerId} after 3 write conflicts`);
  return { success: false, blockedUsers: stored };
};

// ---- active session check (copy verbatim into every deployed script; ActiveSessionGuardTests keeps the copies identical) ----
// ClaimSession records the latest sign-in's session in the Protected active_session item. A call
// naming any other session, or none, comes from a replaced or signed-out device. A failed read
// throws rather than letting the call through.
async function requireActiveSession(context, sessionId) {
  const { DataApi } = require("@unity-services/cloud-save-1.4");
  const { projectId, playerId } = context;
  let current;
  try {
    const res = await new DataApi(context).getProtectedItems(projectId, playerId, ["active_session"]);
    current = res.data.results.find(r => r.key === "active_session")?.value?.sessionId;
  } catch (e) {
    if (e?.response?.status !== 404) throw e;
  }
  if (!sessionId || sessionId !== current) throw Error("SESSION_REPLACED");
}
// ---- end active session check

// Parameter declarations for the UGS CLI / Cloud Code (types only: each script validates its own
// inputs and returns reasons the client understands). Must stay after `module.exports = ...`,
// which would otherwise overwrite it. Pinned by ServerCodeDeployTests.
module.exports.params = {
  targetId: "String",
  blocked: "Boolean",
  sessionId: "String"
};
