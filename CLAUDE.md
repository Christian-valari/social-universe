# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Social Universe is a Unity 6 social MMO where players explore a solar system, mine asteroids, own hexagonal land tiles on planets, and interact with other players. The full architecture, milestone roadmap, and script inventory live in `Social_Universe_Architecture.md` — read it before any task.

**Current state (2026-09-30):** M0–M5 are code-complete and M6 (drones & mining depth) is code-complete and merged into `main`. EditMode 405/405, PlayMode 2/2. The backend is UGS (Auth/Economy/Cloud Save/Cloud Code/Vivox/Friends) with Firebase Auth via OIDC.

**The game is not ready for internal testing.** The three server blockers (Known Issues #10–#12) and server-side mining validation (#16, partly) are fixed in `ServerCode/` but no `ServerCode/` deploy has ever been confirmed, and the release keystore is committed to a public repo. Read `PROGRESS.md` — Known Issues and "Future Tasks" — before planning work; it is the source of truth for status.

## Pre-Task Protocol (mandatory)

Before writing any code, creating any file, or modifying any script — without exception — you MUST:

1. **Read `Social_Universe_Architecture.md`** — specifically §2 (Principles), §4 (Milestones & scope), and the Script Inventory (§8 or §9 depending on version).
2. **Identify the correct namespace and assembly** for every file you will touch using the Project Structure table in this file. Never place a type in the wrong namespace.
3. **Check milestone scope** — confirm the work is within the current milestone. If a request extends scope, flag it explicitly before proceeding.
4. **Verify each Architecture Rule below applies** to your approach. If any rule is violated by the proposed implementation, stop and propose a compliant alternative.
5. **Check Open Decisions** — if the task touches a still-open decision (age policy, land resale), do not resolve it silently; surface the dependency.

> Skip only for pure documentation edits (PROGRESS.md, CLAUDE.md, ARCHITECTURE.md) with no code changes.

## Running Tests

Tests run through Unity's Test Runner (Window > General > Test Runner). EditMode tests require no Play Mode; PlayMode tests run in-editor or on-device. There is no CLI build script yet.

To run tests from the command line (headless):
```
"C:\Program Files\Unity\Hub\Editor\6000.3.12f1\Editor\Unity.exe" -runTests -batchmode -projectPath . -testResults results.xml -testPlatform EditMode
```

Three things that will otherwise waste your time:

- **A headless run cannot share the project folder with an open Editor** (Unity holds a lock). Close the Editor, or copy `Assets/`, `Packages/`, `ProjectSettings/` **and `ServerCode/`** somewhere else and run there. Keep the copy's path short: some asset paths already exceed Windows' 260-character limit, and long paths are not enabled on this machine.
- **The `ServerCode/` drift tests** (`MiningCatalogAlignmentTests`, `DroneCatalogAlignmentTests`, `ServerCodeEconomyApiTests`) read `ServerCode/*.js` from the repo root, so a copy without `ServerCode/` fails them.
- **EditMode tests touch the project's editor `PlayerPrefs`** (music/SFX volume, idle-mining session, asteroid respawn timers). Running the suite resets those values for the Editor on this machine.

Current results (2026-09-30, Unity 6000.3.12f1): EditMode **405/405**; PlayMode **2/2**. `PlanetSceneFlowTests` run the real Planet scene under `PlanetTestRootScope` with a scripted `FakeBackendClient` (Tests/PlayMode/).

## Architecture Rules (enforce on every task)

1. **Server-authoritative economy.** The client never mints currency, grants ownership, or computes rewards. It sends a request; a server function validates and commits. Mining payouts, land purchases, yield claims, upgrades, and IAP all go through `ServerCode/`.

2. **Backend behind interfaces.** All gameplay code depends on `I*Service` abstractions (`IEconomyService`, `IAuthService`, `IChatService`, …). M1 ships against `LocalMock*` implementations. M2 swaps in the real backend. Never reference a backend SDK directly from gameplay code.

3. **ScriptableObjects for data.** Tunable values (prices, yield curves, upgrade stats, quest definitions) belong in `*Definition` or `*Config` ScriptableObjects under `Assets/_Project/ScriptableObjects/`, not hardcoded in scripts.

4. **Decouple via events.** Systems communicate through the `EventBus` or ScriptableObject `GameEvent` channels — not direct cross-namespace calls.

5. **Mobile performance budget.** Pool drones, asteroids, and FX. Load only the active planet's hex grid; don't instantiate every planet's tiles.

## Project Structure

All game code lives under `Assets/_Project/Scripts/` in namespace-per-folder assemblies:

| Folder | Namespace | Scope |
|---|---|---|
| `App/` | `SocialUniverse.App` | Composition root: `RootLifetimeScope`, `PlanetSceneScope`, and the `IStartable` handlers that turn intent events into service calls |
| `Core/` | `SocialUniverse.Core` | Bootstrap, FSM, EventBus, SceneLoader, DI |
| `Config/` | `SocialUniverse.Config` | ScriptableObject definitions + DatabaseRegistry |
| `World/` | `SocialUniverse.World` | Planet, hexasphere, tiles, camera |
| `Mining/` | `SocialUniverse.Mining` | Drones, asteroids, mining loop |
| `Economy/` | `SocialUniverse.Economy` | Wallet, land, marketplace, yield |
| `Net/` | `SocialUniverse.Net` | Auth, backend client, presence, shards |
| `Social/` | `SocialUniverse.Social` | Chat, friends, profiles, moderation |
| `Travel/` | `SocialUniverse.Travel` | Star map, fuel, sky discovery, rocket |
| `Progression/` | `SocialUniverse.Progression` | Player state, XP, quests, daily |
| `Guild/` | `SocialUniverse.Guild` | Stations, guilds, events — **planned (M7), folder does not exist yet** |
| `Store/` | `SocialUniverse.Store` | IAP, season pass, ads — **planned (M9), folder does not exist yet** |
| `Safety/` | `SocialUniverse.Safety` | Age gate, moderation hooks, analytics |
| `UI/` | `SocialUniverse.UI` | UIManager, screens (MVP pattern), HUD, juice |

Server-side logic lives in `ServerCode/` at the repo root as UGS Cloud Code scripts (`*.js`) — this folder is **not** included in the Unity build. Deploying them is a manual dashboard step, and nothing in the repo records what is currently live, so treat `ServerCode/` as source-of-truth-to-be-deployed rather than as what the servers are running.

## Naming Conventions

- Interfaces: `I` prefix (e.g. `IEconomyService`)
- ScriptableObject configs: `Definition` or `Config` suffix
- Services: `Service` suffix
- UI screens: `Screen` suffix; reusable views: `View` suffix
- One public type per file, file named after the type
- Namespaces mirror folder paths

## Scene Flow

`Boot → Auth → (Onboarding) → Hub (SolarSystem) ↔ Planet ↔ Station`

- **Bootstrap** scene: builds the DI container, inits services, loads Auth. Never contains gameplay. Uses `DontDestroyOnLoad`.
- **Planet** scene: loaded additively per planet/shard. Owns the hexasphere, mining, social HUD.

App flow is a `GameStateMachine` FSM — add new states as concrete `IGameState` implementations in `Core/`.

## Open Decisions

**Resolved** (do not reopen without saying so):

- **Backend: UGS** — Authentication, Economy, Cloud Save, Cloud Code, Vivox, Friends. Still reached only through `I*Service`.
- **DI framework: VContainer** — `RootLifetimeScope` (Bootstrap) + `PlanetSceneScope` (Planet), both in `App/`.
- **Sky Discovery: gyroscope starfield** — Input System `AttitudeSensor` with a drag fallback; no AR Foundation.
- **Auth: Firebase Auth via UGS OIDC** (`oidc-firebase`) — email/password + Google Sign-In. The earlier email-verification Cloud Code was retired in `db028f19`.

**Still open (do not resolve silently):**

- **Age policy / content rating** — `SocialConfig` ships a provisional teen-safe default (`ChatFilterLevel.Strict` for everyone); drives M10's `AgeGateService`.
- **Land resale model** — coins-only confirmed; confirm no real-money cash-out before M8.

## Installed Packages

From `Packages/manifest.json` (57 dependencies) unless noted:

- **Rendering:** URP 17.3.0 · **Input:** Input System 1.19.0 · **UI:** UGUI 2.0.0
- **DI:** VContainer 1.18.0 (OpenUPM) · **Testing:** Test Framework 1.6.0
- **UGS:** Core 1.13.0, Authentication 3.6.1, Economy 3.5.3, Cloud Save 3.4.0, Cloud Code 2.10.2, Friends 1.1.1, Vivox 16.11.0
- **Also:** Cinemachine 3.1.7, AI Navigation 2.0.11, Timeline, Visual Scripting, Android Logcat, Multiplayer Center 1.0.1
- **Google/Firebase:** External Dependency Manager 1.2.187 (package) + the Firebase SDK vendored at `Assets/Firebase/` with `Assets/ExternalDependencyManager/`, `Assets/PlayServicesResolver/`, `Assets/GeneratedLocalRepo/`. Google sign-in runs through Firebase Auth (`Net/FirebaseAuthHandler.cs`) — the Google Sign-In and Play Games plugins were removed, though stale `GoogleSignIn.csproj` / `GooglePlayGames*.csproj` files still sit at the repo root
- **Asset-store plugins:** `Assets/Plugins/` — Hexasphere Grid System, DOTween (`Demigiant/`), Lean Touch (`CW/`), Lunar Console, Ultimate Clean GUI Pack, SimpleSky, Starfield Skybox; `Assets/NaughtyAttributes/` and `Assets/Simple Scroll-Snap/` sit at the Assets root
- **Editor-only:** ParrelSync (`Assets/Plugins/ParrelSync/`), MCP for Unity (`com.coplaydev.unity-mcp`, git dependency)
