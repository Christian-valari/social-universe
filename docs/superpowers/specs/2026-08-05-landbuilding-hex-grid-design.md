# LandBuilding Hex-Grid Redesign — Design Spec

**Date:** 2026-08-05
**Branch:** `feature/land-building-mode` (extends the in-progress, unmerged LandBuilding feature)
**Supersedes:** the 8-slot UI-plot model from `2026-08-04-land-building-mode-design.md`

## 1. Summary

Replace the abstract 8-slot UI plot with an in-scene **hexatile board**. Each owned land tile opens into a small hex board of **19 hexatiles** (max). The **5 central hexatiles are free**; the surrounding **14 are locked** and unlocked outward by spending coins. Players **drag buildings** from a palette onto unlocked hexatiles (**1 building = 1 hexatile**), drag to rearrange, and tap to remove. The old slot UI, `SlotAnchors`, and abstract slot model are retired.

This is a substantial extension of the LandBuilding feature but a single coherent piece of work with one spec.

## 2. Decisions (locked in)

| Topic | Decision |
|---|---|
| Coin sinks | **Tiles + buildings.** Unlocking a hexatile costs coins; placing a building costs its `ItemDefinition.Cost`. Move/remove are free; re-placing costs again. |
| Yield basis | **Buildings placed.** `buildLevel` = number of hexatiles with a building (filled count). Yield scales `+25%/buildLevel` plus each item's `YieldBonus`, unchanged. Unlocking an empty hexatile adds no yield. |
| Remove UX | **Tap a placed building → popup → Remove** (no refund). |
| Tile pricing | **Escalating linear, server-computed:** `price = base + step · (unlockedBeyondFree)`. Expensive defaults: **base 200, step 100** → 200, 300, 400 … 1,500 for the 14th tile. |
| Unlock rule | **Adjacent only.** A locked hexatile may be unlocked only if it neighbors an already-unlocked hexatile (organic outward growth; no islands). |
| Board size | **Max 19 (hexagon radius 2), free 5.** Both `EconomyConfig` tunables. |
| Rendering | **Procedural 3D flat hex board** (per-tile GameObject + collider), not Hexasphere, not pure-UI. |

## 3. Board geometry & indexing

The board is a **hexagon of radius 2 = 19 cells**, generated procedurally from axial coordinates by a pure `HexBoardMath` utility.

**Canonical index order (spiral):** index 0 = center; indices 1–6 = ring 1 (clockwise from a fixed start); indices 7–18 = ring 2. This ordering is deterministic and **mirrored on the server**, so `Slots[i]` / `Unlocked[i]` mean the same cell everywhere.

- **Free set** = indices **0–4** (center + the first 4 ring-1 cells) — a contiguous central cluster.
- **Purchasable** = indices **5–18**.

`HexBoardMath` exposes (all pure, unit-tested):
- `Cells(radius)` → ordered list of axial coords in canonical order.
- `LocalPositions(radius, size)` → per-index local-space positions for laying out cells on the plot.
- `Neighbors(radius)` → per-index array of adjacent indices (the topology used for the adjacency rule; mirrored server-side).
- `FreeIndices(freeCount)` → the initially-unlocked indices (0…freeCount-1).

Conceptual layout (F = free, · = locked):

```
      ·   ·   ·
    ·   F   F   ·
  ·   F   F   F   ·
    ·   F   ·   ·
      ·   ·   ·
```

## 4. Interactions

| Action | Input | Cost | Server call |
|---|---|---|---|
| Unlock locked tile | Tap a locked tile adjacent to an unlocked one → confirm popup | coins (server-priced) | `PurchaseHexatile` |
| Place building | Drag from palette onto an unlocked **empty** tile | building `Cost` | `PlaceBuild` |
| Move building | Drag a building tile → another unlocked **empty** tile | free | `MoveBuild` |
| Remove building | Tap a placed building → popup → Remove | free (no refund) | `RemoveBuild` |

**Tap vs drag:** a press that doesn't move past a threshold is a tap (→ purchase popup on a locked tile, remove popup on an occupied tile); a press that drags is place/move. Empty unlocked tiles do nothing on tap.

**Visitor (view) mode:** board renders unlocked tiles + buildings read-only; locked tiles are shown dimmed but non-interactive; no palette, no popups.

## 5. Data model & schema

`LandTileEntry` (in the shared `land_registry` Cloud Save custom data) gains an unlocked mask alongside the widened building array:

```
LandTileEntry {
  OwnerId, LastYieldClaimTs, LastUpkeepTs, VisitCount,  // unchanged
  BuildLevel   // = filled count of Slots (buildings), unchanged semantics
  Slots     : string[19]   // buildingId per hex index, null = empty (widened 8 -> 19)
  Unlocked  : bool[19]     // NEW: true = hexatile unlocked; free indices default true
}
```

Flow of the new `Unlocked` data: `GetLandRegistry` (returns it) → `LandRegistryService.LandTileEntry` → `TileInfoModal` → `LandBuildingHandoff` (gains `Unlocked[]`) → the scene.

**Migration:** pre-launch, no real data to preserve. Legacy `slots[8]` entries are dropped; on first open/write in the new model the entry is initialized with `Unlocked = FreeIndices` and `Slots = new string[19]`, and `BuildLevel` recomputed.

## 6. Server functions (`ServerCode/`)

Registry keyed as today by `planet.name.toLowerCase()`, per-tile by `tileId`. All currency ops use the **correct** Economy SDK pattern (`getPlayerCurrencies` to read; `decrementPlayerCurrencyBalance` with a `configAssignmentHash` from `ConfigurationApi`) — as fixed in `PurchaseLand`/`PlaceBuild`.

- **`PurchaseHexatile.js` (new).** Params: `tileId, planetId, hexIndex`. Validates: caller owns the tile; `hexIndex` in range and currently **locked**; `hexIndex` is **adjacent** to an unlocked hex (via a neighbor map mirrored from `HexBoardMath`); the free set is treated as always-unlocked. Computes `price = base + step·(unlockedCount − FREE_COUNT)` **server-side** (config constants mirrored), checks balance, deducts, sets `Unlocked[hexIndex] = true`, persists. Returns `{ success, newBalance, unlockedCount }`. No client-trusted price.
- **`PlaceBuild.js`.** Re-keyed slot→hex index. Adds a check that `Unlocked[hexIndex]` is true (else `TILE_LOCKED`); still requires the hex empty. Item placement cost remains client-supplied (pre-existing `PurchaseLand`/`PlaceBuild` tampering debt — explicitly **out of scope** here).
- **`RemoveBuild.js`.** Re-keyed to hex index; clears `Slots[hexIndex]`, recomputes `BuildLevel`.
- **`MoveBuild.js`.** Re-keyed to hex indices; validates `fromHex` occupied and `toHex` unlocked + empty.

**Shared constants** (board radius, free count, hexatile base/step, neighbor topology) are duplicated between client `HexBoardMath`/`EconomyConfig` and the server functions, with a comment on each side pointing at the other (same pattern as the existing `SLOT_COUNT` mirror).

## 7. Client components (LandBuilding scene, `SocialUniverse.UI` / `.App`)

- **`PlotHexBoard`** — builds the 19 cells from `HexBoardMath` at scene start (from the handoff's radius/free config), sets each cell's visual state: **locked** (dimmed + lock icon), **unlocked-empty** (plain), **occupied** (instantiates `ItemDefinition.Prefab`). Exposes per-cell anchor/refresh so single cells update after an edit.
- **`PlotBoardInputController`** — raycasts pointer taps and drags to cells and routes: tap-locked → purchase flow; tap-occupied → remove flow; drag-from-palette → place; drag-occupied → move. Touch + mouse.
- **`LandBuildPaletteView`** (reworked) — a **drag-source** list of affordable buildings (uses existing `BuildPaletteService.GetAvailableItems`). Hidden in view mode.
- **Popups** — small in-scene confirm modals: purchase-hexatile (shows server-authoritative price on open) and remove-building.
- **`LandBuildService`** (client) — gains `PurchaseHexatileAsync`; existing `Place/Remove/Move` switch their index argument from slot to hex. On success the scene applies the change locally (update `Slots`/`Unlocked`, refresh coins from `newBalance`, refresh the affected cell) without a full re-fetch.
- **Retired:** `SlotAnchors`, the slot-tap overlay, `PlotSlotCount`, and `LandBuildMath`'s slot-centric helpers (folded into `HexBoardMath`/filled-count).

## 8. Config (`EconomyConfig`)

Add: `HexBoardRadius` (2), `FreeHexCount` (5), `HexatileBasePrice` (200), `HexatilePriceStep` (100). `MaxBuildLevel` becomes the hex count (19). Remove `PlotSlotCount`.

## 9. Testing

**EditMode (unit):**
- `HexBoardMath`: cell count/order for radius 2, neighbor symmetry, free-set, local-position determinism.
- Server price formula & adjacency validation (pure helpers mirrored/covered client-side).
- `buildLevel` = filled count after place/remove/move.
- `LandBuildingHandoff` carries `Unlocked[]` + `Slots[]`.
- `LandBuildService` place/remove/move/purchase against a fake `IBackendClient` (success + failure paths).

**Manual / PlayMode:** board generation & layout, drag-to-place/move, tap-to-purchase/remove, view-mode read-only, full round-trip (own tile → unlock → place → Back → yield/buildLevel reflected).

## 10. Out of scope / tracked debt

- Item **placement** price tampering (client-supplied `cost`) — pre-existing across `PurchaseLand`/`PlaceBuild`; fix later with a server item-price catalog. The new `PurchaseHexatile` does **not** inherit it.
- No refunds on remove or on selling a decorated tile beyond existing land resale.
- Server deploy of the reworked/added Cloud Code (`PurchaseHexatile`, `PlaceBuild`, `RemoveBuild`, `MoveBuild`) is user-owned, as with the current feature.
