# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Social Universe is a Unity 6 social MMO where players explore a solar system, mine asteroids, own hexagonal land tiles on planets, and interact with other players. The full architecture, milestone roadmap, and script inventory live in `Social_Universe_Architecture.md` — read it before any task.

**Current state:** Fresh Unity 6 project (URP). No gameplay scripts exist yet. Development starts at M0 (Foundation).

## Running Tests

Tests run through Unity's Test Runner (Window > General > Test Runner). EditMode tests require no Play Mode; PlayMode tests run in-editor or on-device. There is no CLI build script yet.

To run tests from command line (headless):
```
"C:\Program Files\Unity\Hub\Editor\<version>\Editor\Unity.exe" -runTests -projectPath . -testResults results.xml -testPlatform EditMode
```

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
| `Core/` | `SocialUniverse.Core` | Bootstrap, FSM, EventBus, SceneLoader, DI |
| `Config/` | `SocialUniverse.Config` | ScriptableObject definitions + DatabaseRegistry |
| `World/` | `SocialUniverse.World` | Planet, hexasphere, tiles, camera |
| `Mining/` | `SocialUniverse.Mining` | Drones, asteroids, mining loop |
| `Economy/` | `SocialUniverse.Economy` | Wallet, land, marketplace, yield |
| `Net/` | `SocialUniverse.Net` | Auth, backend client, presence, shards |
| `Social/` | `SocialUniverse.Social` | Chat, friends, profiles, moderation |
| `Travel/` | `SocialUniverse.Travel` | Star map, fuel, sky discovery, rocket |
| `Progression/` | `SocialUniverse.Progression` | Player state, XP, quests, daily |
| `Guild/` | `SocialUniverse.Guild` | Stations, guilds, events |
| `Store/` | `SocialUniverse.Store` | IAP, season pass, ads |
| `Safety/` | `SocialUniverse.Safety` | Age gate, moderation hooks, analytics |
| `UI/` | `SocialUniverse.UI` | UIManager, screens (MVP pattern), HUD, juice |

Server-side logic (Cloud Code / Nakama RPCs) lives in `ServerCode/` at the repo root — this folder is **not** included in the Unity build.

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

## Open Decisions (do not resolve without flagging)

- **Backend:** UGS vs Nakama — undecided until M2. Keep all backend access behind `I*Service`.
- **Sky Discovery:** camera AR (AR Foundation) vs gyroscope starfield — undecided until M5.
- **DI framework:** VContainer vs hand-rolled Service Locator — decide in M0.

## Installed Packages

- **Rendering:** URP 17.3.0
- **Input:** Unity Input System 1.19.0
- **UI:** Unity UGUI 2.0.0
- **Multiplayer tooling:** Multiplayer Center 1.0.1
- **Testing:** Unity Test Framework 1.6.0
- **Hexasphere Grid System** — not yet in `manifest.json`; to be acquired before M1.
