# Cross-device data consistency

**Goal:** player data stays consistent when one account moves between devices. A device always
resumes from the newest server state, and no stale or concurrent request erases newer data.

**Status (2026-10-15):** implemented and tested locally (Known Issue #21). **Deployed to `development` 2026-10-15 (not yet smoke-tested); not
committed.** It builds on [single active session](2026-10-15-single-active-session.md) (Known
Issue #20), and the two ship together.

## Data map (what exists today)

The client **never writes Cloud Save directly**: every write goes through Cloud Code. Every
client copy of server data (wallet, fleet, minerals, fuel, trip, land) is **scene-scoped**, built
fresh and rehydrated from the server on each scene load, and never sent back as a save. So
"force another local save" cannot be the fix. The stale-data risks are the device-local
PlayerPrefs hints and server read-modify-writes that ignore Cloud Save's `writeLock`.

| Data | Source of truth | Client copy | Writers | Concurrency today |
|---|---|---|---|---|
| Coins / Stardust | UGS Economy | `Wallet` (scene) | Cloud Code increments/decrements | atomic ✓ |
| `mineral_inventory`, `mining_claim_log` | Cloud Save Protected | `MineralService` (scene) | `ValidateMining`, `SellMinerals` | `writeLock` ✓ |
| `land_registry` | Custom Data | `LandRegistryService` (scene, polled) | 9 land functions | `writeLock` ✓ (#19) |
| `fuel_state` | Protected | `PlayerState` (scene) | `StartTravel`, `SpendFuel`, `RefillFuel` | `writeLock` ✓ |
| `current_planet` | Protected | PlayerPrefs `last_planet_id_<player>` (fallback only) | `LandTravel` (newest landing wins), `ValidateMining` pin (locked) | ✓ |
| `drone_fleet` | Protected | `DroneService` (scene) | `AcquireDrone`, `UpgradeDrone`, `UnlockDroneSlot`, `SetActiveDrone` | **no lock; coins charged before the write** |
| `owned_tiles_<planet>` | Protected | `LandRegistryService.Hydrate` | `PurchaseLand`, `SellLand` | **no lock** |
| `travel_state` | Protected | `PlayerState` + PlayerPrefs `travel_target_planet_id` (**device-global, used for routing**) | `StartTravel`, `LandTravel` | **check-then-write, no lock** |
| `player_profile` | Cloud Save Default | `ProfileService` | `UpdateProfile` (field-level merge) | **no lock** |
| `blocked_users` | Default | `ReportService` | `BlockUser` | **no lock** |
| Idle mining session, asteroid respawn timers | PlayerPrefs only | — | client | device-local |
| Music/SFX volume, email-prompt flag | PlayerPrefs only | — | client | device preference (intended) |

## Root causes

1. **Where the player is gets decided from a device-local flag** (the literal Mars → Earth case).
   - At launch, `BootState` and `HubState` choose "resume into Travel" from
     `PlayerPrefs.HasKey(TravelTargetId)`. The server's trip is fetched only inside the Travel
     scene, and `current_planet` changes only on landing.
   - So when a trip to Mars was started on device A, device B never learns of it: B resumes on
     the origin planet (Earth) and can mine there while the server has the player en route.
   - Worse, if the trip has arrived but not landed, `StartTravel` (which blocks only trips still
     in flight) lets B start a new trip that **overwrites the Mars trip**: the fuel is spent and
     the landing is lost. `GetTravelState` itself treats that trip as still traveling.
   - Also: the flag isn't per player (it leaks across accounts on one device); `AuthState`
     (explicit sign-in) ignores it entirely; and `StartTravel` takes its origin from B's stale
     scene.
2. **Lost updates on unlocked server writes.** Two overlapping requests — two devices in the
   takeover window, or one device's overlapping calls — each read the record, change it and write
   it back, so the later write erases the earlier one:
   - `drone_fleet`: coins are taken first, so a concurrent op can erase a purchase or upgrade the
     player paid for.
   - `owned_tiles`: two purchases on one planet can drop a tile from the list.
   - `travel_state`: two `StartTravel` calls both pass the check, so fuel is spent twice for one
     trip.
   - `player_profile`: a name change and an avatar change can erase each other.
   - `blocked_users`: a block or unblock can be lost.

## Policy

- **Source of truth is the server, always.** PlayerPrefs hints are mirrors: overwritten from the
  server at every sign-in, read only if that server read fails.
- **Revision = Cloud Save `writeLock`.** It is a per-record revision the server checks and replaces
  atomically. Every read-modify-write passes the lock it read. A 409 means a newer revision exists:
  re-read, **re-apply the operation** to the newest data (server-side merge — the requests are
  deltas, never client snapshots), and retry up to 3 times. After that the call returns
  `CONFLICT` (shown as "Something changed. Try again.").

| Data | Conflict policy |
|---|---|
| `drone_fleet` | Charge coins, then apply the op to the newest fleet under its lock. Charge first because a refund is an increment, which can't fail on balance; payouts keep state-first, as in #19. If the op no longer applies at the price charged (already owned, slots full, level or slot count changed meanwhile) → refund and return that reason or `CONFLICT`. If the write fails → refund. A failed refund is logged `REFUND FAILED`. |
| `owned_tiles_<planet>` | Lock + re-apply the add/remove of one tile id (idempotent). `land_registry` stays the truth; this list is the player's index. |
| `travel_state` | **One unlanded trip at a time.** `StartTravel` refuses while any trip is unlanded (`already_traveling`, which the client already resyncs on) and writes the trip under the lock it read. A concurrent start → 409 → fuel refunded and `already_traveling` returned. The origin comes from server `current_planet`; the client's value is used only when no record exists. |
| `current_planet` | Newest landing wins (no change). |
| `player_profile`, `blocked_users` | Lock + re-apply the single field or id change. |
| Economy and already-locked records | No change. |
| Idle mining session, respawn timers | Stay device-local (rewards are bounded by the server claim budget). Making idle sessions server-tracked would be a feature, not a fix — see follow-ups. |

**Device switch, foreground and reconnect:**
- With the single active session, a device can change data only while it holds the session.
  Another device signing in stops this one with the quit dialog (its next call, its 60 s check, or focus regain),
  and the next sign-in rehydrates every scene from the server.
- So no extra foreground refresh is needed. The only stale path left was the launch and sign-in
  routing above.
- No offline play exists (sign-in and every action need Cloud Code), so there is no offline merge.

**Retries:** `BackendClient` already retries only requests the server never accepted, plus
ambiguous failures for read-only `Get*` calls. Because every write now re-validates against the
newest revision, a delayed or repeated request can't replace newer data.

## Changes

- **Server:**
  - `GetCurrentPlanet` also returns `travelTargetId` (the target of any unlanded trip, or `null`).
  - `StartTravel`, the 4 drone scripts (shared helper block gains the lock), `PurchaseLand` and
    `SellLand` (the owned-tiles write only), `UpdateProfile` and `BlockUser` change as in the
    policy table.
- **Client:**
  - `CurrentPlanetResult.TravelTargetId`.
  - `BootState` and `AuthState` fetch it once, mirror the travel hint from it, then route: a
    pending trip → `TravelState` (`AuthState` gains this route); otherwise the server planet.
  - The routing decision is a pure function in `PlanetResumeResolver`, so it can be tested.
- **Diagnostics:**
  - Server: `warn` on each write conflict and give-up, `info` on refunds, `error` on
    `REFUND FAILED`.
  - Client: one `info` line at sign-in naming the server's planet and trip, or the fallback used.

## Checklist

- [x] Map every read/write path for player data and identify the current source of truth.
- [x] Identify stale-cache, async-race, overwrite, or conflict failure points.
- [x] Define the revision/version and conflict policy per affected data type.
- [x] Prevent stale local data from overwriting newer cloud data.
  - Local hints are mirrors, rewritten from the server at sign-in.
  - Stale origins are ignored.
  - An unlanded trip can't be overwritten.
- [x] Ensure saves are awaited, ordered, and retried safely.
  - `writeLock` on every read-modify-write.
  - A 409 re-applies the change to fresh data.
  - Failed reads throw.
  - Refunds when a paid change doesn't land.
- [x] Refresh/validate cloud state on login, reconnect, foreground, and device switch.
  - Sign-in routes on `GetCurrentPlanet`.
  - Scenes already rehydrate on load.
  - #20 signs out a device that falls behind.
- [x] Update cache only after confirmed cloud persistence. Hints are written from the server's
  answer; caches are already filled only from server replies.
- [x] Add focused diagnostics for save/load revision and conflict outcomes.
  - Server: conflict / give-up / refund / `REFUND FAILED` logs, plus a stale-origin warning.
  - Client: a resume decision line at sign-in.
- [x] Test Mars save → Earth login/load — simulated.
- [x] Test Earth's stale cache cannot overwrite Mars's newer save — simulated.
- [x] Test simultaneous writes from both devices — simulated (overlapping requests on every
  changed record).
- [x] Test interrupted save, reconnect, and app restart behavior — interrupted writes and
  failed reads simulated, app restart = sign-in routing (resolver tests). Not tried on a device.

**Testing plan:**
- Extend the scratchpad Node simulation (real scripts; fake Cloud Save with `writeLock`, fake
  Economy, Custom Data) with two-device scenarios:
  - a Mars trip on A seen by B at sign-in;
  - B's Earth departure refused while the Mars trip is unlanded;
  - concurrent `StartTravel`;
  - concurrent drone ops, both kept with coins matching;
  - concurrent purchases of the same drone, with one refund;
  - two tile purchases on one planet;
  - concurrent name and avatar changes;
  - a failed write followed by a refund;
  - a stale origin being ignored.
- EditMode: routing tests for `PlanetResumeResolver`, a `GetCurrentPlanet` response-contract
  test, and a drift test that the drone helper stays identical and writes with the lock.
- Then the full EditMode + PlayMode suites.

## Results (2026-10-15)

- **Node simulation** (throwaway, in the session scratchpad): real scripts against fakes of Cloud
  Save (protected, default and custom items, with `writeLock`/409 behaviour) and Economy, plus a
  controllable clock and failure injection.
  - **21/21 scenarios pass:** Mars → Earth sign-in, a stale departure refused, stale origin
    ignored, simultaneous departures, failed and interrupted writes, overlapping drone, land,
    profile and block-list writes, and one pinned known limit.
  - **The pre-change scripts fail 19 of 21.** They lose the purchased drone, charge 160 coins for
    one upgrade level, drop a bought tile, resurrect a sold one, and wipe profiles and block lists
    on a failed read.
  - The single-session simulation still passes 15/15.
- **Unity 6000.3.12f1 headless**, on a scratch copy: EditMode **504/504** (8 new), PlayMode
  **12/12**. Three deliberate breakages (an unlocked fleet write, an owned-tiles write outside its
  helper block, and the local trip hint winning over the server's) each fail the intended tests.
- **Not covered by automated tests:**
  - The `BootState` / `AuthState` scene transitions themselves (only the routing decision is
    unit-tested).
  - Anything on a real device or the live UGS service.

## Deploy / migration

- **No data migration:** `writeLock` is metadata, and record shapes are unchanged.
- **Existing arrived-but-unlanded trips** now block a new departure until landed; the client
  routes into Travel to land automatically.
- **Deploy pairing:** deploy the changed scripts together with Known Issue #20 and a client built
  from the same commit. Older clients reject the new `travelTargetId` field and fall back to their
  local hint, but they already fail under #20.

## Follow-ups (not in this change)

- Server-tracked idle mining sessions, so an idle run started on one device can be claimed on
  another.
- `GetBootstrapState` (uncalled) still seeds `drone_fleet` without a lock.
