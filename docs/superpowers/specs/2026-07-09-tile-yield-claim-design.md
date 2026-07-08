# Tile Yield Claim — Live Estimate & Claim Button

**Date:** 2026-07-09
**Status:** Approved for planning

## Problem

M3 shipped a complete, server-authoritative yield pipeline — `ServerCode/ClaimYield.js` computes accrued
coins from a uniform base rate plus a per-build-level bonus and a per-visit bonus, and
`Economy/YieldService.ClaimYieldAsync` calls it and updates the wallet — but nothing in the UI ever
triggers it. There is no Claim button anywhere. Separately, `TileInfoModal` already renders
`$"Yield {tile.YieldRate:0.0}/hr"`, but `TileData.YieldRate` is never assigned anywhere in the
codebase, so this always displays `0.0/hr` regardless of the tile's real, server-tracked rate.

This ships the missing piece: a live "coins ready to claim" estimate and a Claim button on tiles the
player owns, wired through the same request/handler/completed-event pattern the purchase and sell flows
already use, plus a small coin count-up so claiming feels rewarding.

## Explicitly out of scope

- Any change to the yield **formula** itself. `BASE_YIELD_PER_TILE_PER_HOUR`, `BUILD_LEVEL_YIELD_MULTIPLIER`,
  `VISIT_YIELD_BONUS`, `MAX_YIELD_ACCRUAL_HOURS`, `MAX_VISIT_COUNT` are all unchanged.
- Activating `ItemDefinition.YieldBonus` (decorations). It stays unread by the formula, same as today —
  a future task if/when decorations become their own placeable category distinct from build levels.
- A HUD-level "claim all owned tiles" button. Claim is per-tile, from `TileInfoModal`, this pass only.
- A world-space "yield ready" indicator on the hexasphere itself (glow/icon). Out of scope — this pass is
  modal-only.
- Redesigning `TileData.BuildLevel`/`LandTileEntry.BuildLevel` sync (unchanged, already correct).

## Data & calculation layer

New pure class, `Economy/YieldEstimateCalculator.cs` — no MonoBehaviour, no backend calls, mirrors
`ClaimYield.js`'s formula exactly so the client estimate and the server grant never disagree on structure
(only on the clock they read `now` from):

```csharp
namespace SocialUniverse.Economy
{
    public readonly struct YieldEstimate
    {
        public readonly int   AccruedCoins;
        public readonly float RatePerHour;
        public YieldEstimate(int accruedCoins, float ratePerHour) { AccruedCoins = accruedCoins; RatePerHour = ratePerHour; }
    }

    public class YieldEstimateCalculator
    {
        public YieldEstimate Compute(LandTileEntry entry, EconomyConfig config, long nowUnixMs)
        {
            float elapsedHours = Mathf.Min((nowUnixMs - entry.LastYieldClaimTs) / 3600000f, config.MaxYieldAccrualHours);
            float buildBonus   = entry.BuildLevel * config.BuildLevelYieldMultiplier;
            float visitBonus   = Mathf.Min(entry.VisitCount, config.MaxVisitCount) * config.VisitYieldBonus;
            float rate         = config.BaseYieldPerTilePerHour * (1f + buildBonus + visitBonus);
            int   accrued      = Mathf.FloorToInt(rate * elapsedHours);
            return new YieldEstimate(accrued, rate);
        }
    }
}
```

Inputs come from data that already exists: `LandRegistryService.GetEntry(tileId)` (→ `LastYieldClaimTs`,
`BuildLevel`, `VisitCount`) and the injected `EconomyConfig`. No new state, no new server calls.

`TileData.YieldRate` is deleted — it was never assigned, and the calculator is now the single source of
truth for any yield-rate display.

## Claim request/response flow

New events, declared alongside the existing tile events at the top of `World/HexasphereManager.cs`:

```csharp
public class TileYieldClaimRequestedEvent { public TileData Tile; }
public class TileYieldClaimCompletedEvent { public TileData Tile; public bool Success; public int Granted; public string FailureReason; }
```

New `App/YieldClaimHandler.cs`, structurally identical to `LandSaleHandler`:

1. `Start()`/`Dispose()` subscribe/unsubscribe to `TileYieldClaimRequestedEvent`.
2. On request: guard `tile.State == OwnedByPlayer` — if not, log a warning and publish a failure completion
   without calling the server (mirrors `LandSaleHandler`'s ownership guard).
3. Calls `YieldService.ClaimYieldAsync(tile.TileId, planet.name)`.
4. On `result.Success`: publishes `TileYieldClaimCompletedEvent { Tile, Success = true, Granted = result.Granted }`.
5. On failure (including a caught exception — see below): publishes `TileYieldClaimCompletedEvent { Tile, Success = false, FailureReason }`.

Registered in `PlanetSceneScope` as `builder.RegisterEntryPoint<YieldClaimHandler>();`, next to
`LandSaleHandler`.

**Bug fix included in this change:** `YieldService.ClaimYieldAsync` currently calls `_backend.CallAsync`
with no try/catch, unlike `LandSaleService.SellAsync` (fixed for this exact reason in the immediately
preceding commit on this branch). A network exception here would currently propagate unhandled up through
`YieldClaimHandler`. This gets the same fix: catch, log via `SULog.Error`, return
`new YieldClaimResult { Success = false, Reason = "Network error" }`.

## `TileInfoModal` changes

New serialized refs: `TMP_Text _yieldText`, `Button _claimButton`. New injects:
`LandRegistryService`, `EconomyConfig`, `YieldEstimateCalculator`.

- `_yieldText` and `_claimButton` are shown only when `tile.State == OwnedByPlayer` (mirrors the existing
  `_sellButton` visibility rule) — visitors to another player's tile or a landmark see no yield UI at all.
- `Open(TileData tile)`: calls `CancelInvoke(nameof(RefreshYieldEstimate))` first (guards against a
  double-subscription if `Open` is called again — e.g. reselecting the same tile — while a previous
  ticking loop is still running). If owned by the player, calls `RefreshYieldEstimate()` immediately, then
  starts `InvokeRepeating(nameof(RefreshYieldEstimate), 1f, 1f)` so the estimate visibly climbs once per
  second while the modal is open. `Close()` also calls `CancelInvoke(nameof(RefreshYieldEstimate))`.
- `RefreshYieldEstimate()`: `var entry = _landRegistryService.GetEntry(tile.TileId); var estimate = _calculator.Compute(entry, _economyConfig, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()); _yieldText.text = $"{estimate.AccruedCoins} coins ready · {estimate.RatePerHour:0.0}/hr";`. No-ops (returns early) if `entry` is null — defensive only; an owned tile is always expected to have a registry entry.
- `OnClaimClicked()`: `SetBusy(true)`, `_statusText.text = "Claiming…"`, publishes `TileYieldClaimRequestedEvent { Tile = _currentTile }`.
- New subscription (`OnEnable`/`OnDisable`, same pattern as `TileSaleCompletedEvent`) to
  `TileYieldClaimCompletedEvent`, ignoring events for a tile other than `_currentTile`:
  - Success: `_statusText.text = $"+{e.Granted} coins!"`, `SetBusy(false)`, `RefreshYieldEstimate()` (the
    registry entry was reset server-side and optimistically client-side by `YieldService`, so this
    immediately shows `0 coins ready`). Does **not** close the modal — unlike Sell, claiming isn't
    terminal; the player may want to keep watching the estimate climb or check other tiles next.
  - Failure: `_statusText.text = $"Claim failed: {e.FailureReason}"`, `SetBusy(false)`.
- `SetBusy(bool busy)` gains `_claimButton.interactable = !busy;` alongside the existing sell/close toggles.
- The old always-`0.0` `_tileStatsText` yield fragment is removed; `_tileStatsText` now reads
  `$"Build level {tile.BuildLevel}"` only, with rate/accrued moved to the new `_yieldText`.

## Coin count-up feedback

`UI/CurrencyView.SetCoins(int amount)` currently snaps `_coinsText.text` instantly. Changed to: if
`amount > _displayedCoins`, start a coroutine that lerps a running integer from `_displayedCoins` to
`amount` over `0.5s` (simple `Mathf.RoundToInt(Mathf.Lerp(...))` per frame), updating `_coinsText.text`
each step; if `amount <= _displayedCoins` (a spend, or the initial `Bind` snapshot), set instantly as
today — count-up is only for gains. A running coroutine is stopped and restarted if `SetCoins` is called
again before the previous animation finishes (e.g. two rapid claims), snapping to the correct final value
each time so the display never gets stuck mid-animation on a stale target.

This lives in the shared `CurrencyView`, so every bound instance (HUD, shop, land sheet) gets the count-up
for free — not just the yield-claim path.

## Testing / scope boundaries

`YieldEstimateCalculatorTests` (new, EditMode) — table-tests against known `LandTileEntry`/`EconomyConfig`
inputs: zero elapsed time (0 accrued), elapsed time past `MaxYieldAccrualHours` (clamped), zero build
level / zero visits (base rate only), visit count past `MaxVisitCount` (clamped), combined build+visit
bonus. Mirrors the existing `IdleMiningCalculatorTests` style (pure function, no mocks needed).

Following this branch's established boundary (see `2026-07-08-tile-selection-modals-design.md`),
`YieldClaimHandler` (App-layer event handler) and `TileInfoModal`/`CurrencyView` (UI `MonoBehaviour`s) are
not unit-tested. Verification is manual in-editor:

- Select an owned tile, wait a few seconds → "coins ready" ticks upward once per second.
- Tap Claim → button disables, status shows "Claiming…", then "+N coins!"; wallet balance counts up
  instead of snapping; estimate resets to "0 coins ready".
- Tap Claim again immediately after a claim → shows `0 coins ready` (or whatever accrued in the interim),
  Claim still works.
- Close and reopen the modal on the same tile (or select it twice in a row without closing) → the ticking
  estimate keeps updating once per second, not twice — confirms `CancelInvoke` before each `InvokeRepeating`
  actually prevents the double-subscription.
- Select a tile owned by another player, or a landmark → no yield text, no Claim button (matches existing
  Sell-button visibility rule).
- Simulate a backend exception in `LocalMockBackendClient` (or disconnect) during a claim → status shows
  "Claim failed: Network error" instead of an unhandled exception/stuck busy state.

## Out of scope (recap)

- Yield formula changes, decoration yield bonus activation, claim-all HUD button, world-space ready
  indicator on the hexasphere, `ItemDefinition.YieldBonus` wiring. All explicitly deferred — see top of
  this doc.
