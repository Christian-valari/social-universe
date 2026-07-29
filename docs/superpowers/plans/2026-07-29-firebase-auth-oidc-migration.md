# Firebase Auth via UGS OpenID Connect — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Move player authentication from UGS Authentication to Firebase Authentication (email/password + Google), bridging into UGS via a custom `oidc-firebase` OpenID Connect provider so all UGS Cloud Save / Cloud Code data keeps working unchanged.

**Architecture:** Firebase becomes the identity source of truth. `FirebaseAuthHandler` wraps the Firebase SDK; `AuthService` authenticates via Firebase, obtains the Firebase ID token, then calls `AuthenticationService.Instance.SignInWithOpenIdConnectAsync("oidc-firebase", idToken)`. Gameplay depends only on `IAuthService`, so nothing downstream of the auth layer changes.

**Tech Stack:** Unity 6 (URP), C#, VContainer DI, Unity Gaming Services (Authentication + Cloud Save + Cloud Code), Firebase Unity SDK (FirebaseAuth), NUnit EditMode tests, EDM4U for native dependency resolution.

**Spec:** `docs/superpowers/specs/2026-07-29-firebase-auth-oidc-migration-design.md`

## Global Constraints

- **Namespaces/assemblies** (CLAUDE.md): auth runtime code → `SocialUniverse.Net` (`SocialUniverse.Net.asmdef`); interfaces/events → `SocialUniverse.Core`; config SOs → `SocialUniverse.Config`; UI → `SocialUniverse.UI`; DI scopes → `SocialUniverse.App`. Tests → `SocialUniverse.Tests`.
- **Architecture Rule #2:** gameplay depends only on `I*Service`; never reference the Firebase or UGS SDK outside `SocialUniverse.Net`.
- **Architecture Rule #3:** tunables (Firebase project id / web client id) live in a `*Config` ScriptableObject under `Assets/_Project/ScriptableObjects/`, not hardcoded.
- **UGS OIDC provider id:** exactly `oidc-firebase` (must start with `oidc-`).
- **Firebase issuer:** `https://securetoken.google.com/<PROJECT_ID>`; audience/client-id = `<PROJECT_ID>`.
- **Pre-launch cutover:** no account migration — do NOT add linking/backfill logic.
- **Editor safety:** every Firebase native call path must be behind `#if !UNITY_EDITOR` guards or a `NotSupportedException` fallback so the Editor keeps compiling and the `_devMode` mock flow keeps working.
- **`.gitattributes`:** `*.aar`/`*.srcaar`/`*.jar` are marked `binary` — do NOT regress this (prior corruption root cause).
- **Statically-typed compile constraint:** `IAuthService` and all its implementors (`AuthService`, `LocalMockAuthService`) and callers (`AuthScreen`, tests) must compile together. Tasks that change the interface land as one compiling commit.
- **Running EditMode tests:** MCP `run_tests` (EditMode) if the Unity bridge is up, else Test Runner window, else headless: `"C:\Program Files\Unity\Hub\Editor\<version>\Editor\Unity.exe" -runTests -projectPath . -testResults results.xml -testPlatform EditMode`. After any script change, check `read_console` for compile errors before proceeding.

---

## Task ordering rationale

Firebase types can only be referenced after the SDK is imported (Task 1). The `IAuthService` contract change is a single compiling unit (Task 5) because C# cannot half-change an interface. Everything before Task 5 is additive and keeps the current build green; Task 5 is the cutover; Tasks 6–9 clean up and verify.

---

### Task 1: Import Firebase Unity SDK (infra)

**Files:**
- Create: `Assets/Firebase/**` (from the unitypackage), `Assets/google-services.json`, `Assets/StreamingAssets/google-services-desktop.json` (Editor)
- Verify: `.gitattributes` binary rules intact

**Interfaces:**
- Produces: the `Firebase.Auth` and `Firebase` (FirebaseApp) assemblies, referenceable from `SocialUniverse.Net.asmdef` in later tasks.

- [ ] **Step 1:** Create the Firebase project in the Firebase console; enable **Email/Password** and **Google** sign-in providers. Register the Android app with the project's package name and SHA-1; download `google-services.json` into `Assets/`. (This is manual console work — record the `<PROJECT_ID>` for Task 10.)
- [ ] **Step 2:** Import the Firebase Unity SDK `FirebaseAuth` package (`FirebaseAuth.unitypackage`, which also brings `FirebaseApp`). **Exclude any bundled `ExternalDependencyManager/` folder** — the project already has EDM4U as a UPM package; a duplicate causes duplicate-DLL errors (same failure mode documented for prior plugin imports).
- [ ] **Step 3:** Open **Assets → External Dependency Manager → Android Resolver → Force Resolve**. Confirm it completes without duplicate-class errors.
- [ ] **Step 4:** Verify the Editor compiles clean: `read_console` shows no errors, `editor_state.isCompiling` is false. The SDK is imported but unused at this point.
- [ ] **Step 5: Commit**

```bash
git add Assets/Firebase Assets/google-services.json Assets/StreamingAssets .gitattributes Assets/**/*.meta
git commit -m "build: import Firebase Unity SDK (FirebaseAuth) for OIDC auth migration"
```

---

### Task 2: `FirebaseAuthConfig` ScriptableObject

**Files:**
- Create: `Assets/_Project/Scripts/Config/FirebaseAuthConfig.cs`
- Create asset: `Assets/_Project/ScriptableObjects/FirebaseAuthConfig.asset`
- Test: `Assets/_Project/Tests/EditMode/Config/FirebaseAuthConfigTests.cs`
- (Later deleted in Task 8: `Assets/_Project/Scripts/Config/GoogleAuthConfig.cs`)

**Interfaces:**
- Produces: `FirebaseAuthConfig` with `string ProjectId` and `string GoogleWebClientId` getters; used by `FirebaseAuthHandler.Configure` (Task 4) and `RootLifetimeScope` (Task 6).

- [ ] **Step 1: Write the failing test**

```csharp
// Assets/_Project/Tests/EditMode/Config/FirebaseAuthConfigTests.cs
using NUnit.Framework;
using UnityEngine;
using SocialUniverse.Config;

namespace SocialUniverse.Tests
{
    public class FirebaseAuthConfigTests
    {
        [Test]
        public void Defaults_are_placeholders_so_missing_setup_is_detectable()
        {
            var cfg = ScriptableObject.CreateInstance<FirebaseAuthConfig>();
            StringAssert.StartsWith("YOUR_", cfg.ProjectId);
            Assert.IsNotNull(cfg.GoogleWebClientId);
        }
    }
}
```

- [ ] **Step 2:** Run the EditMode suite (filter `FirebaseAuthConfigTests`). Expected: FAIL — `FirebaseAuthConfig` does not exist.
- [ ] **Step 3: Write minimal implementation**

```csharp
// Assets/_Project/Scripts/Config/FirebaseAuthConfig.cs
using UnityEngine;

namespace SocialUniverse.Config
{
    // Firebase project identity used to bridge Firebase Auth into UGS via the
    // custom `oidc-firebase` OpenID Connect provider. ProjectId is the OIDC
    // issuer audience (https://securetoken.google.com/<ProjectId>);
    // GoogleWebClientId is the OAuth web client for the Google provider.
    [CreateAssetMenu(menuName = "SocialUniverse/Config/FirebaseAuthConfig", fileName = "FirebaseAuthConfig")]
    public class FirebaseAuthConfig : ScriptableObject
    {
        [SerializeField] private string _projectId = "YOUR_FIREBASE_PROJECT_ID";
        [SerializeField] private string _googleWebClientId = "YOUR_WEB_CLIENT_ID.apps.googleusercontent.com";

        public string ProjectId         => _projectId;
        public string GoogleWebClientId => _googleWebClientId;
    }
}
```

- [ ] **Step 4:** Run the test. Expected: PASS.
- [ ] **Step 5:** Create the asset via `Assets → Create → SocialUniverse/Config/FirebaseAuthConfig` at `Assets/_Project/ScriptableObjects/FirebaseAuthConfig.asset`; fill in the real `<PROJECT_ID>` and the Google **Web** client id (from the Firebase/Google Cloud console).
- [ ] **Step 6: Commit**

```bash
git add Assets/_Project/Scripts/Config/FirebaseAuthConfig.cs Assets/_Project/ScriptableObjects/FirebaseAuthConfig.asset Assets/_Project/Tests/EditMode/Config/FirebaseAuthConfigTests.cs Assets/**/*.meta
git commit -m "config: add FirebaseAuthConfig ScriptableObject"
```

---

### Task 3: `NetworkBootstrap` initialises FirebaseApp

**Files:**
- Modify: `Assets/_Project/Scripts/Net/NetworkBootstrap.cs`

**Interfaces:**
- Consumes: existing `AppConfig`, `UnityServices.InitializeAsync`.
- Produces: after `InitializeAsync()` completes, `FirebaseApp` dependencies are checked/fixed and Firebase Auth is safe to call.

- [ ] **Step 1:** Add the Firebase dependency check to `InitializeAsync`, after UGS init. Reference `Firebase` and `Firebase.Auth`:

```csharp
using Firebase;
// ...
public async Task InitializeAsync()
{
    if (IsInitialized) return;

    var envName = _appConfig.Environment switch
    {
        AppEnvironment.Production  => "production",
        AppEnvironment.Development => "development",
        _                          => "development"
    };

    var options = new InitializationOptions().SetEnvironmentName(envName);
    await UnityServices.InitializeAsync(options);

    // Firebase is the identity source of truth; ensure its native deps are
    // present before any sign-in. On a misconfigured device this throws and
    // Bootstrap surfaces it rather than failing silently at first sign-in.
    var depStatus = await FirebaseApp.CheckAndFixDependenciesAsync();
    if (depStatus != DependencyStatus.Available)
        throw new System.InvalidOperationException($"Firebase dependencies unavailable: {depStatus}");

    IsInitialized = true;
    SULog.Info($"UGS + Firebase initialized (env: {envName})", SULog.Channel.Net);
}
```

- [ ] **Step 2:** Add `Firebase` (and `Firebase.Auth` if resolver needs it) to the reference list in `SocialUniverse.Net.asmdef` (see Task 4 Step 1 — may already be added there; ensure both `NetworkBootstrap` and `FirebaseAuthHandler` compile).
- [ ] **Step 3:** `read_console` → confirm clean compile. (No unit test — this is device/runtime behaviour; verified in Task 10 smoke test.)
- [ ] **Step 4: Commit**

```bash
git add Assets/_Project/Scripts/Net/NetworkBootstrap.cs Assets/_Project/Scripts/Net/SocialUniverse.Net.asmdef
git commit -m "net: initialise FirebaseApp dependencies in NetworkBootstrap"
```

---

### Task 4: `FirebaseAuthHandler` (native Firebase wrapper)

**Files:**
- Create: `Assets/_Project/Scripts/Net/FirebaseAuthHandler.cs`
- Modify: `Assets/_Project/Scripts/Net/SocialUniverse.Net.asmdef` (add `Firebase.Auth`, `Firebase` refs)

**Interfaces:**
- Consumes: `FirebaseAuthConfig` (Task 2).
- Produces (static API used by `AuthService` in Task 5):
  - `void Configure(FirebaseAuthConfig config)`
  - `Task<string> RegisterEmailAsync(string email, string password)` → returns fresh ID token
  - `Task<string> SignInEmailAsync(string email, string password)` → returns fresh ID token
  - `Task<string> SignInGoogleAsync()` → returns fresh ID token (Android web flow; throws `NotSupportedException` in Editor)
  - `Task<string> GetFreshIdTokenAsync()` → `CurrentUser.TokenAsync(true)`
  - `bool HasCurrentUser { get; }`
  - `string CurrentEmail { get; }`
  - `bool IsEmailVerified { get; }`
  - `Task SendEmailVerificationAsync()`
  - `Task SendPasswordResetAsync(string email)`
  - `Task<bool> ReloadAndCheckVerifiedAsync()`
  - `Task DeleteCurrentUserAsync()`
  - `void SignOut()`

- [ ] **Step 1:** Add `"Firebase.Auth"` and `"Firebase"` to the `references` array in `SocialUniverse.Net.asmdef` (precise assembly names as resolved by EDM4U — confirm via the Firebase asmdef/assembly names in the Project window). Remove nothing yet.
- [ ] **Step 2: Write the implementation**

```csharp
// Assets/_Project/Scripts/Net/FirebaseAuthHandler.cs
using System;
using System.Threading.Tasks;
using Firebase.Auth;
using SocialUniverse.Config;
using SocialUniverse.Core;

namespace SocialUniverse.Net
{
    // Owns every Firebase Auth call and nothing else. AuthService consumes this
    // to get a Firebase ID token, then bridges it into UGS via OpenID Connect.
    // Email/password works in the Editor; Google (FederatedOAuthProvider web
    // flow) does not — the Editor path throws NotSupportedException and
    // AuthScreen substitutes a mock, mirroring the retired GoogleAuthHandler.
    public static class FirebaseAuthHandler
    {
        private static FirebaseAuthConfig _config;
        private static FirebaseAuth Auth => FirebaseAuth.DefaultInstance;

        public static void Configure(FirebaseAuthConfig config) => _config = config;

        public static bool   HasCurrentUser  => Auth.CurrentUser != null;
        public static string CurrentEmail    => Auth.CurrentUser?.Email;
        public static bool   IsEmailVerified => Auth.CurrentUser?.IsEmailVerified ?? false;

        public static async Task<string> RegisterEmailAsync(string email, string password)
        {
            var result = await Auth.CreateUserWithEmailAndPasswordAsync(email, password);
            return await result.User.TokenAsync(true);
        }

        public static async Task<string> SignInEmailAsync(string email, string password)
        {
            var result = await Auth.SignInWithEmailAndPasswordAsync(email, password);
            return await result.User.TokenAsync(true);
        }

        public static async Task<string> SignInGoogleAsync()
        {
#if UNITY_EDITOR
            throw new NotSupportedException("Google sign-in is unavailable in the Unity Editor");
#else
            // FederatedOAuthProvider: Chrome Custom Tabs web consent, no native
            // Google Sign-In dependency. providerId "google.com".
            var provider = new FederatedOAuthProvider();
            provider.SetProviderData(new FederatedOAuthProviderData { ProviderId = "google.com" });
            var result = await Auth.CurrentUser_SignInWithProviderAsync(provider); // see note
            return await result.User.TokenAsync(true);
#endif
        }

        public static Task<string> GetFreshIdTokenAsync() => Auth.CurrentUser.TokenAsync(true);

        public static Task SendEmailVerificationAsync() => Auth.CurrentUser.SendEmailVerificationAsync();

        public static Task SendPasswordResetAsync(string email) => Auth.SendPasswordResetEmailAsync(email);

        public static async Task<bool> ReloadAndCheckVerifiedAsync()
        {
            if (Auth.CurrentUser == null) return false;
            await Auth.CurrentUser.ReloadAsync();
            return Auth.CurrentUser.IsEmailVerified;
        }

        public static Task DeleteCurrentUserAsync() => Auth.CurrentUser.DeleteAsync();

        public static void SignOut() => Auth.SignOut();
    }
}
```

> **Implementation note for the engineer:** the exact Google federated-sign-in call in the Firebase Unity SDK is `auth.SignInWithProviderAsync(FederatedOAuthProvider)` (instance method on `FirebaseAuth`), not the placeholder `CurrentUser_SignInWithProviderAsync` shown above — verify the exact method name against the imported SDK version's `FirebaseAuth`/`FirebaseUser` API and use the one that returns a `SignInResult`/`AuthResult`. Keep the return contract (fresh ID token) identical.

- [ ] **Step 3:** `read_console` → confirm clean compile (Editor uses the `#if UNITY_EDITOR` throw branch, so no native Google symbols are needed to compile).
- [ ] **Step 4: Commit**

```bash
git add Assets/_Project/Scripts/Net/FirebaseAuthHandler.cs Assets/_Project/Scripts/Net/SocialUniverse.Net.asmdef Assets/**/*.meta
git commit -m "net: add FirebaseAuthHandler wrapping Firebase Auth (email/password + Google)"
```

---

### Task 5: Cutover — new `IAuthService` contract, `AuthService`, `LocalMockAuthService`, tests (single compiling commit)

This is the migration's core. It changes the interface and every implementor/caller together. Sub-steps build up the change; it commits once, green.

**Files:**
- Modify: `Assets/_Project/Scripts/Core/IAuthService.cs`
- Modify: `Assets/_Project/Scripts/Net/AuthService.cs`
- Modify: `Assets/_Project/Scripts/Net/LocalMockAuthService.cs`
- Modify: `Assets/_Project/Tests/EditMode/Net/LocalMockAuthServiceTests.cs`
- Modify: `Assets/_Project/Tests/EditMode/Net/AuthServiceTests.cs`

**Interfaces:**
- Consumes: `FirebaseAuthHandler` (Task 4), `AuthenticationService.Instance.SignInWithOpenIdConnectAsync(string, string)` (UGS SDK, already present).
- Produces: the new `IAuthService` surface consumed by `AuthScreen` (Task 7):
  - Unchanged: `IsSignedIn`, `SessionTokenExists`, `PlayerId`, `Username`, `DisplayName`, `Email`, `OnSignedIn`, `OnSignInFailed`, `InitializeAsync()`, `TryAutoSignInAsync()`, `SignInWithEmailAsync(email, password)`, `RegisterAsync(username, password, email)`, `SignOutAsync()`, `UpdateDisplayNameAsync(name)`, `DeleteAccountAsync()`, `RequestPasswordResetAsync(email)`
  - Changed: `SignInWithGoogleAsync()` — **now parameterless**
  - Renamed: `RequestEmailVerificationCodeAsync()` → `SendEmailVerificationAsync()`
  - Added: `Task<bool> ReloadAndCheckVerifiedAsync()`; `bool IsEmailVerified { get; }`
  - Removed: `ConfirmPasswordResetAsync`, `ConfirmEmailVerificationCodeAsync`, `IsEmailAvailableAsync`, `IsAnonymous`
  - Stubbed: `SignInWithAppleAsync(idToken)` throws `NotSupportedException`

- [ ] **Step 1: Rewrite `IAuthService.cs`** to the surface above. Full file:

```csharp
using System;
using System.Threading.Tasks;

namespace SocialUniverse.Core
{
    public interface IAuthService
    {
        bool   IsSignedIn         { get; }
        bool   SessionTokenExists { get; }
        string PlayerId           { get; }
        string Username           { get; }  // cosmetic handle; null for Google/SSO accounts
        string DisplayName        { get; }  // in-game display name
        string Email              { get; }  // Firebase account email; null if unknown
        bool   IsEmailVerified    { get; }  // Firebase email-verification state

        event Action            OnSignedIn;
        event Action<Exception> OnSignInFailed;

        Task InitializeAsync();

        // Resume a persisted Firebase session and re-bridge into UGS via OIDC.
        // Returns true if signed in afterwards.
        Task<bool> TryAutoSignInAsync();

        Task SignInWithEmailAsync(string email, string password);
        Task RegisterAsync(string username, string password, string email);
        Task SignInWithGoogleAsync();
        Task SignInWithAppleAsync(string idToken); // stub for this pass (throws NotSupported)
        Task SignOutAsync();

        Task UpdateDisplayNameAsync(string displayName);
        Task DeleteAccountAsync();

        // Firebase-native email flows (replace the retired Cloud Code OTP).
        Task RequestPasswordResetAsync(string email); // sends Firebase reset link
        Task SendEmailVerificationAsync();             // sends Firebase verification link
        Task<bool> ReloadAndCheckVerifiedAsync();      // reloads Firebase user, returns IsEmailVerified
    }
}
```

- [ ] **Step 2: Rewrite `AuthService.cs`** — Firebase authenticate → OIDC bridge. Key methods (preserve the existing player-name hydration helpers `HydratePlayerNameAsync`/`FetchPlayerNameAsync` and the `SignedIn`/`SignInFailed` event wiring from `InitializeAsync`):

```csharp
private const string OidcProvider = "oidc-firebase";

public bool   IsSignedIn         => AuthenticationService.Instance.IsSignedIn;
public bool   SessionTokenExists => AuthenticationService.Instance.SessionTokenExists;
public string PlayerId           => AuthenticationService.Instance.PlayerId;
public string Username           => AuthenticationService.Instance.PlayerName;
public string DisplayName        => AuthenticationService.Instance.PlayerName;
public string Email              => FirebaseAuthHandler.CurrentEmail;
public bool   IsEmailVerified    => FirebaseAuthHandler.IsEmailVerified;

private async Task BridgeToUgsAsync(string firebaseIdToken)
{
    await AuthenticationService.Instance.SignInWithOpenIdConnectAsync(OidcProvider, firebaseIdToken);
    await HydratePlayerNameAsync();
}

public async Task SignInWithEmailAsync(string email, string password)
{
    string idToken = await FirebaseAuthHandler.SignInEmailAsync(email, password);
    await BridgeToUgsAsync(idToken);
    SULog.Info($"Signed in with Firebase email (playerId: {PlayerId})", SULog.Channel.Net);
}

public async Task RegisterAsync(string username, string password, string email)
{
    string idToken = await FirebaseAuthHandler.RegisterEmailAsync(email, password);
    await BridgeToUgsAsync(idToken);
    if (!string.IsNullOrEmpty(username))
        await AuthenticationService.Instance.UpdatePlayerNameAsync(username);
    await FirebaseAuthHandler.SendEmailVerificationAsync();
    SULog.Info($"Registered Firebase account (playerId: {PlayerId})", SULog.Channel.Net);
}

public async Task SignInWithGoogleAsync()
{
    string idToken = await FirebaseAuthHandler.SignInGoogleAsync();
    await BridgeToUgsAsync(idToken);
    SULog.Info($"Signed in with Google via Firebase (playerId: {PlayerId})", SULog.Channel.Net);
}

public Task SignInWithAppleAsync(string idToken) =>
    throw new NotSupportedException("Apple sign-in is not yet implemented via Firebase");

public async Task<bool> TryAutoSignInAsync()
{
    if (IsSignedIn) return true;
    if (!FirebaseAuthHandler.HasCurrentUser) return false;
    try
    {
        string idToken = await FirebaseAuthHandler.GetFreshIdTokenAsync();
        await BridgeToUgsAsync(idToken);
        SULog.Info($"Restored Firebase session (playerId: {PlayerId})", SULog.Channel.Net);
        return true;
    }
    catch (Exception ex)
    {
        SULog.Warn($"Failed to restore session: {ex.Message}", SULog.Channel.Net);
        return false;
    }
}

public Task RequestPasswordResetAsync(string email) => FirebaseAuthHandler.SendPasswordResetAsync(email);
public Task SendEmailVerificationAsync()            => FirebaseAuthHandler.SendEmailVerificationAsync();
public Task<bool> ReloadAndCheckVerifiedAsync()     => FirebaseAuthHandler.ReloadAndCheckVerifiedAsync();

public async Task DeleteAccountAsync()
{
    // Delete both sides: Firebase identity and UGS account/data.
    try { await FirebaseAuthHandler.DeleteCurrentUserAsync(); }
    catch (Exception ex) { SULog.Warn($"Firebase delete failed: {ex.Message}", SULog.Channel.Net); }
    await AuthenticationService.Instance.DeleteAccountAsync();
    AuthenticationService.Instance.SignOut(clearCredentials: true);
    SULog.Info("Account deleted (Firebase + UGS)", SULog.Channel.Net);
}

public Task SignOutAsync()
{
    FirebaseAuthHandler.SignOut();
    AuthenticationService.Instance.SignOut(clearCredentials: true);
    SULog.Info("Signed out (Firebase + UGS)", SULog.Channel.Net);
    return Task.CompletedTask;
}
```

Remove from `AuthService`: the `_email`/`_isAnonymous` fields and their uses, `EmailLoginKey` usage, the `SaveEmail`/`CheckEmailAvailable`/OTP `_backend.CallAsync` calls, `IsEmailAvailableAsync`, `ConfirmPasswordResetAsync`, `ConfirmEmailVerificationCodeAsync`, `RequestEmailVerificationCodeAsync`. The `IBackendClient _backend` constructor dependency is no longer needed for auth — drop it (and update the DI registration signature in Task 6). Keep `UpdateDisplayNameAsync` as-is (UGS `UpdatePlayerNameAsync`).

- [ ] **Step 3: Rewrite `LocalMockAuthService.cs`** to the new interface. Preserve its in-memory realism but drop anonymous/availability/OTP-confirm. Concretely:
  - Add `bool IsEmailVerified` backed by a per-user `Verified` flag (default false on register, set true by `ReloadAndCheckVerifiedAsync` to simulate the user clicking the link — or keep a mock toggle; simplest: `ReloadAndCheckVerifiedAsync` marks the current user verified and returns true).
  - `SignInWithGoogleAsync()` parameterless → same deterministic `mock_google` identity as before.
  - `SendEmailVerificationAsync()` → no-op logging.
  - `RequestPasswordResetAsync(email)` → keep (no-op/log).
  - Remove `SignInAnonymouslyAsync`, `IsAnonymous`, `IsEmailAvailableAsync`, `ConfirmPasswordResetAsync`, `ConfirmEmailVerificationCodeAsync`, `RequestEmailVerificationCodeAsync`.
  - `SignInWithAppleAsync(idToken)` → `throw new NotSupportedException(...)`.

- [ ] **Step 4: Update `LocalMockAuthServiceTests.cs`** — delete tests for removed behaviour (anonymous session, email availability, OTP confirm codes, anonymous-upgrade-on-register) and add tests for the new contract:

```csharp
[Test]
public async Task Registered_user_starts_unverified_then_verifies()
{
    await _auth.RegisterAsync("Player1", "Passw0rd!", "v@example.com");
    Assert.IsFalse(_auth.IsEmailVerified);
    Assert.IsTrue(await _auth.ReloadAndCheckVerifiedAsync());
    Assert.IsTrue(_auth.IsEmailVerified);
}

[Test]
public async Task Apple_sign_in_is_not_supported()
{
    Assert.ThrowsAsync<System.NotSupportedException>(async () => await _auth.SignInWithAppleAsync("token"));
}

[Test]
public async Task First_google_sign_in_has_no_display_name()
{
    await _auth.SignInWithGoogleAsync();
    Assert.IsNull(_auth.DisplayName);
}
```

- [ ] **Step 5: Update `AuthServiceTests.cs`** — the `IsEmailAvailable` fail-open test no longer applies (method removed). Replace the file's single meaningful test with a compile-check-level test that `AuthService` constructs without a backend dependency (auth no longer uses `IBackendClient`):

```csharp
[Test]
public void AuthService_constructs_without_a_backend_dependency()
{
    // Auth no longer routes through Cloud Code; construction must not require IBackendClient.
    Assert.DoesNotThrow(() => { var _ = new AuthService(); });
}
```

(If `AuthService` retains no constructor args, delete `FakeBackendClient`.)

- [ ] **Step 6:** `read_console` → resolve every compile error until the project is green. This is the moment the whole contract lands.
- [ ] **Step 7:** Run the full EditMode suite. Expected: PASS (mock + config tests). Note `AuthScreen` still references old members — it is updated in Task 7; if `AuthScreen` is in the same assembly and breaks compilation, do Task 7's edits **before** running tests (they compile together). Sequence: apply Task 7 edits, then compile+test, then commit both. *(If executing strictly task-by-task, treat Tasks 5 and 7 as one commit boundary.)*
- [ ] **Step 8: Commit** (with Task 7 if compilation requires)

```bash
git add Assets/_Project/Scripts/Core/IAuthService.cs Assets/_Project/Scripts/Net/AuthService.cs Assets/_Project/Scripts/Net/LocalMockAuthService.cs Assets/_Project/Tests/EditMode/Net/LocalMockAuthServiceTests.cs Assets/_Project/Tests/EditMode/Net/AuthServiceTests.cs
git commit -m "auth: migrate IAuthService/AuthService/mock to Firebase + UGS OIDC"
```

---

### Task 6: DI wiring — `RootLifetimeScope`

**Files:**
- Modify: `Assets/_Project/Scripts/App/RootLifetimeScope.cs`

**Interfaces:**
- Consumes: `FirebaseAuthConfig` (Task 2), `FirebaseAuthHandler.Configure` (Task 4), the new `AuthService` constructor (Task 5).

- [ ] **Step 1:** Replace the `GoogleAuthConfig` serialized field with `FirebaseAuthConfig _firebaseAuthConfig`; replace `GoogleAuthHandler.Configure(_googleAuthConfig)` with `FirebaseAuthHandler.Configure(_firebaseAuthConfig)`. If `AuthService`'s constructor lost its `IBackendClient` arg, VContainer still resolves it via the parameterless/auto constructor — no registration line change beyond the type. Confirm `builder.Register<AuthService>(Lifetime.Singleton).As<IAuthService>();` still compiles.
- [ ] **Step 2:** In the Bootstrap scene, assign the `FirebaseAuthConfig.asset` to the `RootLifetimeScope` component's new field (Inspector). Remove the old `GoogleAuthConfig` assignment.
- [ ] **Step 3:** `read_console` → clean compile.
- [ ] **Step 4: Commit**

```bash
git add Assets/_Project/Scripts/App/RootLifetimeScope.cs Assets/_Project/Scenes/*Bootstrap*.unity
git commit -m "di: wire FirebaseAuthConfig/FirebaseAuthHandler in RootLifetimeScope"
```

---

### Task 7: `AuthScreen` UX — parameterless Google, link-based verify, single-step forgot-password

**Files:**
- Modify: `Assets/_Project/Scripts/UI/AuthScreen.cs`
- Modify: `Assets/_Project/Tests/EditMode/UI/AuthSceneWiringTests.cs` (adjust for removed panels/handlers)

**Interfaces:**
- Consumes: the new `IAuthService` surface (Task 5).

- [ ] **Step 1: `OnGoogleClicked`** — drop token acquisition; call the parameterless method (keep the Editor mock fallback pattern via try/catch on `NotSupportedException`):

```csharp
private async void OnGoogleClicked()
{
    SetBusy(true);
    _loginStatusText.text = "Signing in with Google…";
    try { await _auth.SignInWithGoogleAsync(); }
    catch (NotSupportedException) { _loginStatusText.text = "Google sign-in isn't available here"; SetBusy(false); }
    catch (Exception ex) { _loginStatusText.text = FriendlyError(ex); SetBusy(false); }
}
```

Remove the `GoogleAuthHandler.GetIdTokenAsync()` call and the anonymous-cleanup (`if (_auth.IsSignedIn && _auth.IsAnonymous) …`).

- [ ] **Step 2: `OnRegisterClicked`** — remove `EnsureSessionAsync`, `IsEmailAvailableAsync` pre-check, and the whole `_suppressAutoTransition`/anonymous dance. New flow: validate → `RegisterAsync` → show Verify panel (Firebase verification email already sent by `AuthService.RegisterAsync`). Remove `EnsureSessionAsync`, `_pendingVerification`-based anonymous logic simplification is optional but keep the verify gate.

- [ ] **Step 3: Verify panel** — replace OTP entry with link-based confirmation. Replace `OnVerifyClicked` to poll Firebase:

```csharp
private async void OnVerifyClicked() // button relabelled "I've verified my email"
{
    SetBusy(true);
    _verifyStatusText.text = "Checking…";
    try
    {
        if (await _auth.ReloadAndCheckVerifiedAsync())
        {
            SetBusy(false);
            EventBus.Publish(new PlayerReadyEvent());
        }
        else { _verifyStatusText.text = "Not verified yet — click the link in your email, then try again"; SetBusy(false); }
    }
    catch (Exception ex) { _verifyStatusText.text = FriendlyError(ex); SetBusy(false); }
}

private async void OnResendCodeClicked() // "Resend email"
{
    SetBusy(true);
    try { await _auth.SendEmailVerificationAsync(); _verifyStatusText.text = "Verification email sent"; }
    catch (Exception ex) { _verifyStatusText.text = FriendlyError(ex); }
    finally { SetBusy(false); }
}
```

Remove `_verifyCodeField` usage and `ConfirmEmailVerificationCodeAsync`.

- [ ] **Step 4: Forgot-password** — collapse to one step. `OnSendResetCodeClicked` calls `RequestPasswordResetAsync(email)` then shows a "reset link sent — check your email" confirmation and returns to Login. **Delete** `OnResetPasswordClicked`, the `ForgotPasswordReset` panel handling, `_pendingResetEmail`, and the reset-code/new-password fields' listeners. Remove `EnsureSessionAsync` calls.

- [ ] **Step 5:** Remove now-dead members: `EnsureSessionAsync`, `_suppressAutoTransition` (if fully unused), the `HandleSignedIn` `IsAnonymous` guard (anonymous no longer exists), and the `OnApplicationQuit` anonymous/`_pendingVerification` sign-out (keep a simpler unverified-cleanup: on quit, if signed in and `!IsEmailVerified`, `SignOutAsync`).

- [ ] **Step 6:** Update `AuthSceneWiringTests.cs` — drop assertions about removed panels/fields (reset panel, verify code field, forgot-reset back button) and about `IsEmailAvailable`. Keep wiring assertions for login/register/google/forgot-email/verify buttons.

- [ ] **Step 7:** In the Auth scene, remove the now-unused `ForgotPasswordReset` panel GameObject and the OTP code input; relabel the Verify button to "I've verified my email" and Resend to "Resend email". (Editor scene edit.)

- [ ] **Step 8:** `read_console` clean; run EditMode suite → PASS.
- [ ] **Step 9: Commit** (or fold into Task 5's commit if compilation coupling required)

```bash
git add Assets/_Project/Scripts/UI/AuthScreen.cs Assets/_Project/Tests/EditMode/UI/AuthSceneWiringTests.cs Assets/_Project/Scenes/*Auth*.unity
git commit -m "ui: Firebase auth UX — parameterless Google, link verify, one-step reset"
```

---

### Task 8: Retire Play Games plugin, `GoogleAuthHandler`, `EmailLoginKey`, `GoogleAuthConfig`

**Files:**
- Delete: `Assets/_Project/Scripts/Net/GoogleAuthHandler.cs` (+ `.meta`)
- Delete: `Assets/_Project/Scripts/Net/EmailLoginKey.cs` (+ `.meta`)
- Delete: `Assets/_Project/Tests/EditMode/Net/EmailLoginKeyTests.cs` (+ `.meta`)
- Delete: `Assets/_Project/Scripts/Config/GoogleAuthConfig.cs` (+ `.meta`) and `GoogleAuthConfig.asset`
- Delete: `Assets/GooglePlayGames/**`, `Assets/Plugins/Android/GooglePlayGamesManifest.androidlib/**`, Play Games proguard keeps in `Assets/Plugins/Android/proguard-user.txt`
- Modify: `Assets/_Project/Scripts/Net/SocialUniverse.Net.asmdef` (remove `Google.Play.Games` reference)

- [ ] **Step 1:** Confirm no remaining references: grep `GoogleAuthHandler`, `EmailLoginKey`, `GoogleAuthConfig`, `GooglePlayGames`, `SignInWithGooglePlayGamesAsync` across `Assets/_Project/Scripts` — expect zero hits (all removed in Tasks 5–7). Fix any stragglers.
- [ ] **Step 2:** Delete the files/folders above with their `.meta` files. Remove the `Google.Play.Games` entry from `SocialUniverse.Net.asmdef`. Trim Play Games keep-rules from `proguard-user.txt` (keep the file if other rules remain; delete if now empty).
- [ ] **Step 3:** **Android Resolver → Force Resolve** again to drop the Play Games native deps; confirm no leftover `play-services-games` artifacts.
- [ ] **Step 4:** `read_console` clean compile; run EditMode suite → PASS.
- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "cleanup: remove Play Games plugin, GoogleAuthHandler, EmailLoginKey, GoogleAuthConfig"
```

---

### Task 9: Retire Cloud Code email functions (ServerCode)

**Files:**
- Delete: `ServerCode/SaveEmail.js`, `CheckEmailAvailable.js`, `RequestPasswordReset.js`, `ConfirmPasswordReset.js`, `RequestEmailVerificationCode.js`, `ConfirmEmailVerificationCode.js`, `ClearPlayerEmail.js`, `TestFindPlayerByEmail.js`
- Modify: `ServerCode/GetPlayerProfile.js` (the `emailVerified` field), `ServerCode/CLOUD_CODE_FUNCTIONS.md`
- Investigate: the `email_lookup` Cloud Save index / access class config

- [ ] **Step 1:** Grep `ServerCode/` and the client for any caller of the retired functions and for `player_profile.email` / `emailVerified`. Confirmed consumer: `GetPlayerProfile.js:62` returns `emailVerified: profile?.emailVerified ?? false`. Decide: since Firebase now owns verification, either (a) drop `emailVerified` from the profile DTO and its client consumer, or (b) leave it always-false (harmless) if a UI still reads it. Prefer (a) — trace the client field that consumes `GetPlayerProfile.emailVerified` and remove it, or repoint it to `IAuthService.IsEmailVerified`.
- [ ] **Step 2:** Delete the retired `.js` files. Remove their entries from `CLOUD_CODE_FUNCTIONS.md`. Note in the doc that email verification / password reset / availability are now Firebase-native.
- [ ] **Step 3:** In the UGS dashboard, delete the deployed Cloud Code functions and remove the `email_lookup` Cloud Save index/access-class if it exists (no code references it anymore). *(Manual dashboard step — record it in the doc.)*
- [ ] **Step 4:** `git grep` confirms zero references to the deleted functions.
- [ ] **Step 5: Commit**

```bash
git add -A ServerCode
git commit -m "servercode: retire email OTP/availability Cloud Code (Firebase-native now)"
```

---

### Task 10: UGS OIDC provider config + Firebase console + device smoke test (verification)

**Files:** none (configuration + manual verification). Record outcomes in the spec doc's testing section.

- [ ] **Step 1: UGS Dashboard → Authentication → ID Providers → add OpenID Connect (Custom):** name → `firebase` (provider id becomes `oidc-firebase`); issuer/config URL → `https://securetoken.google.com/<PROJECT_ID>`; client id (audience) → `<PROJECT_ID>`. Enable it in both `development` and `production` environments used by `AppConfig`.
- [ ] **Step 2: Firebase console:** confirm Email/Password + Google providers enabled; Android app registered with the release **and** debug SHA-1; `google-services.json` in `Assets/` matches.
- [ ] **Step 3: Editor `_devMode` smoke:** run Bootstrap → Auth → register/login/google (mock) → reach Planet. Confirms the mock path survived the refactor.
- [ ] **Step 4: Device smoke (real):** build to a **physical** Android device (Play Games/BlueStacks caveats from prior sessions no longer apply, but a real device is still the trustworthy check). Verify:
  - Email register → Firebase verification email arrives → click link → "I've verified" advances to game.
  - Email login → UGS `PlayerId` minted; land/wallet/progression round-trip under it (Cloud Save works).
  - Google (web flow) sign-in → same UGS `PlayerId` bridge; enters game.
  - Forgot-password → Firebase reset link arrives and resets.
  - Sign out and relaunch → `TryAutoSignInAsync` restores via Firebase `CurrentUser`.
  - Delete account → removed from both Firebase and UGS.
- [ ] **Step 5:** Update the spec doc's Testing section with pass/fail results and any device-specific notes. Commit the doc update.

```bash
git add docs/superpowers/specs/2026-07-29-firebase-auth-oidc-migration-design.md
git commit -m "docs: record Firebase OIDC auth smoke-test results"
```

---

## Self-Review

**Spec coverage:**
- Firebase SDK import + google-services.json → Task 1. ✔
- FirebaseAuthConfig SO (Rule #3) → Task 2. ✔
- FirebaseApp init → Task 3. ✔
- FirebaseAuthHandler (email/pw + Google FederatedOAuthProvider) → Task 4. ✔
- OIDC bridge in AuthService + interface change + mock → Task 5. ✔
- DI wiring → Task 6. ✔
- AuthScreen UX (parameterless Google, link verify, one-step reset, remove anon/availability) → Task 7. ✔
- Retire Play Games / GoogleAuthHandler / EmailLoginKey / GoogleAuthConfig → Task 8. ✔
- Retire Cloud Code OTP + `player_profile.email`/`emailVerified` handling → Task 9. ✔
- UGS OIDC provider config + device smoke → Task 10. ✔
- Pre-launch (no migration) constraint honoured — no linking/backfill task. ✔
- Apple stub → Task 5. ✔
- Editor safety guards → Tasks 4, 5, 7. ✔

**Placeholder scan:** No "TBD/handle edge cases" placeholders. The one explicitly-flagged unknown is the exact Firebase Google federated-sign-in method name (Task 4 note) — called out with the concrete resolution step (verify against the imported SDK's `FirebaseAuth.SignInWithProviderAsync`) rather than left vague, because it is SDK-version-dependent.

**Type consistency:** `SignInWithGoogleAsync()` parameterless, `SendEmailVerificationAsync()`, `ReloadAndCheckVerifiedAsync()`, `IsEmailVerified`, `oidc-firebase`, `FirebaseAuthConfig.ProjectId/GoogleWebClientId`, `BridgeToUgsAsync` — used identically across Tasks 4–7.

**Known coupling:** Tasks 5 and 7 (and possibly 6) share a compile boundary because they live in interdependent assemblies; the plan flags folding them into one commit if the compiler requires it. This is inherent to a C# interface change, not a plan defect.
