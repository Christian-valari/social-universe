# LandBuilding Mode — Design Spec

**Date:** 2026-08-04
**Milestone:** Extends M3 (Land System Depth) — build mode.
**Status:** Approved, pending implementation plan.

## Summary

A dedicated **LandBuilding** scene where the owner of a hex tile decorates their plot
by placing buildings/decorations into a fixed set of slots, and any player can visit an
owned tile to view its plot read-only. Each placed item raises the tile's build level
(and therefore its yield); removing an item lowers it. Entry is a "View Land" button on
the existing tile info modal, which swaps the Planet scene for the LandBuilding scene via
a new FSM state.

This replaces the current abstract M3 build model (an opaque `BuildLevel` integer whose
only visual effect is tile extrusion) with a persisted, visible plot layout.

## Locked Decisions

| Decision | Choice |
|---|---|
| Placement model | **Fixed slots** on the plot (owner picks item → snaps into a slot) |
| Scene transition | **Full scene swap** — Planet unloads, LandBuilding loads, Back returns (new FSM state) |
| Who can enter | **Any owned tile** (yours or others'); owner gets edit mode, everyone else view-only |
| Editing | **Move / remove / replace allowed** (not place-only) |
| Remove policy | **No refund; build level drops.** `buildLevel = number of filled slots` |
| Move policy | **Free**, owner-only, target slot must be empty (dedicated server op, not remove+place) |
| 3D assets | **Placeholder primitives now** via `ItemDefinition.Prefab`; real art is a later no-code swap |
| Slot count | **Global constant** `EconomyConfig.PlotSlotCount` (default 8); `MaxBuildLevel` set equal to it |

## Architecture Rules Compliance

- **Server-authoritative economy:** all coin spend, ownership checks, slot writes, and
  build-level changes happen in `ServerCode/` functions. The client requests and reflects.
- **Backend behind interfaces:** the client calls server functions only through
  `IBackendClient`; tests fake it. No SDK reference from gameplay code.
- **Data in ScriptableObjects:** `PlotSlotCount` lives in `EconomyConfig`; buildables are
  `ItemDefinition` assets.
- **Decouple via events:** the entry point publishes `ViewLandRequestedEvent` over `EventBus`;
  no direct cross-namespace call from UI into `Core`.
- **Mobile budget:** full scene swap keeps only one heavy scene (hexasphere OR plot) resident
  at a time.

## 1. Data Model

The central change: a plot must persist **what** is placed, not just how much, so visitors
can render an owner's layout.

### `LandTileEntry` (Economy, mirrored in the server registry JSON)
Add:
- **`Slots`** — fixed-length array of length `PlotSlotCount`. Each element is either `null`
  (empty) or an `itemId` string.

Change:
- **`BuildLevel` becomes derived** = count of non-null entries in `Slots`. It is no longer an
  independent field the server increments; the server recomputes it from `Slots` on every
  write. (The field stays on the DTO for the client's convenience and for existing yield code,
  but its value is always `filled-slot count`.)

Older registry entries with no `slots` key are treated as an all-empty plot of length
`PlotSlotCount` (backward-compatible read).

### `EconomyConfig` (Config)
Add:
- **`PlotSlotCount`** (int, default 8).

Change:
- **`MaxBuildLevel`** is set equal to `PlotSlotCount` (the current `4` was arbitrary). Yield
  code that references `MaxBuildLevel` continues to work; `TileExtrusionView`'s
  `buildLevel / MaxBuildLevel` ratio now spans the full slot range.

### `ItemDefinition` (Config)
- Already has `Prefab` (GameObject) — assign a placeholder primitive prefab per item.
- **`BuildLevel`** (the old per-item "unlock level") is **no longer used for gating**. In the
  slot model any item may occupy any empty slot. Leave the field in place (unused) to avoid a
  churny asset migration; do not read it in the palette.

## 2. Server Functions (`ServerCode/`)

All are server-authoritative; the client never mutates slots, balance, or build level directly.
The `land_registry` Cloud Save Custom Data shape is unchanged except each tile entry now carries
a `slots` array. `buildLevel` is always recomputed as the filled-slot count before writing.

### `PlaceBuild` (rework of the existing function)
Params: `tileId, planetId, slotIndex, itemId, cost`.
1. Validate params (slotIndex in `[0, PlotSlotCount)`, cost positive integer).
2. Load registry; require `entry.ownerId === playerId` → else `NOT_OWNER`.
3. Require `slots[slotIndex]` empty → else `SLOT_OCCUPIED`.
4. Require balance ≥ cost → else `INSUFFICIENT_FUNDS`.
5. Deduct cost, set `slots[slotIndex] = itemId`, recompute `buildLevel = filled count`, write.
Returns `{ success, newBalance, buildLevel }`.

> The existing validate→deduct→write sequence is not transactional — same documented caveat
> as `PurchaseLand`. Not addressed in this feature.

### `RemoveBuild` (new)
Params: `tileId, planetId, slotIndex`.
1. Validate; require ownership → `NOT_OWNER`.
2. If `slots[slotIndex]` already empty → return `{ success: true }` without a write (idempotent).
3. Otherwise clear the slot, recompute `buildLevel`, write. **No coin refund.**
Returns `{ success, buildLevel }`.

### `MoveBuild` (new)
Params: `tileId, planetId, fromSlot, toSlot`.
1. Validate; require ownership → `NOT_OWNER`.
2. Require `fromSlot` occupied and `toSlot` empty → else `INVALID_MOVE`.
3. Move the itemId from `fromSlot` to `toSlot`, write. Free; `buildLevel` unchanged.
Returns `{ success }`.

### `GetLandRegistry` (already exists)
No new call needed — its returned entries now carry `slots`, so any player can render any
owner's plot from the registry already fetched by `LandRegistryService`.

> Rejected alternative: implement "move" client-side as `RemoveBuild` + `PlaceBuild`. Rejected
> because it would re-charge the player and drop/re-raise build level (and yield). A dedicated
> free op is correct.

**Deployment/testing of these JS functions against UGS is user-owned** (per project pattern);
this feature delivers the JS files and the client wiring behind `IBackendClient`.

## 3. Scene Flow & FSM

### New scene: `LandBuilding.unity` (`Assets/Scenes/`)
Contains: a camera, a directional light, a plot ground surface, and `PlotSlotCount` fixed slot
anchor transforms arranged on the plot. Added to build settings + a `Constants.SceneNames`
entry.

### New state: `LandBuildingState : IGameState` (Core)
Mirrors `ActiveMiningState`: full scene swap. `PlanetState.Exit()` unloads Planet before this
state's `Enter()` loads LandBuilding (via `SceneLoader`, optional loading screen). `Back`
transitions the FSM back to `PlanetState` with the correct `TargetPlanetId`.

### New handoff: `LandBuildingHandoff` (Core, DI singleton)
Fields: `TileId`, `PlanetId`, `OwnerId`, `CanEdit`. Populated at entry;
`CanEdit = (OwnerId == localPlayerId)`.

### Entry point
`TileInfoModal` (already opens for any owned tile — yours or others') gains a **"View Land"**
button. On click it publishes `ViewLandRequestedEvent { Tile }` on `EventBus`. A Planet-scene
handler (mirroring the existing active-mining request handler) resolves the tile's owner, fills
`LandBuildingHandoff`, and calls `PlanetState.EnterLandBuilding()`, which transitions the FSM to
`LandBuildingState`.

"Available" (unowned) tiles show the purchase flow as today — no "View Land".

## 4. LandBuilding Scene Behaviour

### `LandBuildingController` (World)
On scene load: read `LandBuildingHandoff` + the tile's `LandTileEntry` from
`LandRegistryService`. For each filled slot, instantiate the slot's `ItemDefinition.Prefab`
onto the matching anchor. Always renders the diorama, edit or view.

### View mode (`CanEdit == false`) — the visitor experience
Diorama + a Back button only. No palette, no interactive slots.

### Edit mode (`CanEdit == true`) — the owner experience
- **`LandBuildPaletteView`** (UI): a bar of available items (name, cost) from a reworked
  `BuildPaletteService` (returns all items the player can afford; rarity/level gating deferred).
- Slots become interactive:
  - Tap empty slot with a palette item selected → `PlaceBuild(slotIndex, itemId, cost)`.
  - Tap filled slot → move / remove actions (`MoveBuild`, `RemoveBuild`).
- **Optimistic UI**, reconciled on the server response. On success, update
  `LandRegistryService` locally so the tile's `Slots` / `BuildLevel` (and thus yield and
  `TileExtrusionView`) are correct the instant the player returns to the planet.
- Failures (`SLOT_OCCUPIED`, `INSUFFICIENT_FUNDS`, `NOT_OWNER`) roll back the optimistic change
  and surface a message.

### `BuildPaletteService` rework (Economy)
Replace the linear `ItemDefinition.BuildLevel == tile.BuildLevel + 1` rule with: return all
`ItemDefinition`s (optionally filtered to those the player can afford), for a tile the player
owns, when the plot has at least one empty slot. Ownership + affordability remain the gates.

### Placeholder art
Placeholder primitive prefabs under `Assets/_Project/Prefabs/Buildables/`, one per
`ItemDefinition`, assigned to the `Prefab` field. Real art later = reassign the field, no code
change.

## 5. Testing

**EditMode (unit):**
- `BuildLevel == filled-slot count` derivation from `Slots` (place raises, remove lowers, move
  leaves unchanged).
- `BuildPaletteService` rework: returns items only for owned tiles with a free slot; respects
  affordability; empty when plot full or tile not owned.
- Place / remove / move request→response mapping against a **fake `IBackendClient`**, including
  each failure reason and optimistic rollback.
- Edit-vs-view gating derived from `CanEdit` / `OwnerId == localPlayerId`.

**PlayMode / manual repro:**
- Scene wiring (slot anchors, prefab spawn, palette, Back) verified by a documented manual
  repro in the plan — scene composition isn't unit-testable.

## 6. Scope Boundary — Explicitly Deferred

- Multiple/variable slot layouts per planet (single global `PlotSlotCount` for now).
- Item rarity / build-level unlock gating in the palette.
- Any refund, undo, or transactional guarantee on server ops.
- Visitor-attraction rewards tied to how decorated a plot is (beyond existing visit-driven yield).
- Real 3D building/decoration art (placeholders only).

## Affected / New Files (indicative, finalized in the plan)

**New:**
- `Assets/Scenes/LandBuilding.unity`
- `Scripts/Core/LandBuildingState.cs`, `LandBuildingHandoff.cs`, `ViewLandRequestedEvent.cs`
- `Scripts/World/LandBuildingController.cs` (+ slot/anchor helpers)
- `Scripts/UI/LandBuildPaletteView.cs`
- `ServerCode/RemoveBuild.js`, `ServerCode/MoveBuild.js`
- `Assets/_Project/Prefabs/Buildables/*` placeholder prefabs
- Tests under `Assets/_Project/Tests/`

**Modified:**
- `Scripts/Economy/LandRegistryService.cs` (`LandTileEntry.Slots`, slot mutators)
- `Scripts/Economy/BuildPaletteService.cs` (rework)
- `Scripts/Config/EconomyConfig.cs` (`PlotSlotCount`; `MaxBuildLevel = PlotSlotCount`)
- `Scripts/Core/PlanetState.cs` (`EnterLandBuilding`), `Constants.cs` (scene name), FSM/DI registration
- `Scripts/UI/TileInfoModal.cs` ("View Land" button)
- `ServerCode/PlaceBuild.js` (slot-aware rework)
