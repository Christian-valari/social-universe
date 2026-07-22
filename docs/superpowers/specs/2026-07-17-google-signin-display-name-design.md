# Google Sign-In with First-Time Display-Name Panel — Design

**Date:** 2026-07-17
**Status:** Approved (chat, 2026-07-17); written doc reviewed and confirmed 2026-07-21
**Scope:** Two deliverables — (1) a mandatory choose-your-name panel shown after a player's *first* Google sign-in, and (2) restoring the deferred real-device Google Sign-In plugin work so the flow works on Android hardware.

## Background / current state

- `main` already has the service-level Google wiring: `IAuthService.SignInWithGoogleAsync(idToken)`, the UGS-backed `AuthService` implementation, `LocalMockAuthService.MockSsoSignInAsync("google")`, and a Google button in `AuthScreen` (`OnGoogleClicked`).
- `GoogleAuthHandler` on `main` is a stub that always throws `NotSupportedException` (commit `309f83ef` "Defer Google Sign-In to feature/google-signin branch"). `AuthScreen` catches this and substitutes `"mock_google_token"`, so the button works against the mock in-editor but cannot work on device.
- The full plugin implementation (Google Sign-In Unity plugin assets, EDM4U, Android glue, real `GoogleAuthHandler` with `WebClientId`) is preserved in git history at `309f83ef~1`. The `feature/google-signin` branch pointer has since moved and is now an ancestor of `main` — it no longer preserves that work. Untracked leftovers (`Assets/GoogleSignIn/`, `Assets/Plugins/Android/`) still sit on disk and must be reconciled, not blindly overwritten.
- The deferral reason (from the commit message): the plugin code lived in `Assembly-CSharp` with no asmdef, so `SocialUniverse.Net` could not reference `Google.*` types — Android Player builds failed with CS0246/CS0103.
- `AuthScreen` already has a panel system (`AuthPanel`: Login, Register, ForgotPasswordEmail, ForgotPasswordReset, VerifyEmail) and a `_suppressAutoTransition` flag that prevents `HandleSignedIn` from auto-publishing `PlayerReadyEvent` — the verify-email flow already holds the player at Auth and publishes `PlayerReadyEvent` manually when its gate passes. The choose-name panel uses the identical mechanism.
- An in-game `DisplayNameModal` (Planet scene, ProfileService-backed rename flow) exists and is untouched by this work.

## Decisions (user-confirmed)

1. **Scope:** modal flow **and** real device sign-in restore.
2. **Trigger:** modal appears on **first sign-in only** — i.e. when the signed-in account has no display name yet. Returning players go straight into the game.
3. **Mandatory:** no cancel/skip. The player cannot proceed past Auth without confirming a valid name (matches email signup, which always collects a username).
4. **OAuth credentials:** not yet created. Code ships with a clearly-marked placeholder plus a setup checklist for the user.
5. **UI home:** a new panel inside `AuthScreen`'s existing panel system (not a separate modal component, not the Planet-scene `DisplayNameModal`).

## Part 1 — Choose-name panel

### AuthScreen changes

- Add `AuthPanel.ChooseName` + serialized fields: `_chooseNamePanel` (GameObject), `_chooseNameInput` (TMP_InputField), `_chooseNameConfirmButton` (Button), `_chooseNameStatusText` (TMP_Text). Null-guarded like the forgot-password/verify fields so the scene degrades gracefully if unwired.
- `ShowPanel` extended to toggle the new panel and clear its status text.
- `OnGoogleClicked`:
  - Set `_suppressAutoTransition = true` before signing in, so `HandleSignedIn` does not auto-publish `PlayerReadyEvent`.
  - After `SignInWithGoogleAsync` succeeds:
    - `_auth.DisplayName` empty → `ShowPanel(AuthPanel.ChooseName)`.
    - `_auth.DisplayName` set → returning player → publish `PlayerReadyEvent` directly (mirroring the verify flow's manual publish).
  - On failure, restore the login panel/status as today.
- Confirm handler (`OnChooseNameConfirmed`):
  - Trim; validate via a pure static helper (see Testing): 2 chars minimum, 20 chars maximum (a local constant mirroring `SocialConfig.MaxDisplayNameLength`'s value — not a dependency on `SocialConfig` itself, which is only DI-registered in `RootLifetimeScope`/`PlanetSceneScope` and unavailable to the standalone `AuthSceneScope`, same reasoning as the ProfileService note below), no spaces (UGS `UpdatePlayerNameAsync` constraint).
  - `await _auth.UpdateDisplayNameAsync(name)`; on success publish `PlayerReadyEvent`; on failure show status text and let the player retry. Busy-state disables the confirm button during the await.

### Naming commit path

**UGS player name only — deliberately no ProfileService call.** Email registration also only calls `UpdatePlayerNameAsync(username)`, and `PlanetSceneScope` hydration prefers the Cloud Save profile name but falls back to `_auth.DisplayName`. Keeping Google and email flows identical avoids a DI problem: `ProfileService` is registered in `RootLifetimeScope`, not in the standalone `AuthSceneScope`, so injecting it into `AuthScreen` would break the scene's standalone mock mode.

### AuthService race fix (required for correct detection)

`AuthService.SignInWithGoogleAsync` currently returns as soon as UGS signs in, but player-name hydration runs asynchronously in the SignedIn callback — a returning player could be misdetected as first-time (empty `DisplayName` at check time). Fix: `await HydratePlayerNameAsync()` inside `SignInWithGoogleAsync`, exactly as `RegisterAsync` already does.

### Mock behavior

`LocalMockAuthService.MockSsoSignInAsync("google")` must yield an empty `DisplayName` on first Google sign-in (so the panel appears in-editor) and persist the chosen name so a second mock sign-in skips the panel. Adjust the mock if its current behavior differs.

## Part 2 — Real device sign-in restore

- **Recover plugin assets** from `309f83ef~1`: `Assets/GoogleSignIn/`, `Assets/GeneratedLocalRepo/`, `Assets/PlayServicesResolver/`, Android/iOS glue under `Assets/Plugins/`, and `com.google.external-dependency-manager` in `manifest.json`. Reconcile with the untracked leftovers already on disk (diff before overwrite; keep whichever is complete).
- **Fix the asmdef gap** that caused the original deferral: add `GoogleSignIn.asmdef` over the plugin runtime code (plus an Editor asmdef if the plugin has an `Editor/` folder), and reference it from `SocialUniverse.Net.asmdef`. Restore the full `GoogleAuthHandler` (`#if UNITY_ANDROID && !UNITY_EDITOR` device path; editor path keeps throwing `NotSupportedException` so the mock fallback still works). Expect a compile-iterate loop here; the plugin's own code may need platform constraints on its asmdef.
- **`GoogleAuthConfig` ScriptableObject** (architecture rule 3) under `Assets/_Project/ScriptableObjects/`, in `SocialUniverse.Config`, holding `WebClientId` with a placeholder value. `GoogleAuthHandler` receives it via a `Configure(string webClientId)` call at bootstrap (RootLifetimeScope) rather than a hardcoded const.
- **Setup checklist doc** for the user-only steps:
  1. Google Cloud Console: OAuth consent screen; create a **Web application** client ID; create an **Android** client ID with the app's package name and the SHA-1 of `zKeystore/user.keystore` (include the `keytool` command).
  2. UGS dashboard: enable Google as an identity provider, using the **Web** client ID.
  3. Paste the Web client ID into the `GoogleAuthConfig` asset.
  4. Device smoke test: first sign-in → name panel → enter game; reinstall/sign-in again → straight in.

## Error handling

- Google token acquisition failure / user cancels the Google sheet → `FriendlyError` status on the login panel, busy cleared (existing path).
- `UpdateDisplayNameAsync` failure (network, UGS rejection) → status text on the choose-name panel, player retries; the session stays signed in but gated at Auth.
- App quit while gated at the name panel: the account exists but is nameless — next Google sign-in re-detects the empty name and shows the panel again (self-healing; no extra state needed).

## Testing

- **EditMode:** pure static display-name validation helper (`DisplayNameValidator` or similar, public-DTO testability pattern per M3) — length bounds, trimming, empty, spaces.
- **Play Mode (manual, in-editor, mock):** first Google sign-in shows panel; invalid names rejected with correct messages; valid name → enters game; second sign-in skips panel.
- **Device (manual, after user completes OAuth setup):** checklist smoke test above.

## Out of scope

- Apple Sign-In.
- Renaming flow changes (existing in-game `DisplayNameModal` untouched).
- Account linking (Google onto an existing email account).
- iOS Google Sign-In (Android first; iOS glue restored but unverified).
