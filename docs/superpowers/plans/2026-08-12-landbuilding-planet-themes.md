# LandBuilding Per-Planet Themes Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the LandBuilding scene's sky texture, hexatile materials, and scene ambient vary per planet, driven by a per-planet ScriptableObject theme, with new planets addable as pure asset work.

**Architecture:** A new `LandBuildingThemeDefinition` ScriptableObject holds one planet's look (sky texture, locked/unlocked hex materials, ambient color+intensity). `PlanetDefinition` gains a reference to its theme. A pure static `LandBuildingThemeResolver` resolves the active theme from the registry + handoff planet id, falling back to a serialized default. Two runtime consumers apply it: `PlotHexBoard` (hex materials, resolved in-place) and a new `LandBuildingThemeApplier` MonoBehaviour (sky texture + `RenderSettings` ambient).

**Tech Stack:** Unity 6 (URP), C#, VContainer DI, NUnit EditMode tests, SimpleSky dome asset.

## Global Constraints

- **Namespaces:** `LandBuildingThemeDefinition` and `LandBuildingThemeResolver` → `SocialUniverse.Config`. `LandBuildingThemeApplier` → `SocialUniverse.UI`. (CLAUDE.md Project Structure table.)
- **UnityEngine.Object null checks:** `PlanetDefinition`, `LandBuildingThemeDefinition`, `Material`, `Texture2D`, and `Renderer` are `UnityEngine.Object`s. Use explicit `!= null` / `== null` comparisons everywhere — **never** `??` or `?.` on these types (those bypass Unity's overridden fake-null equality and can treat a destroyed object as non-null).
- **ScriptableObject data rule:** all tunable visual data lives in `LandBuildingThemeDefinition`, not hardcoded (CLAUDE.md Rule 3).
- **One public type per file, file named after the type** (CLAUDE.md Naming Conventions).
- **Tests:** EditMode, NUnit, namespace `SocialUniverse.Tests`. Construct ScriptableObjects with `ScriptableObject.CreateInstance<T>()` and set private serialized fields via reflection (`BindingFlags.NonPublic | BindingFlags.Instance`), matching `DatabaseRegistryAvatarTests`.
- **Commit after each task.** End commit messages with the Co-Authored-By trailer:
  `Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>`

---

## File Structure

- Create `Assets/_Project/Scripts/Config/LandBuildingThemeDefinition.cs` — the theme data SO.
- Create `Assets/_Project/Scripts/Config/LandBuildingThemeResolver.cs` — pure static resolver.
- Modify `Assets/_Project/Scripts/Config/PlanetDefinition.cs` — add `_landBuildingTheme` field + accessor.
- Modify `Assets/_Project/Scripts/UI/PlotHexBoard.cs` — resolve + prefer theme hex materials.
- Create `Assets/_Project/Scripts/UI/LandBuildingThemeApplier.cs` — apply sky + ambient.
- Modify `Assets/_Project/Scripts/App/LandBuildingSceneScope.cs` — register the applier.
- Create `Assets/_Project/Tests/EditMode/Config/LandBuildingThemeResolverTests.cs` — resolver unit tests.
- Editor assets (Task 7): create Earth `LandBuildingTheme` asset, assign to Earth `PlanetDefinition`, wire the applier + `_defaultTheme` fields into `LandBuilding.unity`.

---

### Task 1: `LandBuildingThemeDefinition` ScriptableObject

**Files:**
- Create: `Assets/_Project/Scripts/Config/LandBuildingThemeDefinition.cs`

**Interfaces:**
- Consumes: nothing.
- Produces: type `SocialUniverse.Config.LandBuildingThemeDefinition` with read-only accessors `Texture2D SkyTexture`, `Material HexLockedMaterial`, `Material HexUnlockedMaterial`, `Color AmbientColor`, `float AmbientIntensity`.

- [ ] **Step 1: Create the ScriptableObject**

```csharp
using UnityEngine;

namespace SocialUniverse.Config
{
    // One planet's LandBuilding look. Referenced by PlanetDefinition._landBuildingTheme and
    // resolved at scene load by LandBuildingThemeResolver. Any field left null/default means
    // "use the scene fallback for this aspect" — a partially-authored theme is valid.
    [CreateAssetMenu(menuName = "SocialUniverse/Config/LandBuildingTheme", fileName = "NewLandBuildingTheme")]
    public class LandBuildingThemeDefinition : ScriptableObject
    {
        [SerializeField] private Texture2D _skyTexture;            // swapped onto the SkyDome
        [SerializeField] private Material  _hexLockedMaterial;     // locked hexatile look
        [SerializeField] private Material  _hexUnlockedMaterial;   // unlocked hexatile look
        [SerializeField] private Color     _ambientColor = new Color(0.6f, 0.6f, 0.6f, 1f);
        [SerializeField] private float     _ambientIntensity = 1f;

        public Texture2D SkyTexture          => _skyTexture;
        public Material  HexLockedMaterial   => _hexLockedMaterial;
        public Material  HexUnlockedMaterial => _hexUnlockedMaterial;
        public Color     AmbientColor        => _ambientColor;
        public float     AmbientIntensity    => _ambientIntensity;
    }
}
```

- [ ] **Step 2: Verify it compiles**

In Unity, let the domain reload finish, then check the console has no compile errors (MCP: `read_console` filtered to errors, or Unity Console window). Expected: no errors; `SocialUniverse/Config/LandBuildingTheme` appears under Assets > Create.

- [ ] **Step 3: Commit**

```bash
git add Assets/_Project/Scripts/Config/LandBuildingThemeDefinition.cs Assets/_Project/Scripts/Config/LandBuildingThemeDefinition.cs.meta
git commit -m "feat(landbuild): add LandBuildingThemeDefinition SO

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

### Task 2: `PlanetDefinition._landBuildingTheme`

**Files:**
- Modify: `Assets/_Project/Scripts/Config/PlanetDefinition.cs`

**Interfaces:**
- Consumes: `LandBuildingThemeDefinition` (Task 1).
- Produces: `PlanetDefinition.LandBuildingTheme` → `LandBuildingThemeDefinition` accessor.

- [ ] **Step 1: Add the serialized field**

After the `_bgmClip` field (line 20), add:

```csharp
        [SerializeField] private LandBuildingThemeDefinition _landBuildingTheme; // per-planet LandBuilding look; null = scene fallback
```

- [ ] **Step 2: Add the accessor**

After the `BgmClip` accessor (line 34), add:

```csharp
        public LandBuildingThemeDefinition LandBuildingTheme => _landBuildingTheme;
```

- [ ] **Step 3: Verify it compiles**

Check the Unity console has no errors after domain reload. Existing `PlanetDefinition` assets keep their values; the new field defaults to `None`.

- [ ] **Step 4: Commit**

```bash
git add Assets/_Project/Scripts/Config/PlanetDefinition.cs
git commit -m "feat(landbuild): PlanetDefinition references a LandBuildingTheme

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

### Task 3: `LandBuildingThemeResolver` (pure) + tests

**Files:**
- Create: `Assets/_Project/Scripts/Config/LandBuildingThemeResolver.cs`
- Test: `Assets/_Project/Tests/EditMode/Config/LandBuildingThemeResolverTests.cs`

**Interfaces:**
- Consumes: `DatabaseRegistry.GetPlanet(string id)` → `PlanetDefinition`; `PlanetDefinition.LandBuildingTheme`; `LandBuildingThemeDefinition`.
- Produces: `static LandBuildingThemeDefinition LandBuildingThemeResolver.Resolve(DatabaseRegistry registry, string planetId, LandBuildingThemeDefinition fallback)`.

- [ ] **Step 1: Write the failing test**

Create `Assets/_Project/Tests/EditMode/Config/LandBuildingThemeResolverTests.cs`:

```csharp
using System.Reflection;
using NUnit.Framework;
using SocialUniverse.Config;
using UnityEngine;

namespace SocialUniverse.Tests
{
    public class LandBuildingThemeResolverTests
    {
        private static void SetField(object target, string fieldName, object value) =>
            target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);

        private static PlanetDefinition MakePlanet(string id, LandBuildingThemeDefinition theme)
        {
            var p = ScriptableObject.CreateInstance<PlanetDefinition>();
            SetField(p, "_planetId", id);
            SetField(p, "_landBuildingTheme", theme);
            return p;
        }

        private static DatabaseRegistry MakeRegistry(params PlanetDefinition[] planets)
        {
            var r = ScriptableObject.CreateInstance<DatabaseRegistry>();
            SetField(r, "_planets", planets);
            return r;
        }

        [Test]
        public void Resolve_returns_planet_theme_when_present()
        {
            var theme    = ScriptableObject.CreateInstance<LandBuildingThemeDefinition>();
            var fallback = ScriptableObject.CreateInstance<LandBuildingThemeDefinition>();
            var registry = MakeRegistry(MakePlanet("earth", theme));

            Assert.AreSame(theme, LandBuildingThemeResolver.Resolve(registry, "earth", fallback));
        }

        [Test]
        public void Resolve_returns_fallback_when_planet_missing()
        {
            var theme    = ScriptableObject.CreateInstance<LandBuildingThemeDefinition>();
            var fallback = ScriptableObject.CreateInstance<LandBuildingThemeDefinition>();
            var registry = MakeRegistry(MakePlanet("earth", theme));

            Assert.AreSame(fallback, LandBuildingThemeResolver.Resolve(registry, "mars", fallback));
        }

        [Test]
        public void Resolve_returns_fallback_when_planet_has_no_theme()
        {
            var fallback = ScriptableObject.CreateInstance<LandBuildingThemeDefinition>();
            var registry = MakeRegistry(MakePlanet("earth", null));

            Assert.AreSame(fallback, LandBuildingThemeResolver.Resolve(registry, "earth", fallback));
        }

        [Test]
        public void Resolve_returns_fallback_when_registry_null()
        {
            var fallback = ScriptableObject.CreateInstance<LandBuildingThemeDefinition>();

            Assert.AreSame(fallback, LandBuildingThemeResolver.Resolve(null, "earth", fallback));
        }

        [Test]
        public void Resolve_returns_fallback_when_planetId_null()
        {
            var theme    = ScriptableObject.CreateInstance<LandBuildingThemeDefinition>();
            var fallback = ScriptableObject.CreateInstance<LandBuildingThemeDefinition>();
            var registry = MakeRegistry(MakePlanet("earth", theme));

            Assert.AreSame(fallback, LandBuildingThemeResolver.Resolve(registry, null, fallback));
        }
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run EditMode tests filtered to `LandBuildingThemeResolverTests` (Test Runner window, or MCP `run_tests` EditMode).
Expected: FAIL — `LandBuildingThemeResolver` does not exist (compile error).

- [ ] **Step 3: Write the resolver**

Create `Assets/_Project/Scripts/Config/LandBuildingThemeResolver.cs`:

```csharp
namespace SocialUniverse.Config
{
    // Single source of truth for picking the active LandBuilding theme. Used by both consumers
    // (PlotHexBoard for hex materials, LandBuildingThemeApplier for sky + ambient). Explicit
    // != null checks throughout — these are UnityEngine.Objects, so ?? / ?. must not be used.
    public static class LandBuildingThemeResolver
    {
        public static LandBuildingThemeDefinition Resolve(
            DatabaseRegistry registry, string planetId, LandBuildingThemeDefinition fallback)
        {
            if (registry != null && !string.IsNullOrEmpty(planetId))
            {
                var planet = registry.GetPlanet(planetId);
                if (planet != null && planet.LandBuildingTheme != null)
                    return planet.LandBuildingTheme;
            }
            return fallback;
        }
    }
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run EditMode tests filtered to `LandBuildingThemeResolverTests`.
Expected: PASS — all 5 tests green.

- [ ] **Step 5: Commit**

```bash
git add Assets/_Project/Scripts/Config/LandBuildingThemeResolver.cs Assets/_Project/Scripts/Config/LandBuildingThemeResolver.cs.meta Assets/_Project/Tests/EditMode/Config/LandBuildingThemeResolverTests.cs Assets/_Project/Tests/EditMode/Config/LandBuildingThemeResolverTests.cs.meta
git commit -m "feat(landbuild): LandBuildingThemeResolver + tests

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

### Task 4: `PlotHexBoard` prefers theme hex materials

**Files:**
- Modify: `Assets/_Project/Scripts/UI/PlotHexBoard.cs`

**Interfaces:**
- Consumes: `LandBuildingThemeResolver.Resolve` (Task 3); `LandBuildingHandoff.PlanetId` (`SocialUniverse.Core`, already a DI singleton in `LandBuildingSceneScope`); `LandBuildingThemeDefinition` (Task 1).
- Produces: no new public API. Board renders hex cells with theme materials when a theme is resolved, else its serialized `_lockedMat`/`_unlockedMat`.

> **Why no unit test:** `PlotHexBoard` is a MonoBehaviour that `Instantiate`s prefabs and reads injected DI singletons; the material-selection branch is trivial and exercised by the manual verification in Task 7. The pure decision (theme vs fallback) is already covered by Task 3's resolver tests.

- [ ] **Step 1: Add the injected handoff, serialized fallback, and resolved fields**

`SocialUniverse.Core` is already imported (line 5). After the existing serialized materials (lines 28-29), add the fallback theme field:

```csharp
        [SerializeField] private LandBuildingThemeDefinition _defaultTheme; // used when the active planet has no theme (standalone / not-yet-authored)
```

Add the handoff injection alongside the existing `[Inject]` fields (after line 32):

```csharp
        [Inject] private LandBuildingHandoff _handoff;
```

Add resolved-material caches next to `_cells` (after line 34):

```csharp
        private Material _resolvedLocked;
        private Material _resolvedUnlocked;
```

- [ ] **Step 2: Resolve the theme materials at the top of `Build`**

In `Build` (line 41), immediately after `_cells.Clear();` (line 44), insert:

```csharp
            var theme = LandBuildingThemeResolver.Resolve(_registry, _handoff != null ? _handoff.PlanetId : null, _defaultTheme);
            _resolvedLocked   = (theme != null && theme.HexLockedMaterial   != null) ? theme.HexLockedMaterial   : _lockedMat;
            _resolvedUnlocked = (theme != null && theme.HexUnlockedMaterial != null) ? theme.HexUnlockedMaterial : _unlockedMat;
```

- [ ] **Step 3: Use the resolved materials in `SetCell`**

In `SetCell` (line 62), replace the `cell.SetLockVisual(...)` call (line 67):

```csharp
            cell.SetLockVisual(state == HexCellVisual.State.Locked, _lockedMat, _unlockedMat);
```

with (coalescing to the serialized fields so a `SetCell` call before `Build` still works):

```csharp
            var lockedMat   = _resolvedLocked   != null ? _resolvedLocked   : _lockedMat;
            var unlockedMat = _resolvedUnlocked != null ? _resolvedUnlocked : _unlockedMat;
            cell.SetLockVisual(state == HexCellVisual.State.Locked, lockedMat, unlockedMat);
```

- [ ] **Step 4: Verify it compiles**

Check the Unity console has no errors after domain reload. (Runtime behavior is verified in Task 7.)

- [ ] **Step 5: Commit**

```bash
git add Assets/_Project/Scripts/UI/PlotHexBoard.cs
git commit -m "feat(landbuild): PlotHexBoard uses per-planet theme hex materials

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

### Task 5: `LandBuildingThemeApplier` (sky + ambient)

**Files:**
- Create: `Assets/_Project/Scripts/UI/LandBuildingThemeApplier.cs`

**Interfaces:**
- Consumes: `LandBuildingThemeResolver.Resolve` (Task 3); `LandBuildingHandoff` + `DatabaseRegistry` (DI); `LandBuildingThemeDefinition` accessors (Task 1).
- Produces: MonoBehaviour `SocialUniverse.UI.LandBuildingThemeApplier` with serialized `Renderer[] _skyRenderers` and `LandBuildingThemeDefinition _defaultTheme`, registered in Task 6.

> **Why no unit test:** applies a runtime `Material` instance to scene `Renderer`s and mutates global `RenderSettings` — not unit-testable in EditMode. Verified manually in Task 7. The theme-selection logic is covered by Task 3.

- [ ] **Step 1: Create the applier**

Create `Assets/_Project/Scripts/UI/LandBuildingThemeApplier.cs`:

```csharp
using UnityEngine;
using UnityEngine.Rendering;
using VContainer;
using SocialUniverse.Config;
using SocialUniverse.Core;

namespace SocialUniverse.UI
{
    // Applies the active planet's LandBuilding theme to the scene sky + ambient at load.
    // Sky: swaps the theme's texture onto the SkyDome renderers via ONE runtime material
    // instance, so the shared SimpleSky.mat asset is never mutated and all dome faces stay
    // in sync. Ambient: sets global RenderSettings (the Planet scene re-establishes its own
    // lighting on return, so this doesn't visually leak). Hex materials are handled separately
    // inside PlotHexBoard. Registered via RegisterComponentInHierarchy in LandBuildingSceneScope.
    public class LandBuildingThemeApplier : MonoBehaviour
    {
        [SerializeField] private Renderer[] _skyRenderers;   // the SimpleSky SkyDome faces
        [SerializeField] private LandBuildingThemeDefinition _defaultTheme; // fallback (standalone / no-theme)

        [Inject] private LandBuildingHandoff _handoff;
        [Inject] private DatabaseRegistry    _registry;

        private void Start()
        {
            var theme = LandBuildingThemeResolver.Resolve(
                _registry, _handoff != null ? _handoff.PlanetId : null, _defaultTheme);
            if (theme == null) return;

            ApplySky(theme.SkyTexture);
            ApplyAmbient(theme.AmbientColor, theme.AmbientIntensity);
        }

        private void ApplySky(Texture2D skyTexture)
        {
            if (skyTexture == null || _skyRenderers == null) return;

            Material instance = null;
            foreach (var r in _skyRenderers)
            {
                if (r == null) continue;
                if (instance == null)
                {
                    instance = new Material(r.sharedMaterial);
                    instance.mainTexture = skyTexture;
                }
                r.sharedMaterial = instance;
            }
        }

        private void ApplyAmbient(Color color, float intensity)
        {
            RenderSettings.ambientMode      = AmbientMode.Flat;
            RenderSettings.ambientLight     = color;
            RenderSettings.ambientIntensity = intensity;
        }
    }
}
```

- [ ] **Step 2: Verify it compiles**

Check the Unity console has no errors after domain reload.

- [ ] **Step 3: Commit**

```bash
git add Assets/_Project/Scripts/UI/LandBuildingThemeApplier.cs Assets/_Project/Scripts/UI/LandBuildingThemeApplier.cs.meta
git commit -m "feat(landbuild): LandBuildingThemeApplier for sky + ambient

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

### Task 6: Register the applier in `LandBuildingSceneScope`

**Files:**
- Modify: `Assets/_Project/Scripts/App/LandBuildingSceneScope.cs`

**Interfaces:**
- Consumes: `LandBuildingThemeApplier` (Task 5).
- Produces: the applier's `[Inject]` fields (`_handoff`, `_registry`) are satisfied at container build time.

- [ ] **Step 1: Register the component**

In `Configure` (line 26), after the existing `RegisterComponentInHierarchy<...PlotHexBoard>();` line (line 51), add:

```csharp
            builder.RegisterComponentInHierarchy<SocialUniverse.UI.LandBuildingThemeApplier>();
```

- [ ] **Step 2: Verify it compiles**

Check the Unity console has no errors after domain reload.

> Note: `RegisterComponentInHierarchy` requires an instance of `LandBuildingThemeApplier` to exist in the scene hierarchy — that GameObject is added in Task 7. Until then, entering the scene through DI would throw. Task 7 completes the wiring; do not play-test the scene between Task 6 and Task 7.

- [ ] **Step 3: Commit**

```bash
git add Assets/_Project/Scripts/App/LandBuildingSceneScope.cs
git commit -m "feat(landbuild): register LandBuildingThemeApplier in scene scope

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

### Task 7: Editor assets + scene wiring, and manual verification

This task is Unity Editor work (asset creation + scene reference assignment). It can be done through the Editor UI or Unity MCP tools (`manage_asset`, `manage_scene`, `manage_gameobject`, `manage_scriptable_object`). It produces the Earth theme and connects every serialized reference the runtime code reads.

**Files:**
- Create asset: `Assets/_Project/ScriptableObjects/LandBuildingThemes/Earth_LandBuildingTheme.asset` (folder may need creating).
- Modify asset: Earth's `PlanetDefinition` asset — set `_landBuildingTheme`.
- Modify scene: `Assets/Scenes/LandBuilding.unity`.

- [ ] **Step 1: Identify the existing Earth hex materials and PlanetDefinition**

- Open `Assets/Scenes/LandBuilding.unity`, select the `PlotHexBoard` component, and note the `Locked Mat` and `Unlocked Mat` assets currently assigned — these are Earth's hex materials.
- Locate Earth's `PlanetDefinition` asset (the one whose `_planetId` is `earth` / name `Planet_Earth`; search the Project for PlanetDefinition assets).

- [ ] **Step 2: Create the Earth LandBuildingTheme asset**

Create `Assets/_Project/ScriptableObjects/LandBuildingThemes/` if it does not exist, then create a `LandBuildingTheme` asset there (Assets > Create > SocialUniverse > Config > LandBuildingTheme) named `Earth_LandBuildingTheme`. Set:
- `Sky Texture` = `Assets/Plugins/SimpleSky/Textures/EarthLandBuildingSky.png` (guid `7bab9ac1487e10c40b9ed77d4c551e04`).
- `Hex Locked Material` = the Locked Mat from Step 1.
- `Hex Unlocked Material` = the Unlocked Mat from Step 1.
- `Ambient Color` / `Ambient Intensity` = choose to match today's scene look (start with the defaults `RGB 0.6,0.6,0.6` / `1.0`; adjust in Step 6 if the scene looks off).

- [ ] **Step 3: Assign the theme to Earth's PlanetDefinition**

Set Earth `PlanetDefinition._landBuildingTheme` = `Earth_LandBuildingTheme`.

- [ ] **Step 4: Add the applier GameObject to the scene**

In `LandBuilding.unity`, create an empty GameObject named `LandBuildingThemeApplier` and add the `LandBuildingThemeApplier` component. Set:
- `Sky Renderers` = the SkyDome's renderers. The SkyDome (SimpleSky prefab, guid `d0223f77114284b47a0e8b317e71e4dc`) has 4 mesh renderers all using `SimpleSky.mat`; drag every SkyDome renderer into this array.
- `Default Theme` = `Earth_LandBuildingTheme`.

- [ ] **Step 5: Set `_defaultTheme` on PlotHexBoard**

Select the `PlotHexBoard` component in the scene and set its `Default Theme` field = `Earth_LandBuildingTheme`. (Its serialized `Locked Mat`/`Unlocked Mat` remain as the ultimate fallback.)

Save the scene.

- [ ] **Step 6: Manual verification**

Run these checks (Play mode / device):
1. **Earth entry:** enter LandBuilding from Earth → sky shows `EarthLandBuildingSky`, hex tiles use the Earth materials, ambient matches. Confirm it looks the same as before this change (no regression). Adjust `Ambient Color`/`Intensity` on the Earth theme if needed.
2. **Themeless planet:** enter LandBuilding from a planet whose `PlanetDefinition` has no `_landBuildingTheme` → falls back to the Earth (default) look; no errors in console.
3. **Standalone scene:** open `LandBuilding.unity` directly and press Play → no NullReference/DI errors; scene shows the default look.
4. **Round trip:** enter LandBuilding then press Back → returning to the Planet scene, lighting looks correct (Planet scene re-establishes its own ambient). Confirm no lingering wrong ambient.

- [ ] **Step 7: Run the full EditMode suite**

Run all EditMode tests. Expected: all green (including `LandBuildingThemeResolverTests`), no new failures.

- [ ] **Step 8: Commit**

```bash
git add Assets/_Project/ScriptableObjects/LandBuildingThemes Assets/Scenes/LandBuilding.unity
# also add the modified Earth PlanetDefinition asset (path from Step 1) and any new .meta files
git commit -m "feat(landbuild): Earth theme asset + scene wiring for per-planet themes

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Adding a new planet theme later (no code)

For Mars/Venus/etc.: make the sky texture, create a new `LandBuildingTheme` asset (sky texture + hex materials + ambient), and assign it to that planet's `PlanetDefinition._landBuildingTheme`. Nothing else changes.

## Self-Review

- **Spec coverage:** SO (§1)→T1; PlanetDefinition field (§2)→T2; resolver (§3)→T3; hex application (§4a)→T4; sky+ambient applier (§4b)→T5+T6; fallback/standalone (§5)→covered by resolver fallback (T3) + `_defaultTheme` wiring (T7) + verification checks 2–3 (T7); Earth asset + scene wiring (§6)→T7; testing (spec Testing)→T3 unit tests + T7 manual checks. No gaps.
- **Placeholders:** none — all code shown in full; the only author-choice values (ambient color/intensity, which existing hex materials) are concrete lookups in T7 with a stated default.
- **Type consistency:** `LandBuildingThemeResolver.Resolve(DatabaseRegistry, string, LandBuildingThemeDefinition)` and accessors `SkyTexture`/`HexLockedMaterial`/`HexUnlockedMaterial`/`AmbientColor`/`AmbientIntensity` used identically in T1, T3, T4, T5. `PlanetDefinition.LandBuildingTheme` used in T3 matches T2. UnityEngine.Object `!= null` checks used consistently.
