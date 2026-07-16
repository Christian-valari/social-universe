# Auth Scene Flow Improvements — Design

**Date:** 2026-07-16
**Branch:** `worktree-fix-auth-forgot-password-verify-email`
**Status:** Approved

## Goal

Four improvements to the Auth scene flow:

1. Registration requires email verification: after account creation a verify
   panel is shown; cancelling it deletes the just-created account.
2. Registration explicitly checks whether the email is already used, via a
   Cloud Code pre-check (not just UGS ENTITY_EXISTS).
3. Forgot Password splits into two sequential panels: (a) email + Send Code,
   (b) code + new password + confirm + Reset Password.
4. Anonymous sign-in is an internal mechanism only: guest play is removed,
   anonymous sessions are signed out when the app closes mid-flow, and no
   anonymous session may ever reach the Planet scene.

## Decisions (user-confirmed)

- **Guest play removed entirely.** The Guest button is deleted from
  `AuthScreen` and `Auth.unity`. Anonymous sign-in exists only to satisfy
  Cloud Code's authenticated-session requirement during registration
  pre-check and forgot-password.
- **Unverified account deletion trigger: Cancel on the verify panel.**
  No app-close deletion, no next-registration cleanup (residual risks
  accepted, see below).
- **Email availability: explicit Cloud Code pre-check** against the
  `email_lookup` Cloud Save index, before account creation.
- **After successful verification the player proceeds straight into the game**
  (`PlayerReadyEvent`), no re-login.

## Registration flow (anonymous-upgrade)

UGS natively upgrades an anonymous account when `SignUpWithUsernamePasswordAsync`
is called while signed in anonymously. The flow is one continuous session:

1. Local validation of username/email/password (unchanged).
2. `EnsureSessionAsync()` (existing) establishes an anonymous session with the
   auto-transition suppression flag raised.
3. New Cloud Code `CheckEmailAvailable(email)` — if taken, show
   "An account with that email already exists", stay on Register panel,
   leave the anonymous session for reuse.
4. `SignUpWithUsernamePasswordAsync(loginKey, password)` upgrades the anonymous
   account; `UpdatePlayerNameAsync`; `SaveEmail` (all existing code paths).
5. `RequestEmailVerificationCodeAsync()` (existing function; reads
   `player_profile.email` server-side), then show the **Verify Email panel**.
6. **Verify** → `ConfirmEmailVerificationCodeAsync(code)` (existing; sets
   `player_profile.emailVerified = true`) → drop suppression → publish
   `PlayerReadyEvent` → Planet.
7. **Resend Code** → `RequestEmailVerificationCodeAsync()` again.
8. **Cancel** → new `IAuthService.DeleteAccountAsync()` → back to Login panel.

The existing ENTITY_EXISTS → "already exists" error mapping stays as a
backstop: accounts registered before the `email_lookup` index existed are
invisible to the pre-check (Cloud Save indexes do not backfill).

## Verify Email panel

A 4th `AuthPanel` inside `AuthScreen` (`VerifyEmail`), not a reuse of the
Planet-scene `EmailVerificationModal` (whose `PlayerState`/`IAudioManager`/HUD
dependencies don't exist in the Auth scene). Fields: code input, Verify
button, Resend Code button, Cancel button, status text. The Planet-scene
modal remains unchanged for legacy/unverified accounts.

## Forgot Password — two panels

- `ForgotPasswordEmail` panel: email field, Send Code button, Back to Login.
- `ForgotPasswordReset` panel: code field, new password, confirm password,
  Reset Password button, Back (to the email panel).
- Send-code success auto-advances email → reset panel.
- **Bugfix:** after a successful `ConfirmPasswordResetAsync`, sign the
  anonymous session out (`SignOutAsync`) before returning to Login. Today the
  anonymous session survives the reset and the next email login throws UGS's
  "already signed in" error.

## Anonymous session lifecycle

- New `IAuthService.IsAnonymous`: true when the UGS account has no external
  identities (`PlayerInfo.Identities` empty; fetched via `GetPlayerInfoAsync`
  where not already cached).
- New `IAuthService.DeleteAccountAsync()`: UGS `DeleteAccountAsync()` +
  sign-out with cleared credentials.
- **AuthScreen guard:** `HandleSignedIn` never publishes `PlayerReadyEvent`
  while `IsAnonymous` (belt-and-suspenders under the suppression flag).
- **BootState guard:** after `TryAutoSignInAsync` restores a cached session,
  check `IsAnonymous`; if anonymous → `SignOut(clearCredentials: true)`,
  return false → normal Auth scene. This is the mobile safety net for
  swipe-kills that skip quit callbacks.
- **App close:** `AuthScreen.OnApplicationQuit` — if signed in and
  (anonymous OR mid-verification) → `SignOut(clearCredentials: true)`.

## Server code

New `ServerCode/CheckEmailAvailable.js`: elevated `email_lookup` Cloud Save
query (same pattern as `RequestPasswordReset.js`'s `findPlayerByEmail`),
returns `{ available: boolean }`. Deployment is manual; it must be deployed
alongside the other auth functions and shares their dashboard prerequisites
(service-account secrets, `email_lookup` index).

## Mocks and tests

- `LocalMockAuthService` implements `IsAnonymous`, `DeleteAccountAsync`, and a
  mock email-availability behavior so the Auth scene runs standalone.
- New EditMode tests where logic is extractable; the existing EditMode suite
  must stay green.

## Residual risks (accepted)

- App killed mid-verification: the unverified account persists and holds its
  email. Next launch lands at Login (boot guard), but re-registering that
  email reports "already exists". A "next registration cleans up" server
  function can be added later without redesign.
- A restored session for a registered-but-unverified account still enters the
  game at boot; verification is enforced at registration UX only. The
  Planet-scene HUD verification modal covers those accounts.
