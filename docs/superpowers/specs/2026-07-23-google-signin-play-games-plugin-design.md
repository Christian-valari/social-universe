# Google Sign-In — Swap Token Acquisition to Google Play Games Plugin — Design

**Date:** 2026-07-23
**Status:** Approved (chat, 2026-07-23)
**Supersedes (partial):** Part 2 of `2026-07-17-google-signin-display-name-design.md` — the token-acquisition plugin choice. Part 1 (choose-name panel) and the whole UGS/`IAuthService` layer are unchanged.
**Reference docs:** https://docs.unity.com/en-us/authentication/platform-signin/google

## Motivation

Two problems, one root:

1. **The Android build is broken.** Gradle fails transforming `google-signin-support-1.0.4.aar` (`java.util.zip.ZipException: invalid distance too far back`) — the `.aar` shipped by the `google-signin-unity` plugin is corrupt. Both `:unityLibrary:compileReleaseJavaWithJavac` and `:launcher:checkReleaseDuplicateClasses` fail on it.
2. **The current token-acquisition plugin diverges from the official Unity docs.** The linked docs acquire the Google ID token via the **Google Play Games plugin** (`PlayGamesPlatform` + `.RequestIdToken()` + `GetIdToken()`), not via `google-signin-unity` (`Google.GoogleSignIn.DefaultInstance.SignIn()`).

User decision (2026-07-23): follow the docs — replace `google-signin-unity` with the Google Play Games plugin. Removing `google-signin-unity` also removes the corrupt `.aar`, fixing the build break.

## Guiding principle — change one layer only

The UGS side already matches the docs and is **not touched**:

- `IAuthService.SignInWithGoogleAsync(idToken)` → `AuthService` → `AuthenticationService.Instance.SignInWithGoogleAsync(idToken)`.
- The Google button, `OnGoogleClicked`, and the first-sign-in choose-name panel in `AuthScreen`.
- The editor mock-token fallback (`catch (NotSupportedException) { idToken = "mock_google_token"; }`).
- `GoogleAuthConfig` (the `WebClientId` ScriptableObject). UGS still validates the device-acquired ID token against the **Web** client ID.

`GoogleAuthHandler`'s **public surface is preserved verbatim** — `Configure(GoogleAuthConfig)` and `Task<string> GetIdTokenAsync()` — so no caller (`AuthScreen`, `AuthService`, `RootLifetimeScope`) changes. Only the `#if UNITY_ANDROID && !UNITY_EDITOR` internals swap from `Google.*` to `GooglePlayGames.*`.

## Current state (verified in worktree)

- `Assets/GoogleSignIn/` — plugin runtime + `Editor/` + bundled `m2repository`, under `GoogleSignIn.asmdef`.
- `Assets/GeneratedLocalRepo/GoogleSignIn/Editor/m2repository` — EDM4U-generated local maven repo (source of the corrupt `.aar`).
- `Assets/GoogleSignIn/Editor/GoogleSignInSupportDependencies.xml` — EDM4U `androidPackage spec="com.google.signin:google-signin-support:1.0.4"`.
- `SocialUniverse.Net.asmdef` references the `GoogleSignIn` asmdef. **It is the only asmdef that does** (besides the plugin's own).
- `GoogleAuthHandler.cs` references `Google.GoogleSignIn`, `Google.GoogleSignInConfiguration`, `Google.GoogleSignInUser` under the Android guard.

## Changes — code (automatable)

### 1. Remove `google-signin-unity`

Delete (with `.meta` files):
- `Assets/GoogleSignIn/` (entire folder — runtime, `Editor/`, bundled `m2repository`, `GoogleSignIn.asmdef`, `GoogleSignInSupportDependencies.xml`).
- `Assets/GeneratedLocalRepo/GoogleSignIn/` (EDM4U-generated; regenerated on demand for other deps if any — this subtree is Google-Sign-In-specific).

This removes the corrupt `.aar` and its EDM4U dependency entry, resolving the Gradle failure.

### 2. Rewrite `GoogleAuthHandler` Android path (follow the docs)

Keep the public surface (`Configure`, `GetIdTokenAsync`) and the editor path (throws `NotSupportedException` → mock fallback intact). Replace the Android internals with the Play Games flow from the docs:

- One-time init (guarded so it runs once per session): build a config requesting an ID token, `PlayGamesPlatform.InitializeInstance(config)`, `PlayGamesPlatform.Activate()`.
- Authenticate, then read the ID token (`GetIdToken()` on the authenticated local user).
- The plugin's auth callback is bridged to the existing `Task<string>` signature via a `TaskCompletionSource<string>`: authentication success with a non-empty ID token completes the task; cancel/failure/empty-token faults it with an exception so `AuthScreen`'s existing `catch` shows `FriendlyError` and clears busy.
- Keep the placeholder guard: if `WebClientId` is empty or starts with `YOUR_`, throw `InvalidOperationException` pointing at the setup checklist.

**API-version caveat (explicit).** The docs' sample uses the older `PlayGamesClientConfiguration.Builder().RequestIdToken()` / `Social.localUser.Authenticate` / `PlayGamesLocalUser.GetIdToken()` surface. Plugin v10.x may expose a revised API (e.g. `PlayGamesPlatform.Instance.Authenticate(callback)`). The handler is written to the docs' surface as the starting point; the **exact calls must be reconciled against the actually-imported plugin** in a compile-iterate loop on the Android target. This cannot be verified in-editor (the code is behind `UNITY_ANDROID && !UNITY_EDITOR`) nor via reflection until the plugin is imported.

### 3. Rewire asmdefs

- **Phase A:** remove `"GoogleSignIn"` from `SocialUniverse.Net.asmdef` references (that asmdef is deleted; a dangling reference would error).
- **Phase B (post-import):** add the Google Play Games plugin runtime assembly reference(s) (the plugin ships its own asmdef, e.g. `GooglePlayGames`) so `SocialUniverse.Net` can see `GooglePlayGames.*`. Exact assembly name(s) confirmed after import. Deferred to Phase B because a reference to a not-yet-present assembly breaks editor compilation (see §Sequencing).

### 4. Update setup checklist

`docs/google-signin-setup-checklist.md` gains the Play Games plugin prerequisites (keep existing Cloud Console / UGS / `WebClientId` steps):
- Import the Google Play Games plugin v10.14 `.unitypackage`.
- Google Play Console → **Play Games Services**: create/configure the game, link the **Android** OAuth client (same SHA-1 + package name as the existing Android client ID step), add license testers.
- Note that the **Web** client ID is still the one pasted into `GoogleAuthConfig` and enabled in UGS (unchanged).

## Changes — manual (user only; cannot be automated)

1. Import Google Play Games plugin **v10.14** `.unitypackage` from
   https://github.com/playgameservices/play-games-plugin-for-unity/releases/tag/v10.14
   into the Unity Editor.
2. Google Play Console → Play Games Services setup (game, Android OAuth client link, testers).
3. Existing checklist items: Cloud Console **Web** + **Android** OAuth client IDs; enable **Google** in the UGS Authentication dashboard with the **Web** client ID; paste the **Web** client ID into `Assets/_Project/ScriptableObjects/GoogleAuthConfig.asset`.

## Sequencing (two phases — split by the manual plugin import)

**Phase A (now, automatable):** remove `google-signin-unity`; rewrite `GoogleAuthHandler`'s Android path per the docs; **remove** the now-dangling `"GoogleSignIn"` reference from `SocialUniverse.Net.asmdef`; update the checklist. Editor compiles green — the new `GooglePlayGames.*` calls sit behind `#if UNITY_ANDROID && !UNITY_EDITOR` and are excluded from the editor assembly, so no plugin is needed for the editor to compile.

The Play Games **asmdef reference is deliberately NOT added in Phase A**: adding a reference to an assembly that doesn't exist yet breaks editor compilation of `SocialUniverse.Net`. Until Phase B, an Android build would fail `CS0246` on the `GooglePlayGames.*` calls — expected and intended.

**Phase B (after the user imports the plugin + completes Play Console/OAuth setup):** add the plugin assembly reference to `SocialUniverse.Net.asmdef`; reconcile the `GoogleAuthHandler` API against the actually-imported plugin (compile-iterate loop, §Changes-code-2 caveat); Android-target compile verification; device smoke test. This pass can be driven via Unity MCP reflection once the plugin's types are present.

## Error handling (unchanged behavior)

- Token acquisition failure / user cancels the Google sheet → task faults → `AuthScreen`'s existing `catch` → `FriendlyError` status on the login panel, busy cleared.
- Missing/placeholder `WebClientId` → `InvalidOperationException` pointing at the checklist.
- Editor / non-Android → `NotSupportedException` → mock token path (dev flow unaffected).

## Testing

- **EditMode:** none added — the Android path can't execute in-editor. Existing `DisplayNameValidator` / auth tests remain green (untouched surface).
- **Play Mode (editor, mock):** Google button → mock token → choose-name panel on first sign-in, straight-in on return. Unchanged by this work; re-run to confirm no regression.
- **Device (manual, after user completes plugin import + Play Console/OAuth setup):** first Google sign-in → name panel → enters game; reinstall/clear-data → sign in again → straight into game.

## Out of scope

- Apple Sign-In; account linking; iOS Google sign-in.
- The choose-name panel and any `AuthScreen`/`AuthService`/UGS behavior (all preserved).
- Migrating to the docs' recommended post-deprecation flows (newer Play Games sign-in / Unity Player Accounts) — considered and declined in favor of following the linked docs directly.
