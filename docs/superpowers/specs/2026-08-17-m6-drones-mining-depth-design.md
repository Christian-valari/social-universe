# M6 — Drones & Mining Depth — Design

**Date:** 2026-08-17
**Feature area:** Mining depth (M6 milestone)
**Milestone scope:** M6 per `Social_Universe_Architecture.md` §7. Adds a drone upgrade/fleet system, a server-backed mineral inventory, mineral→coin selling, and asteroid-tier gating. **Scope flag:** modifies the established M1/M2 mining-reward path (`ValidateMining` grants minerals, not coins) — called out under "Scope flag" below per the CLAUDE.md Pre-Task Protocol. Respects Architecture Rules: server-authoritative economy (Rule 1), backend behind `I*Service` (Rule 2), tunables in ScriptableObjects (Rule 3), event-decoupled UI (Rule 4).

## Problem

The mining loop is one step deep. Today:

- `AsteroidDefinition` holds a raw `_mineralType` string, a `_baseYield`, a `_rarity`, a `_tier` (1–3), and a `_coinsPerUnit`. Mining an asteroid grants **coins directly** via `ValidateMining` (server) using the reward calculator.
- `DroneDefinition` holds only `travelSpeed`, `cargoCap`, and a model — **no tier**, no upgradeable stats.
- `DroneRuntime` is a bare wrapper around the SO — it holds **no live/current stats** despite PROGRESS.md describing it as "live instance + current stats."
- There is **no mineral inventory** — minerals are an instant coin payout with no persistence, no types held, nothing to sell, contribute (M7), or trade (M8).
- Asteroid `_tier` exists but nothing gates access to it — a starting drone can mine any tier.

M6 turns this into a progression loop: mine tier-appropriate asteroids for **typed minerals** → **sell** them for coins → spend coins on **drone upgrades** and **fleet expansion** → unlock **higher-tier drones** that reach **higher-tier asteroids** → rarer, more valuable minerals.

## Design decisions (resolved during brainstorming)

1. **Minerals → sell for coins.** Mining yields typed minerals into a server-backed inventory; coins come from selling minerals to the "house" at a fixed per-mineral value. Minerals persist for M7 (guild contribution) and M8 (marketplace), but in M6 they are effectively a coin proxy.
2. **Upgrades are linear per-stat, paid in coins.** Each upgradeable stat (Cargo, Yield, Speed) is an independent linear track with a scaling cost. One currency sink (coins); no branching prerequisite tree.
3. **Fleet, one active at a time.** The player owns multiple distinct drones (each a `DroneDefinition` with a tier). The Garage sets the *active* drone; the tier gate is `activeDrone.Tier >= asteroid.Tier`. "Slots" = fleet roster capacity (`UnlockDroneSlot` buys more room). **No concurrent mining** — still one idle/active session at a time, so `MiningController` stays largely intact.
4. **Functional UI now.** Real (unpolished) Garage and Mineral Inventory views, HUD-opened panels on the Planet scene, DI-wired via `PlanetSceneScope` (matches `SettingsPanel`/`AvatarSelectionModal`). No new scene.

## Non-goals (out of scope for M6)

- Concurrent multi-drone mining (parallel idle sessions).
- Branching / prerequisite upgrade trees; mineral crafting or refining recipes.
- The M8 marketplace — M6 sells only to the house at a fixed `sellValue`.
- Polished M11 screen styling, animations, and juice.
- UGS Economy "inventory items" for minerals — minerals are stored as a Cloud Save JSON record (see §4), consistent with `fuel_state`/`land_registry`, avoiding per-mineral dashboard configuration.

## Scope flag — mining-reward path change

`ValidateMining.js` currently grants **coins**. In M6 it grants **minerals** (by the mined asteroid's mineral id, into the `mineral_inventory` record, still capped server-side). The client mining grant path routes the result through `MineralInventory` instead of `Wallet`. `MiningRewardCalculator` continues to compute the *quantity* mined (yield × drone effective yield multiplier × session), but the unit is now minerals, not coins. Coins enter the wallet only via `SellMinerals`. This is the one place M6 rewrites established M1/M2 behavior; everything else is additive.

## Design

### 1. Config / ScriptableObjects (`SocialUniverse.Config`)

**`MineralDefinition` (new SO)** — authoritative per-mineral data.

| Field | Type | Purpose |
|---|---|---|
| `_mineralId` | `string` | Stable id (e.g. `iron`, `platinum`). Inventory + save key. |
| `_displayName` | `string` | UI label. |
| `_tier` | `int` | Tier this mineral belongs to (mirrors asteroid tier). |
| `_sellValue` | `int` | Coins granted per unit when sold. **Authoritative sell price** (duplicated server-side, "must match" pattern). |
| `_icon` | `Sprite` | Inventory icon. |
| `_tintColor` | `Color` | Inventory accent / rarity color. |

`[CreateAssetMenu(menuName = "SocialUniverse/Config/MineralDefinition")]`. Author **6 assets** (Iron, Carbon, Silicon, Nickel, Platinum, Iridium) matching the 6 existing asteroid tiers/values.

**`AsteroidDefinition` (change)** — replace `_mineralType` (string) + `_coinsPerUnit` (int) with a single `[SerializeField] private MineralDefinition _mineral;` reference (accessor `Mineral`). Keeps `_baseYield`, `_rarity`, `_tier`, `_modelPrefab`. *Re-authors the 6 existing asteroid assets* to reference the matching `MineralDefinition`. Sell value now lives on the mineral, not the asteroid.

**`DroneDefinition` (additions)** — the existing stats become **base** values that upgrades scale.

| New/changed field | Type | Purpose |
|---|---|---|
| `_tier` | `int` (default 1) | Highest asteroid tier this drone can mine. |
| `_unlockCost` | `int` | Coins to acquire this drone into the fleet (0 for the starter Scout). |
| `_yieldMultiplier` | `float` (default 1) | Base mining-yield multiplier (upgradeable). |
| `_cargoCap`, `_travelSpeed` | (existing) | Now treated as base values scaled by upgrades. |

Author **2 new drone assets**: `Drone_Hauler` (T2), `Drone_Prospector` (T3), with models. `Drone_Scout` stays T1, `_unlockCost = 0`.

**`UpgradeDefinition` (new SO)** — one asset per upgradeable stat.

| Field | Type | Purpose |
|---|---|---|
| `_stat` | `DroneStat` enum (`Cargo`, `Yield`, `Speed`) | Which stat this track upgrades. |
| `_maxLevel` | `int` | Cap for the track. |
| `_baseCost` | `int` | Coin cost of level 1. |
| `_costGrowth` | `float` | Multiplicative cost growth per level. |
| `_deltaPerLevel` | `float` | Additive stat delta per level. |

`DroneStat` is a new enum in `SocialUniverse.Config`. Author **3 assets** (one per stat). Upgrades apply to the **active drone** and are per-drone (each owned drone tracks its own levels).

**`DatabaseRegistry` (additions)** — mirror the existing `AllItems`/`GetItem`: `AllDrones`/`GetDrone(id)` (already partially present — verify), `AllMinerals`/`GetMineral(id)`, `AllUpgrades`/`GetUpgrade(stat)`. Add serialized lists + accessors.

**`EconomyConfig` (additions)** — `[Header("Drones — M6")]`: `StartingFleetSlots` (default 2), `SlotUnlockBaseCost` + `SlotUnlockCostGrowth` (scaling slot price). These are the only new tunable numbers not on a per-asset SO.

### 2. Runtime + persistence (`SocialUniverse.Mining`)

**`DroneRuntime` (rework)** — becomes the live drone. Holds the `DroneDefinition` plus a `Dictionary<DroneStat,int>` of upgrade levels. Exposes **effective stats** computed from base + levels via the shared math (see below):

```
EffectiveCargoCap   = base.CargoCap      + level(Cargo) * upgrade(Cargo).DeltaPerLevel
EffectiveYieldMult  = base.YieldMultiplier + level(Yield) * upgrade(Yield).DeltaPerLevel
EffectiveTravelSpeed= base.TravelSpeed   + level(Speed) * upgrade(Speed).DeltaPerLevel
```

**`DroneUpgradeMath` (new, pure static)** — `NextCost(UpgradeDefinition, currentLevel)` and `EffectiveStat(baseValue, UpgradeDefinition, level)`. Pure functions, unit-tested without a scene (mirrors `HexBoardMath`/`SkyLockOnMath`). The cost formula is **duplicated in `UpgradeDrone.js`** ("must match").

**`MineralInventory` (new)** — client cache of `{ mineralId → qty }` with a `MineralInventoryChangedEvent`. Mirrors `Wallet`↔`IEconomyService`: a view cache, server is the source of truth. Methods: `SetAll(dict)`, `Get(mineralId)`, `Add(mineralId, qty)` (local optimistic), `TotalSellValue(registry)`.

**`DroneFleet` (new)** — client cache of owned drones (`List<DroneRuntime>`), `ActiveDroneId`, `UnlockedSlots`. `DroneFleetChangedEvent` on mutation. Exposes `Active` (the `DroneRuntime`).

**Persistence — Cloud Save JSON records** (player-scoped, matching `fuel_state`):
- `mineral_inventory` → `{ "iron": 12, "platinum": 3, ... }`
- `drone_fleet` → `{ "slots": 2, "activeDroneId": "scout", "drones": [ { "droneId": "scout", "upgrades": { "Cargo": 2, "Yield": 1, "Speed": 0 } }, ... ] }`

Hydrated on Planet scene start by `PlanetSceneBootstrapper` (alongside wallet + owned tiles) and returned in the `GetBootstrapState` round-trip.

### 3. Services + interfaces

All follow the `I*Service` + `LocalMock*` + public-DTO testability pattern (public top-level result DTOs so `FakeBackendClient.CallAsync<T>` can type them).

**`IDroneService` / `DroneService` / `LocalMockDroneService`** (`Mining`)
- `Task<DroneActionResult> AcquireDroneAsync(string droneId)` → `AcquireDrone`
- `Task<DroneActionResult> UnlockSlotAsync()` → `UnlockDroneSlot`
- `Task<DroneActionResult> UpgradeAsync(string droneId, DroneStat stat)` → `UpgradeDrone`
- `Task<DroneActionResult> SetActiveAsync(string droneId)` → `SetActiveDrone`

Each request→`IBackendClient.CallAsync`→on success applies the returned authoritative state to `DroneFleet` + `Wallet`. `DroneActionResult` carries `{ success, newBalance, fleet snapshot }`.

**`IMineralService` / `MineralService` / `LocalMockMineralService`** (`Mining`)
- `Task<SellResult> SellAsync(string mineralId, int qty)` → `SellMinerals`
- `Task<SellResult> SellAllAsync()` → `SellMinerals` (all)
- `Task RefreshAsync()` → hydrates `MineralInventory` from Cloud Save.

`SellResult` = `{ success, newBalance, remainingInventory }`.

Mining grant path: on a successful `ValidateMining`, the client applies granted minerals to `MineralInventory` (not `Wallet`).

### 4. Server functions (`ServerCode/`)

**All must use the proven-correct SDK pattern** — `new CurrenciesApi({ accessToken })`, `new DataApi(context)` / `new PlayerDataApi(context)`, positional `getItems`/`setItem` — to avoid Known Issues #6/#8/#9. Verify against the live dashboard SDK on deploy.

| Function | Responsibility |
|---|---|
| `AcquireDrone` | Validate coin balance ≥ `unlockCost` and `drones.length < slots` and not already owned; deduct coins; append drone (upgrades all 0) to `drone_fleet`. Returns `{ success, newBalance, fleet }`. |
| `UnlockDroneSlot` | Validate coins ≥ scaling slot cost (`SlotUnlockBaseCost × SlotUnlockCostGrowth^(slots-start)`); deduct; `slots++`. |
| `UpgradeDrone` | Validate owned + `level < maxLevel` + coins ≥ `NextCost`; deduct; increment that stat's level. Cost formula duplicated from `DroneUpgradeMath` ("must match"). |
| `SetActiveDrone` | Validate ownership; set `activeDroneId`. No economy mutation. |
| `SellMinerals` | Validate held qty ≥ requested (or "all"); compute payout `Σ qty × MineralDefinition.sellValue` (values duplicated server-side, "must match"); decrement `mineral_inventory`; grant coins. Returns `{ success, newBalance, remainingInventory }`. |
| `ValidateMining` (**change**) | Grant **minerals** instead of coins: increment `mineral_inventory[asteroidMineralId]` by the capped quantity. Reuses the existing session/cap validation. |
| `GetBootstrapState` (**extend**) | Include `mineral_inventory` + `drone_fleet` in the launch round-trip. |

Mineral `sellValue`s and the slot/upgrade cost constants are duplicated between `EconomyConfig`/`MineralDefinition`/`UpgradeDefinition` and the JS — the same "must match" duplication already used for yield/upkeep/fuel formulas. Documented in a comment block in each function.

### 5. Tier gating (`MiningController`)

Before starting an idle or active session, `MiningController` checks `DroneFleet.Active.Definition.Tier >= asteroid.Definition.Tier`. If not, it does **not** start the session and publishes a new `MiningBlockedEvent { Asteroid, RequiredTier }`; the HUD surfaces "Requires a Tier N drone." Cargo cap and yield now read from `DroneFleet.Active.EffectiveCargoCap` / `EffectiveYieldMult` rather than the raw SO. `MiningRewardCalculator` takes the effective values as inputs (already parameterized — verify signature).

### 6. UI (`SocialUniverse.UI`) — functional, HUD-opened panels on the Planet scene

DI-wired via `PlanetSceneScope` (both production and standalone), matching `SettingsPanel`/`AvatarSelectionModal`/`DisplayNameModal`. No new scene. UI **publishes intent events**; App-layer handlers own the service calls (the `TilePurchaseHandler` pattern).

- **`DroneGarageView`** — fleet grid: owned drones (name, tier, active marker), locked/empty slots ("Unlock slot — {cost}"), and acquirable drone types (name, tier, "Acquire — {cost}"). Tapping an owned drone publishes `SetActiveDroneRequestedEvent`. Per-stat upgrade rows show current/max level + next cost, gated on affordability, publishing `DroneUpgradeRequestedEvent`.
- **`MineralInventoryView`** — one row per held mineral (icon, name, qty, unit value), a "Sell all" button and per-type sell, publishing `SellMineralsRequestedEvent`.
- **HUD additions** — a garage button (opens `DroneGarageView`) and a compact minerals readout / inventory button.

### 7. App-layer handlers (`SocialUniverse.App`)

`IStartable`/`IDisposable` controllers subscribing to the intent events and calling services (mirrors `TilePurchaseHandler`/`BuildModeController`):
- `DroneGarageHandler` — handles acquire / unlock-slot / upgrade / set-active events → `IDroneService`.
- `MineralSaleHandler` — handles sell events → `IMineralService`.

Registered in `PlanetSceneScope` (both modes) as entry points.

### 8. Events (`SocialUniverse.Core`)

Intent (UI → App): `DroneUpgradeRequestedEvent`, `DroneAcquireRequestedEvent`, `DroneSlotUnlockRequestedEvent`, `SetActiveDroneRequestedEvent`, `SellMineralsRequestedEvent`.
State (services → UI): `MineralInventoryChangedEvent`, `DroneFleetChangedEvent`, `MiningBlockedEvent`.

### 9. DI / assembly changes

- `PlanetSceneScope`: register `MineralInventory`, `DroneFleet`, `IDroneService`/`IMineralService` (production: real; standalone/dev: `LocalMock*`), and the two App handlers as entry points. Hydrate fleet + inventory in `PlanetSceneBootstrapper.Start()`.
- `RootLifetimeScope`: no change (these are planet-scoped, like `LandRegistry`), unless bootstrap hydration is centralized — decide during planning.
- No new assembly. `MineralDefinition`/`UpgradeDefinition`/`DroneStat` go in `SocialUniverse.Config`; runtime in `SocialUniverse.Mining`; UI in `SocialUniverse.UI`; handlers in `SocialUniverse.App`.

## Testing (EditMode, `FakeBackendClient`)

| Test | Coverage |
|---|---|
| `DroneUpgradeMathTests` | `NextCost` growth curve; `EffectiveStat` base + level×delta; cap at maxLevel. |
| `DroneRuntimeTests` | Effective stats reflect upgrade levels; unknown stat = base. |
| `MineralInventoryTests` | Add/set/get; `TotalSellValue` against a registry; sell decrements. |
| `DroneServiceTests` | Acquire/unlock/upgrade/set-active apply returned state on success, no-op on failure (via `FakeBackendClient`). |
| `MineralServiceTests` | Sell payout applies `newBalance` to `Wallet` and updates inventory on success; unchanged on failure. |
| `MiningControllerTests` (new case) | Tier gate blocks a T2 asteroid with a T1 active drone and publishes `MiningBlockedEvent`; effective cargo/yield feed the reward calc. |
| `ValidateMiningCapAlignmentTests` (extend) | Minerals grant path respects the same cap as the retired coins path. |

## Server deploy / setup (deferred, like M2–M5)

- [ ] Deploy `AcquireDrone`, `UnlockDroneSlot`, `UpgradeDrone`, `SetActiveDrone`, `SellMinerals`, updated `ValidateMining`, extended `GetBootstrapState`.
- [ ] Verify Cloud Save JSON record shapes (`mineral_inventory`, `drone_fleet`) against the live SDK.
- [ ] Author assets: 6 `MineralDefinition`, 3 `UpgradeDefinition`, 2 new drone (`Hauler` T2, `Prospector` T3) + models; re-author 6 `AsteroidDefinition` to reference minerals; add all to `DatabaseRegistry`.
- [ ] Wire `DroneGarageView`/`MineralInventoryView` panels + HUD buttons in `Planet.unity`; assign `PlanetSceneScope` registrations.

## Open questions for planning

1. **Starter fleet:** does a new player start owning `Drone_Scout` (active), or must they acquire it? Assume: **owns Scout by default** (`GetBootstrapState` seeds it if `drone_fleet` is empty).
2. **Downgrade/refund:** none in M6 (upgrades and acquisitions are permanent). Confirm.
3. **Bootstrap hydration location:** planet-scoped (in `PlanetSceneBootstrapper`) vs root — resolve in the implementation plan.
