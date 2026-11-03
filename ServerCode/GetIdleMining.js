// GetIdleMining — the caller's idle mining run in progress, if any, so the Planet scene can show
// (and claim) a run started on another device. A pure read.
// Design: docs/superpowers/plans/2026-10-15-server-tracked-idle-mining.md.
//
// Response: { success: true, planetId, slotId, mineralId, elapsedSeconds, durationSeconds } for a
// run in progress, { success: true } for none. Every field MUST exist on IdleMiningSessionResult.
// elapsedSeconds is measured on the server clock, so every device shows the same progress.
// A missing record (404, or no result) means no run. Any other read failure throws: the client
// then shows no server run (and falls back to a legacy local one) rather than trusting an answer
// the server never gave.
const { DataApi } = require("@unity-services/cloud-save-1.4");

const SAVE_KEY = "idle_mining_session";

/**
 * No parameters.
 */
module.exports = async ({ params, context, logger }) => {
  await requireActiveSession(context, params.sessionId);
  const { projectId, playerId } = context;

  let run = null;
  try {
    const res   = await new DataApi(context).getProtectedItems(projectId, playerId, [SAVE_KEY]);
    const value = res.data.results.find(r => r.key === SAVE_KEY)?.value;
    if (value?.slotId) run = value;
  } catch (err) {
    if ((err?.response?.status ?? err?.status) !== 404) throw err;
  }

  if (!run) return { success: true };

  logger.info(`GetIdleMining: player ${playerId} mining ${run.slotId} on ${run.planetId}`);
  return {
    success: true,
    planetId: run.planetId,
    slotId: run.slotId,
    mineralId: run.mineralId,
    elapsedSeconds: Math.max(0, (Date.now() - run.startTs) / 1000),
    durationSeconds: run.durationSec,
  };
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
