# Land Building — Juice / Feedback Effects (Starter Set)

**Date:** 2026-08-10
**Branch:** `feature/land-building-mode`
**Status:** Design — awaiting review
**Milestone:** Land Building (post-M5 feature work)

## Goal

The Land Building feature currently mutates the board with zero transitions:
buildings pop into existence instantly (`PlotHexBoard.SetCell` → `Instantiate`),
removal is an instant `Destroy`, the build-level readout snaps, and the drag ghost
is one fixed colour regardless of whether the drop target is valid. This makes a
core creative loop feel flat.

This spec adds a **starter set of five feedback ("juice") effects** to make placing,
removing, and progressing feel tactile and responsive, without new art dependencies.

### In scope (the starter set)

1. **Scale-punch pop-in on place** — buildings grow from nothing with an overshoot.
2. **Scale-down poof on remove** — buildings shrink away instead of vanishing.
3. **Animated build-level** — slider lerps and the level text punches on increase.
4. **Drag ghost valid/invalid tint** — green over a valid empty tile, red otherwise.
5. **SFX layer** — place / remove / invalid-drop sounds via the existing audio system.

### Out of scope (explicitly deferred)

- Particle systems (dust puffs, unlock sparkles, confetti) — a later art pass.
- Camera shake and haptics.
- Unlock/purchase material dissolve and neighbour shimmer.
- Coin fly-off to the currency counter.
- Max-level (19/19) celebration banner and milestone pulses.

These are good next steps once the starter set lands; they are noted so the
architecture leaves room for them but are not built now.

## Key Technical Decision: coroutines, not DOTween

DOTween Pro is present in the project (`Assets/Plugins/Demigiant/`) and used in the
`ActiveMining` scene, **but it has no assembly definition** — it compiles into the
predefined `Assembly-CSharp`. The gameplay scripts live in asmdef assemblies
(`SocialUniverse.UI`, etc.), which **cannot reference `Assembly-CSharp`**. This is
why `CurrencyView` already animates its coin count-up with a hand-rolled
`Coroutine` + `Mathf.Lerp` rather than DOTween.

**Decision:** implement all effects with **manual coroutines and pure easing math**,
matching the existing `CurrencyView` convention. This avoids generating DOTween
asmdefs (a fragile, editor-side operation) and keeps the effects self-contained and
unit-testable at the math layer.

## Architecture

### New unit: `BuildFeedback` (static helper, `SocialUniverse.UI`)

A stateless static class of coroutine primitives plus the easing math. It owns **all**
feel constants so tuning happens in one file. It does not run coroutines itself — the
calling `MonoBehaviour` (`PlotHexBoard` / `LandBuildPaletteView`) starts them, exactly
as `CurrencyView` starts its own.

```
public static class BuildFeedback
{
    // Feel constants (durations in seconds).
    public const float PopInDuration    = 0.25f;
    public const float PoofOutDuration  = 0.18f;
    public const float PunchDuration    = 0.20f;
    public const float SliderDuration   = 0.30f;
    public const float PunchScale       = 0.25f;  // +25% at peak
    public const float BackOvershoot    = 1.70158f; // standard OutBack constant

    // Grows `t` from zero to `targetScale` with an OutBack overshoot.
    public static IEnumerator PopIn(Transform t, Vector3 targetScale);

    // Shrinks `go` to zero with an InBack anticipation, then destroys it.
    // onComplete fires after Destroy (may be null).
    public static IEnumerator PoofOut(GameObject go, Action onComplete = null);

    // Momentary overshoot-and-settle on `t.localScale` (for the level text).
    public static IEnumerator PunchScale(Transform t, Vector3 baseScale);

    // Lerps a Slider's value from its current value to `to`.
    public static IEnumerator AnimateSlider(Slider s, float to);

    // Pure easing — unit-tested.
    public static float EaseOutBack(float x); // x in [0,1]; overshoots >1 near the end
    public static float EaseInBack(float x);  // x in [0,1]; dips <0 near the start
}
```

Guarantees for the easing functions (the test contract):
- `EaseOutBack(0) == 0`, `EaseOutBack(1) == 1`, and `EaseOutBack(x) > 1` for some `x` in `(0,1)` (the overshoot).
- `EaseInBack(0) == 0`, `EaseInBack(1) == 1`, and `EaseInBack(x) < 0` for some `x` in `(0,1)` (the anticipation dip).

### Effect 1 — Pop-in on place (`PlotHexBoard`)

`SetCell` gains an optional `bool animate = false`:

```
public void SetCell(int index, bool unlocked, string itemId, bool animate = false)
```

- When `animate && state == Occupied`, after instantiating the building it captures the
  prefab's intended `localScale`, sets the instance to `Vector3.zero`, and
  `StartCoroutine(BuildFeedback.PopIn(instance.transform, targetScale))`.
- `Build()` (initial scene load) calls `SetCell(..., animate: false)` so existing
  buildings do **not** all pop on entering the scene.
- Only the interactive placement paths pass `animate: true`.

### Effect 2 — Poof on remove (`PlotHexBoard`)

New method:

```
public void PlayRemove(int index)
```

Finds the current occupied child under the cell's `Anchor` and
`StartCoroutine(BuildFeedback.PoofOut(child))`, which shrinks then destroys it. The
logical slot is already cleared server-side by the time this is called, so the board
only needs to animate the existing visual away (it no longer instant-`Destroy`s via
`SetCell(..., null)`). No-op if the cell has no building.

### Effect 3 — Animated build-level (`LandBuildPaletteView`)

`UpdateBuildLevel` gains `bool animate`:

```
private void UpdateBuildLevel(bool animate = false)
```

- Text is set immediately; when `animate` and the level **increased**, start
  `BuildFeedback.PunchScale(_buildLevelText.transform, baseScale)`.
- Slider: when `animate`, start `BuildFeedback.AnimateSlider(_buildLevelBar, level)`;
  otherwise set `value` instantly.
- `Start()` calls `UpdateBuildLevel(animate: false)` (no fill-up on load); the place and
  remove paths call `UpdateBuildLevel(animate: true)`.
- The view caches the level text's base scale once (in `Start`) so repeated punches
  always settle back to the correct size, and tracks the last level to detect increases.

### Effect 4 — Drag ghost valid/invalid tint (`PaletteItemDragHandler`)

`Init` is extended:

```
public void Init(Camera cam, GameObject previewPrefab,
                 Material validMaterial, Material invalidMaterial,
                 float groundY, Func<int,bool> isValidTarget, Action<int> onDrop)
```

- The ghost material is chosen **per frame** in `PositionGhost`: when the pointer is over
  a `HexCell` and `isValidTarget(cell.Index)` is true → `validMaterial`; otherwise (locked
  cell, occupied cell, or off-board) → `invalidMaterial`.
- Material is only re-applied to the ghost renderers when it changes (track the current
  material) to avoid per-frame churn.
- `isValidTarget` is supplied by `LandBuildPaletteView`:
  `hex => hex >= 0 && _unlocked[hex] && string.IsNullOrEmpty(_slots[hex])` — the same
  predicate `PlaceFromPalette` already enforces, so the ghost colour never lies about
  where a drop will succeed.
- `validMaterial` is the existing `_dragGhostMaterial`; a new `_dragGhostInvalidMaterial`
  serialized field on the view supplies the red one. If the invalid material is unassigned,
  the ghost falls back to the valid material (graceful degradation).

### Effect 5 — SFX layer

- `SfxId` gains two entries: `BuildPlace`, `BuildRemove`.
- `LandBuildPaletteView` injects `IAudioManager`. In **production** mode this resolves from
  the `RootLifetimeScope` parent (which registers it). In **standalone** mode (opening
  `LandBuilding.unity` directly, `parentReference.Type == null`) there is no parent, so the
  injection would throw at scene load. **`LandBuildingSceneScope` must therefore register the
  audio stack inside its existing `if (standalone)` branch**, exactly mirroring
  `PlanetSceneScope` (lines 90-94): serialized `AudioConfig` + `AudioCatalog` fallbacks, then
  `builder.Register<AudioManager>(Lifetime.Singleton).As<IAudioManager>()`. `AudioManager`'s
  constructor needs only `AudioConfig` + `AudioCatalog` — `AudioSettingsService` is **not**
  required (no settings panel in this scene), so it is omitted.
- Trigger points:
  - `PlaceFromPalette` success → `PlaySfx(BuildPlace)`.
  - `OnBuildingDragged` (move) success → `PlaySfx(BuildPlace)`.
  - `Remove` success → `PlaySfx(BuildRemove)`.
  - Invalid drop / can't-afford / failed server call → `PlaySfx(Cancel)` (existing id).
- No unlock/purchase SFX in this pass (out of scope); the `Cancel`/`BuildPlace` ids cover
  the starter interactions.

## Data flow (place, as the representative case)

1. User drags a palette item; `PaletteItemDragHandler` shows a ghost tinted valid/invalid
   via `isValidTarget`.
2. On drop over a valid cell, the handler reports `hexIndex` to
   `LandBuildPaletteView.PlaceFromPalette`.
3. View validates + calls `LandBuildService.PlaceAsync` (server-authoritative — unchanged).
4. On success: update local `_slots`, then `_board.SetCell(hex, true, itemId, animate: true)`
   (pop-in), `PlaySfx(BuildPlace)`, and `UpdateBuildLevel(animate: true)` (slider lerp +
   text punch).
5. On failure/invalid: `SetStatus(...)` + `PlaySfx(Cancel)`; no board change.

The economy/authority path is untouched — this spec only adds presentation on top of the
existing success/failure branches.

## Files touched

| File | Change |
|---|---|
| `Assets/_Project/Scripts/UI/BuildFeedback.cs` | **New.** Coroutine primitives + easing math + feel constants. |
| `Assets/_Project/Scripts/UI/PlotHexBoard.cs` | `SetCell` gains `animate`; new `PlayRemove`. |
| `Assets/_Project/Scripts/UI/LandBuildPaletteView.cs` | Inject `IAudioManager`; animate build-level; SFX; supply `isValidTarget` + invalid material to drag handler; call `PlayRemove` on remove. |
| `Assets/_Project/Scripts/UI/PaletteItemDragHandler.cs` | `Init` gains valid/invalid materials + `isValidTarget`; per-frame tint. |
| `Assets/_Project/Scripts/Config/SfxId.cs` | Add `BuildPlace`, `BuildRemove`. |
| `Assets/_Project/Scripts/App/LandBuildingSceneScope.cs` | In the `standalone` branch, register `AudioConfig` + `AudioCatalog` + `AudioManager` (as `IAudioManager`) so SFX resolve when the scene is opened directly. |
| `Assets/_Project/Tests/EditMode/UI/BuildFeedbackTests.cs` | **New.** EditMode tests for `EaseOutBack`/`EaseInBack`. |

## Testing

- **Unit (EditMode, TDD):** `EaseOutBack` / `EaseInBack` boundary + overshoot/dip
  guarantees listed above. These are pure functions and are the only cleanly unit-testable
  part.
- **Manual playtest (owner + visitor):**
  - Place a building → it pops in with overshoot + `BuildPlace` sound.
  - Remove a building → it shrinks away + `BuildRemove` sound.
  - Drag over locked/occupied/off-board → ghost is red; over valid empty → green.
  - Drop on invalid → `Cancel` sound, no placement.
  - Build-level slider eases up and the text punches when a building is added.
  - Enter scene with existing buildings → **no** mass pop-in, slider is already filled.

## User-owned follow-ups (not code)

- Assign audio clips for `SfxId.BuildPlace` and `SfxId.BuildRemove` in the `AudioManager`
  SFX mapping.
- Create/assign a red **invalid** drag-ghost material (e.g. `TransparentRed.mat`) to
  `LandBuildPaletteView._dragGhostInvalidMaterial` in `LandBuilding.unity`.
- Playtest pass on device (mobile is the target platform).

## Open questions

- None blocking. Feel constants are first-pass values in `BuildFeedback` and can be tuned
  after a playtest.
