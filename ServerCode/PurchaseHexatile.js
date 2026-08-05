// PurchaseHexatile — unlocks hexatile[hexIndex] on an owned tile if it is adjacent
// to an already-unlocked hexatile. Price is computed SERVER-SIDE from the current
// unlocked count (base + step*(unlocked - FREE)); never trusts a client price.
// Board geometry (radius, free count, spiral order, neighbors) MIRRORS
// Assets/_Project/Scripts/Economy/HexBoardMath.cs — keep in sync.
const { CurrenciesApi, ConfigurationApi } = require("@unity-services/economy-2.5");
const { DataApi }                         = require("@unity-services/cloud-save-1.4");

const CURRENCY_ID  = "COINS";
const REGISTRY_KEY = "land_registry";
const RADIUS = 2, FREE_COUNT = 5, BASE_PRICE = 200, PRICE_STEP = 100;
const DIRS = [[1,0],[1,-1],[0,-1],[-1,0],[-1,1],[0,1]];

function cells(radius) {
  const out = [[0,0]];
  for (let k = 1; k <= radius; k++) {
    let hex = [DIRS[4][0]*k, DIRS[4][1]*k];
    for (let side = 0; side < 6; side++)
      for (let step = 0; step < k; step++) { out.push([hex[0], hex[1]]); hex = [hex[0]+DIRS[side][0], hex[1]+DIRS[side][1]]; }
  }
  return out;
}
function neighbors(radius) {
  const c = cells(radius), key = (a) => `${a[0]},${a[1]}`, idx = {};
  c.forEach((a, i) => idx[key(a)] = i);
  return c.map(a => DIRS.map(d => idx[key([a[0]+d[0], a[1]+d[1]])]).filter(n => n !== undefined));
}
function hexCount(radius) { return 3*radius*radius + 3*radius + 1; }

module.exports = async ({ params, context, logger }) => {
  const { tileId, planetId, hexIndex } = params;
  const N = hexCount(RADIUS);
  if (!tileId || !planetId || !Number.isInteger(hexIndex) || hexIndex < 0 || hexIndex >= N) {
    throw new Error("Invalid params: tileId, planetId, hexIndex required");
  }

  const { projectId, playerId, accessToken } = context;
  const econApi = new CurrenciesApi({ accessToken });
  const config  = new ConfigurationApi({ accessToken });
  const dataApi = new DataApi(context);
  const customId = planetId.toLowerCase();

  try {
    let registry = {};
    try {
      const r = await dataApi.getCustomItems(projectId, customId, [REGISTRY_KEY]);
      const item = r.data.results.find(x => x.key === REGISTRY_KEY);
      if (item?.value) registry = item.value;
    } catch (_) {}

    const entry = registry[tileId];
    if (!entry || entry.ownerId !== playerId) return { success: false, reason: "NOT_OWNER" };

    // Normalize unlocked mask: free indices default true.
    let unlocked = Array.isArray(entry.unlocked) ? entry.unlocked.slice(0, N) : [];
    while (unlocked.length < N) unlocked.push(false);
    if (!Array.isArray(entry.unlocked)) for (let i = 0; i < FREE_COUNT; i++) unlocked[i] = true;

    if (unlocked[hexIndex]) return { success: false, reason: "ALREADY_UNLOCKED" };

    const nb = neighbors(RADIUS);
    const adjacent = nb[hexIndex].some(n => unlocked[n]);
    if (!adjacent) return { success: false, reason: "NOT_ADJACENT" };

    const unlockedCount = unlocked.filter(Boolean).length;
    const price = BASE_PRICE + PRICE_STEP * (unlockedCount - FREE_COUNT);

    const balances = await econApi.getPlayerCurrencies({ projectId, playerId });
    const coins = balances.data.results.find(c => c.currencyId === CURRENCY_ID);
    if ((coins ? coins.balance : 0) < price) return { success: false, reason: "INSUFFICIENT_FUNDS" };

    const cfg = await config.getPlayerConfiguration({ projectId, playerId });
    const configAssignmentHash = cfg.data.metadata.configAssignmentHash;
    const deduct = await econApi.decrementPlayerCurrencyBalance({
      projectId, playerId, currencyId: CURRENCY_ID, configAssignmentHash,
      currencyModifyBalanceRequest: { currencyId: CURRENCY_ID, amount: price }
    });

    unlocked[hexIndex] = true;
    entry.unlocked = unlocked;
    registry[tileId] = entry;
    await dataApi.setCustomItem(projectId, customId, { key: REGISTRY_KEY, value: registry });

    logger.info(`PurchaseHexatile: ${playerId} unlocked hex ${hexIndex} of ${tileId} (${planetId}) for ${price}`);
    return { success: true, newBalance: deduct.data.balance, unlockedCount: unlocked.filter(Boolean).length };
  } catch (err) {
    logger.error("PurchaseHexatile failed", { "error.message": err.message });
    throw err;
  }
};
