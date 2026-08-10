# Land Building Juice / Feedback Effects — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add five feedback ("juice") effects to the Land Building feature — pop-in on place, poof on remove, animated build-level, drag-ghost valid/invalid tint, and an SFX layer — so the core build loop feels tactile.

**Architecture:** All motion is done with **manual coroutines + pure easing math** in one new static helper (`BuildFeedback`), matching the existing `CurrencyView` convention. DOTween is deliberately avoided (it has no asmdef and this assembly can't reference it). The economy/server-authoritative path is untouched; effects only decorate the existing success/failure branches. Effects are wired into `PlotHexBoard` (spawn/despawn) and `LandBuildPaletteView` (orchestration), with drag tint in `PaletteItemDragHandler`.

**Tech Stack:** Unity 6 (URP), C#, VContainer DI, coroutines, uGUI/TMP, Unity Test Framework (NUnit EditMode), `IAudioManager`/`SfxId` audio system.

## Global Constraints

- **No DOTween.** Use coroutines + `Mathf` easing only (asmdef barrier — `SocialUniverse.UI` cannot reference `Assembly-CSharp`).
- **Namespaces mirror folders:** new UI code is `SocialUniverse.UI`; `SfxId` is `SocialUniverse.Config`; scope is `SocialUniverse.App`.
- **Server-authoritative economy untouched:** never move/skip the `LandBuildService.*Async` calls; effects run only after a successful result (or on the existing failure branch for the error sound).
- **Initial scene load must not animate:** entering the scene with existing buildings must NOT pop them all in, and the build-level slider must already be filled (animate only on interactive change).
- **All feel constants live in `BuildFeedback`.** No magic timing numbers scattered across call sites.
- **Graceful optionals:** every new serialized `Material`/audio reference is optional — a null must degrade gracefully, never throw or `NullReferenceException`.
- **Verify compilation after every script change** via `read_console` (types are unusable until the domain reload finishes; poll `editor_state.isCompiling`).

---

### Task 1: `BuildFeedback` easing math + coroutine primitives

The foundation: pure easing functions (unit-tested) and the coroutine tweens every later task calls. Nothing else depends on scene state, so this is built and tested first in isolation.

**Files:**
- Create: `Assets/_Project/Scripts/UI/BuildFeedback.cs`
- Test: `Assets/_Project/Tests/EditMode/UI/BuildFeedbackTests.cs`

**Interfaces:**
- Consumes: nothing (leaf utility).
- Produces (later tasks rely on these exact signatures):
  - `IEnumerator BuildFeedback.PopIn(Transform t, Vector3 targetScale)`
  - `IEnumerator BuildFeedback.PoofOut(GameObject go, Action onComplete = null)`
  - `IEnumerator BuildFeedback.PunchScale(Transform t, Vector3 baseScale)`
  - `IEnumerator BuildFeedback.AnimateSlider(Slider s, float to)`
  - `float BuildFeedback.EaseOutBack(float x)` / `float BuildFeedback.EaseInBack(float x)`

- [ ] **Step 1: Write the failing test**

Create `Assets/_Project/Tests/EditMode/UI/BuildFeedbackTests.cs`:

```csharp
using NUnit.Framework;
using SocialUniverse.UI;

namespace SocialUniverse.Tests
{
    public class BuildFeedbackTests
    {
        [Test] public void EaseOutBack_is_zero_at_start() =>
            Assert.AreEqual(0f, BuildFeedback.EaseOutBack(0f), 1e-4f);

        [Test] public void EaseOutBack_is_one_at_end() =>
            Assert.AreEqual(1f, BuildFeedback.EaseOutBack(1f), 1e-4f);

        [Test] public void EaseOutBack_overshoots_above_one_near_end() =>
            Assert.Greater(BuildFeedback.EaseOutBack(0.8f), 1f);

        [Test] public void EaseInBack_is_zero_at_start() =>
            Assert.AreEqual(0f, BuildFeedback.EaseInBack(0f), 1e-4f);

        [Test] public void EaseInBack_is_one_at_end() =>
            Assert.AreEqual(1f, BuildFeedback.EaseInBack(1f), 1e-4f);

        [Test] public void EaseInBack_dips_below_zero_near_start() =>
            Assert.Less(BuildFeedback.EaseInBack(0.2f), 0f);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run EditMode tests filtered to `BuildFeedbackTests` (Unity Test Runner, or MCP `run_tests` with `mode: EditMode`, `test_filter: BuildFeedbackTests`).
Expected: FAIL to compile — `BuildFeedback` does not exist yet.

- [ ] **Step 3: Write the implementation**

Create `Assets/_Project/Scripts/UI/BuildFeedback.cs`:

```csharp
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace SocialUniverse.UI
{
    // Coroutine-based feedback ("juice") primitives for the Land Building board. Manual coroutines +
    // pure easing instead of DOTween: DOTween has no asmdef (it lives in Assembly-CSharp) and this
    // assembly can't reference it — the same reason CurrencyView hand-rolls its coin count-up. All
    // feel constants live here so tuning happens in one place. Callers (PlotHexBoard /
    // LandBuildPaletteView) own the StartCoroutine, exactly like CurrencyView does.
    public static class BuildFeedback
    {
        public const float PopInDuration   = 0.25f;
        public const float PoofOutDuration = 0.18f;
        public const float PunchDuration   = 0.20f;
        public const float SliderDuration  = 0.30f;
        public const float PunchAmount     = 0.25f;    // +25% at the punch peak
        private const float BackConst      = 1.70158f; // standard "back" ease overshoot constant

        // Grows from zero to targetScale with an overshoot (OutBack). Bails safely if the
        // transform is destroyed mid-tween (e.g. the tile is rebuilt).
        public static IEnumerator PopIn(Transform t, Vector3 targetScale)
        {
            if (t == null) yield break;
            t.localScale = Vector3.zero;
            float elapsed = 0f;
            while (elapsed < PopInDuration)
            {
                elapsed += Time.deltaTime;
                if (t == null) yield break;
                float k = EaseOutBack(Mathf.Clamp01(elapsed / PopInDuration));
                t.localScale = targetScale * k;
                yield return null;
            }
            if (t != null) t.localScale = targetScale;
        }

        // Shrinks to zero with a small anticipation grow (InBack), then destroys the object.
        public static IEnumerator PoofOut(GameObject go, Action onComplete = null)
        {
            if (go != null)
            {
                Transform t = go.transform;
                Vector3 start = t.localScale;
                float elapsed = 0f;
                while (elapsed < PoofOutDuration)
                {
                    elapsed += Time.deltaTime;
                    if (go == null) break;
                    float k = 1f - EaseInBack(Mathf.Clamp01(elapsed / PoofOutDuration));
                    t.localScale = start * Mathf.Max(0f, k);
                    yield return null;
                }
                if (go != null) UnityEngine.Object.Destroy(go);
            }
            onComplete?.Invoke();
        }

        // Momentary overshoot-and-settle back to baseScale (a sin arch: 0 -> peak -> 0).
        public static IEnumerator PunchScale(Transform t, Vector3 baseScale)
        {
            if (t == null) yield break;
            float elapsed = 0f;
            while (elapsed < PunchDuration)
            {
                elapsed += Time.deltaTime;
                if (t == null) yield break;
                float p = Mathf.Clamp01(elapsed / PunchDuration);
                float bump = Mathf.Sin(p * Mathf.PI) * PunchAmount;
                t.localScale = baseScale * (1f + bump);
                yield return null;
            }
            if (t != null) t.localScale = baseScale;
        }

        // Lerps a Slider's value from its current value to `to` over SliderDuration.
        public static IEnumerator AnimateSlider(Slider s, float to)
        {
            if (s == null) yield break;
            float from = s.value;
            float elapsed = 0f;
            while (elapsed < SliderDuration)
            {
                elapsed += Time.deltaTime;
                if (s == null) yield break;
                s.value = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / SliderDuration));
                yield return null;
            }
            if (s != null) s.value = to;
        }

        // Overshoots above 1 near the end, settling to 1. EaseOutBack(0)=0, EaseOutBack(1)=1.
        public static float EaseOutBack(float x)
        {
            const float c1 = BackConst;
            const float c3 = c1 + 1f;
            float xm1 = x - 1f;
            return 1f + c3 * xm1 * xm1 * xm1 + c1 * xm1 * xm1;
        }

        // Dips below 0 near the start, arriving at 1. EaseInBack(0)=0, EaseInBack(1)=1.
        public static float EaseInBack(float x)
        {
            const float c1 = BackConst;
            const float c3 = c1 + 1f;
            return c3 * x * x * x - c1 * x * x;
        }
    }
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run EditMode tests filtered to `BuildFeedbackTests`.
Expected: 6 PASS. Also confirm `read_console` shows no compile errors.

- [ ] **Step 5: Commit**

```bash
git add Assets/_Project/Scripts/UI/BuildFeedback.cs Assets/_Project/Tests/EditMode/UI/BuildFeedbackTests.cs
git commit -m "feat(landbuild): BuildFeedback easing + coroutine juice primitives"
```

---

### Task 2: Pop-in on place (`PlotHexBoard.SetCell` animate flag)

Wire the pop-in into building spawn, keeping the initial `Build()` instant. Verified by compile + manual playtest (no unit test — it's a visual/coroutine effect on instantiated prefabs).

**Files:**
- Modify: `Assets/_Project/Scripts/UI/PlotHexBoard.cs`
- Modify: `Assets/_Project/Scripts/UI/LandBuildPaletteView.cs`

**Interfaces:**
- Consumes: `BuildFeedback.PopIn(Transform, Vector3)` (Task 1).
- Produces: `PlotHexBoard.SetCell(int index, bool unlocked, string itemId, bool animate = false)` — the new optional `animate` parameter; existing 3-arg callers keep working via the default.

- [ ] **Step 1: Add the `animate` parameter and pop-in in `PlotHexBoard.SetCell`**

In `Assets/_Project/Scripts/UI/PlotHexBoard.cs`, change the signature and the occupied branch:

```csharp
public void SetCell(int index, bool unlocked, string itemId, bool animate = false)
{
    if (index < 0 || index >= _cells.Count) return;
    var cell  = _cells[index];
    var state = HexCellVisual.Resolve(unlocked, itemId);
    cell.SetLockVisual(state == HexCellVisual.State.Locked, _lockedMat, _unlockedMat);

    for (int c = cell.Anchor.childCount - 1; c >= 0; c--) Destroy(cell.Anchor.GetChild(c).gameObject);
    if (state == HexCellVisual.State.Occupied)
    {
        var item = _registry.GetItem(itemId);
        if (item != null && item.Prefab != null)
        {
            var instance = Instantiate(item.Prefab, cell.Anchor.position, cell.Anchor.rotation, cell.Anchor);
            if (animate)
                StartCoroutine(BuildFeedback.PopIn(instance.transform, instance.transform.localScale));
        }
    }
}
```

Note: `Build()` already calls `SetCell(i, u, item)` with three args — it now implicitly passes `animate: false`, so initial load stays instant. No change needed in `Build()`.

- [ ] **Step 2: Pass `animate: true` from the interactive place path**

In `Assets/_Project/Scripts/UI/LandBuildPaletteView.cs`, in `PlaceFromPalette`, change the board update on success:

```csharp
_slots[hexIndex] = item.ItemId;
if (r.NewBalance >= 0) _localCoins = r.NewBalance;
_board.SetCell(hexIndex, true, item.ItemId, animate: true);
SetStatus("");
BuildPalette();
UpdateBuildLevel();
```

And in `OnBuildingDragged` (a move re-lands the building), animate the destination:

```csharp
_slots[toHex] = _slots[fromHex];
_slots[fromHex] = null;
_board.SetCell(fromHex, true, null);
_board.SetCell(toHex, true, _slots[toHex], animate: true);
SetStatus("");
```

- [ ] **Step 3: Verify compilation**

`read_console` (types: error). Poll `editor_state.isCompiling` until false.
Expected: no errors.

- [ ] **Step 4: Manual verification (playtest)**

Enter Play Mode on `LandBuilding.unity` (owner/`canEdit=true`). Drag a building onto an empty unlocked tile → it grows from nothing with a slight overshoot. Re-enter the scene with an existing building → it does NOT pop (instant). Move a building → it pops at its new tile.

- [ ] **Step 5: Commit**

```bash
git add Assets/_Project/Scripts/UI/PlotHexBoard.cs Assets/_Project/Scripts/UI/LandBuildPaletteView.cs
git commit -m "feat(landbuild): scale-punch pop-in when a building is placed"
```

---

### Task 3: Poof on remove (`PlotHexBoard.PlayRemove`)

Replace the instant destroy on removal with a shrink-away poof.

**Files:**
- Modify: `Assets/_Project/Scripts/UI/PlotHexBoard.cs`
- Modify: `Assets/_Project/Scripts/UI/LandBuildPaletteView.cs`

**Interfaces:**
- Consumes: `BuildFeedback.PoofOut(GameObject, Action)` (Task 1).
- Produces: `PlotHexBoard.PlayRemove(int index)` — animates the current building on the cell out, then destroys it.

- [ ] **Step 1: Add `PlayRemove` to `PlotHexBoard`**

Append to `Assets/_Project/Scripts/UI/PlotHexBoard.cs`:

```csharp
// Poofs the building currently on `index` out of existence (shrink + destroy). The caller has
// already cleared the logical slot server-side, so the cell stays unlocked+empty — only the
// visual needs animating away. No-op if the cell has no building.
public void PlayRemove(int index)
{
    if (index < 0 || index >= _cells.Count) return;
    var anchor = _cells[index].Anchor;
    if (anchor.childCount == 0) return;
    var building = anchor.GetChild(anchor.childCount - 1).gameObject;
    StartCoroutine(BuildFeedback.PoofOut(building));
}
```

- [ ] **Step 2: Call `PlayRemove` from the remove path**

In `Assets/_Project/Scripts/UI/LandBuildPaletteView.cs`, in `Remove`, replace the instant `SetCell(..., null)` with `PlayRemove`:

```csharp
private async void Remove(int hexIndex)
{
    var r = await _buildService.RemoveAsync(_handoff.TileId, _handoff.RegistryPlanetId, hexIndex);
    if (!r.Success) { SetStatus($"Remove failed: {r.Reason}"); return; }

    _slots[hexIndex] = null;
    _board.PlayRemove(hexIndex);
    SetStatus("");
    BuildPalette();
    UpdateBuildLevel();
}
```

- [ ] **Step 3: Verify compilation**

`read_console` (types: error). Expected: no errors.

- [ ] **Step 4: Manual verification (playtest)**

Tap an occupied tile → confirm popup → the building shrinks away (with a tiny anticipation) instead of vanishing instantly. The tile is empty and immediately re-buildable afterward.

- [ ] **Step 5: Commit**

```bash
git add Assets/_Project/Scripts/UI/PlotHexBoard.cs Assets/_Project/Scripts/UI/LandBuildPaletteView.cs
git commit -m "feat(landbuild): scale-down poof when a building is removed"
```

---

### Task 4: Animated build-level (slider lerp + text punch)

Make the build-level readout ease and punch on increase instead of snapping, without animating on scene load.

**Files:**
- Modify: `Assets/_Project/Scripts/UI/LandBuildPaletteView.cs`

**Interfaces:**
- Consumes: `BuildFeedback.PunchScale(Transform, Vector3)`, `BuildFeedback.AnimateSlider(Slider, float)` (Task 1).
- Produces: `LandBuildPaletteView.UpdateBuildLevel(bool animate = false)` — replaces the current no-arg method; all existing internal callers must pass the new flag as specified below.

- [ ] **Step 1: Add base-scale + last-level caching fields**

In `Assets/_Project/Scripts/UI/LandBuildPaletteView.cs`, add near the other private fields (with `_localCoins`, `_unlocked`, `_slots`, `_activeCategory`):

```csharp
private Vector3 _buildLevelTextBaseScale = Vector3.one;
private int     _lastBuildLevel;
```

- [ ] **Step 2: Cache base scale + seed last level in `Start`, before the first `UpdateBuildLevel`**

In `Start`, replace the existing `UpdateBuildLevel();` call (which currently sits just before the `canEdit` early-return) with the seeded, non-animated version:

```csharp
if (_buildLevelText != null) _buildLevelTextBaseScale = _buildLevelText.transform.localScale;
_lastBuildLevel = LandBuildMath.FilledCount(_slots);
UpdateBuildLevel(animate: false);
```

(Everything else in `Start` — the `_board.Build`, the `canEdit` gating, the category-bar hide, the input/palette wiring — stays exactly as-is.)

- [ ] **Step 3: Replace `UpdateBuildLevel` with the animated version**

Replace the whole method:

```csharp
// Build level = number of occupied hexatiles; max = every plot occupied (HexCount, 19).
// Shown to everyone (owner + visitors). `animate` is true only for interactive changes —
// on scene load it is false so the bar is pre-filled and the text doesn't punch.
private void UpdateBuildLevel(bool animate = false)
{
    int level = LandBuildMath.FilledCount(_slots);
    int max   = _config.MaxBuildLevel;

    if (_buildLevelText != null)
    {
        _buildLevelText.text = $"Build Level {level}/{max}";
        if (animate && level > _lastBuildLevel)
            StartCoroutine(BuildFeedback.PunchScale(_buildLevelText.transform, _buildLevelTextBaseScale));
    }
    if (_buildLevelBar != null)
    {
        _buildLevelBar.maxValue = max;
        if (animate) StartCoroutine(BuildFeedback.AnimateSlider(_buildLevelBar, level));
        else         _buildLevelBar.value = level;
    }
    _lastBuildLevel = level;
}
```

- [ ] **Step 4: Animate on interactive place/remove**

Change the two interactive callers to pass `animate: true`. In `PlaceFromPalette` (last line of the success branch):

```csharp
        UpdateBuildLevel(animate: true);
```

In `Remove` (last line):

```csharp
        UpdateBuildLevel(animate: true);
```

(`Purchase` does not change the occupied count, so it keeps calling — or omitting — the default; leave `Purchase` as-is, it does not call `UpdateBuildLevel`.)

- [ ] **Step 5: Verify compilation**

`read_console` (types: error). Expected: no errors.

- [ ] **Step 6: Manual verification (playtest)**

Place a building → slider eases up smoothly and the "Build Level X/Y" text does a quick punch. Remove one → slider eases down (no text punch, since level decreased). Enter the scene with existing buildings → slider is already filled, no animation, text at normal size.

- [ ] **Step 7: Commit**

```bash
git add Assets/_Project/Scripts/UI/LandBuildPaletteView.cs
git commit -m "feat(landbuild): animate build-level slider + punch the level text on increase"
```

---

### Task 5: Drag ghost valid/invalid tint

Tint the drag ghost green over a valid empty tile and red otherwise, using a predicate supplied by the view.

**Files:**
- Modify: `Assets/_Project/Scripts/UI/PaletteItemDragHandler.cs`
- Modify: `Assets/_Project/Scripts/UI/LandBuildPaletteView.cs`

**Interfaces:**
- Consumes: nothing new.
- Produces: `PaletteItemDragHandler.Init(Camera cam, GameObject previewPrefab, Material validMaterial, Material invalidMaterial, float groundY, Func<int,bool> isValidTarget, Action<int> onDrop)` — replaces the old 5-arg `Init`.

- [ ] **Step 1: Rework `PaletteItemDragHandler` to hold two materials + a validity predicate**

Rewrite `Assets/_Project/Scripts/UI/PaletteItemDragHandler.cs`. Change the fields, `Init`, `OnBeginDrag`, and `PositionGhost`; add an `ApplyMaterial` helper. `OnDrag`, `OnEndDrag`, `RaycastCell`, and `RuntimeGhostMaterial` are unchanged.

Replace the field block and `Init`:

```csharp
        private Camera         _camera;
        private GameObject     _previewPrefab;
        private Material        _validMaterial;
        private Material        _invalidMaterial;
        private float          _groundY;
        private Func<int,bool> _isValidTarget;
        private Action<int>    _onDrop;

        private GameObject _ghost;
        private Material   _currentMaterial;  // last material applied to the ghost (avoid per-frame churn)
        private Material   _fallbackMaterial; // lazily-built runtime ghost when nothing is assigned

        public void Init(Camera cam, GameObject previewPrefab, Material validMaterial, Material invalidMaterial,
                         float groundY, Func<int,bool> isValidTarget, Action<int> onDrop)
        {
            _camera          = cam;
            _previewPrefab   = previewPrefab;
            _validMaterial   = validMaterial;
            _invalidMaterial = invalidMaterial;
            _groundY         = groundY;
            _isValidTarget   = isValidTarget;
            _onDrop          = onDrop;
        }
```

Replace `OnBeginDrag` (the material is now chosen per-frame in `PositionGhost`, so drop the old material loop and let the first `PositionGhost` apply it):

```csharp
        public void OnBeginDrag(PointerEventData e)
        {
            if (_previewPrefab == null) return;

            _ghost = Instantiate(_previewPrefab);
            _ghost.name = "DragGhost";

            // Don't let the ghost block the board raycast.
            foreach (var col in _ghost.GetComponentsInChildren<Collider>()) col.enabled = false;

            _currentMaterial = null;   // force the first ApplyMaterial
            PositionGhost(e.position); // positions AND tints
        }
```

Replace `PositionGhost` and add `ApplyMaterial`:

```csharp
        // The ghost always follows the pointer and is tinted valid (green) only when it is over a
        // cell the drop would actually succeed on; otherwise invalid (red) — including off-board.
        private void PositionGhost(Vector2 screen)
        {
            if (_ghost == null || _camera == null) return;
            var ray = _camera.ScreenPointToRay(screen);

            HexCell cell = null;
            if (Physics.Raycast(ray, out var hit, 100f))
            {
                cell = hit.collider.GetComponentInParent<HexCell>();
                _ghost.transform.position = cell != null ? cell.Anchor.position : hit.point;
            }
            else
            {
                var plane = new Plane(Vector3.up, new Vector3(0f, _groundY, 0f));
                if (plane.Raycast(ray, out float enter))
                    _ghost.transform.position = ray.GetPoint(enter);
            }

            bool valid = cell != null && _isValidTarget != null && _isValidTarget(cell.Index);
            ApplyMaterial(valid ? _validMaterial : _invalidMaterial);
        }

        // Applies `mat` to every ghost renderer, only when it changed. Falls back to the valid
        // material, then to a runtime transparent material, if `mat` is null (graceful optional).
        private void ApplyMaterial(Material mat)
        {
            if (mat == null) mat = _validMaterial;
            if (mat == null) mat = _fallbackMaterial ??= RuntimeGhostMaterial();
            if (mat == _currentMaterial || _ghost == null) return;
            _currentMaterial = mat;

            foreach (var r in _ghost.GetComponentsInChildren<Renderer>())
            {
                var mats = new Material[r.sharedMaterials.Length == 0 ? 1 : r.sharedMaterials.Length];
                for (int i = 0; i < mats.Length; i++) mats[i] = mat;
                r.sharedMaterials = mats;
            }
        }
```

Ensure `using System;` is present (it already is) for `Func`/`Action`.

- [ ] **Step 2: Add the invalid-material field + validity predicate to the view**

In `Assets/_Project/Scripts/UI/LandBuildPaletteView.cs`, add a serialized field next to `_dragGhostMaterial`:

```csharp
        [SerializeField] private Material       _dragGhostInvalidMaterial; // red ghost for an invalid drop target (optional)
```

Add the predicate method (near `PlaceFromPalette`):

```csharp
        // A drop is valid on an unlocked, empty tile — the same rule PlaceFromPalette enforces, so
        // the ghost colour never promises a placement that would be rejected.
        private bool IsValidDropTarget(int hex) =>
            hex >= 0 && hex < _unlocked.Length && _unlocked[hex] && string.IsNullOrEmpty(_slots[hex]);
```

- [ ] **Step 3: Update the `drag.Init` call in `BuildPalette`**

In `BuildPalette`, replace the existing `drag.Init(...)` line:

```csharp
                    var drag = btn.gameObject.AddComponent<PaletteItemDragHandler>();
                    drag.Init(_camera, captured.Prefab, _dragGhostMaterial, _dragGhostInvalidMaterial,
                              _board.transform.position.y, IsValidDropTarget, hex => PlaceFromPalette(captured, hex));
```

- [ ] **Step 4: Verify compilation**

`read_console` (types: error). Expected: no errors. (The old 5-arg `Init` is fully replaced — confirm no other caller references it via `find_in_file`/grep for `.Init(` on the handler.)

- [ ] **Step 5: Manual verification (playtest)**

Drag a palette item: over an empty unlocked tile the ghost is green (valid material); over a locked tile, an occupied tile, or off the board it is red (invalid material). Dropping on red does nothing (placement already gated); dropping on green places. If `_dragGhostInvalidMaterial` is left unassigned, the ghost simply stays the valid material everywhere (no error).

- [ ] **Step 6: Commit**

```bash
git add Assets/_Project/Scripts/UI/PaletteItemDragHandler.cs Assets/_Project/Scripts/UI/LandBuildPaletteView.cs
git commit -m "feat(landbuild): tint drag ghost green/red by drop-target validity"
```

---

### Task 6: SFX layer

Add place/remove sounds and an invalid-drop sound, plus the standalone audio registration so it works when the scene is opened directly.

**Files:**
- Modify: `Assets/_Project/Scripts/Config/SfxId.cs`
- Modify: `Assets/_Project/Scripts/App/LandBuildingSceneScope.cs`
- Modify: `Assets/_Project/Scripts/UI/LandBuildPaletteView.cs`

**Interfaces:**
- Consumes: `IAudioManager.PlaySfx(SfxId)` (existing), `SfxId.Cancel` (existing).
- Produces: `SfxId.BuildPlace`, `SfxId.BuildRemove` (new enum members, appended).

- [ ] **Step 1: Add the two `SfxId` members**

In `Assets/_Project/Scripts/Config/SfxId.cs`, append to the enum (append at the end so existing serialized clip mappings keep their indices):

```csharp
namespace SocialUniverse.Config
{
    public enum SfxId
    {
        Confirm,
        Cancel,
        OpenPanel,
        MiningComplete,
        ActiveMiningTap,
        NewMessage,
        TravelConfirm,
        PlanetObserveConfirm,
        CoinsReward,
        AsteroidDestroyed,
        RocketDepart,
        RocketArrive,
        BuildPlace,
        BuildRemove
    }
}
```

- [ ] **Step 2: Register the audio stack in the scope's standalone branch**

In `Assets/_Project/Scripts/App/LandBuildingSceneScope.cs`, add `using SocialUniverse.Safety;` to the usings, add two serialized fields, and register audio inside the existing `if (standalone)` block.

Add fields (next to `_databaseRegistry` / `_economyConfig`):

```csharp
        [SerializeField] private AudioConfig  _audioConfig;   // standalone-mode fallback, mirrors PlanetSceneScope
        [SerializeField] private AudioCatalog _audioCatalog;  // standalone-mode fallback, mirrors PlanetSceneScope
```

Inside `if (standalone) { ... }`, after the existing backend registrations, add:

```csharp
                // Audio — production resolves IAudioManager from the RootLifetimeScope parent;
                // standalone has no parent, so provide the same stack here (mirrors PlanetSceneScope).
                builder.RegisterInstance(_audioConfig  != null ? _audioConfig  : ScriptableObject.CreateInstance<AudioConfig>());
                builder.RegisterInstance(_audioCatalog != null ? _audioCatalog : ScriptableObject.CreateInstance<AudioCatalog>());
                builder.Register<AudioManager>(Lifetime.Singleton).As<IAudioManager>();
```

- [ ] **Step 3: Inject `IAudioManager` into the view and play SFX at the trigger points**

In `Assets/_Project/Scripts/UI/LandBuildPaletteView.cs`, add `using SocialUniverse.Safety;` and an injected field near the other `[Inject]` fields:

```csharp
        [Inject] private IAudioManager _audio;
```

Then add the sounds (each guarded for a null audio manager):

- In `PlaceFromPalette`, the invalid/failure branches and the success branch:

```csharp
    if (hexIndex < 0) { if (_audio != null) _audio.PlaySfx(SfxId.Cancel); return; }
    if (!_unlocked[hexIndex] || !string.IsNullOrEmpty(_slots[hexIndex])) { SetStatus("Pick an unlocked empty tile"); if (_audio != null) _audio.PlaySfx(SfxId.Cancel); return; }
    if (item.Cost > _localCoins) { SetStatus("Not enough coins"); if (_audio != null) _audio.PlaySfx(SfxId.Cancel); return; }

    var r = await _buildService.PlaceAsync(_handoff.TileId, _handoff.RegistryPlanetId, hexIndex, item.ItemId, item.Cost);
    if (!r.Success) { SetStatus($"Place failed: {r.Reason}"); if (_audio != null) _audio.PlaySfx(SfxId.Cancel); return; }

    _slots[hexIndex] = item.ItemId;
    if (r.NewBalance >= 0) _localCoins = r.NewBalance;
    _board.SetCell(hexIndex, true, item.ItemId, animate: true);
    if (_audio != null) _audio.PlaySfx(SfxId.BuildPlace);
    SetStatus("");
    BuildPalette();
    UpdateBuildLevel(animate: true);
```

- In `Remove`, on success (after `_board.PlayRemove(hexIndex);`):

```csharp
    _slots[hexIndex] = null;
    _board.PlayRemove(hexIndex);
    if (_audio != null) _audio.PlaySfx(SfxId.BuildRemove);
    SetStatus("");
    BuildPalette();
    UpdateBuildLevel(animate: true);
```

- In `OnBuildingDragged` (move), on success (after the destination `SetCell`):

```csharp
    _board.SetCell(toHex, true, _slots[toHex], animate: true);
    if (_audio != null) _audio.PlaySfx(SfxId.BuildPlace);
    SetStatus("");
```

- [ ] **Step 4: Verify compilation**

`read_console` (types: error). Poll `editor_state.isCompiling` until false. Expected: no errors.

- [ ] **Step 5: Manual verification (playtest)**

In production flow (enter via a planet tile) and standalone (open `LandBuilding.unity` directly): placing plays `BuildPlace`, removing plays `BuildRemove`, an invalid drop / can't-afford plays `Cancel`. Standalone must NOT throw a VContainer resolution error for `IAudioManager`. (Sounds are silent until clips are mapped — see follow-ups — but `AudioManager` logs a benign "no clip mapped" warning rather than erroring.)

- [ ] **Step 6: Commit**

```bash
git add Assets/_Project/Scripts/Config/SfxId.cs Assets/_Project/Scripts/App/LandBuildingSceneScope.cs Assets/_Project/Scripts/UI/LandBuildPaletteView.cs
git commit -m "feat(landbuild): SFX on place/remove/invalid + standalone audio registration"
```

---

## Post-Implementation: user-owned follow-ups (not code)

These require the Unity Editor / assets and are the user's to do:

1. **Map SFX clips:** assign `AudioClip`s for `SfxId.BuildPlace` and `SfxId.BuildRemove` in the `AudioCatalog` used by `AudioManager` (the `GetSfxClip(id)` mapping). Until mapped, those sounds are silent (benign warning logged).
2. **Invalid ghost material:** create a red transparent material (e.g. `TransparentRed.mat`) and assign it to `LandBuildPaletteView._dragGhostInvalidMaterial` in `LandBuilding.unity`.
3. **Standalone audio SO refs (optional):** to hear SFX when opening `LandBuilding.unity` directly, assign `AudioConfig`/`AudioCatalog` on the `LandBuildingSceneScope` component (unassigned falls back to empty runtime instances — no crash, but no clips).
4. **Device playtest:** validate feel on a mobile device (target platform); tune `BuildFeedback` constants if needed.

## Self-Review Notes

- **Spec coverage:** all five effects map to Tasks 2–6; `BuildFeedback` (Task 1) backs them; standalone audio registration (spec's SFX section) is Task 6 Step 2. Out-of-scope items (particles, haptics, celebration) are intentionally excluded.
- **Type consistency:** `SetCell(..., bool animate = false)` (Task 2) is reused identically in Tasks 3/6; `UpdateBuildLevel(bool animate = false)` (Task 4) callers all updated; `PaletteItemDragHandler.Init(...)` 7-arg signature (Task 5) matches its single caller in `BuildPalette`; `SfxId.BuildPlace/BuildRemove` (Task 6) are the only new enum members and are used exactly as declared.
- **No placeholders:** every code step contains the literal code to write.
