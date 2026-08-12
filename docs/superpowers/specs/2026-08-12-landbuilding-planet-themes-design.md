# LandBuilding Per-Planet Themes — Design

**Date:** 2026-08-12
**Feature area:** Land Building (cosmetic/config polish)
**Milestone scope:** Within the existing Land Building feature. No server, economy, or gameplay changes. Satisfies CLAUDE.md Architecture Rule 3 (tunable/visual data lives in ScriptableObjects).

## Problem

The `LandBuilding` scene is hardcoded to an Earth look in two places:

- **Sky:** the scene's SimpleSky `SkyDome` has 4 renderers all sharing `Assets/Plugins/SimpleSky/Materials/SimpleSky.mat`, and that one material points at the `EarthLandBuildingSky` texture (guid `7bab9ac1487e10c40b9ed77d4c551e04`). Every planet shows the Earth sky.
- **Hexatiles:** `PlotHexBoard` holds two serialized materials (`_lockedMat` / `_unlockedMat`) assigned in the scene — one fixed look for every planet.

The scene already knows which planet the player entered from via `LandBuildingHandoff.PlanetId`, and `DatabaseRegistry.GetPlanet(planetId)` resolves the `PlanetDefinition`. Today nothing reads that identity for visuals.

We want the sky texture, hexatile materials, and scene ambient to vary per planet (Earth, Mars, Venus, ...), with new planets addable as pure asset work.

## Non-goals

- The ground / base plane under the hex board is **not** re-skinned per planet.
- No changes to economy, land registry, server code, or the build/place/remove flows.
- Per-planet directional-light tinting is out (ambient color + intensity only). See Open question resolution below.

## Design

### 1. `LandBuildingThemeDefinition` (new ScriptableObject, `SocialUniverse.Config`)

Groups one planet's LandBuilding look:

| Field | Type | Purpose |
|---|---|---|
| `_skyTexture` | `Texture2D` | Swapped onto the SkyDome. Earth = existing `EarthLandBuildingSky`. |
| `_hexLockedMaterial` | `Material` | Locked hexatile look. |
| `_hexUnlockedMaterial` | `Material` | Unlocked hexatile look. |
| `_ambientColor` | `Color` | Scene ambient light color for the planet's atmosphere. |
| `_ambientIntensity` | `float` (default 1) | Scene ambient intensity. |

Public read-only accessors for each. `[CreateAssetMenu(menuName = "SocialUniverse/Config/LandBuildingTheme")]`.

Any field left null/default means "use the scene fallback for this aspect" (see §4), so a partially-authored theme is valid.

### 2. `PlanetDefinition` — one new field

Add `[SerializeField] private LandBuildingThemeDefinition _landBuildingTheme;` plus a `LandBuildingTheme` accessor. No other change. A planet with no theme assigned resolves to the fallback, so Mars/Venus/etc. are added later with no code change.

### 3. Theme resolution (single source of truth)

A pure static helper resolves the active theme:

```
LandBuildingThemeResolver.Resolve(DatabaseRegistry registry, string planetId, LandBuildingThemeDefinition fallback)
  => registry?.GetPlanet(planetId)?.LandBuildingTheme ?? fallback
```

Placed in `SocialUniverse.Config`, since `DatabaseRegistry` and `PlanetDefinition` both already live there. This is the one testable unit.

### 4. Runtime application (two consumers)

Both consumers resolve the theme via §3 with `handoff.PlanetId` and a serialized default-theme fallback.

**(a) Hexatiles — inside `PlotHexBoard`.**
`PlotHexBoard` already injects `DatabaseRegistry`. Add `[Inject] private LandBuildingHandoff _handoff;` and a serialized `_defaultTheme` fallback. In `Build`, resolve the theme once and prefer `theme.HexLockedMaterial` / `theme.HexUnlockedMaterial` over the existing serialized `_lockedMat` / `_unlockedMat` (which remain as the ultimate fallback when a theme is null or leaves a material unset). The board resolving its own materials avoids any inter-`Start()` ordering hazard.

**(b) Sky + ambient — new `LandBuildingThemeApplier` MonoBehaviour (`SocialUniverse.UI`).**
Registered in `LandBuildingSceneScope` via `RegisterComponentInHierarchy`, injected with `LandBuildingHandoff` + `DatabaseRegistry`, plus serialized:
- `Renderer[] _skyRenderers` — the SkyDome renderers.
- `LandBuildingThemeDefinition _defaultTheme` — fallback for standalone/no-theme.

On `Start`:
1. Resolve the theme.
2. If `theme.SkyTexture != null`: create **one** material instance from the sky renderers' shared material, set its `mainTexture` to the theme texture, and assign that single instance to all `_skyRenderers` — so the shared `SimpleSky.mat` asset is never mutated and all dome faces stay in sync.
3. Apply ambient: set `RenderSettings.ambientMode = Flat`, `RenderSettings.ambientLight = theme.AmbientColor`, `RenderSettings.ambientIntensity = theme.AmbientIntensity`.

Lives in `SocialUniverse.UI` alongside the other LandBuilding scene controllers (`LandBuildingController`, `LandBuildPaletteView`, `PlotHexBoard`), which already depend on `Config`/`Core`.

### 5. Fallback / standalone behavior

In standalone mode (`LandBuilding.unity` opened directly), the handoff is empty and `GetPlanet(null)` returns null, so resolution yields the serialized `_defaultTheme`. If `_defaultTheme` is also unset, the applier leaves the sky and ambient as authored in the scene, and `PlotHexBoard` uses its serialized `_lockedMat`/`_unlockedMat`. Nothing crashes and the scene looks exactly as it does today.

### 6. Assets scaffolded now vs. authored later

Scaffolded as part of this work:
- **Earth `LandBuildingThemeDefinition` asset** wired to the existing `EarthLandBuildingSky` texture + the current hex materials + a chosen ambient, assigned to Earth's `PlanetDefinition`.
- Scene wiring: `LandBuildingThemeApplier` added to the scene with `_skyRenderers` pointing at the SkyDome faces and `_defaultTheme` = the Earth theme; `PlotHexBoard._defaultTheme` = the Earth theme; `LandBuildingSceneScope` registration.

Authored later by the user (no code): one `LandBuildingThemeDefinition` per additional planet as each sky texture is made, assigned to that planet's `PlanetDefinition`.

## Testing

- **EditMode unit test** for `LandBuildingThemeResolver.Resolve`: returns the planet's theme when present; returns the fallback when the planet is missing, has no theme, or the registry is null.
- **Manual verification (Unity-runtime):** enter LandBuilding from Earth → Earth sky + hex + ambient; enter from a themeless planet → fallback look; open the scene standalone → no crash, scene default look. Material/`RenderSettings` application is runtime-only and not unit-testable.

## Architecture rule check (CLAUDE.md)

- **Rule 3 (data in SOs):** satisfied — all visual data in `LandBuildingThemeDefinition`.
- **Rule 4 (decouple via events):** applier reads handoff/registry at scene load; no cross-namespace gameplay call. OK.
- **Rules 1, 2, 5:** not touched (no economy, no backend, no per-planet grid instantiation change).
- **Namespaces:** `LandBuildingThemeDefinition` + resolver in `SocialUniverse.Config`; `LandBuildingThemeApplier` in `SocialUniverse.UI`.

## Open questions — resolved

- **Sky representation:** texture swap on the shared dome material (theme carries a `Texture2D`, not a full `Material`). Confirmed.
- **Lighting scope:** ambient color + intensity only; no per-planet directional-light tint. Confirmed.
