const { CurrenciesApi } = require("@unity-services/economy-2.5");
const { DataApi }       = require("@unity-services/cloud-save-1.4");

const START_SLOTS = 2; // MUST MATCH EconomyConfig._startingFleetSlots

module.exports = async ({ params, context, logger }) => {
  const { projectId, playerId, accessToken } = context;
  const economyApi   = new CurrenciesApi({ accessToken });
  const cloudSaveApi = new DataApi(context);

  const [balancesRes, saveRes] = await Promise.all([
    economyApi.getPlayerCurrencies({ projectId, playerId }),
    cloudSaveApi.getItems(projectId, playerId, ["player_profile", "mineral_inventory", "drone_fleet"])
      .catch(() => ({ data: { results: [] } }))
  ]);

  const balances = {};
  for (const currency of balancesRes.data.results) balances[currency.currencyId] = currency.balance;

  const results = saveRes.data.results;
  const get = (k) => { const it = results.find(r => r.key === k); return it ? it.value : null; };

  const profile          = get("player_profile");
  const mineralInventory = get("mineral_inventory") || {};
  let   droneFleet       = get("drone_fleet");

  // Seed the starter fleet for a brand-new player so they own Scout by default.
  if (!droneFleet || !Array.isArray(droneFleet.drones) || droneFleet.drones.length === 0) {
    droneFleet = { slots: START_SLOTS, activeDroneId: "scout", drones: [{ droneId: "scout", upgrades: { Cargo: 0, Yield: 0, Speed: 0 } }] };
    await cloudSaveApi.setItem(projectId, playerId, { key: "drone_fleet", value: droneFleet });
  }

  return {
    serverTimeMs: Date.now(),
    balances,
    profile: typeof profile === "string" ? JSON.parse(profile) : profile,
    mineralInventory,
    droneFleet
  };
};
