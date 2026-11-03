# Single active session per account

**Goal:** one account is in active use on one device at a time. The newest sign-in wins; the
device it replaces is stopped promptly and told why (since 2026-10-17 it stays signed in and is
asked to quit; reopening the app takes the account back). Enforced server-side in Cloud Code; the
client only reacts.

**Status (2026-10-15):** implemented and tested locally (Known Issue #20). **Deployed to `development` 2026-10-15 (not yet smoke-tested); not
committed.** A live two-device test still needs the pending OIDC/Economy dashboard setup and a
client built from the deployed commit.

## Current flow (what exists today)

- **Identity:** Firebase Auth (email/password, Google) → `AuthService.BridgeToUgsAsync` exchanges
  the Firebase ID token for a UGS session (`SignInWithOpenIdConnectAsync`, provider
  `oidc-firebase`). Every sign-in path goes through it: email, register, Google, and
  `TryAutoSignInAsync` (relaunch with a persisted Firebase user, called by `BootState`).
- **Tokens:** the UGS SDK refreshes its 1-hour access token on its own. `SignedIn` does not fire on
  refresh (`HandleStateChanged` skips `Refreshing → Authorized`). UGS has **no server-side API to
  revoke a player's session**: an old device's UGS token stays valid until it signs itself out.
- **Server authority:** every economy and state write goes through Cloud Code (`ServerCode/*.js`,
  called through `IBackendClient` / `BackendClient`). Server-trusted records are Cloud Save
  **Protected** items (player-readable, only Cloud Code writes them).
- **Direct SDK paths (not Cloud Code):** Economy balance reads (`EconomyService.GetWalletAsync`),
  Cloud Save default-class reads/writes (`CloudSaveService`), Vivox chat and presence, Friends.
- **Lifecycle:** no persistent connection (no Netcode/Relay); presence is the Vivox channel roster.
  `PlayerReadyEvent` brings chat online and starts the game; `LogoutState` signs out, publishes
  `PlayerLoggedOutEvent` (chat disconnects) and reloads the Auth scene. No reconnect manager, and
  nothing reacts to app backgrounding today.
- **Ordering hazard:** `OnSignedIn` is raised from the UGS `SignedIn` event, so `AuthScreen`
  publishes `PlayerReadyEvent` and game calls start before `BridgeToUgsAsync` has returned.

## Design

**Enforcement point:** Cloud Code. No Cloud Code middleware exists, so every deployed script runs
the same `requireActiveSession` block first (duplicated per script, like the `land_registry`
helper, with a drift test).

**Server record:** Protected Cloud Save item `active_session` = `{ sessionId, claimedAt }`, one per
player.

| Script | Behaviour |
|---|---|
| `ClaimSession` (new) | No `sessionId` → fresh sign-in: writes a new id (`context.correlationId`) and returns it. That one write replaces any other device's session (last write wins, so two simultaneous sign-ins leave exactly one valid). With `sessionId` → read-only confirmation that it is still current. |
| `ReleaseSession` (new) | Explicit logout: clears the record only if it still holds the caller's id, writing with the record's `writeLock` so a newer claim that lands in between is never cleared. |
| Every other deployed script (30) | First statement: `await requireActiveSession(context, params.sessionId)`. A missing or non-current id throws `SESSION_REPLACED`. Read failures fail closed. Declares `sessionId: "String"`. |

**Client:**

- `BackendClient` stamps `sessionId` on every call (`IBackendClient.SessionId`). If a call fails
  with `SESSION_REPLACED`, it clears the id, publishes `SessionReplacedEvent` (Core) and rethrows.
- `AuthService` claims the session inside `BridgeToUgsAsync`, after the name fetch, and raises
  `OnSignedIn` only once the claim succeeds. That fixes the ordering hazard: no game call can go
  out without a session.
  - Every sign-in — explicit (email, Google, register) or a relaunch (`TryAutoSignInAsync`) —
    claims a fresh session (newest launch wins). Changed 2026-10-17 at the owner's request:
    before, a relaunch resumed the id saved in PlayerPrefs and a replaced device had to sign in
    again; now reopening the app on a replaced device takes the account back without credentials.
  - Claim fails for any other reason (e.g. offline) → UGS is signed out and the sign-in fails as it
    would today.
  - `SignOutAsync` calls `ReleaseSession` (best effort) and then clears local state.
- `ActiveSessionController` (App, new entry point): while in a game state it re-checks
  `ClaimSession` every 60 s and immediately when the app regains focus. When
  `SessionReplacedEvent` fires in a game state, it leaves chat and shows `QuitDialogView`
  (prefab `Assets/_Project/Prefabs/UI/Resources/QuitDialog.prefab`), whose only button quits the
  app; the device is not signed out. In Boot/Auth/Logout it does nothing.

**Behaviour matrix:**

| Case | Result |
|---|---|
| New device signs in | Old device is rejected on its next call, its next 60 s check, or when it regains focus. It leaves chat and is shown a dialog whose only button, Quit, closes the app. It stays signed in; reopening the app takes the account back (2026-10-17). |
| Two simultaneous sign-ins | Last write wins; the loser gets the quit dialog on its next call. |
| Explicit logout | Session released; the id can never be used again. |
| Crash / OS kill | Nothing is held open. Relaunch claims a fresh session; any new sign-in replaces it. No lockout. |
| Same-device reconnect / network loss | The id lives in memory, independent of the UGS token. Calls resume when the network returns. Network errors never sign the player out. |
| Token refresh | Unaffected: the session id is not tied to the UGS access token. |

## Limitations / follow-ups (not in this change)

- **Direct SDK paths are not enforced server-side.** A modified client that ignores the sign-out
  keeps its UGS token: it can keep chatting on Vivox, using Friends, and writing default-class
  Cloud Save (`player_profile`, `blocked_users`). The economy is fully covered. Closing this needs
  UGS Access Control rules denying direct Cloud Save writes (profile writes would move behind
  Cloud Code) and server-issued Vivox tokens.
- **A colluding modified client** could copy its own session id to a second device. No
  session-id scheme stops the account owner from doing that without device attestation.
- **An old device's in-flight call** that passed the check just before a takeover still commits.
- **Client and server must ship as a pair** (Known Issue #13 again). Once deployed, every older build
  fails every Cloud Code call with `SESSION_REPLACED`.
- **Not verified against the live server:** the `SESSION_REPLACED` text in
  `CloudCodeException.Message` (`ScriptError`), and `context.correlationId` being present.
- **Cost:** one extra Cloud Save read per Cloud Code call, plus one check per minute per active
  player.

## Checklist

- [x] Map current authentication, token, and network session flow.
- [x] Identify the authoritative backend/session enforcement point.
- [x] Define active-session data and invalidation behavior.
- [x] Implement atomic session replacement during login (`ClaimSession.js`, `AuthService`).
- [x] Validate current session ownership for authenticated/network access (guard in every deployed script, `BackendClient` stamping).
- [x] Handle old-device invalidation and user-facing messaging (`SessionReplacedEvent`, `ActiveSessionController`, `AuthScreen`).
- [x] Handle logout, reconnect, backgrounding, crashes, and session expiry (`ReleaseSession.js`, fresh claim on relaunch, focus check).
- [x] Test simultaneous logins from two devices — simulated (server scripts); live test pending deploy.
- [x] Test old-device requests after new-device login — simulated, all 30 guarded scripts; live test pending deploy.
- [x] Test logout and same-device reconnect — simulated; live test pending deploy (runbook §4 steps 10–12).
- [ ] Test temporary network loss and stale-session recovery — stale resume is simulated, but the client's network-loss handling is checked only by code review. Needs a device.

## Results (2026-10-15)

- **Node simulation** (throwaway, in the session scratchpad): loads the real `ServerCode/*.js`
  against an in-memory Cloud Save that honours `writeLock`. 15/15 scenarios pass:
  - one fresh claim, then a takeover by a second device;
  - 25 rounds of simultaneous claims, each leaving exactly one valid session;
  - resume of the current session (no write) and of a stale one (rejected);
  - a missing id, a missing record (empty result and 404), and a failed read (fails closed);
  - release of the current id, release of a stale id, a claim racing a release, and release with
    no id;
  - all 30 guarded scripts reject a stale or missing id before any other I/O, and accept the
    current one.

  The simulation catches both deliberate breakages tried: release without the `writeLock`, and a
  guard that swallows read errors.
- **Unity 6000.3.12f1 headless**, on a scratch copy:
  - Before the change: EditMode 487/487 existing tests pass; the 2 new guard tests fail as
    expected.
  - After the change: EditMode **496/496**, PlayMode **12/12**.
  - The new controller and auth tests were also run against deliberately broken code: 4 of them
    fail, each where expected.
- **Not covered by automated tests:**
  - `BackendClient`'s stamping and `SESSION_REPLACED` detection, and `AuthService`'s
    claim/resume/release. Both call static UGS/Firebase SDKs, so they are checked only by review.
  - Focus-regain checks, since `Application.focusChanged` can't be raised from a test.
