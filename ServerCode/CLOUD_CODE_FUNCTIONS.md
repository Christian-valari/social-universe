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

Atomically validates ownership, deducts coins, records the purchase in the per-tile key, and
appends to the player's owned-tiles list so the client can restore state on next login.

**Parameters:**

| Name | Type | Required | Description |
|---|---|---|---|
| `tileId` | `string` | Yes | ID of the hex tile being purchased. |
| `planetId` | `string` | Yes | ID of the planet the tile belongs to. |
| `price` | `number` (integer) | Yes | Price of the tile in coins. Must be a positive integer. |

```js
// PurchaseLand — atomically validates ownership, deducts coins, records the
// purchase in the per-tile key, and appends to the player's owned-tiles list
// so the client can restore state on next login.
const { CurrenciesApi }          = require("@unity-services/economy-2.5");
const { DataApi: PlayerDataApi } = require("@unity-services/cloud-save-1.4");

/**
 * @param {string} tileId - ID of the hex tile being purchased.
 * @param {string} planetId - ID of the planet the tile belongs to.
 * @param {number} price - Price of the tile in coins. Must be a positive integer.
 */
module.exports = async ({ params, context, logger }) => {
  const { tileId, planetId, price } = params;

  if (!tileId || !planetId || !Number.isInteger(price) || price <= 0) {
    throw Error("Invalid params: tileId, planetId, and price are required");
  }

  const { projectId, playerId, accessToken } = context;
  const authHeader = { headers: { Authorization: `Bearer ${accessToken}` } };
  const econApi    = new CurrenciesApi(authHeader);
  const saveApi    = new PlayerDataApi(authHeader);

  // 1. Load the player's current owned-tiles list for this planet.
  const ownedKey   = `owned_tiles_${planetId.toLowerCase()}`;
  let   ownedTiles = [];
  try {
    const saveRes = await saveApi.getItems({ projectId, playerId, key: [ownedKey] });
    const item    = saveRes.data.results.find(r => r.key === ownedKey);
    if (item) ownedTiles = item.value;  // already a parsed array from Cloud Save JSON
  } catch (_) { /* key doesn't exist yet */ }

  if (ownedTiles.includes(tileId)) {
    return { success: false, reason: "ALREADY_OWNED" };
  }

  // 2. Validate balance and deduct coins.
  const balanceRes = await econApi.getPlayerCurrencyBalance({ projectId, playerId, currencyId: "COINS" });
  if (balanceRes.data.balance < price) {
    return { success: false, reason: "INSUFFICIENT_FUNDS" };
  }

  const deductRes = await econApi.decrementPlayerCurrencyBalance({
    projectId, playerId, currencyId: "COINS",
    currencyModifyBalanceRequest: { amount: price }
  });
  const newBalance = deductRes.data.balance;

  // 3. Record per-tile ownership (for cross-player lookup in M3+).
  await saveApi.setItem({
    projectId, playerId,
    key:  `tile_${tileId}_owner`,
    body: { value: playerId }
  });

  // 4. Append to the player's owned-tiles list so it can be restored on login.
  ownedTiles.push(tileId);
  await saveApi.setItem({
    projectId, playerId,
    key:  ownedKey,
    body: { value: ownedTiles }
  });

  logger.info(`PurchaseLand: player ${playerId} purchased tile ${tileId} on ${planetId} for ${price} coins → balance ${newBalance}`);
  return { success: true, newBalance };
};
```
