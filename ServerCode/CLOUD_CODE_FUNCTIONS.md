# UGS Cloud Code Functions — Reference

Combined reference of all `ServerCode/` functions for review. Each function is deployed to
Unity Gaming Services Cloud Code as its own module (filename = function name). This document
is **not** a deployment artifact — see the individual `.js` files for that.

Modules used: `@unity-services/economy-2.5`, `@unity-services/cloud-save-1.4`.

---

## GetServerTime

Returns the authoritative server timestamp in milliseconds. Used by the client's `ServerTime`
class to calibrate its local clock offset.

**Parameters:** none

```js
// GetServerTime — returns the authoritative server timestamp in milliseconds.
// Used by the client's ServerTime class to calibrate its local clock offset.
/**
 * No parameters.
 */
module.exports = async ({ params, context, logger }) => {
  return Date.now();
};
```

---

## GetBootstrapState

Returns wallet balances and the saved player profile in a single call, minimizing
round-trips on app launch.

**Parameters:** none

```js
// GetBootstrapState — returns wallet balances and server time in a single call,
// minimizing round-trips on app launch.
const { CurrenciesApi } = require("@unity-services/economy-2.5");
const { DataApi }       = require("@unity-services/cloud-save-1.4");

/**
 * No parameters.
 */
module.exports = async ({ params, context, logger }) => {
  const { projectId, playerId, accessToken } = context;
  const authHeader = { headers: { Authorization: `Bearer ${accessToken}` } };

  const economyApi   = new CurrenciesApi(authHeader);
  const cloudSaveApi = new DataApi(authHeader);

  // Fetch balances + saved profile in parallel.
  const [balancesRes, profileRes] = await Promise.all([
    economyApi.getPlayerCurrencies({ projectId, playerId }),
    cloudSaveApi.getItems({ projectId, playerId, key: ["player_profile"] }).catch(() => ({ data: { results: [] } }))
  ]);

  const balances = {};
  for (const currency of balancesRes.data.results) {
    balances[currency.currencyId] = currency.balance;
  }

  const profileItem = profileRes.data.results.find(r => r.key === "player_profile");
  const profile     = profileItem ? JSON.parse(profileItem.value) : null;

  return {
    serverTimeMs: Date.now(),
    balances,
    profile
  };
};
```

---

## GrantCoins

Server-authoritative coin grant. Called after idle mining claim; server validates the amount
is within the session's maximum payout before incrementing the balance.

**Parameters:**

| Name | Type | Required | Description |
|---|---|---|---|
| `amount` | `number` (integer) | Yes | Coins to grant. Must be `0 < amount <= 100000`. |

```js
// GrantCoins — server-authoritative coin grant.
// Called after idle mining claim; server validates the amount is within the
// session's maximum payout before incrementing the balance.
const { CurrenciesApi } = require("@unity-services/economy-2.5");

const MAX_GRANT_PER_CALL = 100_000; // sanity cap against runaway clients

/**
 * @param {number} amount - Number of coins to grant. Must be a positive integer no greater than 100000.
 */
module.exports = async ({ params, context, logger }) => {
  const { amount } = params;

  if (!Number.isInteger(amount) || amount <= 0 || amount > MAX_GRANT_PER_CALL) {
    throw Error(`Invalid amount: ${amount}`);
  }

  const { projectId, playerId, accessToken } = context;
  const api = new CurrenciesApi({ headers: { Authorization: `Bearer ${accessToken}` } });

  const res = await api.incrementPlayerCurrencyBalance({
    projectId,
    playerId,
    currencyId: "COINS",
    currencyModifyBalanceRequest: { amount }
  });

  logger.info(`GrantCoins: player ${playerId} +${amount} → ${res.data.balance}`);
  return { newBalance: res.data.balance };
};
```

---

## GrantStardust

Server-authoritative stardust grant (premium currency).

**Parameters:**

| Name | Type | Required | Description |
|---|---|---|---|
| `amount` | `number` (integer) | Yes | Stardust to grant. Must be `0 < amount <= 10000`. |

```js
// GrantStardust — server-authoritative stardust grant (premium currency).
const { CurrenciesApi } = require("@unity-services/economy-2.5");

const MAX_GRANT_PER_CALL = 10_000;

/**
 * @param {number} amount - Number of stardust to grant. Must be a positive integer no greater than 10000.
 */
module.exports = async ({ params, context, logger }) => {
  const { amount } = params;

  if (!Number.isInteger(amount) || amount <= 0 || amount > MAX_GRANT_PER_CALL) {
    throw Error(`Invalid amount: ${amount}`);
  }

  const { projectId, playerId, accessToken } = context;
  const api = new CurrenciesApi({ headers: { Authorization: `Bearer ${accessToken}` } });

  const res = await api.incrementPlayerCurrencyBalance({
    projectId,
    playerId,
    currencyId: "STARDUST",
    currencyModifyBalanceRequest: { amount }
  });

  logger.info(`GrantStardust: player ${playerId} +${amount} → ${res.data.balance}`);
  return { newBalance: res.data.balance };
};
```

---

## SpendCoins

Validates the player can afford the spend, then decrements. Returns `{ success, newBalance }`.

**Parameters:**

| Name | Type | Required | Description |
|---|---|---|---|
| `amount` | `number` (integer) | Yes | Coins to spend. Must be a positive integer. |

```js
// SpendCoins — validates the player can afford the spend, then decrements.
// Returns { success, newBalance }.
const { CurrenciesApi } = require("@unity-services/economy-2.5");

/**
 * @param {number} amount - Number of coins to spend. Must be a positive integer not exceeding the player's current balance.
 */
module.exports = async ({ params, context, logger }) => {
  const { amount } = params;

  if (!Number.isInteger(amount) || amount <= 0) {
    throw Error(`Invalid amount: ${amount}`);
  }

  const { projectId, playerId, accessToken } = context;
  const api = new CurrenciesApi({ headers: { Authorization: `Bearer ${accessToken}` } });

  // Fetch current balance to validate afford-ability server-side.
  const balanceRes = await api.getPlayerCurrencyBalance({ projectId, playerId, currencyId: "COINS" });
  const current    = balanceRes.data.balance;

  if (current < amount) {
    logger.warn(`SpendCoins: insufficient balance (have ${current}, need ${amount})`);
    return { success: false, newBalance: current };
  }

  const res = await api.decrementPlayerCurrencyBalance({
    projectId,
    playerId,
    currencyId: "COINS",
    currencyModifyBalanceRequest: { amount }
  });

  logger.info(`SpendCoins: player ${playerId} -${amount} → ${res.data.balance}`);
  return { success: true, newBalance: res.data.balance };
};
```

---

## ValidateMining

Validates an idle mining session payout and grants coins. The client sends claimed coins and
session parameters; the server caps the grant at `sessionDurationSec * coinsPerSec` to prevent
inflated claims. Full anti-cheat with a server-stored session token is scheduled for M3.

**Parameters:**

| Name | Type | Required | Description |
|---|---|---|---|
| `claimedCoins` | `number` (integer) | Yes | Coins the client claims to have mined this session. Must be a positive integer. |
| `sessionDurationSec` | `number` | No | Session length in seconds. Defaults to `30`, capped at `300`. |
| `coinsPerSec` | `number` | No | Coin yield rate per second. Defaults to `1`. |

```js
// ValidateMining — validates an idle mining session payout and grants coins.
// The client sends claimed coins and the session parameters; the server caps the
// grant at (sessionDurationSec * coinsPerSec) to prevent inflated claims.
// Full anti-cheat with a server-stored session token is scheduled for M3.
const { CurrenciesApi } = require("@unity-services/economy-2.5");

const ABSOLUTE_SESSION_CAP_SECONDS = 300; // 5-minute hard cap per session
const ABSOLUTE_COINS_CAP           = 10000; // hard upper bound per call

/**
 * @param {number} claimedCoins - Coins the client claims to have mined this session. Must be a positive integer.
 * @param {number} [sessionDurationSec] - Session length in seconds. Optional, defaults to 30 and is capped at 300.
 * @param {number} [coinsPerSec] - Coin yield rate per second for the session. Optional, defaults to 1.
 */
module.exports = async ({ params, context, logger }) => {
  const { claimedCoins, sessionDurationSec, coinsPerSec } = params;

  if (!Number.isInteger(claimedCoins) || claimedCoins <= 0) {
    throw new Error(`Invalid claimedCoins: ${claimedCoins}`);
  }

  const cappedDuration = Math.min(sessionDurationSec ?? 30, ABSOLUTE_SESSION_CAP_SECONDS);
  const maxByRate      = Math.floor(cappedDuration * (coinsPerSec ?? 1));
  const grantAmount    = Math.min(claimedCoins, maxByRate, ABSOLUTE_COINS_CAP);

  if (grantAmount <= 0) {
    return { granted: 0, newBalance: null };
  }

  const { projectId, playerId, accessToken } = context;
  const authHeader = { headers: { Authorization: `Bearer ${accessToken}` } };
  const econApi    = new CurrenciesApi(authHeader);

  const res = await econApi.incrementPlayerCurrencyBalance({
    projectId,
    playerId,
    currencyId: "COINS",
    currencyModifyBalanceRequest: { amount: grantAmount }
  });

  logger.info(`ValidateMining: player ${playerId} claimed ${claimedCoins}, granted ${grantAmount} → balance ${res.data.balance}`);
  return { granted: grantAmount, newBalance: res.data.balance };
};
```

---

## GrantOfflineIncome

Validates the claimed offline yield against the server's authoritative session-end timestamp,
then grants coins. The client sends the claimed amount; the server caps it at the theoretical
max (`offlineSeconds * IDLE_RATE_PER_SEC`, capped at `MAX_OFFLINE_SECONDS`).

**Parameters:**

| Name | Type | Required | Description |
|---|---|---|---|
| `claimedAmount` | `number` (integer) | Yes | Coins the client claims to have earned while offline. Must be a positive integer; the server caps the actual grant. |

```js
// GrantOfflineIncome — validates the claimed offline yield against the server's
// authoritative session-end timestamp, then grants coins.
// The client sends the claimed amount; the server caps it at the theoretical max.
const { CurrenciesApi } = require("@unity-services/economy-2.5");
const { DataApi }       = require("@unity-services/cloud-save-1.4");

const IDLE_RATE_PER_SEC   = 1;   // must match EconomyConfig.IdleMiningRate
const MAX_OFFLINE_SECONDS = 8 * 3600; // 8 hours, must match EconomyConfig.MaxOfflineHours

/**
 * @param {number} claimedAmount - Coins the client claims to have earned while offline. Must be a positive integer; the server caps the actual grant at the theoretical max for the elapsed offline time.
 */
module.exports = async ({ params, context, logger }) => {
  const { claimedAmount } = params;

  if (!Number.isInteger(claimedAmount) || claimedAmount <= 0) {
    throw Error(`Invalid claimedAmount: ${claimedAmount}`);
  }

  const { projectId, playerId, accessToken } = context;
  const authHeader = { headers: { Authorization: `Bearer ${accessToken}` } };
  const econApi    = new CurrenciesApi(authHeader);
  const saveApi    = new DataApi(authHeader);

  // Load the server-recorded session end time.
  let sessionEndMs = Date.now(); // fallback: assume just now
  try {
    const res    = await saveApi.getItems({ projectId, playerId, key: ["last_session_end"] });
    const record = res.data.results.find(r => r.key === "last_session_end");
    if (record) sessionEndMs = parseInt(record.value, 10);
  } catch (_) {}

  const offlineSeconds   = Math.min((Date.now() - sessionEndMs) / 1000, MAX_OFFLINE_SECONDS);
  const maxGrantable     = Math.floor(offlineSeconds * IDLE_RATE_PER_SEC);
  const grantAmount      = Math.min(claimedAmount, maxGrantable);

  if (grantAmount <= 0) {
    return { granted: 0, newBalance: (await econApi.getPlayerCurrencyBalance({ projectId, playerId, currencyId: "COINS" })).data.balance };
  }

  const res = await econApi.incrementPlayerCurrencyBalance({
    projectId, playerId, currencyId: "COINS",
    currencyModifyBalanceRequest: { amount: grantAmount }
  });

  // Reset session end to now.
  await saveApi.setItem({ projectId, playerId, key: "last_session_end", body: { value: String(Date.now()) } });

  logger.info(`GrantOfflineIncome: player ${playerId} claimed ${claimedAmount}, granted ${grantAmount} → ${res.data.balance}`);
  return { granted: grantAmount, newBalance: res.data.balance };
};
```

---

## PurchaseLand

Atomically validates ownership, deducts coins, records the purchase in the per-tile key,
appends to the player's owned-tiles list so the client can restore state on next login, and
updates the planet's global land registry (Custom Data) so other players see the new owner.

The registry entry written in step 5 is the schema v2 shape:
`{ ownerId, buildLevel, lastYieldClaimTs, lastUpkeepTs, visitCount }`. See `GetLandRegistry`,
`PlaceBuild`, `ClaimYield`, `RecordVisit`, `ApplyUpkeep`, and `SellLand` for the other
readers/writers of this entry.

**Parameters:**

| Name | Type | Required | Description |
|---|---|---|---|
| `tileId` | `string` | Yes | ID of the hex tile being purchased. |
| `planetId` | `string` | Yes | ID of the planet the tile belongs to. |
| `price` | `number` (integer) | Yes | Price of the tile in coins. Must be a positive integer. |

```js
// PurchaseLand — validates ownership, deducts coins, records per-tile ownership,
// appends to the player's owned-tiles list so the client can restore state on login,
// and updates the planet's global land registry so other players see the new owner.
// The registry entry is { ownerId, buildLevel, lastYieldClaimTs, lastUpkeepTs, visitCount } —
// see GetLandRegistry, PlaceBuild, ClaimYield, RecordVisit, ApplyUpkeep, SellLand for readers/writers.
// NOTE: the deduct -> record -> registry sequence is not transactional; see the
// design caveat above.
const { CurrenciesApi, ConfigurationApi } = require("@unity-services/economy-2.5");
const { DataApi: PlayerDataApi }          = require("@unity-services/cloud-save-1.4");

const CURRENCY_ID  = "COINS";
const REGISTRY_KEY = "land_registry";

/**
 * @param {string} tileId - ID of the hex tile being purchased.
 * @param {string} planetId - ID of the planet the tile belongs to.
 * @param {number} price - Price of the tile in coins. Must be a positive integer.
 */
module.exports = async ({ params, context, logger }) => {
  const { tileId, planetId, price } = params;

  if (!tileId || !planetId || !Number.isInteger(price) || price <= 0) {
    throw new Error("Invalid params: tileId, planetId, and price are required");
  }

  const { projectId, playerId, accessToken } = context;

  // FIX 1: constructors don't accept { headers: { Authorization: ... } }.
  // Economy: { accessToken } authenticates as the calling player.
  // Cloud Save: DataApi(context) uses the service token (required for both
  // player-scoped writes and custom/game data writes).
  const econApi       = new CurrenciesApi({ accessToken });
  const config        = new ConfigurationApi({ accessToken });
  const saveApi       = new PlayerDataApi(context);
  const customDataApi = new PlayerDataApi(context); // same instance is fine; kept separate for clarity

  try {
    const ownedKey = `owned_tiles_${planetId.toLowerCase()}`;

    // 1. Load the player's current owned-tiles list for this planet.
    let ownedTiles = [];
    try {
      // FIX 2: getItems takes positional args (projectId, playerId, keys[]),
      // not an options object.
      const saveRes = await saveApi.getItems(projectId, playerId, [ownedKey]);
      const item    = saveRes.data.results.find(r => r.key === ownedKey);
      if (item && Array.isArray(item.value)) ownedTiles = item.value;
    } catch (_) { /* key doesn't exist yet */ }

    if (ownedTiles.includes(tileId)) {
      return { success: false, reason: "ALREADY_OWNED" };
    }

    // 2. Validate balance.
    // FIX 3: getPlayerCurrencyBalance does not exist on CurrenciesApi.
    // The only read method is getPlayerCurrencies (returns all balances).
    const balancesRes = await econApi.getPlayerCurrencies({ projectId, playerId });
    const coins       = balancesRes.data.results.find(c => c.currencyId === CURRENCY_ID);
    const balance     = coins ? coins.balance : 0;

    if (balance < price) {
      return { success: false, reason: "INSUFFICIENT_FUNDS" };
    }

    // 3. Deduct coins.
    // FIX 4: fetch configAssignmentHash before any currency write (documented requirement).
    // FIX 5: currencyModifyBalanceRequest must include currencyId in the body.
    const cfg = await config.getPlayerConfiguration({ projectId, playerId });
    const configAssignmentHash = cfg.data.metadata.configAssignmentHash;

    const deductRes = await econApi.decrementPlayerCurrencyBalance({
      projectId,
      playerId,
      currencyId: CURRENCY_ID,
      configAssignmentHash,
      currencyModifyBalanceRequest: { currencyId: CURRENCY_ID, amount: price }
    });
    const newBalance = deductRes.data.balance;

    // 4. Record per-tile ownership (for cross-player lookup in M3+).
    // FIX 6: setItem takes positional args (projectId, playerId, { key, value }),
    // not an options object with a nested `body` field.
    await saveApi.setItem(projectId, playerId, { key: `tile_${tileId}_owner`, value: playerId });

    // 5. Append to the player's owned-tiles list so it can be restored on login.
    ownedTiles.push(tileId);
    await saveApi.setItem(projectId, playerId, { key: ownedKey, value: ownedTiles });

    // 6. Update the planet's global land registry (Custom Data, shared across all
    //    players) so other clients render this tile as "owned by other".
    const customId = planetId.toLowerCase();
    let registry = {};
    try {
      const regRes = await customDataApi.getCustomItems(projectId, customId, [REGISTRY_KEY]);
      const item   = regRes.data.results.find(r => r.key === REGISTRY_KEY);
      if (item?.value) registry = item.value;
    } catch (_) { /* registry doesn't exist yet */ }

    const now = Date.now();
    registry[tileId] = {
      ownerId:          playerId,
      buildLevel:       0,
      lastYieldClaimTs: now,
      lastUpkeepTs:     now,
      visitCount:       0
    };
    await customDataApi.setCustomItem(projectId, customId, { key: REGISTRY_KEY, value: registry });

    logger.info(`PurchaseLand: player ${playerId} purchased tile ${tileId} on ${planetId} for ${price} → ${newBalance}`);
    return { success: true, newBalance };
  } catch (err) {
    logger.error("PurchaseLand failed", { "error.message": err.message });
    throw err;
  }
};
```

---

## GetLandRegistry

Returns the global tile-ownership map for a planet, so a client can render other players' tiles
as "owned by other". Reads from the same Cloud Save Custom Data item that `PurchaseLand` writes
to (`customId = planetId.toLowerCase()`, `key = "land_registry"`). Returns an empty map if the
registry doesn't exist yet (e.g. no tiles purchased on this planet so far).

This is a generic passthrough — each entry in `tiles` is the schema v2
`{ ownerId, buildLevel, lastYieldClaimTs, lastUpkeepTs, visitCount }` object written by
`PurchaseLand`/`PlaceBuild`/`ClaimYield`/`RecordVisit`/`ApplyUpkeep`. No code change was needed
here when the registry schema was upgraded from a bare `ownerId` string to this object shape.

**Parameters:**

| Name | Type | Required | Description |
|---|---|---|---|
| `planetId` | `string` | Yes | ID of the planet to fetch the land registry for. |

```js
// GetLandRegistry — returns the global tile-ownership map for a planet so a
// client can render other players' tiles as "owned by other".
// Reads from Cloud Save Custom Data (shared across all players, keyed by
// planet rather than by player) — written by PurchaseLand (and future
// SellLand). Returns an empty map if the registry doesn't exist yet.
const { DataApi } = require("@unity-services/cloud-save-1.4");

const REGISTRY_KEY = "land_registry";

/**
 * @param {string} planetId - ID of the planet to fetch the land registry for.
 */
module.exports = async ({ params, context, logger }) => {
  const { planetId } = params;

  if (!planetId) {
    throw new Error("Invalid params: planetId is required");
  }

  const { projectId } = context;
  const customDataApi = new DataApi(context);
  const customId = planetId.toLowerCase();

  try {
    const res  = await customDataApi.getCustomItems(projectId, customId, [REGISTRY_KEY]);
    const item = res.data.results.find(r => r.key === REGISTRY_KEY);
    return { tiles: item?.value ?? {} };
  } catch (err) {
    logger.warn(`GetLandRegistry: no registry yet for ${planetId} (${err.message})`);
    return { tiles: {} };
  }
};
```

---

## PlaceBuild

Validates tile ownership, deducts the item's coin cost, and increments the tile's `buildLevel`
in the planet's global land registry (Custom Data) so `TileExtrusionView` reflects the new
build level for everyone.

**Parameters:**

| Name | Type | Required | Description |
|---|---|---|---|
| `tileId` | `string` | Yes | ID of the hex tile being built on. |
| `planetId` | `string` | Yes | ID of the planet the tile belongs to. |
| `itemId` | `string` | Yes | ID of the `ItemDefinition` being placed. |
| `cost` | `number` (integer) | Yes | Coin cost of the item. Must be a positive integer. |

```js
// PlaceBuild — validates tile ownership, deducts the item's coin cost, and
// increments the tile's buildLevel in the planet's global land registry
// (Custom Data, shared across all players) so TileExtrusionView reflects the
// new build level for everyone.
// NOTE: the validate -> deduct -> registry-write sequence is not transactional;
// same caveat as PurchaseLand.
const { CurrenciesApi }           = require("@unity-services/economy-2.5");
const { DataApi } = require("@unity-services/cloud-save-1.4");

const CURRENCY_ID  = "COINS";
const REGISTRY_KEY = "land_registry";

/**
 * @param {string} tileId - ID of the hex tile being built on.
 * @param {string} planetId - ID of the planet the tile belongs to.
 * @param {string} itemId - ID of the ItemDefinition being placed.
 * @param {number} cost - Coin cost of the item. Must be a positive integer.
 */
module.exports = async ({ params, context, logger }) => {
  const { tileId, planetId, itemId, cost } = params;

  if (!tileId || !planetId || !itemId || !Number.isInteger(cost) || cost <= 0) {
    throw new Error("Invalid params: tileId, planetId, itemId, and cost are required");
  }

  const { projectId, playerId, accessToken } = context;
  const authHeader    = { headers: { Authorization: `Bearer ${accessToken}` } };
  const econApi       = new CurrenciesApi(authHeader);
  const customDataApi = new DataApi(context);
  const customId      = planetId.toLowerCase();

  try {
    // 1. Load the planet's land registry and validate ownership.
    let registry = {};
    try {
      const regRes = await customDataApi.getCustomItems(projectId, customId, [REGISTRY_KEY]);
      const item    = regRes.data.results.find(r => r.key === REGISTRY_KEY);
      if (item?.value) registry = item.value;
    } catch (_) { /* registry doesn't exist yet */ }

    const entry = registry[tileId];
    if (!entry || entry.ownerId !== playerId) {
      return { success: false, reason: "NOT_OWNER" };
    }

    // 2. Validate balance and deduct the item's cost.
    const balanceRes = await econApi.getPlayerCurrencyBalance({ projectId, playerId, currencyId: CURRENCY_ID });
    if (balanceRes.data.balance < cost) {
      return { success: false, reason: "INSUFFICIENT_FUNDS" };
    }

    const deductRes = await econApi.decrementPlayerCurrencyBalance({
      projectId,
      playerId,
      currencyId: CURRENCY_ID,
      currencyModifyBalanceRequest: { amount: cost }
    });
    const newBalance = deductRes.data.balance;

    // 3. Increment the tile's build level and write the registry back.
    entry.buildLevel = (entry.buildLevel ?? 0) + 1;
    registry[tileId] = entry;
    await customDataApi.setCustomItem(projectId, customId, { key: REGISTRY_KEY, value: registry });

    logger.info(`PlaceBuild: player ${playerId} placed ${itemId} on tile ${tileId} (${planetId}) for ${cost} → ${newBalance}, buildLevel ${entry.buildLevel}`);
    return { success: true, newBalance, buildLevel: entry.buildLevel };
  } catch (err) {
    logger.error("PlaceBuild failed", { "error.message": err.message });
    throw err;
  }
};
```

---

## ClaimYield

Computes accrued visitor-driven land income for an owned tile and grants it, then resets the
tile's yield-accrual state (`lastYieldClaimTs`, `visitCount`) in the planet's global land
registry (Custom Data).

**Formula** (constants below must match `EconomyConfig`'s `[Header("Yield")]` values):

```
elapsedHours = min((now - lastYieldClaimTs) / 3600000, MAX_YIELD_ACCRUAL_HOURS)
buildBonus   = buildLevel * BUILD_LEVEL_YIELD_MULTIPLIER
visitBonus   = min(visitCount, MAX_VISIT_COUNT) * VISIT_YIELD_BONUS
granted      = floor(BASE_YIELD_PER_TILE_PER_HOUR * (1 + buildBonus + visitBonus) * elapsedHours)
```

**Parameters:**

| Name | Type | Required | Description |
|---|---|---|---|
| `tileId` | `string` | Yes | ID of the hex tile to claim yield for. |
| `planetId` | `string` | Yes | ID of the planet the tile belongs to. |

```js
// ClaimYield — computes and grants accrued visitor-driven land income for an
// owned tile, then resets its yield-accrual state in the planet's global land
// registry (Custom Data).
// Formula (constants below must match EconomyConfig's [Header("Yield")] values):
//   elapsedHours = min((now - entry.lastYieldClaimTs) / 3600000, MAX_YIELD_ACCRUAL_HOURS)
//   buildBonus   = entry.buildLevel * BUILD_LEVEL_YIELD_MULTIPLIER
//   visitBonus   = min(entry.visitCount, MAX_VISIT_COUNT) * VISIT_YIELD_BONUS
//   granted      = floor(BASE_YIELD_PER_TILE_PER_HOUR * (1 + buildBonus + visitBonus) * elapsedHours)
// NOTE: the validate -> grant -> registry-write sequence is not transactional;
// same caveat as PurchaseLand.
const { CurrenciesApi }           = require("@unity-services/economy-2.5");
const { DataApi } = require("@unity-services/cloud-save-1.4");

const CURRENCY_ID  = "COINS";
const REGISTRY_KEY = "land_registry";

const BASE_YIELD_PER_TILE_PER_HOUR = 2;    // must match EconomyConfig.BaseYieldPerTilePerHour
const BUILD_LEVEL_YIELD_MULTIPLIER = 0.25; // must match EconomyConfig.BuildLevelYieldMultiplier
const VISIT_YIELD_BONUS            = 0.1;  // must match EconomyConfig.VisitYieldBonus
const MAX_YIELD_ACCRUAL_HOURS      = 24;   // must match EconomyConfig.MaxYieldAccrualHours
const MAX_VISIT_COUNT              = 50;   // must match EconomyConfig.MaxVisitCount

const MS_PER_HOUR = 3600000;

/**
 * @param {string} tileId - ID of the hex tile to claim yield for.
 * @param {string} planetId - ID of the planet the tile belongs to.
 */
module.exports = async ({ params, context, logger }) => {
  const { tileId, planetId } = params;

  if (!tileId || !planetId) {
    throw new Error("Invalid params: tileId and planetId are required");
  }

  const { projectId, playerId, accessToken } = context;
  const authHeader    = { headers: { Authorization: `Bearer ${accessToken}` } };
  const econApi       = new CurrenciesApi(authHeader);
  const customDataApi = new DataApi(context);
  const customId      = planetId.toLowerCase();

  try {
    // 1. Load the planet's land registry and validate ownership.
    let registry = {};
    try {
      const regRes = await customDataApi.getCustomItems(projectId, customId, [REGISTRY_KEY]);
      const item    = regRes.data.results.find(r => r.key === REGISTRY_KEY);
      if (item?.value) registry = item.value;
    } catch (_) { /* registry doesn't exist yet */ }

    const entry = registry[tileId];
    if (!entry || entry.ownerId !== playerId) {
      return { success: false, reason: "NOT_OWNER" };
    }

    // 2. Compute accrued yield since the last claim.
    const now          = Date.now();
    const elapsedHours = Math.min((now - (entry.lastYieldClaimTs ?? now)) / MS_PER_HOUR, MAX_YIELD_ACCRUAL_HOURS);
    const buildBonus   = (entry.buildLevel ?? 0) * BUILD_LEVEL_YIELD_MULTIPLIER;
    const visitBonus   = Math.min(entry.visitCount ?? 0, MAX_VISIT_COUNT) * VISIT_YIELD_BONUS;
    const granted      = Math.floor(BASE_YIELD_PER_TILE_PER_HOUR * (1 + buildBonus + visitBonus) * elapsedHours);

    let newBalance;
    if (granted > 0) {
      const grantRes = await econApi.incrementPlayerCurrencyBalance({
        projectId, playerId, currencyId: CURRENCY_ID,
        currencyModifyBalanceRequest: { amount: granted }
      });
      newBalance = grantRes.data.balance;
    } else {
      newBalance = (await econApi.getPlayerCurrencyBalance({ projectId, playerId, currencyId: CURRENCY_ID })).data.balance;
    }

    // 3. Reset yield-accrual state and write the registry back.
    entry.lastYieldClaimTs = now;
    entry.visitCount       = 0;
    registry[tileId]       = entry;
    await customDataApi.setCustomItem(projectId, customId, { key: REGISTRY_KEY, value: registry });

    logger.info(`ClaimYield: player ${playerId} claimed tile ${tileId} (${planetId}) — granted ${granted} → ${newBalance}`);
    return { success: true, granted, newBalance };
  } catch (err) {
    logger.error("ClaimYield failed", { "error.message": err.message });
    throw err;
  }
};
```

---

## RecordVisit

Increments a tile's `visitCount` in the planet's global land registry (Custom Data) when
another player visits it. This is the **M3 stand-in for real presence-based visit detection**
— the client calls this when a player selects a tile they don't own (see
`VisitorTrackingController`). Feeds into `ClaimYield`'s visit bonus. No economy mutation.

**Parameters:**

| Name | Type | Required | Description |
|---|---|---|---|
| `tileId` | `string` | Yes | ID of the hex tile being visited. |
| `planetId` | `string` | Yes | ID of the planet the tile belongs to. |

```js
// RecordVisit — increments a tile's visitCount in the planet's global land
// registry (Custom Data) when another player visits it.
// This is the M3 stand-in for real presence-based visit detection — the
// client calls this when a player selects a tile they don't own (see
// VisitorTrackingController). Feeds into ClaimYield's visit bonus. No economy
// mutation.
const { DataApi } = require("@unity-services/cloud-save-1.4");

const REGISTRY_KEY    = "land_registry";
const MAX_VISIT_COUNT = 50; // must match EconomyConfig.MaxVisitCount

/**
 * @param {string} tileId - ID of the hex tile being visited.
 * @param {string} planetId - ID of the planet the tile belongs to.
 */
module.exports = async ({ params, context, logger }) => {
  const { tileId, planetId } = params;

  if (!tileId || !planetId) {
    throw new Error("Invalid params: tileId and planetId are required");
  }

  const { projectId, playerId } = context;
  const customDataApi = new DataApi(context);
  const customId      = planetId.toLowerCase();

  try {
    let registry = {};
    try {
      const regRes = await customDataApi.getCustomItems(projectId, customId, [REGISTRY_KEY]);
      const item    = regRes.data.results.find(r => r.key === REGISTRY_KEY);
      if (item?.value) registry = item.value;
    } catch (_) { /* registry doesn't exist yet */ }

    const entry = registry[tileId];
    if (!entry || entry.ownerId === playerId) {
      return { success: false, visitCount: entry?.visitCount ?? 0 };
    }

    entry.visitCount = Math.min((entry.visitCount ?? 0) + 1, MAX_VISIT_COUNT);
    registry[tileId] = entry;
    await customDataApi.setCustomItem(projectId, customId, { key: REGISTRY_KEY, value: registry });

    return { success: true, visitCount: entry.visitCount };
  } catch (err) {
    logger.error("RecordVisit failed", { "error.message": err.message });
    throw err;
  }
};
```

---

## ApplyUpkeep

Charges recurring per-tile upkeep for all of the caller's tiles on a planet. For each owned
tile where at least one full day has elapsed since `lastUpkeepTs`: if the player can afford
`UPKEEP_PER_TILE_PER_DAY * daysElapsed`, the cost is deducted and `lastUpkeepTs` advances by
that many days (`chargedTiles`); otherwise the tile's registry entry is deleted and it reverts
to `Available` for everyone (`revertedTiles`).

**Parameters:**

| Name | Type | Required | Description |
|---|---|---|---|
| `planetId` | `string` | Yes | ID of the planet to apply upkeep for. |

```js
// ApplyUpkeep — charges recurring per-tile upkeep for all of the caller's
// tiles on a planet. For each owned tile where at least one full day has
// elapsed since lastUpkeepTs:
//   cost = UPKEEP_PER_TILE_PER_DAY * daysElapsed
//   if balance >= cost: deduct cost, advance lastUpkeepTs by daysElapsed days (chargedTiles)
//   else: remove the tile from the registry — it reverts to Available for everyone (revertedTiles)
// NOTE: the read -> deduct -> registry-write sequence is not transactional;
// same caveat as PurchaseLand.
const { CurrenciesApi }           = require("@unity-services/economy-2.5");
const { DataApi } = require("@unity-services/cloud-save-1.4");

const CURRENCY_ID  = "COINS";
const REGISTRY_KEY = "land_registry";

const UPKEEP_PER_TILE_PER_DAY = 5; // must match EconomyConfig.UpkeepPerTilePerDay
const MS_PER_DAY = 86400000;

/**
 * @param {string} planetId - ID of the planet to apply upkeep for.
 */
module.exports = async ({ params, context, logger }) => {
  const { planetId } = params;

  if (!planetId) {
    throw new Error("Invalid params: planetId is required");
  }

  const { projectId, playerId, accessToken } = context;
  const authHeader    = { headers: { Authorization: `Bearer ${accessToken}` } };
  const econApi       = new CurrenciesApi(authHeader);
  const customDataApi = new DataApi(context);
  const customId      = planetId.toLowerCase();

  try {
    let registry = {};
    try {
      const regRes = await customDataApi.getCustomItems(projectId, customId, [REGISTRY_KEY]);
      const item    = regRes.data.results.find(r => r.key === REGISTRY_KEY);
      if (item?.value) registry = item.value;
    } catch (_) { /* registry doesn't exist yet */ }

    const now = Date.now();
    let balance = (await econApi.getPlayerCurrencyBalance({ projectId, playerId, currencyId: CURRENCY_ID })).data.balance;

    const chargedTiles  = [];
    const revertedTiles = [];
    let registryDirty   = false;

    for (const [tileId, entry] of Object.entries(registry)) {
      if (entry.ownerId !== playerId) continue;

      const daysElapsed = Math.floor((now - (entry.lastUpkeepTs ?? now)) / MS_PER_DAY);
      if (daysElapsed < 1) continue;

      const cost = UPKEEP_PER_TILE_PER_DAY * daysElapsed;
      if (balance >= cost) {
        const deductRes = await econApi.decrementPlayerCurrencyBalance({
          projectId, playerId, currencyId: CURRENCY_ID,
          currencyModifyBalanceRequest: { amount: cost }
        });
        balance = deductRes.data.balance;
        entry.lastUpkeepTs = (entry.lastUpkeepTs ?? now) + daysElapsed * MS_PER_DAY;
        chargedTiles.push(tileId);
      } else {
        delete registry[tileId];
        revertedTiles.push(tileId);
      }
      registryDirty = true;
    }

    if (registryDirty) {
      await customDataApi.setCustomItem(projectId, customId, { key: REGISTRY_KEY, value: registry });
    }

    logger.info(`ApplyUpkeep: player ${playerId} on ${planetId} — charged ${chargedTiles.length}, reverted ${revertedTiles.length} → balance ${balance}`);
    return { newBalance: balance, chargedTiles, revertedTiles };
  } catch (err) {
    logger.error("ApplyUpkeep failed", { "error.message": err.message });
    throw err;
  }
};
```

---

## SellLand

Validates ownership, grants the (client-computed) refund, removes the tile from the player's
owned-tiles list, and removes the tile from the planet's global land registry so it reverts to
`Available` for everyone. `refund` is computed client-side from `EconomyConfig.BaseLandPrice *
PlanetDefinition.LandPriceMultiplier * EconomyConfig.LandResaleRate` — the server doesn't have
planet pricing data, but still gates the payout on ownership, same trust model as
`PurchaseLand`'s `price` param.

**Parameters:**

| Name | Type | Required | Description |
|---|---|---|---|
| `tileId` | `string` | Yes | ID of the hex tile being sold. |
| `planetId` | `string` | Yes | ID of the planet the tile belongs to. |
| `refund` | `number` (integer) | Yes | Coins to refund the seller. Must be a non-negative integer. |

```js
// SellLand — validates ownership, grants the (client-computed) refund, removes
// the tile from the player's owned-tiles list, and removes the tile from the
// planet's global land registry so it reverts to Available for everyone.
// `refund` is computed client-side from EconomyConfig.BaseLandPrice *
// PlanetDefinition.LandPriceMultiplier * EconomyConfig.LandResaleRate — the
// server doesn't have planet pricing data, but still gates the payout on
// ownership, same trust model as PurchaseLand's `price` param.
// NOTE: the validate -> grant -> cleanup sequence is not transactional; same
// caveat as PurchaseLand.
const { CurrenciesApi }            = require("@unity-services/economy-2.5");
const { DataApi: PlayerDataApi }   = require("@unity-services/cloud-save-1.4");

const CURRENCY_ID  = "COINS";
const REGISTRY_KEY = "land_registry";

/**
 * @param {string} tileId - ID of the hex tile being sold.
 * @param {string} planetId - ID of the planet the tile belongs to.
 * @param {number} refund - Coins to refund the seller. Must be a non-negative integer.
 */
module.exports = async ({ params, context, logger }) => {
  const { tileId, planetId, refund } = params;

  if (!tileId || !planetId || !Number.isInteger(refund) || refund < 0) {
    throw new Error("Invalid params: tileId, planetId, and refund are required");
  }

  const { projectId, playerId, accessToken } = context;
  const authHeader    = { headers: { Authorization: `Bearer ${accessToken}` } };
  const econApi       = new CurrenciesApi(authHeader);
  const saveApi       = new PlayerDataApi(authHeader);
  const customDataApi = new PlayerDataApi(context);
  const customId      = planetId.toLowerCase();

  try {
    // 1. Load the planet's land registry and validate ownership.
    let registry = {};
    try {
      const regRes = await customDataApi.getCustomItems(projectId, customId, [REGISTRY_KEY]);
      const item    = regRes.data.results.find(r => r.key === REGISTRY_KEY);
      if (item?.value) registry = item.value;
    } catch (_) { /* registry doesn't exist yet */ }

    const entry = registry[tileId];
    if (!entry || entry.ownerId !== playerId) {
      return { success: false, reason: "NOT_OWNER" };
    }

    // 2. Grant the refund.
    let newBalance = (await econApi.getPlayerCurrencyBalance({ projectId, playerId, currencyId: CURRENCY_ID })).data.balance;
    if (refund > 0) {
      const grantRes = await econApi.incrementPlayerCurrencyBalance({
        projectId, playerId, currencyId: CURRENCY_ID,
        currencyModifyBalanceRequest: { amount: refund }
      });
      newBalance = grantRes.data.balance;
    }

    // 3. Remove the tile from the player's owned-tiles list.
    const ownedKey = `owned_tiles_${planetId.toLowerCase()}`;
    try {
      const saveRes = await saveApi.getItems({ projectId, playerId, key: [ownedKey] });
      const item    = saveRes.data.results.find(r => r.key === ownedKey);
      if (item && Array.isArray(item.value)) {
        const ownedTiles = item.value.filter(id => id !== tileId);
        await saveApi.setItem({ projectId, playerId, key: ownedKey, body: { value: ownedTiles } });
      }
    } catch (_) { /* key doesn't exist yet */ }

    // 4. Remove the tile from the global land registry.
    delete registry[tileId];
    await customDataApi.setCustomItem(projectId, customId, { key: REGISTRY_KEY, value: registry });

    logger.info(`SellLand: player ${playerId} sold tile ${tileId} on ${planetId} for ${refund} → ${newBalance}`);
    return { success: true, newBalance };
  } catch (err) {
    logger.error("SellLand failed", { "error.message": err.message });
    throw err;
  }
};
```

---

## GetFuelState

Returns the caller's current fuel, recharging it server-side based on elapsed time since the
last update (player-scoped Cloud Save key `fuel_state = { fuel, maxFuel, lastUpdateTs }`).
Initializes a fresh fuel_state record (full tank) on first call for a player.

**Parameters:** none

```js
// GetFuelState — returns the caller's current fuel, recharged server-side based
// on elapsed time since the last update.
//
// FIX 1: this is now a PURE READ. The old version wrote to Cloud Save on every
// call, which turned every gauge refresh into a write (cost + rate-limit risk,
// and a read that mutates state). Recharge is fully derived from lastUpdateTs,
// so there is nothing to persist here. The fuel_state record is created lazily
// on the first SpendFuel / RefillFuel call.
// FIX 2: DataApi's constructor doesn't read a { headers: ... } field, and
// getItems takes positional args (projectId, playerId, keys[]), not an options
// object — same SDK-shape mismatch documented as Known Issue #6 for the old
// PurchaseLand.js. DataApi(context) authenticates as the calling player via
// the service token, which is correct for player-scoped reads.
const { DataApi } = require("@unity-services/cloud-save-1.4");

const SAVE_KEY               = "fuel_state";
const MAX_FUEL               = 100; // keep in sync with EconomyConfig.MaxFuel
const FUEL_RECHARGE_PER_HOUR = 10;  // keep in sync with EconomyConfig.FuelRechargePerHour
const MS_PER_HOUR            = 3600000;

// Returns { fuel, maxFuel } with time-based recharge applied (no mutation).
function rechargedFuel(state, now) {
  if (!state) return { fuel: MAX_FUEL, maxFuel: MAX_FUEL };
  const maxFuel = state.maxFuel ?? MAX_FUEL;
  const last    = state.lastUpdateTs ?? now;
  const elapsedHours = Math.max(0, (now - last) / MS_PER_HOUR);
  const fuel    = Math.min(maxFuel, (state.fuel ?? 0) + elapsedHours * FUEL_RECHARGE_PER_HOUR);
  return { fuel, maxFuel };
}

/**
 * No parameters.
 */
module.exports = async ({ context, logger }) => {
  const { projectId, playerId } = context;
  const saveApi = new DataApi(context);

  let state = null;
  try {
    const res    = await saveApi.getItems(projectId, playerId, [SAVE_KEY]);
    const record = res.data.results.find(r => r.key === SAVE_KEY);
    if (record?.value) state = record.value;
  } catch (_) { /* no record yet */ }

  const { fuel, maxFuel } = rechargedFuel(state, Date.now());

  logger.info(`GetFuelState: player ${playerId} fuel ${fuel.toFixed(2)}/${maxFuel}`);
  return { success: true, fuel, maxFuel };
};
```

---

## SpendFuel

Recharges fuel up to now (same formula as `GetFuelState`), then validates and deducts the
requested amount. Returns the post-spend state either way so the client can resync its gauge.
Used by `TravelService` to pay for a star-map trip (trips home are free — the client never
calls this for the home planet).

**Parameters:**

| Name | Type | Required | Description |
|---|---|---|---|
| `amount` | `number` | Yes | Fuel units to spend. Must be a non-negative number. |

```js
// SpendFuel — recharges fuel up to now, then validates and deducts the requested
// amount. Returns the post-spend state either way so the client can resync.
//
// FIX 1: DataApi's constructor doesn't read a { headers: ... } field, and
// getItems/setItem take positional args, not an options object — same
// SDK-shape mismatch documented as Known Issue #6 for the old PurchaseLand.js.
// DataApi(context) authenticates as the calling player via the service token.
// FIX 2 (important): the old version did a plain read-modify-write, so two
// concurrent calls could each read a full tank and both succeed — letting a
// player spend the same fuel twice (e.g. travel twice on one tank). This version
// commits under a Cloud Save write-lock (optimistic concurrency) and retries on
// conflict, so concurrent spends can't double-spend.
// FIX 3: amount validation used typeof === "number", which lets NaN/Infinity
// through (NaN < 0 is false). Now uses Number.isFinite.
const { DataApi } = require("@unity-services/cloud-save-1.4");

const SAVE_KEY               = "fuel_state";
const MAX_FUEL               = 100; // keep in sync with EconomyConfig.MaxFuel
const FUEL_RECHARGE_PER_HOUR = 10;  // keep in sync with EconomyConfig.FuelRechargePerHour
const MS_PER_HOUR            = 3600000;
const MAX_RETRIES            = 3;

function recharge(state, now) {
  if (!state) return { fuel: MAX_FUEL, maxFuel: MAX_FUEL, lastUpdateTs: now };
  const maxFuel = state.maxFuel ?? MAX_FUEL;
  const last    = state.lastUpdateTs ?? now;
  const elapsedHours = Math.max(0, (now - last) / MS_PER_HOUR);
  const fuel    = Math.min(maxFuel, (state.fuel ?? 0) + elapsedHours * FUEL_RECHARGE_PER_HOUR);
  return { fuel, maxFuel, lastUpdateTs: now };
}

function isConflict(err) {
  const status = err?.response?.status ?? err?.status;
  return status === 409; // Cloud Save write-lock mismatch
}

/**
 * @param {number} amount - Fuel units to spend. Must be a finite, non-negative number.
 */
module.exports = async ({ params, context, logger }) => {
  const { amount } = params;
  if (!Number.isFinite(amount) || amount < 0) {
    throw new Error(`Invalid amount: ${amount}`);
  }

  const { projectId, playerId } = context;
  const saveApi = new DataApi(context);

  for (let attempt = 0; attempt < MAX_RETRIES; attempt++) {
    // Read current record + its writeLock.
    let raw = null, writeLock;
    try {
      const res    = await saveApi.getItems(projectId, playerId, [SAVE_KEY]);
      const record = res.data.results.find(r => r.key === SAVE_KEY);
      if (record?.value) { raw = record.value; writeLock = record.writeLock; }
    } catch (_) { /* no record yet */ }

    const state = recharge(raw, Date.now());

    let success = false;
    if (state.fuel >= amount) { state.fuel -= amount; success = true; }

    // Conditional write: include writeLock when we had a record. If your Cloud
    // Save SDK version doesn't accept writeLock here, drop it (you lose the
    // double-spend guard but keep the rest of the logic).
    const body = writeLock ? { key: SAVE_KEY, value: state, writeLock } : { key: SAVE_KEY, value: state };
    try {
      await saveApi.setItem(projectId, playerId, body);
    } catch (err) {
      if (isConflict(err) && attempt < MAX_RETRIES - 1) continue; // someone else wrote; retry
      throw err;
    }

    if (success) logger.info(`SpendFuel: player ${playerId} -${amount} → ${state.fuel.toFixed(2)}/${state.maxFuel}`);
    else         logger.warn(`SpendFuel: insufficient fuel for ${playerId} (have ${state.fuel.toFixed(2)}, need ${amount})`);
    return { success, fuel: state.fuel, maxFuel: state.maxFuel };
  }

  logger.error(`SpendFuel: write-lock retries exhausted for ${playerId}`);
  throw new Error("SpendFuel: concurrent update conflict, please retry.");
};
```

---

## RefillFuel

Instantly tops the caller's fuel up to `maxFuel` in exchange for coins. Validates affordability
server-side before deducting — the manual-refill path for `FuelSystem.RefillAsync`.

**Parameters:** none

```js
// RefillFuel — instantly tops the caller's fuel up to maxFuel in exchange for
// coins.
//
// FIX 1: DataApi's constructor doesn't read a { headers: ... } field, and
// getItems/setItem take positional args, not an options object — same
// SDK-shape mismatch documented as Known Issue #6 for the old PurchaseLand.js.
// DataApi(context) authenticates as the calling player via the service token;
// Economy's CurrenciesApi still authenticates via { accessToken }.
// FIX 2: rejects with no charge if the tank is already full (old version
//        happily took 50 coins for 0 fuel).
// FIX 3: treats a failed coin decrement as "insufficient funds" instead of
//        relying on a racy pre-read + letting the decrement throw uncaught.
// FIX 4: closes the "charged but not granted" window — if the fuel write can't
//        commit after coins were deducted, it refunds the coins (compensating
//        action).
// FIX 5: writes fuel under a Cloud Save write-lock (optimistic concurrency).
// FIX 6: consistent return shape on every path.
const { CurrenciesApi } = require("@unity-services/economy-2.5");
const { DataApi }       = require("@unity-services/cloud-save-1.4");

const SAVE_KEY               = "fuel_state";
const CURRENCY_ID            = "COINS";
const MAX_FUEL               = 100; // keep in sync with EconomyConfig.MaxFuel
const FUEL_RECHARGE_PER_HOUR = 10;  // keep in sync with EconomyConfig.FuelRechargePerHour
const MS_PER_HOUR            = 3600000;
const REFILL_COST            = 50;  // keep in sync with EconomyConfig.FuelRefillCost
const MAX_RETRIES            = 3;

function recharge(state, now) {
  if (!state) return { fuel: MAX_FUEL, maxFuel: MAX_FUEL };
  const maxFuel = state.maxFuel ?? MAX_FUEL;
  const last    = state.lastUpdateTs ?? now;
  const elapsedHours = Math.max(0, (now - last) / MS_PER_HOUR);
  const fuel    = Math.min(maxFuel, (state.fuel ?? 0) + elapsedHours * FUEL_RECHARGE_PER_HOUR);
  return { fuel, maxFuel };
}

function isConflict(err) {
  const status = err?.response?.status ?? err?.status;
  return status === 409;
}

async function readBalance(econApi, projectId, playerId) {
  try {
    const res = await econApi.getPlayerCurrencyBalance({ projectId, playerId, currencyId: CURRENCY_ID });
    return res.data.balance;
  } catch (_) { return undefined; }
}

/**
 * No parameters.
 */
module.exports = async ({ context, logger }) => {
  const { projectId, playerId, accessToken } = context;
  const econApi = new CurrenciesApi({ accessToken });
  const saveApi = new DataApi(context);

  // 1. Read current fuel (recharged) + writeLock.
  let raw = null, writeLock;
  try {
    const res    = await saveApi.getItems(projectId, playerId, [SAVE_KEY]);
    const record = res.data.results.find(r => r.key === SAVE_KEY);
    if (record?.value) { raw = record.value; writeLock = record.writeLock; }
  } catch (_) { /* no record yet */ }

  const now     = Date.now();
  const current = recharge(raw, now);

  // 2. Already full → don't charge.
  if (current.fuel >= current.maxFuel) {
    return {
      success: false, reason: "already_full",
      fuel: current.fuel, maxFuel: current.maxFuel,
      newBalance: await readBalance(econApi, projectId, playerId)
    };
  }

  // 3. Charge coins. Economy is atomic & authoritative; a throw = can't afford.
  let newBalance;
  try {
    const deduct = await econApi.decrementPlayerCurrencyBalance({
      projectId, playerId, currencyId: CURRENCY_ID,
      currencyModifyBalanceRequest: { amount: REFILL_COST }
    });
    newBalance = deduct.data.balance;
  } catch (err) {
    logger.warn(`RefillFuel: charge failed for ${playerId} (insufficient or error): ${err?.message}`);
    return {
      success: false, reason: "insufficient_funds",
      fuel: current.fuel, maxFuel: current.maxFuel,
      newBalance: await readBalance(econApi, projectId, playerId)
    };
  }

  // 4. Grant fuel (full tank) under write-lock, with retry; refund on failure.
  const full = { fuel: current.maxFuel, maxFuel: current.maxFuel, lastUpdateTs: now };
  let lock = writeLock;
  for (let attempt = 0; attempt < MAX_RETRIES; attempt++) {
    const body = lock ? { key: SAVE_KEY, value: full, writeLock: lock } : { key: SAVE_KEY, value: full };
    try {
      await saveApi.setItem(projectId, playerId, body);
      logger.info(`RefillFuel: player ${playerId} refilled to ${full.fuel}/${full.maxFuel} for ${REFILL_COST} → ${newBalance}`);
      return { success: true, fuel: full.fuel, maxFuel: full.maxFuel, newBalance };
    } catch (err) {
      if (isConflict(err) && attempt < MAX_RETRIES - 1) {
        // Re-read the latest writeLock and retry (value is always a full tank).
        try {
          const res    = await saveApi.getItems(projectId, playerId, [SAVE_KEY]);
          const record = res.data.results.find(r => r.key === SAVE_KEY);
          lock = record?.writeLock;
        } catch (_) { lock = undefined; }
        continue;
      }
      // Couldn't grant fuel after charging → refund the coins.
      logger.error(`RefillFuel: fuel write failed after charge for ${playerId}; refunding ${REFILL_COST}. ${err?.message}`);
      try {
        const refund = await econApi.incrementPlayerCurrencyBalance({
          projectId, playerId, currencyId: CURRENCY_ID,
          currencyModifyBalanceRequest: { amount: REFILL_COST }
        });
        newBalance = refund.data.balance;
      } catch (refundErr) {
        logger.error(`RefillFuel: REFUND FAILED for ${playerId} — manual reconciliation needed: ${refundErr?.message}`);
      }
      return { success: false, reason: "write_failed", fuel: current.fuel, maxFuel: current.maxFuel, newBalance };
    }
  }
};
```

### UpdateProfile
```js
// UpdateProfile — validates and commits the caller's display name into their
// "player_profile" Cloud Save record (merging with any existing profile
// fields such as level/xp/badges). The name is re-moderated server-side: the
// client's ChatModerationFilter check is only fast feedback.
// BLOCKED_WORDS / CHAR_MAP / MAX_DISPLAY_NAME_LENGTH must match
// SocialConfig.BlockedWords / ChatModerationFilter / MaxDisplayNameLength —
// same "must match" pattern as ModerateMessage.js (Cloud Code modules deploy
// as standalone files, so the filter is duplicated rather than required).
const { DataApi } = require("@unity-services/cloud-save-1.4");

const PROFILE_KEY             = "player_profile";
const MAX_DISPLAY_NAME_LENGTH = 20; // must match SocialConfig.MaxDisplayNameLength

const BLOCKED_WORDS = [
  "fuck", "shit", "bitch", "asshole", "cunt", "dick", "faggot",
  "nigger", "nigga", "whore", "slut", "retard", "kys"
];
const CHAR_MAP = { "@": "a", "4": "a", "1": "i", "!": "i", "0": "o", "3": "e", "$": "s", "5": "s", "7": "t" };

function isClean(text) {
  let normalized = "";
  for (const ch of text.toLowerCase()) normalized += CHAR_MAP[ch] ?? ch;
  return !BLOCKED_WORDS.some(word => normalized.includes(word));
}

/**
 * @param {string} displayName - The new display name. 1–20 characters, must pass moderation.
 */
module.exports = async ({ params, context, logger }) => {
  const displayName = (params.displayName ?? "").trim();

  if (displayName.length === 0) {
    return { success: false, reason: "NAME_EMPTY", displayName: null };
  }
  if (displayName.length > MAX_DISPLAY_NAME_LENGTH) {
    return { success: false, reason: "NAME_TOO_LONG", displayName: null };
  }
  if (!isClean(displayName)) {
    logger.info(`UpdateProfile: rejected display name from ${context.playerId}`);
    return { success: false, reason: "NAME_REJECTED", displayName: null };
  }

  const { projectId, playerId } = context;
  // FIX: was new DataApi({ headers: { Authorization: ... } }) — that field is not
  // read by the constructor. For player-scoped data, DataApi(context) is correct
  // and authenticates as the calling player via the service token.
  const saveApi = new DataApi(context);

  let profile = {};
  try {
    // FIX: getItems takes positional args (projectId, playerId, keys[]), not an options object.
    const res  = await saveApi.getItems(projectId, playerId, [PROFILE_KEY]);
    const item = res.data.results.find(r => r.key === PROFILE_KEY);
    // FIX: Cloud Save returns values already deserialized — JSON.parse throws on an object.
    if (item?.value && typeof item.value === "object") profile = item.value;
  } catch (_) { /* no profile yet */ }

  profile.displayName = displayName;
  profile.updatedMs   = Date.now();

  // FIX: setItem takes positional args (projectId, playerId, { key, value }),
  // not an options object with a nested `body` field.
  await saveApi.setItem(projectId, playerId, { key: PROFILE_KEY, value: profile });

  logger.info(`UpdateProfile: ${playerId} → "${displayName}"`);
  return { success: true, reason: null, displayName };
};
```

### GetPlayerProfile

```js
// GetPlayerProfile — returns any player's public profile. Reads the target
// player's "player_profile" Cloud Save record (the same one GetBootstrapState
// returns for the caller) plus a tile count derived from their owned-tiles
// lists. Returns defaults for players who haven't saved a profile yet.
//
// FIX: DataApi's constructor doesn't read a { headers: ... } field, and
// getItems takes positional args (projectId, playerId, keys[]), not an
// options object — same SDK-shape mismatch as Known Issue #6 (see
// SaveEmail.js). The old call silently failed every time (caught below),
// so `profile` was always null. DataApi(context) authenticates via the
// service token, which is required to read another player's data.
//
// FIX 2: displayName no longer defaults to a synthetic "Pilot {id6}"
// placeholder — it's null when the player hasn't saved a custom display
// name. The only current caller (PlanetSceneScope.HydrateServerStateAsync,
// hydrating the signed-in player's own HUD name) already falls back to the
// UGS auth username when displayName is empty; the placeholder default was
// unconditionally overriding that correct username for every player who
// hadn't explicitly customized their name.
const { DataApi } = require("@unity-services/cloud-save-1.4");

const PROFILE_KEY = "player_profile";

/**
 * @param {string} playerId - Player ID whose public profile to fetch.
 */
module.exports = async ({ params, context, logger }) => {
  const targetId = params.playerId;

  if (!targetId) {
    throw new Error("Invalid params: playerId is required");
  }

  const { projectId } = context;
  const saveApi = new DataApi(context);

  let profile = null;
  let tilesOwned = 0;
  try {
    // Cloud Code's service auth may read another player's data by passing
    // their playerId explicitly.
    const res = await saveApi.getItems(projectId, targetId);
    for (const item of res.data.results) {
      if (item.key === PROFILE_KEY) {
        profile = typeof item.value === "string" ? JSON.parse(item.value) : item.value;
      } else if (item.key.startsWith("owned_tiles_") && Array.isArray(item.value)) {
        tilesOwned += item.value.length;
      }
    }
  } catch (err) {
    logger.warn(`GetPlayerProfile: read failed for ${targetId} (${err.message})`);
  }

  return {
    playerId:    targetId,
    displayName: profile?.displayName ?? null,
    level:       profile?.level ?? 1,
    xp:          profile?.xp ?? 0,
    badges:      profile?.badges ?? [],
    tilesOwned
  };
};

```

### BlockUser

```js
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

  const { projectId, playerId } = context;

  if (targetId === playerId) {
    return { success: false, blockedUsers: [] };
  }

  // FIX: was new DataApi({ headers: { Authorization: ... } }) — constructor does
  // not accept that field. DataApi(context) authenticates as the calling player
  // via the service token, which is correct for player-scoped data.
  const saveApi = new DataApi(context);

  let blockedUsers = [];
  try {
    // FIX: getItems takes positional args (projectId, playerId, keys[]),
    // not an options object.
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

  // FIX: setItem takes positional args (projectId, playerId, { key, value }),
  // not an options object with a nested `body` field.
  await saveApi.setItem(projectId, playerId, { key: BLOCKED_KEY, value: blockedUsers });

  logger.info(`BlockUser: ${playerId} ${blocked ? "blocked" : "unblocked"} ${targetId} (${blockedUsers.length} total)`);
  return { success: true, blockedUsers };
};
```


### SubmitReport

```js
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
```


### ModerateMessage

```js
// ModerateMessage — server-side text moderation. Returns whether the text is
// allowed and a masked version. Used by UpdateProfile for display names, and
// available to any future server-mediated message path. (Vivox channel chat is
// filtered client-side by ChatModerationFilter and by Vivox's own moderation
// tooling — this function is the in-house enforcement point.)
// BLOCKED_WORDS and the normalization map must match SocialConfig.BlockedWords
// / ChatModerationFilter.NormalizeChar — same "must match" pattern as
// ClaimYield.js's yield constants.

const BLOCKED_WORDS = [
  "fuck", "shit", "bitch", "asshole", "cunt", "dick", "faggot",
  "nigger", "nigga", "whore", "slut", "retard", "kys"
];

const MAX_MESSAGE_LENGTH = 200; // must match SocialConfig.MaxMessageLength

const CHAR_MAP = { "@": "a", "4": "a", "1": "i", "!": "i", "0": "o", "3": "e", "$": "s", "5": "s", "7": "t" };

function normalize(text) {
  let out = "";
  for (const ch of text.toLowerCase()) out += CHAR_MAP[ch] ?? ch;
  return out;
}

function moderate(text) {
  const normalized = normalize(text);
  let masked = text;
  let clean  = true;

  for (const word of BLOCKED_WORDS) {
    let index = 0;
    while ((index = normalized.indexOf(word, index)) >= 0) {
      clean  = false;
      masked = masked.slice(0, index) + "*".repeat(word.length) + masked.slice(index + word.length);
      index += word.length;
    }
  }
  return { clean, masked };
}

/**
 * @param {string} text - The text to moderate. Must be non-empty and at most 200 characters.
 */
module.exports = async ({ params, context, logger }) => {
  const { text } = params;

  if (typeof text !== "string" || text.length === 0 || text.length > MAX_MESSAGE_LENGTH) {
    throw new Error("Invalid params: text is required and must be at most 200 characters");
  }

  const { clean, masked } = moderate(text);

  if (!clean) {
    logger.info(`ModerateMessage: blocked content from ${context.playerId}`);
  }
  return { allowed: clean, filteredText: masked };
};

```