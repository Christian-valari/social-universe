# UGS Cloud Code Functions — Reference

Combined reference of all `ServerCode/` functions for review. Each function is deployed to
Unity Gaming Services Cloud Code as its own module (filename = function name). This document
is **not** a deployment artifact — see the individual `.js` files for that (deploy them with
`./ServerCode/deploy.ps1`, see `docs/deploy-runbook.md`). The code blocks below omit the
`module.exports.params` declarations each `.js` now ends with.

Modules used: `@unity-services/economy-2.5`, `@unity-services/cloud-save-1.4`.

**Cloud Save access classes (Known Issue #16).** Records the server trusts are in the
**Protected** class — the player can read them, only Cloud Code can write them — and are accessed
with `getProtectedItems` / `setProtectedItem` (same positional args as `getItems` / `setItem`):
`drone_fleet`, `mineral_inventory`, `current_planet`, `mining_claim_log`, `fuel_state`,
`travel_state` and `owned_tiles_<planet>`. The client reads them with
`ICloudSave.LoadProtectedAsync`. Player-written records (`player_profile`, `blocked_users`) stay in
the Default class. `ServerCodeAccessClassTests` fails if a protected key goes through
`getItems`/`setItem`. Some code blocks below predate this; the `.js` files are authoritative.

**Land registry writes (Known Issue #19).** `land_registry` is one Custom Data item per planet
holding every tile, so every writer (`PurchaseLand`, `SellLand`, `ClaimYield`, `RecordVisit`,
`ApplyUpkeep`, `PlaceBuild`, `MoveBuild`, `RemoveBuild`, `PurchaseHexatile`) reads and writes it
only through the shared `loadRegistry`/`updateRegistry` helper block: the write carries the
`writeLock` it read, a 409 re-runs the validation on a fresh read (up to 3 attempts, then
`CONFLICT`), and any registry change is written *before* coins move, with a rollback if the coin
call fails. `LandRegistryLockTests` keeps the copies identical and fails on any registry access
outside them.

**Single active session (Known Issue #20).** One account is in active use on one device at a
time; the newest sign-in wins. The Protected item `active_session` = `{ sessionId, claimedAt }`
holds the current device's session. `ClaimSession` writes it on every fresh sign-in and
`ReleaseSession` clears it on logout. Every other deployed script starts with
`await requireActiveSession(context, params.sessionId);` (a shared block, copied verbatim): a
missing or non-current `sessionId` throws `SESSION_REPLACED`, and a failed read throws. The client
(`BackendClient`) sends `sessionId` with every call and signs the device out on that error.
`ActiveSessionGuardTests` keeps the block identical in every deployed script and checks that it is
the handler's first `await`. **New scripts must include it**, and declare `sessionId: "String"`.
Design: `docs/superpowers/plans/2026-10-15-single-active-session.md`.

**Cross-device consistency (Known Issue #21).** Every read-modify-write of a player record works the
same way:
- **The write passes the `writeLock` it read** — the record's revision.
- **A 409 means another request wrote first** (another device, or a second call from the same one).
  The change is re-applied to a fresh read, up to 3 times, then the call returns `CONFLICT`.
- **A failed read throws** rather than reading as "no record". An empty profile, block list or
  fleet built from a read error would otherwise overwrite the real one, and a failed fuel read
  would refill the tank.

Where it applies:
- **`drone_fleet`:** written only through the shared `drone_fleet access` block in the four drone
  functions. Coins are charged first and refunded if the change doesn't land.
- **`owned_tiles_<planet>`:** written only through the `owned_tiles access` block in
  `PurchaseLand` / `SellLand`. `PlayerRecordLockTests` keeps both blocks identical.
- **Inline in `UpdateProfile`, `BlockUser` and `StartTravel`.** `StartTravel` also refuses while
  any trip is unlanded — in flight or arrived, from any device — and takes its origin from
  `current_planet`.
- **`GetCurrentPlanet`** also returns `travelTargetId`, so signing in on any device resumes an
  unlanded trip.

**Limit:** Cloud Save has no create-only write, so a record's very first write carries no lock.
Design: `docs/superpowers/plans/2026-10-15-cross-device-data-consistency.md`.

---

## ClaimSession

Claims or confirms the account's single active session. `AuthService` calls it on every sign-in,
before any other function, and `ActiveSessionController` calls it every 60 s in game.

- **No `sessionId`** (a fresh sign-in): writes `active_session` with a new id
  (`context.correlationId`) and returns that id as a string. This one unconditional write replaces
  any other device's session. With two simultaneous sign-ins the last write wins, so exactly one
  stays valid.
- **With `sessionId`** (the client's periodic check): read-only. It
  returns the same id if that session is still current, and throws `SESSION_REPLACED` otherwise.

**Parameters:** `sessionId` (`String`, optional). Code: `ClaimSession.js`.

---

## ReleaseSession

Explicit logout (`AuthService.SignOutAsync`). If `active_session` still holds the caller's
`sessionId`, it is overwritten with `{ sessionId: null, releasedAt }` under the record's
`writeLock`. If another device claimed in between, the write gets a 409 and the newer session
stays. A stale or missing id is a no-op. Returns nothing.

**Parameters:** `sessionId` (`String`). Code: `ReleaseSession.js`.

---

## StartIdleMining

Records the caller's idle mining run on the server, so any device the player signs in on can show
and claim it. `MiningController.BeginIdleMiningAsync` awaits it before the drone leaves. Design:
`docs/superpowers/plans/2026-10-15-server-tracked-idle-mining.md`.

**Parameters:** `planetId`, `slotId` (the asteroid slot, `"{MineralType}#{index}"`), `mineralId`
(`String`), `durationSec` (`Numeric`, the client's run length, in (0, 86400]), optional
`replaceSlotId` (`String`), `sessionId`.

**Record:** Protected `idle_mining_session`, one per player:
`{ planetId, slotId, mineralId, startTs, durationSec }` (server clock), or `{ slotId: null, endedTs }`
once ended. Written only by this function and `ValidateMining`, through a shared helper block whose
write carries the record's `writeLock` (409 → re-decide on a fresh read, 3 attempts).

**Response:** `{ success, reason?, planetId, slotId, mineralId, elapsedSeconds, durationSeconds }`
(`IdleMiningSessionResult`).

| Outcome | When |
|---|---|
| `success: true` | No run, or a run on another planet (which this one replaces) |
| `ALREADY_MINING` + that run's fields | A run is already in progress on this planet — the client takes it over |
| `success: true` (replace) | …unless `replaceSlotId` names that run: a client that can't place its asteroid replaces it instead of being locked out |
| `WRONG_PLANET` | `current_planet` is recorded and differs. With no record the start is accepted and the planet is **not** pinned |
| `INVALID_PARAMS` | a missing/oversized name or a bad duration |
| `CONFLICT` | 3 write conflicts in a row |

Readiness is not enforced: the claim budget in `ValidateMining` is the reward bound.

---

## GetIdleMining

The caller's run in progress, read at every Planet load (`PlanetSceneBootstrapper`). Pure read, so
`BackendRetryPolicy` retries it. **Parameters:** `sessionId`. **Response:** `{ success: true,
planetId, slotId, mineralId, elapsedSeconds, durationSeconds }`, or `{ success: true }` for none.
`elapsedSeconds` is on the server clock. A 404 means none; any other read failure throws.

---

## GetAsteroidRespawns

Which of a planet's asteroid slots the caller emptied in the last `RESPAWN_HOURS` (4), on any
device, read at every Planet load so every device shows the same field
(`docs/superpowers/plans/2026-10-18-cloud-synced-planet-data.md`). Pure read of the Protected
`mining_claim_log` that `ValidateMining` writes (one `{ ts, slotId, mineralId }` entry per granted
claim; `ValidateMining` takes the slot from its optional `slotId`, else `idleSlotId`). **Parameters:**
`planetId`, `sessionId`. **Response:** `{ success: true, respawns: [{ slotId, mineralId, secondsLeft
}] }` — `secondsLeft` is an integer on the server clock. Entries with no slot (malformed, or bare
timestamps logged before slots were recorded) still count toward the claim budget but are left out.
A 404 means none; any other read failure throws.

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

  const economyApi   = new CurrenciesApi({ accessToken });
  const cloudSaveApi = new DataApi(context);

  // Fetch balances + saved profile in parallel.
  const [balancesRes, profileRes] = await Promise.all([
    economyApi.getPlayerCurrencies({ projectId, playerId }),
    cloudSaveApi.getItems(projectId, playerId, ["player_profile"]).catch(() => ({ data: { results: [] } }))
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
  const api = new CurrenciesApi({ accessToken });

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
  const api = new CurrenciesApi({ accessToken });

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
  const api = new CurrenciesApi({ accessToken });

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

Grants **minerals** (not coins) for an idle or active mining claim (M6), server-authoritatively.
**`ServerCode/ValidateMining.js` is the source of truth to deploy** — this section only summarizes
its contract. Design: `docs/superpowers/specs/2026-09-29-mining-claim-budget-design.md`.

**Parameters:**

| Name | Type | Required | Description |
|---|---|---|---|
| `planetId` | `string` | Yes | Planet the claim was mined on (`PlanetDefinition._planetId`). |
| `mineralId` | `string` | Yes | Mineral mined (`MineralDefinition._mineralId`). |
| `claimedQty` | `number` (integer) | Yes | Units the client computed. Positive integer; clamped server-side. |
| `slotId` | `string` | No | Asteroid slot the claim emptied (`"Iron#1"`). Stored in the claim's `mining_claim_log` entry (`{ ts, slotId, mineralId }`) so `GetAsteroidRespawns` can report it to every device; `idleSlotId` is used when absent, and a malformed slot is stored as `null`. |
| `forfeit` | `boolean` | No | `true` for a failed active-mining minigame: `planetId`, `mineralId` and a valid `slotId` are checked (planet, mineral on planet — not the drone tier), one budget entry is logged for the slot, nothing is granted and the planet is never pinned. Answers `{ granted: 0, mineralId, reason: "FORFEITED" }`; no usable slot → `INVALID_PARAMS`. `claimedQty` is not needed. |
| `idleSlotId` | `string` | No | Slot of the server-tracked idle run this claim collects (see `StartIdleMining`). Once the claim has an answer (grant or rejection) the run is ended if it still names this planet and slot; a claim that throws leaves it. Ending is best effort (logged, never fails the claim). |

`sessionDurationSec`, `unitsPerSec` and `claimedCoins` are no longer read.

**Response:** `{ granted: int, mineralId: string, reason?: string }`. A rejection returns
`granted: 0` plus a `reason` (and logs one `logger.warn` line) instead of throwing:

| Reason | When |
|---|---|
| `INVALID_PARAMS` | `planetId` / `mineralId` missing, or `claimedQty` not a positive integer |
| `WRONG_PLANET` | `planetId` differs from the player's `current_planet` record. A player with no record has never landed via `LandTravel`, so the first valid claim pins `current_planet` to its planet |
| `MINERAL_NOT_ON_PLANET` | the mineral is not one of that planet's asteroid minerals (or the planet is unknown) |
| `TIER_TOO_LOW` | the mineral's asteroid tier exceeds the saved fleet's active drone tier |
| `BUDGET_EXHAUSTED` | the player already has 6 claims on this planet in the last 4 hours |

An accepted claim grants `min(claimedQty, ceil(ceil(baseYield × 1.2) × effectiveYieldMult))` to the
`mineral_inventory` Cloud Save record (read-modify-write under its `writeLock`).

**`mining_claim_log` record** (player Cloud Save): `{ [planetId]: number[] }` — claim timestamps in
ms, pruned to the last 4 hours on each write. A slot is reserved (under the record's `writeLock`,
409 → retry up to 3 attempts) before minerals are granted, and released if the grant fails.
Like `current_planet`, `drone_fleet` and `mineral_inventory`, it is in the Protected access class, so
the client cannot reset it (Known Issue #16).

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
  const econApi    = new CurrenciesApi({ accessToken });
  const saveApi    = new DataApi(context);

  // Load the server-recorded session end time.
  let sessionEndMs = Date.now(); // fallback: assume just now
  try {
    const res    = await saveApi.getItems(projectId, playerId, ["last_session_end"]);
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
  await saveApi.setItem(projectId, playerId, { key: "last_session_end", value: String(Date.now()) });

  logger.info(`GrantOfflineIncome: player ${playerId} claimed ${claimedAmount}, granted ${grantAmount} → ${res.data.balance}`);
  return { granted: grantAmount, newBalance: res.data.balance };
};
```

---

## PurchaseLand

Prices the tile **server-side** from `LAND_PRICES` (Known Issue #19), refuses any tile the planet's
land registry already assigns (`OWNED_BY_OTHER`, or `ALREADY_OWNED` when it names the caller),
claims the registry entry under its `writeLock` (a concurrent write re-reads; a concurrent buyer
of the same tile makes this one `OWNED_BY_OTHER`), deducts coins (releasing the claim if that fails), records the purchase in the per-tile key and appends to the
player's owned-tiles list so the client can restore state on next login.

`LAND_PRICES` is keyed by `PlanetDefinition.name` lowercased (the `planetId` every land function
receives) and MUST MATCH `round(EconomyConfig.BaseLandPrice × PlanetDefinition.LandPriceMultiplier)`
— `LandPriceAlignmentTests` checks every planet asset. Unknown planets return `UNKNOWN_PLANET`.

The registry entry is the schema v2 shape:
`{ ownerId, buildLevel, lastYieldClaimTs, lastUpkeepTs, visitCount }`. See `GetLandRegistry`,
`PlaceBuild`, `ClaimYield`, `RecordVisit`, `ApplyUpkeep`, and `SellLand` for the other
readers/writers of this entry.

**Parameters:**

| Name | Type | Required | Description |
|---|---|---|---|
| `tileId` | `string` | Yes | ID of the hex tile being purchased. |
| `planetId` | `string` | Yes | `PlanetDefinition.name` of the planet the tile belongs to. |
| `price` | `number` (integer) | No | The client's displayed price. Never charged — a mismatch is only logged. |

**Returns:** `{ success: true, newBalance }` or `{ success: false, reason }` — `PurchaseLandResponse`
in `LandPurchaseService` has exactly `Success`, `Reason`, `NewBalance` (Known Issue #18).

```js
// PurchaseLand — prices the tile server-side (LAND_PRICES), refuses a tile the planet's land
// registry already assigns to anyone, claims the registry entry under its writeLock, deducts
// coins, records per-tile ownership and appends to the player's owned-tiles list so the client
// can restore state on login. The registry is what other players see as the owner.
// The registry entry is { ownerId, buildLevel, lastYieldClaimTs, lastUpkeepTs, visitCount } —
// see GetLandRegistry, PlaceBuild, ClaimYield, RecordVisit, ApplyUpkeep, SellLand for readers/writers.
//
// Known Issue #19: the price used to come from the client (`price`, any positive integer), so a
// modified client bought land for 1 coin and SellLand refunded it at full rate. The client may
// still send `price`; it is only compared against LAND_PRICES and logged when they differ.
// The registry is claimed under its writeLock before coins move, so two buyers racing for one
// tile can't both pay: the second write gets a 409, re-reads and answers OWNED_BY_OTHER. If the
// deduction then fails the claim is released. Every registry writer uses the same locked
// read-modify-write (updateRegistry below).
//
// FIX (Known Issue #6): constructors don't accept { headers: { Authorization: ... } }.
// Economy: { accessToken } authenticates as the calling player. Cloud Save:
// DataApi(context) uses the service token (required for both player-scoped
// writes and custom/game data writes). getItems/setItem take positional args
// (projectId, playerId, ...), not an options object. getPlayerCurrencyBalance
// does not exist on CurrenciesApi — the read method is getPlayerCurrencies.
// decrementPlayerCurrencyBalance requires a configAssignmentHash fetched via
// ConfigurationApi before the write.
const { CurrenciesApi, ConfigurationApi } = require("@unity-services/economy-2.5");
const { DataApi: PlayerDataApi }          = require("@unity-services/cloud-save-1.4");

const CURRENCY_ID  = "COINS";
const REGISTRY_KEY = "land_registry";

// MUST MATCH round(EconomyConfig._baseLandPrice × PlanetDefinition._landPriceMultiplier) for every
// PlanetDefinition (LandPriceAlignmentTests). Keyed by the planet asset's name lowercased — the
// client sends PlanetDefinition.name as `planetId` to every land function, and the registry's
// customId is that same lowercased name. SellLand.js keeps an identical copy.
const LAND_PRICES = {
  planet_earth:   150,
  planet_jupiter: 200,
  planet_mars:    100,
  planet_mercury: 80,
  planet_moon:    120,
  planet_neptune: 300,
  planet_pluto:   500,
  planet_saturn:  180,
  planet_uranus:  250,
  planet_venus:   100,
};

/**
 * @param {string} tileId - ID of the hex tile being purchased.
 * @param {string} planetId - PlanetDefinition.name of the planet the tile belongs to.
 * @param {number} [price] - The client's displayed price. Informational only: never charged.
 */
module.exports = async ({ params, context, logger }) => {
  const { tileId, planetId, price: clientPrice } = params;

  if (!tileId || !planetId) {
    throw new Error("Invalid params: tileId and planetId are required");
  }

  const customId = planetId.toLowerCase();
  const price    = LAND_PRICES[customId];
  if (price === undefined) {
    logger.warn(`PurchaseLand: no land price for planet ${planetId}`);
    return { success: false, reason: "UNKNOWN_PLANET" };
  }
  if (clientPrice !== undefined && clientPrice !== price) {
    logger.warn(`PurchaseLand: client price ${clientPrice} for ${planetId} differs from server price ${price}`);
  }

  const { projectId, playerId, accessToken } = context;
  const econApi       = new CurrenciesApi({ accessToken });
  const config        = new ConfigurationApi({ accessToken });
  const saveApi       = new PlayerDataApi(context);
  const customDataApi = new PlayerDataApi(context); // same instance is fine; kept separate for clarity

  try {
    const ownedKey = `owned_tiles_${customId}`;

    // 1. Load the player's current owned-tiles list for this planet.
    let ownedTiles = [];
    try {
      const saveRes = await saveApi.getProtectedItems(projectId, playerId, [ownedKey]);
      const item    = saveRes.data.results.find(r => r.key === ownedKey);
      if (item && Array.isArray(item.value)) ownedTiles = item.value; // already parsed by Cloud Save
    } catch (_) { /* key doesn't exist yet */ }

    if (ownedTiles.includes(tileId)) {
      return { success: false, reason: "ALREADY_OWNED" };
    }

    // 2. Claim the tile in the planet's registry (Custom Data, shared across all players) so other
    //    clients render it as "owned by other". The registry is the authority on who owns a tile:
    //    any entry means it is taken — including one naming this player (e.g. an owned-tiles list
    //    reset by the access-class migration), since buying it again would charge twice and wipe
    //    its buildLevel. Balance is checked against the same read, before anything is written.
    const now  = Date.now();
    const step = await updateRegistry(customDataApi, projectId, customId, async registry => {
      const existing = registry[tileId];
      if (existing) {
        return { write: false, reason: existing.ownerId === playerId ? "ALREADY_OWNED" : "OWNED_BY_OTHER" };
      }

      const balancesRes = await econApi.getPlayerCurrencies({ projectId, playerId });
      const coins       = balancesRes.data.results.find(c => c.currencyId === CURRENCY_ID);
      if ((coins ? coins.balance : 0) < price) {
        return { write: false, reason: "INSUFFICIENT_FUNDS" };
      }

      registry[tileId] = {
        ownerId: playerId,
        buildLevel: 0,
        lastYieldClaimTs: now,
        lastUpkeepTs: now,
        visitCount: 0
      };
      return { write: true };
    });
    if (step.conflict) return { success: false, reason: "CONFLICT" };
    if (!step.write)   return { success: false, reason: step.reason };

    // 3. Deduct coins. configAssignmentHash is required by decrementPlayerCurrencyBalance.
    //    If it fails (e.g. the balance dropped since step 2), release the claim.
    let newBalance;
    try {
      const cfg = await config.getPlayerConfiguration({ projectId, playerId });
      const configAssignmentHash = cfg.data.metadata.configAssignmentHash;

      const deductRes = await econApi.decrementPlayerCurrencyBalance({
        projectId,
        playerId,
        currencyId: CURRENCY_ID,
        configAssignmentHash,
        currencyModifyBalanceRequest: { currencyId: CURRENCY_ID, amount: price }
      });
      newBalance = deductRes.data.balance;
    } catch (err) {
      logger.error(`PurchaseLand: charge failed for ${playerId}; releasing tile ${tileId} on ${planetId}. ${err?.message}`);
      await releaseClaim(customDataApi, projectId, customId, tileId, playerId, logger);
      throw err;
    }

    // 4. Record per-tile ownership (for cross-player lookup in M3+).
    await saveApi.setItem(projectId, playerId, { key: `tile_${tileId}_owner`, value: playerId });

    // 5. Append to the player's owned-tiles list so it can be restored on login. Protected class:
    //    the client reads it but can't write it (GetPlayerProfile counts it — Known Issue #16).
    ownedTiles.push(tileId);
    await saveApi.setProtectedItem(projectId, playerId, { key: ownedKey, value: ownedTiles });

    logger.info(`PurchaseLand: player ${playerId} purchased tile ${tileId} on ${planetId} for ${price} → ${newBalance}`);
    return { success: true, newBalance };
  } catch (err) {
    logger.error("PurchaseLand failed", { "error.message": err.message });
    throw err;
  }
};

// Removes this player's claim on tileId (re-read under the lock: other tiles may have changed).
async function releaseClaim(api, projectId, customId, tileId, playerId, logger) {
  try {
    const step = await updateRegistry(api, projectId, customId, registry => {
      if (registry[tileId]?.ownerId !== playerId) return { write: false };
      delete registry[tileId];
      return { write: true };
    });
    if (step.conflict) throw new Error(`${MAX_ATTEMPTS} write conflicts`);
  } catch (err) {
    logger.error(`PurchaseLand: RELEASE FAILED for tile ${tileId} on ${customId} (player ${playerId}) — manual cleanup needed: ${err?.message}`);
  }
}

// ---- land_registry access (Known Issue #19) — identical in every script that writes the registry,
// pinned by LandRegistryLockTests. The registry is one Custom Data item holding every tile on the
// planet, so each write is a read-modify-write of the whole map: it must carry the writeLock it
// read, or it silently undoes a concurrent write (e.g. puts back a tile SellLand just sold).
const MAX_ATTEMPTS = 3;

// Reads the registry and its writeLock. Only a missing registry reads as {}: any other failure
// throws, so a transient read error can never turn into a write that wipes every tile.
async function loadRegistry(api, projectId, customId) {
  try {
    const res  = await api.getCustomItems(projectId, customId, [REGISTRY_KEY]);
    const item = res.data.results.find(r => r.key === REGISTRY_KEY);
    return item?.value ? { registry: item.value, writeLock: item.writeLock } : { registry: {}, writeLock: undefined };
  } catch (err) {
    if (statusOf(err) === 404) return { registry: {}, writeLock: undefined };
    throw err;
  }
}

// Read-modify-write under the writeLock. `mutate(registry)` validates against a fresh read and
// either edits the registry and returns { write: true, ... } or returns { write: false, ... } to stop.
// A 409 (someone wrote since the read) re-runs mutate on a fresh read, up to MAX_ATTEMPTS; after
// that the result is { write: false, conflict: true }. Returns whatever mutate returned.
async function updateRegistry(api, projectId, customId, mutate) {
  for (let attempt = 1; attempt <= MAX_ATTEMPTS; attempt++) {
    const { registry, writeLock } = await loadRegistry(api, projectId, customId);
    const step = await mutate(registry);
    if (!step.write) return step;
    try {
      await api.setCustomItem(projectId, customId,
        writeLock ? { key: REGISTRY_KEY, value: registry, writeLock } : { key: REGISTRY_KEY, value: registry });
      return step;
    } catch (err) {
      if (statusOf(err) !== 409) throw err;
    }
  }
  return { write: false, conflict: true };
}

function statusOf(err) {
  return err?.response?.status ?? err?.status;
}
// ---- end land_registry access
```

---

## GetLandRegistry

Returns the global tile-ownership map for a planet, so a client can render other players' tiles
as "owned by other". Reads from the same Cloud Save Custom Data item that `PurchaseLand` writes
to (`customId = planetId.toLowerCase()`, `key = "land_registry"`). Returns an empty map if the
registry doesn't exist yet (e.g. no tiles purchased on this planet so far — a 404 or no item). Any
other read failure throws (2026-10-18): answering `{}` would show every tile as unowned, and the
client retries a failed Planet load instead.

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

Validates tile ownership, writes the item into its slot and updates the tile's `buildLevel` in
the planet's global land registry (Custom Data) under its `writeLock`, then deducts the item's
coin cost (clearing the slot again if that fails), so `TileExtrusionView` reflects the new build
level for everyone. **Still trusts the client's `cost`** — see Known Issue #19.

**Parameters:**

| Name | Type | Required | Description |
|---|---|---|---|
| `tileId` | `string` | Yes | ID of the hex tile being built on. |
| `planetId` | `string` | Yes | ID of the planet the tile belongs to. |
| `itemId` | `string` | Yes | ID of the `ItemDefinition` being placed. |
| `cost` | `number` (integer) | Yes | Coin cost of the item. Must be a positive integer. |

```js
// PlaceBuild — validates tile ownership, that the target slot is empty, and the
// player's balance, then writes itemId into slots[hexIndex] in the planet's shared
// land registry, sets buildLevel to the number of filled slots, and deducts the
// item's coin cost.
// Known Issue #19: the slot is written under the registry's writeLock *before* coins are deducted,
// so two concurrent placements into one slot can't both charge (the second re-reads and gets
// SLOT_OCCUPIED). If the deduction fails, the slot is cleared again.
// NOTE (same as PurchaseLand): CurrenciesApi has no getPlayerCurrencyBalance — read via
// getPlayerCurrencies and find the currency. decrementPlayerCurrencyBalance requires a
// configAssignmentHash fetched from ConfigurationApi and a request body carrying currencyId.
const { CurrenciesApi, ConfigurationApi } = require("@unity-services/economy-2.5");
const { DataApi }                         = require("@unity-services/cloud-save-1.4");

const CURRENCY_ID  = "COINS";
const REGISTRY_KEY = "land_registry";
const HEX_COUNT    = 19; // must match HexBoardMath.HexCount(2) / EconomyConfig.HexCount
const FREE_COUNT   = 5;  // free hexatiles default unlocked

function filledCount(slots) {
  if (!Array.isArray(slots)) return 0;
  return slots.filter(s => s !== null && s !== undefined && s !== "").length;
}

/**
 * @param {string} tileId
 * @param {string} planetId
 * @param {number} hexIndex - target hexatile, integer in [0, HEX_COUNT).
 * @param {string} itemId - ItemDefinition id being placed.
 * @param {number} cost - coin cost, positive integer.
 */
module.exports = async ({ params, context, logger }) => {
  const { tileId, planetId, hexIndex, itemId, cost } = params;

  if (!tileId || !planetId || !itemId ||
      !Number.isInteger(hexIndex) || hexIndex < 0 || hexIndex >= HEX_COUNT ||
      !Number.isInteger(cost) || cost <= 0) {
    throw new Error("Invalid params: tileId, planetId, hexIndex, itemId, cost required");
  }

  const { projectId, playerId, accessToken } = context;
  const econApi       = new CurrenciesApi({ accessToken });
  const config        = new ConfigurationApi({ accessToken });
  const customDataApi = new DataApi(context);
  const customId      = planetId.toLowerCase();

  try {
    // 1. Validate and fill the slot — one locked write.
    const step = await updateRegistry(customDataApi, projectId, customId, async registry => {
      const entry = registry[tileId];
      if (!entry || entry.ownerId !== playerId) {
        return { write: false, reason: "NOT_OWNER" };
      }

      // Normalize unlocked mask (free indices default true) and require the hex unlocked.
      let unlocked = Array.isArray(entry.unlocked) ? entry.unlocked.slice(0, HEX_COUNT) : [];
      while (unlocked.length < HEX_COUNT) unlocked.push(false);
      if (!Array.isArray(entry.unlocked)) for (let i = 0; i < FREE_COUNT; i++) unlocked[i] = true;
      if (!unlocked[hexIndex]) {
        return { write: false, reason: "TILE_LOCKED" };
      }

      const slots = Array.isArray(entry.slots) ? entry.slots : new Array(HEX_COUNT).fill(null);
      if (slots[hexIndex]) {
        return { write: false, reason: "SLOT_OCCUPIED" };
      }

      const balancesRes = await econApi.getPlayerCurrencies({ projectId, playerId });
      const coins       = balancesRes.data.results.find(c => c.currencyId === CURRENCY_ID);
      if ((coins ? coins.balance : 0) < cost) {
        return { write: false, reason: "INSUFFICIENT_FUNDS" };
      }

      entry.unlocked = unlocked;
      entry.slots    = slots;
      entry.slots[hexIndex] = itemId;
      entry.buildLevel = filledCount(entry.slots);
      return { write: true, buildLevel: entry.buildLevel };
    });

    if (step.conflict) return { success: false, reason: "CONFLICT" };
    if (!step.write)   return { success: false, reason: step.reason };

    // 2. Deduct. decrementPlayerCurrencyBalance requires a configAssignmentHash fetched via
    //    ConfigurationApi. If it fails (e.g. the balance dropped since step 1), clear the slot.
    let newBalance;
    try {
      const cfg = await config.getPlayerConfiguration({ projectId, playerId });
      const configAssignmentHash = cfg.data.metadata.configAssignmentHash;

      const deductRes = await econApi.decrementPlayerCurrencyBalance({
        projectId, playerId, currencyId: CURRENCY_ID, configAssignmentHash,
        currencyModifyBalanceRequest: { currencyId: CURRENCY_ID, amount: cost }
      });
      newBalance = deductRes.data.balance;
    } catch (err) {
      logger.error(`PlaceBuild: charge failed for ${playerId}; clearing hex ${hexIndex} of ${tileId} (${planetId}). ${err?.message}`);
      await clearSlot(customDataApi, projectId, customId, tileId, playerId, hexIndex, itemId, logger);
      throw err;
    }

    logger.info(`PlaceBuild: ${playerId} placed ${itemId} in hex ${hexIndex} of ${tileId} (${planetId}) for ${cost} -> ${newBalance}, buildLevel ${step.buildLevel}`);
    return { success: true, newBalance, buildLevel: step.buildLevel };
  } catch (err) {
    logger.error("PlaceBuild failed", { "error.message": err.message });
    throw err;
  }
};

// Undoes step 1 if the slot still holds this item on this player's tile.
async function clearSlot(api, projectId, customId, tileId, playerId, hexIndex, itemId, logger) {
  try {
    const step = await updateRegistry(api, projectId, customId, registry => {
      const entry = registry[tileId];
      if (!entry || entry.ownerId !== playerId || !Array.isArray(entry.slots) || entry.slots[hexIndex] !== itemId) return { write: false };
      entry.slots[hexIndex] = null;
      entry.buildLevel = filledCount(entry.slots);
      return { write: true };
    });
    if (step.conflict) throw new Error(`${MAX_ATTEMPTS} write conflicts`);
  } catch (err) {
    logger.error(`PlaceBuild: CLEAR FAILED for hex ${hexIndex} of ${tileId} on ${customId} (player ${playerId}) — item placed unpaid: ${err?.message}`);
  }
}

// ---- land_registry access (Known Issue #19) — identical in every script that writes the registry,
// pinned by LandRegistryLockTests. The registry is one Custom Data item holding every tile on the
// planet, so each write is a read-modify-write of the whole map: it must carry the writeLock it
// read, or it silently undoes a concurrent write (e.g. puts back a tile SellLand just sold).
const MAX_ATTEMPTS = 3;

// Reads the registry and its writeLock. Only a missing registry reads as {}: any other failure
// throws, so a transient read error can never turn into a write that wipes every tile.
async function loadRegistry(api, projectId, customId) {
  try {
    const res  = await api.getCustomItems(projectId, customId, [REGISTRY_KEY]);
    const item = res.data.results.find(r => r.key === REGISTRY_KEY);
    return item?.value ? { registry: item.value, writeLock: item.writeLock } : { registry: {}, writeLock: undefined };
  } catch (err) {
    if (statusOf(err) === 404) return { registry: {}, writeLock: undefined };
    throw err;
  }
}

// Read-modify-write under the writeLock. `mutate(registry)` validates against a fresh read and
// either edits the registry and returns { write: true, ... } or returns { write: false, ... } to stop.
// A 409 (someone wrote since the read) re-runs mutate on a fresh read, up to MAX_ATTEMPTS; after
// that the result is { write: false, conflict: true }. Returns whatever mutate returned.
async function updateRegistry(api, projectId, customId, mutate) {
  for (let attempt = 1; attempt <= MAX_ATTEMPTS; attempt++) {
    const { registry, writeLock } = await loadRegistry(api, projectId, customId);
    const step = await mutate(registry);
    if (!step.write) return step;
    try {
      await api.setCustomItem(projectId, customId,
        writeLock ? { key: REGISTRY_KEY, value: registry, writeLock } : { key: REGISTRY_KEY, value: registry });
      return step;
    } catch (err) {
      if (statusOf(err) !== 409) throw err;
    }
  }
  return { write: false, conflict: true };
}

function statusOf(err) {
  return err?.response?.status ?? err?.status;
}
// ---- end land_registry access
```

---

## ClaimYield

Computes accrued visitor-driven land income for an owned tile, resets the tile's yield-accrual
state (`lastYieldClaimTs`, `visitCount`) in the planet's global land registry (Custom Data) under
its `writeLock`, then grants the income — restoring the accrual state if the grant fails. Two
concurrent claims can't both pay, and a claim racing `SellLand` re-reads and gets `NOT_OWNER`.

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
// Known Issue #19: the accrual reset is written under the registry's writeLock *before* coins are
// granted, so two concurrent claims can't both pay, and this write can no longer put back a tile
// SellLand removed in between (the retry re-reads and answers NOT_OWNER). If the grant fails the
// accrual state is restored.
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
  const econApi       = new CurrenciesApi({ accessToken });
  const customDataApi = new DataApi(context);
  const customId      = planetId.toLowerCase();

  try {
    // 1. Validate ownership, compute accrued yield and reset the accrual state — one locked write.
    const now = Date.now();
    const step = await updateRegistry(customDataApi, projectId, customId, registry => {
      const entry = registry[tileId];
      if (!entry || entry.ownerId !== playerId) {
        return { write: false, reason: "NOT_OWNER" };
      }

      const elapsedHours = Math.min((now - (entry.lastYieldClaimTs ?? now)) / MS_PER_HOUR, MAX_YIELD_ACCRUAL_HOURS);
      const buildBonus   = (entry.buildLevel ?? 0) * BUILD_LEVEL_YIELD_MULTIPLIER;
      const visitBonus   = Math.min(entry.visitCount ?? 0, MAX_VISIT_COUNT) * VISIT_YIELD_BONUS;
      const granted      = Math.floor(BASE_YIELD_PER_TILE_PER_HOUR * (1 + buildBonus + visitBonus) * elapsedHours);
      const previous     = { lastYieldClaimTs: entry.lastYieldClaimTs, visitCount: entry.visitCount ?? 0 };

      entry.lastYieldClaimTs = now;
      entry.visitCount       = 0;
      return { write: true, granted, previous };
    });

    if (step.conflict) return { success: false, reason: "CONFLICT" };
    if (!step.write)   return { success: false, reason: step.reason };
    const { granted, previous } = step;

    // 2. Grant. If it fails, restore the accrual state this claim reset.
    let newBalance;
    try {
      if (granted > 0) {
        const grantRes = await econApi.incrementPlayerCurrencyBalance({
          projectId, playerId, currencyId: CURRENCY_ID,
          currencyModifyBalanceRequest: { amount: granted }
        });
        newBalance = grantRes.data.balance;
      } else {
        newBalance = await currentBalance(econApi, projectId, playerId);
      }
    } catch (err) {
      logger.error(`ClaimYield: grant failed for ${playerId}; restoring accrual on ${tileId} (${planetId}). ${err?.message}`);
      await restoreAccrual(customDataApi, projectId, customId, tileId, playerId, now, previous, logger);
      throw err;
    }

    logger.info(`ClaimYield: player ${playerId} claimed tile ${tileId} (${planetId}) — granted ${granted} → ${newBalance}`);
    return { success: true, granted, newBalance };
  } catch (err) {
    logger.error("ClaimYield failed", { "error.message": err.message });
    throw err;
  }
};

// CurrenciesApi has no getPlayerCurrencyBalance (Known Issue #12) — read all balances and pick one.
async function currentBalance(econApi, projectId, playerId) {
  const res = await econApi.getPlayerCurrencies({ projectId, playerId });
  const c   = res.data.results.find(x => x.currencyId === "COINS");
  return c ? c.balance : 0;
}

// Puts back lastYieldClaimTs and the visits this claim consumed, unless the tile has changed
// hands or been claimed again since. Visits recorded meanwhile are kept.
async function restoreAccrual(api, projectId, customId, tileId, playerId, claimTs, previous, logger) {
  try {
    const step = await updateRegistry(api, projectId, customId, registry => {
      const entry = registry[tileId];
      if (!entry || entry.ownerId !== playerId || entry.lastYieldClaimTs !== claimTs) return { write: false };
      entry.lastYieldClaimTs = previous.lastYieldClaimTs;
      entry.visitCount       = Math.min((entry.visitCount ?? 0) + previous.visitCount, MAX_VISIT_COUNT);
      return { write: true };
    });
    if (step.conflict) throw new Error(`${MAX_ATTEMPTS} write conflicts`);
  } catch (err) {
    logger.error(`ClaimYield: RESTORE FAILED for tile ${tileId} on ${customId} (player ${playerId}) — accrual lost: ${err?.message}`);
  }
}

// ---- land_registry access (Known Issue #19) — identical in every script that writes the registry,
// pinned by LandRegistryLockTests. The registry is one Custom Data item holding every tile on the
// planet, so each write is a read-modify-write of the whole map: it must carry the writeLock it
// read, or it silently undoes a concurrent write (e.g. puts back a tile SellLand just sold).
const MAX_ATTEMPTS = 3;

// Reads the registry and its writeLock. Only a missing registry reads as {}: any other failure
// throws, so a transient read error can never turn into a write that wipes every tile.
async function loadRegistry(api, projectId, customId) {
  try {
    const res  = await api.getCustomItems(projectId, customId, [REGISTRY_KEY]);
    const item = res.data.results.find(r => r.key === REGISTRY_KEY);
    return item?.value ? { registry: item.value, writeLock: item.writeLock } : { registry: {}, writeLock: undefined };
  } catch (err) {
    if (statusOf(err) === 404) return { registry: {}, writeLock: undefined };
    throw err;
  }
}

// Read-modify-write under the writeLock. `mutate(registry)` validates against a fresh read and
// either edits the registry and returns { write: true, ... } or returns { write: false, ... } to stop.
// A 409 (someone wrote since the read) re-runs mutate on a fresh read, up to MAX_ATTEMPTS; after
// that the result is { write: false, conflict: true }. Returns whatever mutate returned.
async function updateRegistry(api, projectId, customId, mutate) {
  for (let attempt = 1; attempt <= MAX_ATTEMPTS; attempt++) {
    const { registry, writeLock } = await loadRegistry(api, projectId, customId);
    const step = await mutate(registry);
    if (!step.write) return step;
    try {
      await api.setCustomItem(projectId, customId,
        writeLock ? { key: REGISTRY_KEY, value: registry, writeLock } : { key: REGISTRY_KEY, value: registry });
      return step;
    } catch (err) {
      if (statusOf(err) !== 409) throw err;
    }
  }
  return { write: false, conflict: true };
}

function statusOf(err) {
  return err?.response?.status ?? err?.status;
}
// ---- end land_registry access
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
// mutation. The registry write carries its writeLock and retries on a conflict (Known Issue #19).
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
    const step = await updateRegistry(customDataApi, projectId, customId, registry => {
      const entry = registry[tileId];
      if (!entry || entry.ownerId === playerId) return { write: false, visitCount: entry?.visitCount ?? 0 };
      // Already capped: report the visit without a write (every write bumps the shared lock).
      if ((entry.visitCount ?? 0) >= MAX_VISIT_COUNT) return { write: false, recorded: true, visitCount: MAX_VISIT_COUNT };

      entry.visitCount = (entry.visitCount ?? 0) + 1;
      return { write: true, visitCount: entry.visitCount };
    });
    if (step.conflict) logger.warn(`RecordVisit: gave up on tile ${tileId} (${planetId}) after ${MAX_ATTEMPTS} write conflicts`);

    // RecordVisitResult has only Success and VisitCount (Known Issue #18) — no reason field.
    return { success: Boolean(step.write || step.recorded), visitCount: step.visitCount ?? 0 };
  } catch (err) {
    logger.error("RecordVisit failed", { "error.message": err.message });
    throw err;
  }
};

// ---- land_registry access (Known Issue #19) — identical in every script that writes the registry,
// pinned by LandRegistryLockTests. The registry is one Custom Data item holding every tile on the
// planet, so each write is a read-modify-write of the whole map: it must carry the writeLock it
// read, or it silently undoes a concurrent write (e.g. puts back a tile SellLand just sold).
const MAX_ATTEMPTS = 3;

// Reads the registry and its writeLock. Only a missing registry reads as {}: any other failure
// throws, so a transient read error can never turn into a write that wipes every tile.
async function loadRegistry(api, projectId, customId) {
  try {
    const res  = await api.getCustomItems(projectId, customId, [REGISTRY_KEY]);
    const item = res.data.results.find(r => r.key === REGISTRY_KEY);
    return item?.value ? { registry: item.value, writeLock: item.writeLock } : { registry: {}, writeLock: undefined };
  } catch (err) {
    if (statusOf(err) === 404) return { registry: {}, writeLock: undefined };
    throw err;
  }
}

// Read-modify-write under the writeLock. `mutate(registry)` validates against a fresh read and
// either edits the registry and returns { write: true, ... } or returns { write: false, ... } to stop.
// A 409 (someone wrote since the read) re-runs mutate on a fresh read, up to MAX_ATTEMPTS; after
// that the result is { write: false, conflict: true }. Returns whatever mutate returned.
async function updateRegistry(api, projectId, customId, mutate) {
  for (let attempt = 1; attempt <= MAX_ATTEMPTS; attempt++) {
    const { registry, writeLock } = await loadRegistry(api, projectId, customId);
    const step = await mutate(registry);
    if (!step.write) return step;
    try {
      await api.setCustomItem(projectId, customId,
        writeLock ? { key: REGISTRY_KEY, value: registry, writeLock } : { key: REGISTRY_KEY, value: registry });
      return step;
    } catch (err) {
      if (statusOf(err) !== 409) throw err;
    }
  }
  return { write: false, conflict: true };
}

function statusOf(err) {
  return err?.response?.status ?? err?.status;
}
// ---- end land_registry access
```

---

## ApplyUpkeep

Charges recurring per-tile upkeep for all of the caller's tiles on a planet. For each owned
tile where at least one full day has elapsed since `lastUpkeepTs`: if the player can afford
`UPKEEP_PER_TILE_PER_DAY * daysElapsed`, the cost is deducted and `lastUpkeepTs` advances by
that many days (`chargedTiles`); otherwise the tile's registry entry is deleted and it reverts
to `Available` for everyone (`revertedTiles`). The registry changes are written under its
`writeLock` first and the total is deducted in one call (advances are undone if it fails), so two
concurrent calls can't charge the same days twice. After 3 write conflicts it charges nothing and
returns empty lists; the client's poll retries.

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
// Known Issue #19: the registry changes are written under its writeLock *before* the coins are
// deducted (as one charge), so two concurrent calls can't both charge the same days — the second
// re-reads and finds nothing due. If the charge fails, the advanced lastUpkeepTs values are put
// back (reverted tiles stay reverted: the player could not afford them at the time).
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
  const econApi       = new CurrenciesApi({ accessToken });
  const customDataApi = new DataApi(context);
  const customId      = planetId.toLowerCase();

  try {
    const now = Date.now();

    // 1. Plan against a fresh read of the registry and balance, then write it under the lock.
    const step = await updateRegistry(customDataApi, projectId, customId, async registry => {
      let balance = await currentBalance(econApi, projectId, playerId);
      const plan  = { write: false, balance, cost: 0, chargedTiles: [], revertedTiles: [], advanced: {} };

      for (const [tileId, entry] of Object.entries(registry)) {
        if (entry.ownerId !== playerId) continue;

        const daysElapsed = Math.floor((now - (entry.lastUpkeepTs ?? now)) / MS_PER_DAY);
        if (daysElapsed < 1) continue;

        const cost = UPKEEP_PER_TILE_PER_DAY * daysElapsed;
        if (balance >= cost) {
          balance   -= cost;
          plan.cost += cost;
          // daysElapsed >= 1 implies lastUpkeepTs is set.
          const from = entry.lastUpkeepTs;
          entry.lastUpkeepTs = from + daysElapsed * MS_PER_DAY;
          plan.advanced[tileId] = { from, to: entry.lastUpkeepTs };
          plan.chargedTiles.push(tileId);
        } else {
          delete registry[tileId];
          plan.revertedTiles.push(tileId);
        }
        plan.write = true;
      }
      return plan;
    });

    if (step.conflict) {
      // Busy planet: charge nothing now; the client polls again (EconomyConfig.UpkeepPollIntervalSec).
      logger.warn(`ApplyUpkeep: gave up on ${planetId} for ${playerId} after ${MAX_ATTEMPTS} write conflicts`);
      const balance = await currentBalance(econApi, projectId, playerId);
      return { newBalance: balance, chargedTiles: [], revertedTiles: [] };
    }

    // 2. One deduction for every charged tile. If it fails, undo the lastUpkeepTs advances.
    let newBalance = step.balance;
    if (step.cost > 0) {
      try {
        const deductRes = await econApi.decrementPlayerCurrencyBalance({
          projectId, playerId, currencyId: CURRENCY_ID,
          currencyModifyBalanceRequest: { amount: step.cost }
        });
        newBalance = deductRes.data.balance;
      } catch (err) {
        logger.error(`ApplyUpkeep: charge of ${step.cost} failed for ${playerId} on ${planetId}; undoing ${step.chargedTiles.length} advances. ${err?.message}`);
        await undoAdvances(customDataApi, projectId, customId, playerId, step.advanced, logger);
        throw err;
      }
    }

    logger.info(`ApplyUpkeep: player ${playerId} on ${planetId} — charged ${step.chargedTiles.length}, reverted ${step.revertedTiles.length} → balance ${newBalance}`);
    return { newBalance, chargedTiles: step.chargedTiles, revertedTiles: step.revertedTiles };
  } catch (err) {
    logger.error("ApplyUpkeep failed", { "error.message": err.message });
    throw err;
  }
};

// CurrenciesApi has no getPlayerCurrencyBalance (Known Issue #12) — read all balances and pick one.
async function currentBalance(econApi, projectId, playerId) {
  const res = await econApi.getPlayerCurrencies({ projectId, playerId });
  const c   = res.data.results.find(x => x.currencyId === "COINS");
  return c ? c.balance : 0;
}

// Puts each charged tile's lastUpkeepTs back where this call found it, if nothing has moved it since.
async function undoAdvances(api, projectId, customId, playerId, advanced, logger) {
  try {
    const step = await updateRegistry(api, projectId, customId, registry => {
      let write = false;
      for (const [tileId, { from, to }] of Object.entries(advanced)) {
        const entry = registry[tileId];
        if (!entry || entry.ownerId !== playerId || entry.lastUpkeepTs !== to) continue;
        entry.lastUpkeepTs = from;
        write = true;
      }
      return { write };
    });
    if (step.conflict) throw new Error(`${MAX_ATTEMPTS} write conflicts`);
  } catch (err) {
    logger.error(`ApplyUpkeep: UNDO FAILED on ${customId} (player ${playerId}) — upkeep days given away: ${err?.message}`);
  }
}

// ---- land_registry access (Known Issue #19) — identical in every script that writes the registry,
// pinned by LandRegistryLockTests. The registry is one Custom Data item holding every tile on the
// planet, so each write is a read-modify-write of the whole map: it must carry the writeLock it
// read, or it silently undoes a concurrent write (e.g. puts back a tile SellLand just sold).
const MAX_ATTEMPTS = 3;

// Reads the registry and its writeLock. Only a missing registry reads as {}: any other failure
// throws, so a transient read error can never turn into a write that wipes every tile.
async function loadRegistry(api, projectId, customId) {
  try {
    const res  = await api.getCustomItems(projectId, customId, [REGISTRY_KEY]);
    const item = res.data.results.find(r => r.key === REGISTRY_KEY);
    return item?.value ? { registry: item.value, writeLock: item.writeLock } : { registry: {}, writeLock: undefined };
  } catch (err) {
    if (statusOf(err) === 404) return { registry: {}, writeLock: undefined };
    throw err;
  }
}

// Read-modify-write under the writeLock. `mutate(registry)` validates against a fresh read and
// either edits the registry and returns { write: true, ... } or returns { write: false, ... } to stop.
// A 409 (someone wrote since the read) re-runs mutate on a fresh read, up to MAX_ATTEMPTS; after
// that the result is { write: false, conflict: true }. Returns whatever mutate returned.
async function updateRegistry(api, projectId, customId, mutate) {
  for (let attempt = 1; attempt <= MAX_ATTEMPTS; attempt++) {
    const { registry, writeLock } = await loadRegistry(api, projectId, customId);
    const step = await mutate(registry);
    if (!step.write) return step;
    try {
      await api.setCustomItem(projectId, customId,
        writeLock ? { key: REGISTRY_KEY, value: registry, writeLock } : { key: REGISTRY_KEY, value: registry });
      return step;
    } catch (err) {
      if (statusOf(err) !== 409) throw err;
    }
  }
  return { write: false, conflict: true };
}

function statusOf(err) {
  return err?.response?.status ?? err?.status;
}
// ---- end land_registry access
```

---

## SellLand

Validates ownership, removes the tile from the planet's land registry under its `writeLock`
(a concurrent sale re-reads and gets `NOT_OWNER`, so a tile is paid out at most once), grants the
refund (putting the entry back if the grant fails), and removes the tile from the player's
owned-tiles list. The refund is computed **server-side** (Known Issue #19) as
`Math.round(LAND_PRICES[planet] × RESALE_RATE)` — `LAND_PRICES` is the same map as
`PurchaseLand`'s, `RESALE_RATE` MUST MATCH `EconomyConfig.LandResaleRate` (coins-only resale is
an Open Decision), and `LandPriceAlignmentTests` checks the result equals what `LandSaleService`
shows.

**Parameters:**

| Name | Type | Required | Description |
|---|---|---|---|
| `tileId` | `string` | Yes | ID of the hex tile being sold. |
| `planetId` | `string` | Yes | `PlanetDefinition.name` of the planet the tile belongs to. |
| `refund` | `number` (integer) | No | The client's displayed refund. Never granted — a mismatch is only logged. |

**Returns:** `{ success: true, newBalance }` or `{ success: false, reason }` (`NOT_OWNER`,
`CONFLICT`, `UNKNOWN_PLANET`) — matches `LandSaleResult` (Known Issue #18).

```js
// SellLand — validates ownership, removes the tile from the planet's global land registry (so it
// reverts to Available for everyone) under the registry's writeLock, grants the refund, and
// removes the tile from the player's owned-tiles list.
//
// Known Issue #19: the refund used to come from the client (`refund`, any non-negative integer),
// which minted unlimited COINS. It is now computed here as round(LAND_PRICES × RESALE_RATE) —
// half the planet's current purchase price, the same formula LandSaleService shows the player.
// The client may still send `refund`; it is only compared and logged when it differs.
// The registry entry is removed under its writeLock before the refund is granted, so two
// concurrent sales of one tile can't both pay: the second write gets a 409, re-reads and answers
// NOT_OWNER. If the grant fails the entry is put back. Every registry writer uses the same locked
// read-modify-write (updateRegistry below), so none of them can re-add a sold tile.
const { CurrenciesApi }            = require("@unity-services/economy-2.5");
const { DataApi: PlayerDataApi }   = require("@unity-services/cloud-save-1.4");

const CURRENCY_ID  = "COINS";
const REGISTRY_KEY = "land_registry";

// MUST MATCH PurchaseLand.js LAND_PRICES, i.e. round(EconomyConfig._baseLandPrice ×
// PlanetDefinition._landPriceMultiplier), keyed by the planet asset's name lowercased
// (LandPriceAlignmentTests).
const LAND_PRICES = {
  planet_earth:   150,
  planet_jupiter: 200,
  planet_mars:    100,
  planet_mercury: 80,
  planet_moon:    120,
  planet_neptune: 300,
  planet_pluto:   500,
  planet_saturn:  180,
  planet_uranus:  250,
  planet_venus:   100,
};
const RESALE_RATE = 0.5; // MUST MATCH EconomyConfig._landResaleRate (Open Decision: coins-only resale)

/**
 * @param {string} tileId - ID of the hex tile being sold.
 * @param {string} planetId - PlanetDefinition.name of the planet the tile belongs to.
 * @param {number} [refund] - The client's displayed refund. Informational only: never granted.
 */
module.exports = async ({ params, context, logger }) => {
  const { tileId, planetId, refund: clientRefund } = params;

  if (!tileId || !planetId) {
    throw new Error("Invalid params: tileId and planetId are required");
  }

  const customId = planetId.toLowerCase();
  const price    = LAND_PRICES[customId];
  if (price === undefined) {
    logger.warn(`SellLand: no land price for planet ${planetId}`);
    return { success: false, reason: "UNKNOWN_PLANET" };
  }
  const refund = Math.round(price * RESALE_RATE);
  if (clientRefund !== undefined && clientRefund !== refund) {
    logger.warn(`SellLand: client refund ${clientRefund} for ${planetId} differs from server refund ${refund}`);
  }

  const { projectId, playerId, accessToken } = context;
  const econApi       = new CurrenciesApi({ accessToken });
  const saveApi       = new PlayerDataApi(context);
  const customDataApi = new PlayerDataApi(context);

  try {
    // 1. Validate ownership and remove the tile from the global registry — one locked write. A
    //    concurrent sale (or any write) since the read is a 409 and a fresh read, so the tile is
    //    paid out at most once.
    const step = await updateRegistry(customDataApi, projectId, customId, registry => {
      const entry = registry[tileId];
      if (!entry || entry.ownerId !== playerId) return { write: false, reason: "NOT_OWNER" };
      delete registry[tileId];
      return { write: true, entry };
    });
    if (step.conflict) return { success: false, reason: "CONFLICT" };
    if (!step.write)   return { success: false, reason: step.reason };
    const { entry } = step;

    // 2. Grant the refund. If it fails, put the tile back.
    let newBalance;
    try {
      newBalance = await currentBalance(econApi, projectId, playerId);
      if (refund > 0) {
        const grantRes = await econApi.incrementPlayerCurrencyBalance({
          projectId, playerId, currencyId: CURRENCY_ID,
          currencyModifyBalanceRequest: { amount: refund }
        });
        newBalance = grantRes.data.balance;
      }
    } catch (err) {
      logger.error(`SellLand: grant failed for ${playerId}; restoring tile ${tileId} on ${planetId}. ${err?.message}`);
      await restoreEntry(customDataApi, projectId, customId, tileId, entry, logger);
      throw err;
    }

    // 3. Remove the tile from the player's owned-tiles list.
    const ownedKey = `owned_tiles_${customId}`;
    try {
      const saveRes = await saveApi.getProtectedItems(projectId, playerId, [ownedKey]);
      const item    = saveRes.data.results.find(r => r.key === ownedKey);
      if (item && Array.isArray(item.value)) {
        const ownedTiles = item.value.filter(id => id !== tileId);
        await saveApi.setProtectedItem(projectId, playerId, { key: ownedKey, value: ownedTiles });
      }
    } catch (_) { /* key doesn't exist yet */ }

    logger.info(`SellLand: player ${playerId} sold tile ${tileId} on ${planetId} for ${refund} → ${newBalance}`);
    return { success: true, newBalance };
  } catch (err) {
    logger.error("SellLand failed", { "error.message": err.message });
    throw err;
  }
};

// CurrenciesApi has no getPlayerCurrencyBalance (Known Issue #12) — read all balances and pick one.
async function currentBalance(econApi, projectId, playerId) {
  const res = await econApi.getPlayerCurrencies({ projectId, playerId });
  const c   = res.data.results.find(x => x.currencyId === "COINS");
  return c ? c.balance : 0;
}

// Puts the seller's entry back (re-read under the lock: other tiles may have changed), unless
// someone has claimed the tile meanwhile.
async function restoreEntry(api, projectId, customId, tileId, entry, logger) {
  try {
    const step = await updateRegistry(api, projectId, customId, registry => {
      if (registry[tileId]) return { write: false, claimed: true };
      registry[tileId] = entry;
      return { write: true };
    });
    if (step.conflict) throw new Error(`${MAX_ATTEMPTS} write conflicts`);
    if (step.claimed)  throw new Error("the tile was claimed meanwhile");
  } catch (err) {
    logger.error(`SellLand: RESTORE FAILED for tile ${tileId} on ${customId} (player ${entry.ownerId}) — manual reconciliation needed: ${err?.message}`);
  }
}

// ---- land_registry access (Known Issue #19) — identical in every script that writes the registry,
// pinned by LandRegistryLockTests. The registry is one Custom Data item holding every tile on the
// planet, so each write is a read-modify-write of the whole map: it must carry the writeLock it
// read, or it silently undoes a concurrent write (e.g. puts back a tile SellLand just sold).
const MAX_ATTEMPTS = 3;

// Reads the registry and its writeLock. Only a missing registry reads as {}: any other failure
// throws, so a transient read error can never turn into a write that wipes every tile.
async function loadRegistry(api, projectId, customId) {
  try {
    const res  = await api.getCustomItems(projectId, customId, [REGISTRY_KEY]);
    const item = res.data.results.find(r => r.key === REGISTRY_KEY);
    return item?.value ? { registry: item.value, writeLock: item.writeLock } : { registry: {}, writeLock: undefined };
  } catch (err) {
    if (statusOf(err) === 404) return { registry: {}, writeLock: undefined };
    throw err;
  }
}

// Read-modify-write under the writeLock. `mutate(registry)` validates against a fresh read and
// either edits the registry and returns { write: true, ... } or returns { write: false, ... } to stop.
// A 409 (someone wrote since the read) re-runs mutate on a fresh read, up to MAX_ATTEMPTS; after
// that the result is { write: false, conflict: true }. Returns whatever mutate returned.
async function updateRegistry(api, projectId, customId, mutate) {
  for (let attempt = 1; attempt <= MAX_ATTEMPTS; attempt++) {
    const { registry, writeLock } = await loadRegistry(api, projectId, customId);
    const step = await mutate(registry);
    if (!step.write) return step;
    try {
      await api.setCustomItem(projectId, customId,
        writeLock ? { key: REGISTRY_KEY, value: registry, writeLock } : { key: REGISTRY_KEY, value: registry });
      return step;
    } catch (err) {
      if (statusOf(err) !== 409) throw err;
    }
  }
  return { write: false, conflict: true };
}

function statusOf(err) {
  return err?.response?.status ?? err?.status;
}
// ---- end land_registry access
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
    const res    = await saveApi.getProtectedItems(projectId, playerId, [SAVE_KEY]);
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
      const res    = await saveApi.getProtectedItems(projectId, playerId, [SAVE_KEY]);
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
      await saveApi.setProtectedItem(projectId, playerId, body);
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
    const res    = await saveApi.getProtectedItems(projectId, playerId, [SAVE_KEY]);
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
      await saveApi.setProtectedItem(projectId, playerId, body);
      logger.info(`RefillFuel: player ${playerId} refilled to ${full.fuel}/${full.maxFuel} for ${REFILL_COST} → ${newBalance}`);
      return { success: true, fuel: full.fuel, maxFuel: full.maxFuel, newBalance };
    } catch (err) {
      if (isConflict(err) && attempt < MAX_RETRIES - 1) {
        // Re-read the latest writeLock and retry (value is always a full tank).
        try {
          const res    = await saveApi.getProtectedItems(projectId, playerId, [SAVE_KEY]);
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
// UpdateProfile — validates and commits the caller's display name and/or
// avatar into their "player_profile" Cloud Save record (merging with any
// existing profile fields such as level/xp/badges). Both params are
// independently optional: a call may update just the name, just the
// avatar, or both. The name is re-moderated server-side: the client's
// ChatModerationFilter check is only fast feedback.
// BLOCKED_WORDS / CHAR_MAP / MAX_DISPLAY_NAME_LENGTH must match
// SocialConfig.BlockedWords / ChatModerationFilter / MaxDisplayNameLength —
// same "must match" pattern as the other ServerCode catalogs (Cloud Code modules deploy
// as standalone files, so the filter is duplicated rather than required).
// AVATAR_IDS must match the 25 AvatarDefinition assets registered on
// DatabaseRegistry (see docs/superpowers/plans/2026-07-06-avatar-selection.md).
//
// FIX: DataApi's constructor doesn't read a { headers: ... } field, and
// getItems/setItem take positional args (projectId, playerId, ...), not an
// options object — same SDK-shape mismatch as GetPlayerProfile.js / GetFuelState.js
// / SpendFuel.js. The old setItem call silently dropped playerId, causing a
// 422 RequiredError; getItems had the same bug but it was masked by the
// surrounding try/catch.
const { DataApi } = require("@unity-services/cloud-save-1.4");

const PROFILE_KEY             = "player_profile";
const MAX_DISPLAY_NAME_LENGTH = 20; // must match SocialConfig.MaxDisplayNameLength

const BLOCKED_WORDS = [
  "fuck", "shit", "bitch", "asshole", "cunt", "dick", "faggot",
  "nigger", "nigga", "whore", "slut", "retard", "kys"
];
const CHAR_MAP = { "@": "a", "4": "a", "1": "i", "!": "i", "0": "o", "3": "e", "$": "s", "5": "s", "7": "t" };

const AVATAR_IDS = [
  "avatar_alien_blue", "avatar_alien_green", "avatar_boy1", "avatar_boy_1_dark",
  "avatar_boy_2", "avatar_boy_3", "avatar_boy_4", "avatar_boy_5", "avatar_boy_6",
  "avatar_boy_6_light", "avatar_boy_7", "avatar_boy_8", "avatar_boy_9", "avatar_boy_10",
  "avatar_dark", "avatar_girl_1", "avatar_girl_2", "avatar_girl_2_dark", "avatar_girl_3",
  "avatar_girl_4", "avatar_girl_5", "avatar_girl_6", "avatar_girl_7", "avatar_girl_8",
  "avatar_wizard"
];

function isClean(text) {
  let normalized = "";
  for (const ch of text.toLowerCase()) normalized += CHAR_MAP[ch] ?? ch;
  return !BLOCKED_WORDS.some(word => normalized.includes(word));
}

/**
 * @param {string} [params.displayName] - New display name, 1-20 chars, must pass moderation.
 * @param {string} [params.avatarId] - New avatar id, must be one of AVATAR_IDS.
 */
module.exports = async ({ params, context, logger }) => {
  const hasDisplayName = params.displayName !== undefined && params.displayName !== null;
  const hasAvatarId    = params.avatarId    !== undefined && params.avatarId    !== null;

  let displayName = null;
  if (hasDisplayName) {
    displayName = params.displayName.trim();

    if (displayName.length === 0) {
      return { success: false, reason: "NAME_EMPTY", displayName: null, avatarId: null };
    }
    if (displayName.length > MAX_DISPLAY_NAME_LENGTH) {
      return { success: false, reason: "NAME_TOO_LONG", displayName: null, avatarId: null };
    }
    if (!isClean(displayName)) {
      logger.info(`UpdateProfile: rejected display name from ${context.playerId}`);
      return { success: false, reason: "NAME_REJECTED", displayName: null, avatarId: null };
    }
  }

  let avatarId = null;
  if (hasAvatarId) {
    avatarId = params.avatarId;
    if (!AVATAR_IDS.includes(avatarId)) {
      logger.info(`UpdateProfile: rejected unknown avatarId "${avatarId}" from ${context.playerId}`);
      return { success: false, reason: "AVATAR_INVALID", displayName: null, avatarId: null };
    }
  }

  const { projectId, playerId } = context;
  const saveApi = new DataApi(context);

  let profile = {};
  try {
    const res  = await saveApi.getItems(projectId, playerId, [PROFILE_KEY]);
    const item = res.data.results.find(r => r.key === PROFILE_KEY);
    if (item?.value) profile = typeof item.value === "string" ? JSON.parse(item.value) : item.value;
  } catch (_) { /* no profile yet */ }

  if (hasDisplayName) profile.displayName = displayName;
  if (hasAvatarId)    profile.avatarId    = avatarId;
  profile.updatedMs = Date.now();

  await saveApi.setItem(projectId, playerId, { key: PROFILE_KEY, value: profile });

  logger.info(`UpdateProfile: ${playerId} → displayName=${profile.displayName ?? "(unchanged)"} avatarId=${profile.avatarId ?? "(unchanged)"}`);
  return {
    success: true,
    reason: null,
    displayName: profile.displayName ?? null,
    avatarId: profile.avatarId ?? null
  };
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
// options object — same SDK-shape mismatch as Known Issue #6. The old call
// silently failed every time (caught below), so `profile` was always null.
// DataApi(context) authenticates via the service token, which is required
// to read another player's data.
//
// FIX 2: displayName no longer defaults to a synthetic "Pilot {id6}"
// placeholder — it's null when the player hasn't saved a custom display
// name. The only current caller (PlanetSceneScope.HydrateServerStateAsync,
// hydrating the signed-in player's own HUD name) already falls back to the
// UGS auth username when displayName is empty; the placeholder default was
// unconditionally overriding that correct username for every player who
// hadn't explicitly customized their name.
const { DataApi } = require("@unity-services/cloud-save-1.4");

const PROFILE_KEY        = "player_profile";
const OWNED_TILES_PREFIX = "owned_tiles_";
const MAX_PAGES          = 5; // Cloud Save pages hold 20 keys; a player has well under 100

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
    const res  = await saveApi.getItems(projectId, targetId, [PROFILE_KEY]);
    const item = res.data.results.find(r => r.key === PROFILE_KEY);
    if (item) profile = typeof item.value === "string" ? JSON.parse(item.value) : item.value;

    // owned_tiles_* lists are server-written (Protected class, Known Issue #16). There is one per
    // planet and the planet ids aren't known here, so page through every protected key.
    let after;
    for (let page = 0; page < MAX_PAGES; page++) {
      const pres  = await saveApi.getProtectedItems(projectId, targetId, undefined, after);
      const items = pres.data.results;
      for (const it of items) {
        if (it.key.startsWith(OWNED_TILES_PREFIX) && Array.isArray(it.value)) tilesOwned += it.value.length;
      }
      if (!pres.data.links?.next || items.length === 0) break;
      after = items[items.length - 1].key;
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

