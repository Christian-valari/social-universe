# Handoff — LandBuilding Per-Planet Themes

Branch: `feature/landbuilding-planet-themes`. Code for Tasks 1–6 is committed. This file covers the two things that require your live Unity Editor: **Task 7 (Editor asset + scene wiring)** and **deferred verification** (compile + tests). None of it could run against the worktree because the Editor is open on the main checkout.

## Why this is manual

Code changes were made in a git worktree (`.claude/worktrees/landbuild-themes`). The running Unity Editor is on the main project path, so it never saw the worktree files. To verify and to do the Editor wiring, get this branch into an Editor — either merge it to `main`/your working branch and let your open Editor import it, or open the worktree as a project.

## A. Deferred verification (do first, after the Editor imports the branch)

1. **Compile:** let the domain reload finish; open Console and confirm **no compile errors**. New types: `LandBuildingThemeDefinition`, `LandBuildingThemeResolver`, `LandBuildingThemeApplier`; modified: `PlanetDefinition`, `PlotHexBoard`, `LandBuildingSceneScope`.
2. **Unit tests:** Window > General > Test Runner > EditMode. Run `LandBuildingThemeResolverTests` (5 tests) — all should pass:
   - `Resolve_returns_planet_theme_when_present`
   - `Resolve_returns_fallback_when_planet_missing`
   - `Resolve_returns_fallback_when_planet_has_no_theme`
   - `Resolve_returns_fallback_when_registry_null`
   - `Resolve_returns_fallback_when_planetId_null`
   Also run the full EditMode suite to confirm no regressions.

## B. Task 7 — Editor asset + scene wiring

> Note: your working tree already has the `SimpleSky.png → EarthLandBuildingSky.png` rename (uncommitted). The Earth theme below points at `EarthLandBuildingSky.png`.

1. **Find the current Earth hex materials + PlanetDefinition.**
   - Open `Assets/Scenes/LandBuilding.unity`, select the `PlotHexBoard` component, note its `Locked Mat` and `Unlocked Mat` assets (these are Earth's hex materials).
   - Locate Earth's `PlanetDefinition` asset (`_planetId` = `earth`, i.e. `Planet_Earth`).

2. **Create the Earth theme asset.**
   - Create folder `Assets/_Project/ScriptableObjects/LandBuildingThemes/` if missing.
   - Assets > Create > SocialUniverse > Config > LandBuildingTheme → name it `Earth_LandBuildingTheme`. Set:
     - `Sky Texture` = `Assets/Plugins/SimpleSky/Textures/EarthLandBuildingSky.png`
     - `Hex Locked Material` = the Locked Mat from step 1
     - `Hex Unlocked Material` = the Unlocked Mat from step 1
     - `Ambient Color` / `Ambient Intensity` = start at defaults (`0.6,0.6,0.6` / `1.0`); tune in step 5.

3. **Assign the theme to Earth's PlanetDefinition:** set `Land Building Theme` = `Earth_LandBuildingTheme`.

4. **Wire the scene (`LandBuilding.unity`):**
   - Create an empty GameObject `LandBuildingThemeApplier`, add the `LandBuildingThemeApplier` component.
     - `Sky Renderers` = all 4 SkyDome mesh renderers (SimpleSky `SkyDome`, prefab guid `d0223f77114284b47a0e8b317e71e4dc`; all faces share `SimpleSky.mat`).
     - `Default Theme` = `Earth_LandBuildingTheme`.
   - Select `PlotHexBoard` and set its new `Default Theme` = `Earth_LandBuildingTheme`. (Its `Locked Mat`/`Unlocked Mat` remain as ultimate fallback.)
   - **Save the scene.**

5. **Manual visual verification:**
   1. Enter LandBuilding from Earth → Earth sky + Earth hex materials + ambient; confirm no regression vs. before. Tune the theme's ambient if needed.
   2. Enter from a planet with no `_landBuildingTheme` → falls back to the Earth (default) look; no console errors.
   3. Open `LandBuilding.unity` directly and Play → no NullReference/DI errors; default look.
   4. Enter LandBuilding then Back → Planet scene lighting looks correct (no lingering ambient).
   5. **Confirm the sky texture visibly changes** (not just "no errors"): the applier sets `Material.mainTexture`, which routes to the shader's main-texture property. If SimpleSky's dome shader exposes its texture under a non-main-tagged property, the swap silently no-ops and every planet keeps the same sky. Verify by giving a second planet a distinct sky texture and confirming it renders. If it no-ops, change `ApplySky` to set the shader's actual property, e.g. `_skyInstance.SetTexture("_MainTex", skyTexture)` (or whatever `SimpleSky.mat` uses).

6. **Commit the Editor changes** (do this in whichever checkout has the Editor):
   - New: `Assets/_Project/ScriptableObjects/LandBuildingThemes/Earth_LandBuildingTheme.asset` (+ .meta)
   - Modified: Earth `PlanetDefinition` asset, `Assets/Scenes/LandBuilding.unity`
   - Also: the new script `.meta` files Unity generated on import (for `LandBuildingThemeDefinition.cs`, `LandBuildingThemeResolver.cs`, `LandBuildingThemeApplier.cs`, and the test) — commit these so the guids are stable.

## C. Adding a new planet later (no code)

Make the sky texture → create a new `LandBuildingTheme` asset (sky texture + hex materials + ambient) → assign it to that planet's `PlanetDefinition._landBuildingTheme`. Done.
