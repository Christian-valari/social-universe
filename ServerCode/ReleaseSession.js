// ReleaseSession — explicit logout (AuthService.SignOutAsync). Clears the account's active session
// if it is still the caller's, so the signed-out device's id can never be used again. The write
// carries the record's writeLock: if another device claimed in between, it gets a 409 and that
// newer session is left alone. A stale or missing id is a no-op — that device was already replaced.
const { DataApi } = require("@unity-services/cloud-save-1.4");

module.exports = async ({ params, context, logger }) => {
  const { projectId, playerId } = context;
  const saveApi = new DataApi(context);

  const res  = await saveApi.getProtectedItems(projectId, playerId, ["active_session"]);
  const item = res.data.results.find(r => r.key === "active_session");
  if (!params.sessionId || item?.value?.sessionId !== params.sessionId) return;

  try {
    await saveApi.setProtectedItem(projectId, playerId, {
      key: "active_session",
      value: { sessionId: null, releasedAt: Date.now() },
      writeLock: item.writeLock
    });
    logger.info(`ReleaseSession: ${playerId} released ${params.sessionId}`);
  } catch (e) {
    if (e?.response?.status !== 409) throw e;
  }
};

// Parameter declarations for the UGS CLI / Cloud Code (types only: each script validates its own
// inputs and returns reasons the client understands). Must stay after `module.exports = ...`,
// which would otherwise overwrite it. Pinned by ServerCodeDeployTests.
module.exports.params = {
  sessionId: "String"
};
