// ClaimSession — one active device per account. AuthService calls it on every sign-in, before any
// other function. Without a sessionId (a fresh sign-in) it starts a new session and records it in
// the Protected active_session item: that single write replaces any other device's session, whose
// next call fails with SESSION_REPLACED (two simultaneous sign-ins — the last write wins, so exactly
// one stays valid). With a sessionId (ActiveSessionController's periodic check) it only confirms
// that session is still the active one.
const { DataApi } = require("@unity-services/cloud-save-1.4");

module.exports = async ({ params, context, logger }) => {
  if (params.sessionId) {
    await requireActiveSession(context, params.sessionId);
    return params.sessionId;
  }

  const { projectId, playerId, correlationId } = context;
  if (!correlationId) throw Error("NO_CORRELATION_ID");
  await new DataApi(context).setProtectedItem(projectId, playerId, {
    key: "active_session",
    value: { sessionId: correlationId, claimedAt: Date.now() }
  });
  logger.info(`ClaimSession: ${playerId} -> new session ${correlationId}`);
  return correlationId;
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
  sessionId: "String"
};
