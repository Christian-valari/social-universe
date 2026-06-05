# Social Universe — Technology Stack

*Companion to `ARCHITECTURE.md`. The recommended default stack, the alternatives, what each piece does, and what it costs to start. Prices/tiers move — treat the cost notes as "as of 2026, verify at checkout."*

---

## 1. Recommended Default Stack (at a glance)

| Layer | Choice | Why |
|---|---|---|
| Engine | **Unity 6** (URP) | Confirmed; URP for mobile-friendly cosmic visuals |
| Language | **C#** | Unity standard; server logic also C# (UGS Cloud Code) |
| Land grid | **Hexasphere Grid System** | Confirmed asset; tiles, selection, 12 pentagons |
| Input | **Unity Input System** | Touch + gyroscope (attitude sensor) |
| UI | **UI Toolkit** (+ **PrimeTween** for juice) | Modern Unity UI; uGUI is the fallback |
| Asset streaming | **Addressables** | Load only the active planet; stream tile state |
| DI / wiring | **VContainer** | Lightweight, fast dependency injection |
| Backend (online) | **Unity Gaming Services (UGS)** | One Unity-native ecosystem; server-authoritative economy |
| Realtime/presence | **Netcode for GameObjects v2** + **Relay/Lobby** | Casual co-located players per planet shard |
| Chat + safety | **Vivox** (Unified Safety / AI moderation) | Text + voice + built-in moderation (covers GDD safety) |
| Economy/persistence | **UGS Economy + Cloud Save + Cloud Code** | Currencies, inventory, purchases — server-authoritative |
| Monetization | **Unity IAP** + **Unity LevelPlay** (ads) | Store/season pass + opt-in rewarded ads |
| Notifications | **Mobile Notifications** + **FCM** | Cargo-full / fuel-ready / events |
| Diagnostics | **Unity Cloud Diagnostics** (or Sentry) | Crash + error reporting |
| Source control | **Git + Git LFS** on **GitHub** | Works cleanly with Claude Code |
| CI/CD | **GameCI** (GitHub Actions) or **Unity Build Automation** | Automated mobile builds |
| IDE | **Rider** or **Visual Studio** + **Claude Code** | C# editing + AI agent |
| Testing | **Unity Test Framework** (EditMode/PlayMode) | Per-service tests from the milestone plan |

**Strong alternatives** (pick during M0–M2): **Nakama** as a single cohesive backend (realtime + chat + storage + wallet + RPC) instead of UGS; **Photon Fusion 2** as the realtime layer instead of Netcode for GameObjects. Because gameplay sits behind `I*Service` interfaces (see `ARCHITECTURE.md`), this choice is swappable without rewriting gameplay.

---

## 2. Engine & Core (Unity-side)

- **Unity 6** — the engine and editor. Unity **Personal is free** for individuals/teams under **$200,000** annual revenue/funding, and the controversial Runtime Fee was **canceled** (reverted to seat-based subscriptions); the "Made with Unity" splash is optional in Unity 6 Personal. Unity Pro applies above $200k (≈$2,200/seat/yr, with a ~5% increase in Jan 2026). For an indie start, Personal is almost certainly your tier.
- **URP (Universal Render Pipeline)** — best balance of looks and mobile performance for the glow/space aesthetic.
- **C#** — all client code; UGS server logic (Cloud Code modules) is also C#.
- **Input System** — touch controls and the gyroscope via the **AttitudeSensor** (for Sky Discovery); always keep a non-gyro fallback path.
- **UI Toolkit** — for menus/HUD/screens (data-bound, scalable). **uGUI** (Canvas) is the fallback if you need heavy world-space or very custom animated widgets. Pair with **PrimeTween** (free, fast) or **DOTween** for the button-press, reward-burst, and HUD juice.
- **Addressables** — load/stream the active planet and its tile state; don't instantiate every planet.
- **VContainer** — dependency injection so services are constructed and swapped cleanly (Zenject is the heavier alternative).
- **Unity Test Framework** — EditMode + PlayMode tests; the milestone plan asks for one test per service.

---

## 3. World & Assets

- **Hexasphere Grid System** (Unity Asset Store) — confirmed; generates the sphere tiling, handles raycast selection and coloring, and gives you the 12 pentagons for free as landmark plots.
- **Have:** planet + moon, drone, asteroid, rocket models.
- **To acquire/commission:** a **customizable player avatar** (critical for a social game), a **space-station/hub** model, a **building/decoration set** for land, and a **UI kit**. A starfield skybox or VFX pack is also worth buying rather than building.

---

## 4. Backend & Online (the central decision)

A persistent social MMO with an economy needs auth, persistence, a server-authoritative economy, realtime presence, and chat. Two cohesive ways to get all of it:

### Option A — Unity Gaming Services (recommended)
One Unity-native suite; minimal backend code:
- **Authentication** — anonymous + Apple/Google sign-in.
- **Economy** — currencies (Coins/Stardust), inventory, virtual purchases, **server-authoritative** out of the box — a direct fit for your model.
- **Cloud Code** — your custom server functions (PurchaseLand, GrantOfflineIncome, ClaimYield, UpgradeDrone, ValidateReceipt…). C# modules.
- **Cloud Save** — player profile and state records.
- **Lobby + Relay** — group players into planet shards/rooms without dedicated servers to start.
- **Netcode for GameObjects v2** — replicate co-located players on a planet; Unity-6 compatible, supports server-authoritative and the newer distributed-authority topology.
- **Vivox** — text + voice chat with a built-in **AI moderation / Unified Safety** layer (Safe Text/Safe Voice), engine-agnostic SDK. This covers the GDD's launch-blocking moderation requirement.
- **Remote Config, Analytics, Matchmaker** — live tuning, funnels, optional matchmaking.

### Option B — Nakama (Heroic Labs)
A single open-source game server (Apache-2.0, free to self-host; managed cloud is paid) that bundles realtime multiplayer, **chat**, **storage**, **RPCs**, a **wallet/economy**, and **leaderboards**. Server logic in Go/TypeScript/Lua. Most cohesive if you'd rather own one backend than wire several UGS services.

### Realtime alternative — Photon Fusion 2
If you don't want Netcode for GameObjects, **Photon Fusion 2** is a Unity-verified state-sync SDK that supports up to ~200 players per room, with a **free 100-CCU tier** (≈40k MAU) and a 200-CCU "Plus" bundle for ~$95/year, scaling by CCU after. Note Photon's relay does **not** run your game logic, so you still need a backend (UGS Economy/Cloud Code or Nakama) for currency, ownership, and persistence.

> **Non-negotiable regardless of choice:** currency, land ownership, mining payouts, yield, upgrades, and purchases are computed and committed **on the server**. The client requests and reflects results.

---

## 5. Monetization

- **Unity IAP** — consumables (Stardust packs, fuel) and the season pass on the App Store / Google Play; pair with server **receipt validation** (Cloud Code / Nakama RPC).
- **Unity LevelPlay** (ironSource mediation) — opt-in **rewarded** ads only (fuel refill, double haul), per the GDD's cosmetics-first stance.
- **RevenueCat** (optional) — if you later add real subscriptions and want cross-platform entitlement management.

---

## 6. Platform & Live-Ops Services

- **Mobile Notifications** (Unity package) + **Firebase Cloud Messaging** — local + push notifications (cargo full, fuel ready, events, friend online).
- **Unity Cloud Diagnostics** (or **Sentry** / **Firebase Crashlytics**) — crash and error monitoring.
- **GameAnalytics** (free, mobile-focused) — alternative/supplement to UGS Analytics for retention/economy dashboards.

---

## 7. Sensors / AR

- **Default (recommended):** gyroscope-controlled virtual starfield via the **Input System AttitudeSensor** — simple, reliable, performant.
- **If you want true camera AR for Sky Discovery:** **AR Foundation** (ARKit on iOS, ARCore on Android) — more capability, more complexity and device gating. Decide before M5.

---

## 8. Tooling & Workflow (for building with Claude Code)

- **Git + Git LFS** on **GitHub** — Claude Code works directly with git; Git LFS handles large binary assets (models, textures). Add a Unity `.gitignore` and a `.gitattributes` for LFS. (**Unity Version Control / Plastic** is the alternative if your binary churn is heavy.)
- **GameCI** (GitHub Actions) or **Unity Build Automation** — automated iOS/Android builds and test runs on push. Unity DevOps free storage/egress allotments are expanding in 2026.
- **Rider** or **Visual Studio** — C# IDE; **Claude Code** as the coding agent driven by `ARCHITECTURE.md` + `CLAUDE.md`.
- **Xcode** (iOS) and **Android Studio / SDK** (Android) — required to build and ship to devices.
- **Project management** — Linear / Jira / Trello (optional); map issues to the M0–M11 milestones.

---

## 9. Cost to Start (indie scale)

You can build through prototype and soft-launch at roughly **$0 in engine/backend baseline**:
- **Unity Personal** is free under $200k revenue/funding.
- **UGS** services have free monthly allotments at low volume; **Vivox** has a free tier; **Photon** gives a free 100-CCU tier (≈40k MAU).
- **GitHub** and **GameCI** (GitHub Actions minutes) are free at small scale.

You start paying mainly when you **scale** (CCU/bandwidth, cloud save volume, IAP store fees of ~30% to Apple/Google, ad mediation revenue share) — i.e., costs track success. The notable fixed costs early are **Asset Store purchases** (Hexasphere you have; avatar/station/decor/UI to buy) and Apple's **$99/yr** developer program + Google Play's one-time **$25** fee.

---

## 10. Decisions to Lock (early)

1. **Backend:** UGS (recommended) vs Nakama — by M2.
2. **Realtime:** Netcode for GameObjects v2 vs Photon Fusion 2 — by M2/M4.
3. **UI:** UI Toolkit (recommended) vs uGUI — by M0/M11.
4. **Sky Discovery:** gyro starfield (recommended) vs AR Foundation — by M5.
5. **DI:** VContainer (recommended) vs Zenject — by M0.
6. **Source control:** Git + LFS (recommended) vs Unity Version Control — by M0.

All six are isolated behind interfaces/config in the architecture, so an early "good enough" pick is safe to revise.
