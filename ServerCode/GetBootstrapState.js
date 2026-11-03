const { CurrenciesApi } = require("@unity-services/economy-2.5");
const { DataApi }       = require("@unity-services/cloud-save-1.4");

const START_SLOTS      = 2;       // MUST MATCH EconomyConfig._startingFleetSlots
const STARTER_DRONE_ID = "scout"; // MUST MATCH the Tier-1, zero-cost DroneDefinition

module.exports = async ({ params, context, logger }) => {
  await requireActiveSession(context, params.sessionId);
  const { projectId, playerId, accessToken } = context;
  const economyApi   = new CurrenciesApi({ accessToken });
  const cloudSaveApi = new DataApi(context);

  // player_profile is player-written (Default class); the inventory and fleet are server-written
  // (Protected class) so a client can't rewrite them — Known Issue #16.
  const none = () => ({ data: { results: [] } });
  const [balancesRes, profileRes, protectedRes] = await Promise.all([
    economyApi.getPlayerCurrencies({ projectId, playerId }),
    cloudSaveApi.getItems(projectId, playerId, ["player_profile"]).catch(none),
    cloudSaveApi.getProtectedItems(projectId, playerId, ["mineral_inventory", "drone_fleet"]).catch(none)
  ]);

  const balances = {};
  for (const currency of balancesRes.data.results) balances[currency.currencyId] = currency.balance;

  const results = [...profileRes.data.results, ...protectedRes.data.results];
  const get = (k) => { const it = results.find(r => r.key === k); return it ? it.value : null; };

  const profile          = get("player_profile");
  const mineralInventory = get("mineral_inventory") || {};
  let   droneFleet       = get("drone_fleet");

  // Seed the starter fleet for a brand-new player so they own Scout by default.
  if (!droneFleet || !Array.isArray(droneFleet.drones) || droneFleet.drones.length === 0) {
    droneFleet = { slots: START_SLOTS, activeDroneId: STARTER_DRONE_ID, drones: [{ droneId: STARTER_DRONE_ID, upgrades: { Cargo: 0, Yield: 0, Speed: 0 } }] };
    await cloudSaveApi.setProtectedItem(projectId, playerId, { key: "drone_fleet", value: droneFleet });
  }

  return {
    serverTimeMs: Date.now(),
    balances,
    profile: typeof profile === "string" ? JSON.parse(profile) : profile,
    mineralInventory,
    droneFleet
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
