# Firebase Authentication via UGS OpenID Connect — Design

**Date:** 2026-07-29
**Status:** Approved (chat, 2026-07-29)
**Supersedes (partial):** The identity/token-acquisition layer of `2026-07-23-google-signin-play-games-plugin-design.md`, `2026-07-17-google-signin-display-name-design.md`, `2026-07-01-email-login-auth-design.md`, and `2026-07-02-post-login-email-verification-design.md`. The UGS **data** layer (Cloud Save, Cloud Code economy, land, wallet, progression) is unchanged.
**Reference docs:**
- UGS custom OpenID Connect: https://docs.unity.com/ugs/manual/authentication/manual/platform-signin-custom-openid-connect
- Firebase Auth (Unity): https://firebase.google.com/docs/auth/unity/start

## Motivation

Move **player authentication** from UGS Authentication to **Firebase Authentication** (email/password + Google), while **UGS keeps handling all player data unchanged**. Firebase becomes the identity source of truth; UGS trusts Firebase-issued ID tokens through a custom **OpenID Connect (OIDC)** provider, so every existing Cloud Save / Cloud Code system keeps working keyed to the same UGS `PlayerId`.

Secondary wins from the cutover:
- Retires the entire **Google Play Games v2** plugin and its SHA-1 / Play-upload-block / emulator pain.
- Retires the **custom Cloud Code OTP** machinery (email verification, password reset, email-availability index) in favour of Firebase's built-in, standard flows.
- Removes the **`EmailLoginKey` hashing hack** — Firebase uses the real email as the identity.

## Decisions (chat, 2026-07-29)

1. **Pre-launch cutover** — no production accounts exist, so **no data migration / account-linking** is required. Firebase OIDC mints fresh UGS PlayerIds.
2. **Firebase-native** email verification + password reset (email-link based). The Cloud Code OTP functions are retired.
3. **Plain Google via Firebase** (not Play Games). The Play Games plugin is removed.
4. **Google token acquisition on Android:** start with Firebase `FederatedOAuthProvider` (`SignInWithProviderAsync`, `"google.com"`) — a Chrome Custom Tabs web flow with **zero extra native dependency**. The native Credential Manager sheet is a later polish upgrade, out of scope here.
5. **Apple sign-in** is out of scope for this pass; the `IAuthService` method is left as a stub to be routed through Firebase's Apple provider later.

## Guiding principle — change one layer only

Gameplay depends on `IAuthService` (`PlayerId`, `IsSignedIn`, `DisplayName`, …) per Architecture Rule #2. Only the **implementation** changes; the abstraction and every data-layer consumer stay put. UGS remains the game-data backend.

## Target flow

```
AuthScreen ──1. authenticate (email/pw or Google)──▶ Firebase Auth (identity)
AuthScreen ◀──── Firebase User + ID token (JWT) ────
   │ 2. AuthenticationService.Instance.SignInWithOpenIdConnectAsync("oidc-firebase", idToken)
   ▼
UGS Authentication — validates JWT vs Firebase issuer/JWKS, returns UGS PlayerId keyed to Firebase uid (`sub`)
   │ 3. same PlayerId as always
   ▼
UGS Cloud Save + Cloud Code (economy, land, wallet, progression) — UNCHANGED
```

**Why OIDC validates:** Firebase ID tokens are JWTs with `iss = https://securetoken.google.com/<PROJECT_ID>`, `aud = <PROJECT_ID>`, `sub = <firebase-uid>`, and Firebase publishes the OIDC discovery doc + JWKS at that issuer.

## One-time setup (outside code)

1. **Firebase project** — enable Email/Password + Google providers. Add `google-services.json` (Android) and `GoogleService-Info.plist` (iOS) under `Assets/`.
2. **Import Firebase Unity SDK** — `FirebaseAuth` (+ `FirebaseApp`). EDM4U (already present as a UPM package) resolves native deps. Exclude any bundled EDM4U copy to avoid the duplicate-DLL problem seen with prior plugin imports.
3. **UGS Dashboard → Authentication → ID Providers → OpenID Connect (Custom):**
   - Name → provider id **`oidc-firebase`**
   - Issuer / config URL → `https://securetoken.google.com/<PROJECT_ID>`
   - Client ID (audience) → `<PROJECT_ID>`

## Changes — code

### New / rewritten

- **`Net/FirebaseAuthHandler.cs` (new).** Owns every Firebase call and nothing else: `CreateUserWithEmailAndPasswordAsync`, `SignInWithEmailAndPasswordAsync`, Google sign-in via `FederatedOAuthProvider`, `SendEmailVerificationAsync`, `SendPasswordResetEmailAsync`, `TokenAsync(forceRefresh)`, `User.ReloadAsync()`, `CurrentUser`, `SignOut`, `User.DeleteAsync`. Native-only paths (Google) are guarded so the Editor keeps compiling; `Configure(FirebaseAuthConfig)` mirrors the old `GoogleAuthHandler.Configure`.
- **`Net/AuthService.cs`.** Each method becomes: authenticate via `FirebaseAuthHandler` → get ID token → `AuthenticationService.Instance.SignInWithOpenIdConnectAsync("oidc-firebase", token)` → hydrate player name (existing logic preserved). `TryAutoSignInAsync` now checks `FirebaseAuth.CurrentUser`, gets a fresh token, and re-bridges to UGS — replacing the UGS anonymous-session-restore trick.
- **`Core/IAuthService.cs`.**
  - Keep: `SignInWithEmailAsync(email, password)`, `RegisterAsync(username, password, email)` (username is display-name only now), `SignOutAsync`, `UpdateDisplayNameAsync`, `DeleteAccountAsync`, `TryAutoSignInAsync`, `InitializeAsync`, `RequestPasswordResetAsync(email)` (→ Firebase reset email).
  - Change: `SignInWithGoogleAsync()` becomes parameterless (handler runs the whole flow). `RequestEmailVerificationCodeAsync` → `SendEmailVerificationAsync`. Add `Task<bool> ReloadAndCheckVerifiedAsync()`.
  - Remove: `ConfirmPasswordResetAsync`, `ConfirmEmailVerificationCodeAsync`, `IsEmailAvailableAsync`, `IsAnonymous`.
  - `SignInWithAppleAsync` → stub (throws NotSupported for now).
- **`Net/NetworkBootstrap.cs`.** Initialise `FirebaseApp` (`FirebaseApp.CheckAndFixDependenciesAsync`) alongside `UnityServices.InitializeAsync`; expose readiness.
- **`App/RootLifetimeScope.cs`.** Replace the `GoogleAuthConfig` field/wiring with `FirebaseAuthConfig`; call `FirebaseAuthHandler.Configure(...)`. `AuthService`/mocks registration otherwise unchanged.
- **`Config/FirebaseAuthConfig.cs` (new ScriptableObject; replaces `GoogleAuthConfig`).** Holds `ProjectId` and any web client id needed for the Google provider. Per Architecture Rule #3, tunables stay in a `*Config` SO.
- **`UI/AuthScreen.cs`.**
  - Google button calls `SignInWithGoogleAsync()` (no token param); Editor keeps a mock fallback.
  - **Verify-email panel** UX changes from "enter OTP code" to "we emailed a verification link → tap *I've verified*", which calls `ReloadAndCheckVerifiedAsync()` and advances only when verified.
  - **Forgot-password** collapses to a single step: enter email → "reset link sent". The code+new-password reset panel is removed.
  - Remove the `IsEmailAvailable` pre-check and the anonymous `EnsureSession` transport dance.
- **`Net/LocalMockAuthService.cs`.** Update to the new interface so the `_devMode` editor flow keeps working end-to-end.

### Retired (deleted with `.meta`)

- `Net/EmailLoginKey.cs` — the UGS-username hashing hack.
- `Net/GoogleAuthHandler.cs` and the **Google Play Games v2 plugin**: `Assets/GooglePlayGames/…`, `Assets/Plugins/Android/GooglePlayGamesManifest.androidlib`, the Play Games proguard keep-rules, and the `Google.Play.Games` reference in `SocialUniverse.Net.asmdef`.
- **Cloud Code functions** `SaveEmail`, `CheckEmailAvailable`, `RequestPasswordReset`, `ConfirmPasswordReset`, `RequestEmailVerificationCode`, `ConfirmEmailVerificationCode`, `ClearPlayerEmail`, and the `email_lookup` Cloud Save index.
- The anonymous "transport session" concept throughout `AuthScreen`/`AuthService`.

### asmdef / packages

- `SocialUniverse.Net.asmdef`: drop `Google.Play.Games`, add the Firebase.Auth assembly reference.
- Import the Firebase Unity SDK unitypackage; commit `google-services.json`. Confirm `.gitattributes` keeps `.aar`/`.srcaar` marked `binary` (already fixed previously — do not regress).

## Session / verification semantics

- **Boot:** init UnityServices + FirebaseApp → if `FirebaseAuth.CurrentUser != null`, get a fresh ID token and OIDC-sign-in to UGS. Google users are always verified; email users must have `IsEmailVerified` (else route to the verify panel). Otherwise show Login.
- **Email verification gate is retained** — an unverified email account does not enter the game. The gate is enforced client-side via `User.IsEmailVerified` after `ReloadAsync`.
- **Sign-out / delete:** sign out or delete the Firebase user *and* delete the UGS account/data (both sides), replacing the old UGS-only paths.

## Risks / open items

1. **`player_profile.email` sourcing.** Before deleting the Cloud Code email functions, grep `ServerCode/` and the client for any gameplay read of `player_profile.email` (profile screen, moderation). If found, either keep a slimmed `SaveEmail` or source the address from `FirebaseAuth.CurrentUser.Email`.
2. **Editor Firebase support.** Firebase Auth email/password works in Editor play mode; Google (`FederatedOAuthProvider`) does not. The existing `_devMode` mock path and the Google mock fallback cover Editor work.
3. **Google web-flow UX.** `FederatedOAuthProvider` shows a Custom Tabs web consent, not the native one-tap sheet — accepted for this pass; Credential Manager native sheet is a later upgrade.
4. **Firebase password policy** defaults to 6+ chars; the existing 8–30 complexity check stays client-side for consistent UX.

## Testing

- **Editor `_devMode`** (mocks): full Bootstrap → Auth → Planet scene flow with no Firebase/UGS dependency.
- **Device:** real Firebase email/password + Google sign-in → confirm a UGS `PlayerId` is minted, and Cloud Save / land / wallet round-trip under it.
- **Email verification gate** and **Firebase reset link** verified end-to-end.
- Sign-out and delete-account clear both Firebase and UGS state.
