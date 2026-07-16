# Auth Scene Flow Improvements Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Registration-time email verification (cancel deletes the account), Cloud Code email-availability pre-check, a two-panel forgot-password flow, and a locked-down anonymous-session lifecycle (guest play removed, boot guard, sign-out on quit).

**Architecture:** Registration becomes an anonymous-upgrade flow: an anonymous UGS session runs the `CheckEmailAvailable` pre-check, `AddUsernamePasswordAsync` upgrades that same session into the real account, and the new in-scene Verify Email panel gates entry into the game. Anonymous sessions are a Cloud Code transport only — `AuthScreen` refuses to publish `PlayerReadyEvent` for them, `BootState` signs restored ones out, and `OnApplicationQuit` drops them.

**Tech Stack:** Unity 6 (legacy UGUI in the Auth scene), UGS Authentication + Cloud Code + Cloud Save, VContainer, NUnit EditMode tests, Unity MCP for scene edits and test runs.

**Spec:** `docs/superpowers/specs/2026-07-16-auth-flow-improvements-design.md`

## Global Constraints

- Namespaces mirror folders: `SocialUniverse.Core` for `Core/`, `SocialUniverse.Net` for `Net/`, `SocialUniverse.UI` for `UI/`. One public type per file, file named after the type.
- Gameplay code depends on `I*Service` abstractions only; UGS SDK calls live in `Assets/_Project/Scripts/Net/` and `ServerCode/`.
- Auth scene UI uses **legacy UGUI** (`InputField`, `Text`, `Button` from `UnityEngine.UI`) — not TMP.
- `ServerCode/` is not part of the Unity build; deployment to UGS is manual (flag it, don't attempt it).
- Tests: EditMode suite must stay green. Run via Unity MCP `run_tests` (testMode: EditMode); CLI fallback: `"C:\Program Files\Unity\Hub\Editor\<version>\Editor\Unity.exe" -runTests -projectPath . -testResults results.xml -testPlatform EditMode`.
- After any script change, use Unity MCP `read_console` to confirm zero compile errors before proceeding.
- Unity MCP notes (from project memory): `execute_code` is unusable; if the bridge goes unresponsive it self-recovers — wait and retry, don't restart Unity.
- Commit messages end with `Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>`.

---

### Task 1: Auth service surface — `IsAnonymous`, `IsEmailAvailableAsync`, `DeleteAccountAsync`

**Files:**
- Modify: `Assets/_Project/Scripts/Core/IAuthService.cs`
- Create: `Assets/_Project/Scripts/Core/EmailAvailableResult.cs`
- Modify: `Assets/_Project/Scripts/Net/LocalMockAuthService.cs`
- Modify: `Assets/_Project/Scripts/Net/AuthService.cs`
- Test: `Assets/_Project/Tests/EditMode/Net/LocalMockAuthServiceTests.cs`

**Interfaces:**
- Consumes: existing `IAuthService`, `IBackendClient.CallAsync<T>(string, Dictionary<string, object>)`, `SaveKeys.AuthSession`, `EmailLoginKey.Derive(string)`.
- Produces (later tasks rely on these exact members):
  - `bool IAuthService.IsAnonymous { get; }`
  - `Task<bool> IAuthService.IsEmailAvailableAsync(string email)`
  - `Task IAuthService.DeleteAccountAsync()`
  - `SocialUniverse.Core.EmailAvailableResult` with public field `bool Available`
  - Cloud Code function name consumed by `AuthService`: `"CheckEmailAvailable"` with arg `email`, returning `{ available: bool }` (Task 2 implements it server-side).

- [ ] **Step 1: Write the failing tests**

Append inside the existing `LocalMockAuthServiceTests` class (keep the existing four tests untouched) and add a TearDown:

```csharp
[TearDown]
public void TearDown()
{
    if (_auth.IsSignedIn) _auth.SignOutAsync();
}

[Test]
public async Task Anonymous_sign_in_is_reported_anonymous()
{
    await _auth.SignInAnonymouslyAsync();
    Assert.IsTrue(_auth.IsAnonymous);
}

[Test]
public async Task Email_availability_reflects_registration()
{
    Assert.IsTrue(await _auth.IsEmailAvailableAsync("new@example.com"));
    await _auth.RegisterAsync("Player1", "Passw0rd!", "new@example.com");
    Assert.IsFalse(await _auth.IsEmailAvailableAsync("New@Example.com")); // case-insensitive
}

[Test]
public async Task Registering_over_an_anonymous_session_upgrades_it()
{
    await _auth.SignInAnonymouslyAsync();
    string anonId = _auth.PlayerId;
    await _auth.RegisterAsync("Player1", "Passw0rd!", "up@example.com");
    Assert.AreEqual(anonId, _auth.PlayerId);   // same account, upgraded
    Assert.IsFalse(_auth.IsAnonymous);
}

[Test]
public async Task Deleting_account_frees_the_email_and_signs_out()
{
    await _auth.RegisterAsync("Player1", "Passw0rd!", "del@example.com");
    await _auth.DeleteAccountAsync();
    Assert.IsFalse(_auth.IsSignedIn);
    Assert.IsTrue(await _auth.IsEmailAvailableAsync("del@example.com"));
}

[Test]
public async Task Restored_session_remembers_it_was_anonymous()
{
    await _auth.SignInAnonymouslyAsync();
    var restored = new LocalMockAuthService();
    await restored.TryAutoSignInAsync();
    Assert.IsTrue(restored.IsAnonymous);
    await restored.SignOutAsync();
}
```

- [ ] **Step 2: Verify the tests fail**

Unity MCP `read_console` after script reload. Expected: compile errors — `'LocalMockAuthService' does not contain a definition for 'IsAnonymous'` (etc.). A compile error is this step's expected "failing test".

- [ ] **Step 3: Extend `IAuthService`**

Add after `Task SignOutAsync();` in `Assets/_Project/Scripts/Core/IAuthService.cs`:

```csharp
        // True while the current session has no external identities (UGS anonymous
        // account). Anonymous sessions exist only as a Cloud Code transport during
        // registration / forgot-password and must never enter the game.
        bool IsAnonymous { get; }

        // Registration pre-check against the server-side email_lookup index.
        // Requires an authenticated (anonymous) session. True = free to register.
        // Accounts predating the email_lookup index are invisible to this check —
        // sign-up's ENTITY_EXISTS error remains the backstop.
        Task<bool> IsEmailAvailableAsync(string email);

        // Deletes the signed-in account (rollback for a cancelled registration)
        // and signs out, clearing cached credentials.
        Task DeleteAccountAsync();
```

- [ ] **Step 4: Create the result DTO**

`Assets/_Project/Scripts/Core/EmailAvailableResult.cs`:

```csharp
namespace SocialUniverse.Core
{
    // Response shape for the "CheckEmailAvailable" Cloud Code function. Public so
    // tests can construct it for a fake IBackendClient.
    public class EmailAvailableResult
    {
        public bool Available;
    }
}
```

- [ ] **Step 5: Implement in `LocalMockAuthService`**

(a) Add the flag beside the other private state fields:

```csharp
        private bool _isAnonymous;
```

and the property next to `IsSignedIn`:

```csharp
        public bool IsAnonymous => _isAnonymous;
```

(b) In `SignInAnonymouslyAsync`, set `_isAnonymous = true;` immediately before `PersistSession();`.

(c) In `SignInWithEmailAsync`, set `_isAnonymous = false;` immediately before `PersistSession();`.

(d) Replace `RegisterAsync`'s body between the duplicate check and `PersistSession()` so an anonymous session upgrades in place (same playerId), mirroring UGS:

```csharp
            _users[key] = new UserRecord(password, username);
            if (!_isSignedIn)
                _playerId = "mock_" + key;   // fresh account; anonymous upgrade keeps its id
            _username    = username;
            _email       = email;
            _isAnonymous = false;
            _isSignedIn  = true;
```

(e) Replace the Apple/Google delegation (SSO accounts are NOT anonymous):

```csharp
        public Task SignInWithAppleAsync(string idToken)  => MockSsoSignInAsync("apple");
        public Task SignInWithGoogleAsync(string idToken) => MockSsoSignInAsync("google");

        private async Task MockSsoSignInAsync(string provider)
        {
            if (_isSignedIn) { OnSignedIn?.Invoke(); return; }
            await Task.Delay(900);
            _playerId    = provider + "_" + UnityEngine.Random.Range(10000, 99999);
            _isAnonymous = false;
            _isSignedIn  = true;
            PersistSession();
            SULog.Info($"[MOCK] Signed in with {provider} ({_playerId})", SULog.Channel.Net);
            OnSignedIn?.Invoke();
        }
```

(f) In `TryAutoSignInAsync`, after the `_email = ...` line add:

```csharp
            _isAnonymous = PlayerPrefs.GetInt(SaveKeys.AuthSession + "_anon", 0) == 1;
```

(g) In `PersistSession`, before `PlayerPrefs.Save();` add:

```csharp
            PlayerPrefs.SetInt(SaveKeys.AuthSession + "_anon", _isAnonymous ? 1 : 0);
```

(h) In `SignOutAsync`, add `_isAnonymous = false;` beside `_isSignedIn = false;` and `PlayerPrefs.DeleteKey(SaveKeys.AuthSession + "_anon");` beside the existing `DeleteKey`.

(i) Add the two new methods (after `SignOutAsync`):

```csharp
        public async Task<bool> IsEmailAvailableAsync(string email)
        {
            await Task.Delay(300);
            return !_users.ContainsKey(NormalizeEmail(email));
        }

        public Task DeleteAccountAsync()
        {
            if (!_isSignedIn) throw new InvalidOperationException("Not signed in");
            if (!string.IsNullOrEmpty(_email))
                _users.Remove(NormalizeEmail(_email));
            _pendingEmailVerificationCode = false;
            SULog.Info($"[MOCK] Account deleted ({_playerId})", SULog.Channel.Net);
            return SignOutAsync();
        }
```

- [ ] **Step 6: Implement in `AuthService` (UGS)**

(a) Add the field beside `_email`:

```csharp
        private bool _isAnonymous;
```

and the property beside `Email`:

```csharp
        public bool IsAnonymous => _isAnonymous;
```

(b) In `SignInAnonymouslyAsync`, after the UGS call add `_isAnonymous = true;`.

(c) In `SignInWithEmailAsync`, after the UGS sign-in line add `_isAnonymous = false;`.

(d) In `SignInWithAppleAsync` and `SignInWithGoogleAsync`, add `_isAnonymous = false;` after their UGS calls.

(e) In `RegisterAsync`, replace the single `SignUpWithUsernamePasswordAsync` line with the upgrade branch:

```csharp
            string loginKey = EmailLoginKey.Derive(email);
            // Registration now starts from the anonymous pre-check session
            // (see AuthScreen.OnRegisterClicked): AddUsernamePasswordAsync
            // upgrades that account in place instead of creating a second one.
            // The signed-out path is kept for any caller without a session.
            if (IsSignedIn && _isAnonymous)
                await AuthenticationService.Instance.AddUsernamePasswordAsync(loginKey, password);
            else
                await AuthenticationService.Instance.SignUpWithUsernamePasswordAsync(loginKey, password);
            _isAnonymous = false;
```

(f) In `TryAutoSignInAsync`, after `await HydratePlayerNameAsync();` add:

```csharp
                // Restored sessions don't reveal how the account was created —
                // ask UGS for its identities. No external identities = anonymous,
                // and BootState must not let it into the game. On lookup failure
                // assume non-anonymous: wrongly gating a real account out would
                // force a pointless re-login on every network blip.
                try
                {
                    var info = await AuthenticationService.Instance.GetPlayerInfoAsync();
                    _isAnonymous = (info?.Identities?.Count ?? 0) == 0;
                }
                catch (Exception ex)
                {
                    _isAnonymous = false;
                    SULog.Warn($"AuthService: identity lookup failed ({ex.Message})", SULog.Channel.Net);
                }
```

(g) In `SignOutAsync`, add `_isAnonymous = false;` beside `_email = null;`.

(h) Add the two new methods (after `SignOutAsync`):

```csharp
        public async Task<bool> IsEmailAvailableAsync(string email)
        {
            var result = await _backend.CallAsync<EmailAvailableResult>("CheckEmailAvailable",
                new Dictionary<string, object> { { "email", email } });
            // Fail open on a null payload: sign-up's ENTITY_EXISTS is the backstop.
            return result?.Available ?? true;
        }

        public async Task DeleteAccountAsync()
        {
            await AuthenticationService.Instance.DeleteAccountAsync();
            AuthenticationService.Instance.SignOut(clearCredentials: true);
            _email       = null;
            _isAnonymous = false;
            SULog.Info("Account deleted and signed out", SULog.Channel.Net);
        }
```

- [ ] **Step 7: Verify compile + tests pass**

Unity MCP `read_console`: zero errors. Then `run_tests` (EditMode, filter: `LocalMockAuthServiceTests`). Expected: all pass (4 existing + 5 new).

- [ ] **Step 8: Commit**

```bash
git add Assets/_Project/Scripts/Core/IAuthService.cs Assets/_Project/Scripts/Core/EmailAvailableResult.cs Assets/_Project/Scripts/Net/LocalMockAuthService.cs Assets/_Project/Scripts/Net/AuthService.cs Assets/_Project/Tests/EditMode/Net/LocalMockAuthServiceTests.cs
git commit -m "Add IsAnonymous, IsEmailAvailableAsync, DeleteAccountAsync to auth services"
```

---

### Task 2: `CheckEmailAvailable` Cloud Code function

**Files:**
- Create: `ServerCode/CheckEmailAvailable.js`
- Modify: `ServerCode/CLOUD_CODE_FUNCTIONS.md` (append a section in the same format as the others)

**Interfaces:**
- Consumes: the `email_lookup` Cloud Save key written by `ServerCode/SaveEmail.js`; the elevated `DataApi(context).queryDefaultPlayerData` pattern from `ServerCode/RequestPasswordReset.js`.
- Produces: Cloud Code endpoint `CheckEmailAvailable(email)` → `{ available: boolean }`, called by `AuthService.IsEmailAvailableAsync` (Task 1).

- [ ] **Step 1: Write the function**

`ServerCode/CheckEmailAvailable.js`:

```js
// CheckEmailAvailable — registration pre-check: returns whether an email is
// free to register, by querying the cross-player email_lookup index (written
// by SaveEmail.js) through the elevated Cloud Code DataApi. Same setup
// prerequisites as RequestPasswordReset.js: the "email_lookup" Cloud Save
// index (Player Data, Default access class) must exist, and values saved
// before the index was created are never matched (no backfill).
//
// Unlike RequestPasswordReset, this endpoint intentionally reveals whether an
// email is registered — that is its purpose (pre-registration duplicate
// check), and the same fact already leaks through sign-up's ENTITY_EXISTS.
//
// Fails OPEN ({ available: true }) on query errors: sign-up's ENTITY_EXISTS
// remains the duplicate backstop, and a broken index shouldn't block all
// registrations.
const { DataApi } = require("@unity-services/cloud-save-1.4");

const EMAIL_LOOKUP_KEY = "email_lookup"; // must match SaveEmail.js
const EMAIL_REGEX = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

/**
 * @param {string} email - The address the player wants to register with.
 */
module.exports = async ({ params, context, logger }) => {
  const email = (params.email ?? "").trim().toLowerCase();
  if (!EMAIL_REGEX.test(email)) throw new Error("Invalid email address");

  const { projectId } = context;
  const saveApi = new DataApi(context);

  try {
    const res = await saveApi.queryDefaultPlayerData(projectId, {
      // asc is mandatory on every query field (400 "asc must be specified"
      // without it), even though sort order is irrelevant for an EQ match.
      fields: [{ key: EMAIL_LOOKUP_KEY, op: "EQ", value: email, asc: true }],
    });
    const matches = res.data.results ?? [];
    logger.info(`CheckEmailAvailable: ${matches.length} match(es)`);
    return { available: matches.length === 0 };
  } catch (err) {
    const detail = err.response?.data ? JSON.stringify(err.response.data) : err.message;
    logger.error(`CheckEmailAvailable: query FAILED (treating as available): ${detail}`);
    return { available: true };
  }
};
```

- [ ] **Step 2: Document it**

Append to `ServerCode/CLOUD_CODE_FUNCTIONS.md` (matching the existing per-function format: `## CheckEmailAvailable`, one-paragraph description, `**Parameters:** email (string)`, then the full source in a ```js fence).

- [ ] **Step 3: Sanity-check the JS**

Run: `node --check ServerCode/CheckEmailAvailable.js`
Expected: no output (syntax OK). (`require` of UGS modules only resolves in Cloud Code — `--check` parses without executing.)

- [ ] **Step 4: Commit**

```bash
git add ServerCode/CheckEmailAvailable.js ServerCode/CLOUD_CODE_FUNCTIONS.md
git commit -m "Add CheckEmailAvailable Cloud Code function for registration pre-check"
```

---

### Task 3: BootState anonymous-session guard

**Files:**
- Modify: `Assets/_Project/Scripts/Core/BootState.cs:43-45`

**Interfaces:**
- Consumes: `IAuthService.IsAnonymous`, `IAuthService.SignOutAsync()` (Task 1).
- Produces: nothing new — behavioral guarantee that a restored anonymous session falls through to the Auth scene.

- [ ] **Step 1: Add the guard**

In `RunAsync`, immediately after the `TryAutoSignInAsync` block (line 43-44), insert:

```csharp
            // A leftover anonymous session (app killed mid-registration or
            // mid-password-reset before the quit-time sign-out ran) must never
            // walk into the game: guest play was removed, and anonymous sessions
            // exist only as a Cloud Code transport inside the Auth scene flows.
            // Signing out (credentials cleared) drops us into the normal
            // Auth-scene path below.
            if (_auth.IsSignedIn && _auth.IsAnonymous)
            {
                SULog.Info("Boot: restored session is anonymous — signing out");
                await _auth.SignOutAsync();
            }
```

- [ ] **Step 2: Verify compile**

Unity MCP `read_console`: zero errors.

- [ ] **Step 3: Commit**

```bash
git add Assets/_Project/Scripts/Core/BootState.cs
git commit -m "Sign restored anonymous sessions out at boot instead of entering the game"
```

---

### Task 4: AuthScreen rework — verify panel, two-panel forgot password, guest removal, quit handler

**Files:**
- Modify: `Assets/_Project/Scripts/UI/AuthScreen.cs` (full-file replacement below)

**Interfaces:**
- Consumes: all Task 1 `IAuthService` members; existing `EventBus`, `PlayerReadyEvent`, `SULog`.
- Produces: serialized fields Task 5 must wire in `Auth.unity` — exact names:
  - Panels: `_loginPanel`, `_registerPanel`, `_forgotEmailPanel`, `_forgotResetPanel`, `_verifyEmailPanel`
  - Forgot/email: `_forgotEmailField`, `_forgotEmailStatusText`, `_sendResetCodeButton`, `_forgotBackToLoginButton`
  - Forgot/reset: `_forgotCodeField`, `_forgotNewPasswordField`, `_forgotConfirmField`, `_forgotResetStatusText`, `_resetPasswordButton`, `_forgotResetBackButton`
  - Verify: `_verifyCodeField`, `_verifyStatusText`, `_verifyButton`, `_resendCodeButton`, `_verifyCancelButton`
  - REMOVED (Task 5 deletes the scene object): `_guestButton`; RENAMED: `_forgotPasswordPanel` → `_forgotEmailPanel`, `_forgotStatusText` → per-panel status texts.

- [ ] **Step 1: Replace `AuthScreen.cs` with the following**

```csharp
using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using SocialUniverse.Core;
using SocialUniverse.Net;

namespace SocialUniverse.UI
{
    public class AuthScreen : MonoBehaviour
    {
        private enum AuthPanel { Login, Register, ForgotPasswordEmail, ForgotPasswordReset, VerifyEmail }

        // --- Panels ---
        [SerializeField] private GameObject _loginPanel;
        [SerializeField] private GameObject _registerPanel;
        [SerializeField] private GameObject _forgotEmailPanel;
        [SerializeField] private GameObject _forgotResetPanel;
        [SerializeField] private GameObject _verifyEmailPanel;

        // --- Login panel ---
        [SerializeField] private InputField _loginEmailField;
        [SerializeField] private InputField _loginPasswordField;
        [SerializeField] private Text       _loginStatusText;
        [SerializeField] private Button     _loginButton;
        [SerializeField] private Button     _googleButton;
        [SerializeField] private Button     _goToRegisterButton;
        [SerializeField] private Button     _forgotPasswordButton;

        // --- Register panel ---
        [SerializeField] private InputField _regUsernameField;
        [SerializeField] private InputField _regEmailField;
        [SerializeField] private InputField _regPasswordField;
        [SerializeField] private InputField _regConfirmField;
        [SerializeField] private Text       _regStatusText;
        [SerializeField] private Button     _registerButton;
        [SerializeField] private Button     _goToLoginButton;

        // --- Forgot password: email panel ---
        [SerializeField] private InputField _forgotEmailField;
        [SerializeField] private Text       _forgotEmailStatusText;
        [SerializeField] private Button     _sendResetCodeButton;
        [SerializeField] private Button     _forgotBackToLoginButton;

        // --- Forgot password: reset panel ---
        [SerializeField] private InputField _forgotCodeField;
        [SerializeField] private InputField _forgotNewPasswordField;
        [SerializeField] private InputField _forgotConfirmField;
        [SerializeField] private Text       _forgotResetStatusText;
        [SerializeField] private Button     _resetPasswordButton;
        [SerializeField] private Button     _forgotResetBackButton;

        // --- Verify email panel ---
        [SerializeField] private InputField _verifyCodeField;
        [SerializeField] private Text       _verifyStatusText;
        [SerializeField] private Button     _verifyButton;
        [SerializeField] private Button     _resendCodeButton;
        [SerializeField] private Button     _verifyCancelButton;

        private IAuthService _auth;
        private bool         _busy;
        private bool         _suppressAutoTransition;
        private bool         _pendingVerification; // account created, email not yet verified
        private string       _pendingResetEmail;   // captured on the send-code panel for the reset call

        [Inject]
        public void Construct(IAuthService auth) => _auth = auth;

        private void Start()
        {
            _auth.OnSignedIn     += HandleSignedIn;
            _auth.OnSignInFailed += HandleSignInFailed;

            _loginButton       .onClick.AddListener(OnLoginClicked);
            _goToRegisterButton.onClick.AddListener(() => ShowPanel(AuthPanel.Register));
            _registerButton    .onClick.AddListener(OnRegisterClicked);
            _goToLoginButton   .onClick.AddListener(() => ShowPanel(AuthPanel.Login));

            if (_googleButton            != null) _googleButton           .onClick.AddListener(OnGoogleClicked);
            if (_forgotPasswordButton    != null) _forgotPasswordButton   .onClick.AddListener(() => ShowPanel(AuthPanel.ForgotPasswordEmail));
            if (_forgotBackToLoginButton != null) _forgotBackToLoginButton.onClick.AddListener(() => ShowPanel(AuthPanel.Login));
            if (_forgotResetBackButton   != null) _forgotResetBackButton  .onClick.AddListener(() => ShowPanel(AuthPanel.ForgotPasswordEmail));
            if (_sendResetCodeButton     != null) _sendResetCodeButton    .onClick.AddListener(OnSendResetCodeClicked);
            if (_resetPasswordButton     != null) _resetPasswordButton    .onClick.AddListener(OnResetPasswordClicked);
            if (_verifyButton            != null) _verifyButton           .onClick.AddListener(OnVerifyClicked);
            if (_resendCodeButton        != null) _resendCodeButton       .onClick.AddListener(OnResendCodeClicked);
            if (_verifyCancelButton      != null) _verifyCancelButton     .onClick.AddListener(OnVerifyCancelClicked);

            if (_auth.IsSignedIn)
                HandleSignedIn();
            else
                ShowPanel(AuthPanel.Login);
        }

        private void OnDestroy()
        {
            if (_auth == null) return;
            _auth.OnSignedIn     -= HandleSignedIn;
            _auth.OnSignInFailed -= HandleSignInFailed;
        }

        // Desktop/editor quits: drop any session that must not survive into the
        // next launch — the throwaway anonymous Cloud Code session, or a freshly
        // registered account whose email was never verified. Mobile swipe-kills
        // skip this callback entirely; BootState's IsAnonymous guard is the
        // safety net there.
        private void OnApplicationQuit()
        {
            if (_auth == null || !_auth.IsSignedIn) return;
            if (_auth.IsAnonymous || _pendingVerification)
                _ = _auth.SignOutAsync();
        }

        private void ShowPanel(AuthPanel panel)
        {
            _loginPanel   .SetActive(panel == AuthPanel.Login);
            _registerPanel.SetActive(panel == AuthPanel.Register);
            if (_forgotEmailPanel != null) _forgotEmailPanel.SetActive(panel == AuthPanel.ForgotPasswordEmail);
            if (_forgotResetPanel != null) _forgotResetPanel.SetActive(panel == AuthPanel.ForgotPasswordReset);
            if (_verifyEmailPanel != null) _verifyEmailPanel.SetActive(panel == AuthPanel.VerifyEmail);

            _loginStatusText.text = "";
            _regStatusText  .text = "";
            if (_forgotEmailStatusText != null) _forgotEmailStatusText.text = "";
            if (_forgotResetStatusText != null) _forgotResetStatusText.text = "";
            if (_verifyStatusText      != null) _verifyStatusText     .text = "";

            // Leaving a flow for the Login panel ends any in-flight registration
            // suppression; the flows themselves manage the flag while active.
            if (panel == AuthPanel.Login)
                _suppressAutoTransition = false;
        }

        // -------------------------------------------------------------------------
        private void HandleSignedIn()
        {
            if (_suppressAutoTransition) return;
            // Anonymous sessions are a Cloud Code transport, never a player:
            // guest play was removed, so nothing anonymous may enter the game.
            if (_auth.IsAnonymous)
            {
                SULog.Warn("Auth: anonymous session may not enter the game — ignoring sign-in", SULog.Channel.Net);
                return;
            }
            SetBusy(false);
            SULog.Info("Auth: signed in — advancing to game", SULog.Channel.Net);
            EventBus.Publish(new PlayerReadyEvent());
        }

        private void HandleSignInFailed(Exception ex)
        {
            SetActiveStatus(FriendlyError(ex));
            SetBusy(false);
        }

        // Cloud Code calls require an authenticated UGS session even before an
        // account exists — CheckEmailAvailable and the password-reset functions
        // fail with PlayerIdMissing otherwise. Silently establishes an anonymous
        // session if none exists yet. Preserves the caller's suppression state
        // instead of clobbering it: registration holds the flag across this call.
        private async Task EnsureSessionAsync()
        {
            if (_auth.IsSignedIn) return;
            bool prev = _suppressAutoTransition;
            _suppressAutoTransition = true;
            try   { await _auth.SignInAnonymouslyAsync(); }
            finally { _suppressAutoTransition = prev; }
        }

        // -------------------------------------------------------------------------
        private async void OnLoginClicked()
        {
            string email    = _loginEmailField.text.Trim();
            string password = _loginPasswordField.text;

            if (!ValidateEmail(email, out string emailErr))
            {
                _loginStatusText.text = emailErr;
                return;
            }
            if (string.IsNullOrEmpty(password))
            {
                _loginStatusText.text = "Enter your password";
                return;
            }

            SetBusy(true);
            _loginStatusText.text = "Signing in…";
            try   { await _auth.SignInWithEmailAsync(email, password); }
            catch (Exception ex) { _loginStatusText.text = FriendlyError(ex); SetBusy(false); }
        }

        private async void OnGoogleClicked()
        {
            SetBusy(true);
            _loginStatusText.text = "Signing in with Google…";
            try
            {
                string idToken;
                try   { idToken = await GoogleAuthHandler.GetIdTokenAsync(); }
                catch (NotSupportedException) { idToken = "mock_google_token"; }
                await _auth.SignInWithGoogleAsync(idToken);
            }
            catch (Exception ex) { _loginStatusText.text = FriendlyError(ex); SetBusy(false); }
        }

        private async void OnRegisterClicked()
        {
            string username = _regUsernameField.text.Trim();
            string email    = _regEmailField.text.Trim();
            string password = _regPasswordField.text;
            string confirm  = _regConfirmField.text;

            if (!ValidateUsername(username, out string nameErr))
            {
                _regStatusText.text = nameErr;
                return;
            }
            if (!ValidateEmail(email, out string emailErr))
            {
                _regStatusText.text = emailErr;
                return;
            }
            if (!ValidatePassword(password, out string passErr))
            {
                _regStatusText.text = passErr;
                return;
            }
            if (password != confirm)
            {
                _regStatusText.text = "Passwords do not match";
                return;
            }

            SetBusy(true);
            // Raised for the whole registration flow: the anonymous pre-check
            // session and the account upgrade both fire sign-in signals that
            // must not advance to the game — only a verified email does (see
            // OnVerifyClicked). Cleared on verify success, cancel, or returning
            // to the Login panel.
            _suppressAutoTransition = true;
            try
            {
                _regStatusText.text = "Checking email…";
                await EnsureSessionAsync();
                if (!await _auth.IsEmailAvailableAsync(email))
                {
                    _regStatusText.text = "An account with that email already exists";
                    SetBusy(false);
                    return;
                }

                _regStatusText.text = "Creating account…";
                await _auth.RegisterAsync(username, password, email);
                _pendingVerification = true;

                ShowPanel(AuthPanel.VerifyEmail);
                SetBusy(false);
                await SendVerificationCodeAsync();
            }
            catch (Exception ex)
            {
                _regStatusText.text = FriendlyError(ex);
                SetBusy(false);
            }
        }

        // -------------------------------------------------------------------------
        private async Task SendVerificationCodeAsync()
        {
            if (_resendCodeButton != null) _resendCodeButton.interactable = false;
            _verifyStatusText.text = "Sending verification code…";
            try
            {
                await _auth.RequestEmailVerificationCodeAsync();
                _verifyStatusText.text = "Verification code sent — check your email";
            }
            catch (Exception ex)
            {
                _verifyStatusText.text = FriendlyError(ex);
            }
            finally
            {
                if (_resendCodeButton != null) _resendCodeButton.interactable = true;
            }
        }

        private async void OnResendCodeClicked() => await SendVerificationCodeAsync();

        private async void OnVerifyClicked()
        {
            string code = _verifyCodeField.text.Trim();
            if (string.IsNullOrWhiteSpace(code))
            {
                _verifyStatusText.text = "Enter the verification code sent to your email";
                return;
            }

            SetBusy(true);
            _verifyStatusText.text = "Verifying…";
            try
            {
                await _auth.ConfirmEmailVerificationCodeAsync(code);
                _pendingVerification    = false;
                _suppressAutoTransition = false;
                SetBusy(false);
                SULog.Info("Auth: email verified — advancing to game", SULog.Channel.Net);
                EventBus.Publish(new PlayerReadyEvent());
            }
            catch (Exception ex)
            {
                _verifyStatusText.text = FriendlyError(ex);
                SetBusy(false);
            }
        }

        private async void OnVerifyCancelClicked()
        {
            SetBusy(true);
            _verifyStatusText.text = "Removing unverified account…";
            try
            {
                await _auth.DeleteAccountAsync();
            }
            catch (Exception ex)
            {
                // Deletion needs a live session; if it fails, at least drop the
                // session so the unverified account can't be resumed into the
                // game on the next launch.
                SULog.Warn($"Auth: account deletion failed ({ex.Message}) — signing out instead", SULog.Channel.Net);
                try { await _auth.SignOutAsync(); } catch { /* best effort */ }
            }
            _pendingVerification = false;
            SetBusy(false);
            ShowPanel(AuthPanel.Login);
            _loginStatusText.text = "Registration cancelled — the unverified account was removed";
        }

        // -------------------------------------------------------------------------
        private async void OnSendResetCodeClicked()
        {
            string email = _forgotEmailField.text.Trim();
            if (!ValidateEmail(email, out string err))
            {
                _forgotEmailStatusText.text = err;
                return;
            }

            _sendResetCodeButton.interactable = false;
            _forgotEmailStatusText.text = "Sending reset code…";
            try
            {
                await EnsureSessionAsync();
                await _auth.RequestPasswordResetAsync(email);
                _pendingResetEmail = email;
                ShowPanel(AuthPanel.ForgotPasswordReset);
                _forgotResetStatusText.text = "Reset code sent — check your email";
            }
            catch (Exception ex)
            {
                _forgotEmailStatusText.text = FriendlyError(ex);
            }
            finally
            {
                _sendResetCodeButton.interactable = true;
            }
        }

        private async void OnResetPasswordClicked()
        {
            string code        = _forgotCodeField.text.Trim();
            string newPassword = _forgotNewPasswordField.text;
            string confirm     = _forgotConfirmField.text;

            if (string.IsNullOrWhiteSpace(code))
            {
                _forgotResetStatusText.text = "Enter the reset code from your email";
                return;
            }
            if (!ValidatePassword(newPassword, out string passErr))
            {
                _forgotResetStatusText.text = passErr;
                return;
            }
            if (newPassword != confirm)
            {
                _forgotResetStatusText.text = "Passwords do not match";
                return;
            }

            _resetPasswordButton.interactable = false;
            _forgotResetStatusText.text = "Resetting password…";
            try
            {
                await EnsureSessionAsync();
                await _auth.ConfirmPasswordResetAsync(_pendingResetEmail, code, newPassword);
                // The reset ran on a throwaway anonymous session — drop it now,
                // or the upcoming email login throws UGS's "already signed in".
                if (_auth.IsAnonymous)
                    await _auth.SignOutAsync();
                ShowPanel(AuthPanel.Login);
                _loginStatusText.text = "Password reset — please sign in with your new password";
            }
            catch (Exception ex)
            {
                _forgotResetStatusText.text = FriendlyError(ex);
            }
            finally
            {
                _resetPasswordButton.interactable = true;
            }
        }

        // -------------------------------------------------------------------------
        private void SetBusy(bool busy)
        {
            _busy = busy;
            _loginButton   .interactable = !busy;
            _registerButton.interactable = !busy;
            if (_googleButton       != null) _googleButton      .interactable = !busy;
            if (_verifyButton       != null) _verifyButton      .interactable = !busy;
            if (_verifyCancelButton != null) _verifyCancelButton.interactable = !busy;
        }

        private void SetActiveStatus(string message)
        {
            if (_loginPanel.activeSelf)
                _loginStatusText.text = message;
            else if (_registerPanel.activeSelf)
                _regStatusText.text = message;
            else if (_forgotEmailPanel != null && _forgotEmailPanel.activeSelf && _forgotEmailStatusText != null)
                _forgotEmailStatusText.text = message;
            else if (_forgotResetPanel != null && _forgotResetPanel.activeSelf && _forgotResetStatusText != null)
                _forgotResetStatusText.text = message;
            else if (_verifyEmailPanel != null && _verifyEmailPanel.activeSelf && _verifyStatusText != null)
                _verifyStatusText.text = message;
        }

        // UGS's real password policy: 8-30 chars, at least one uppercase, lowercase,
        // digit, and symbol. Enforced client-side so players get instant feedback
        // instead of a server rejection after submitting.
        private static bool ValidatePassword(string password, out string error)
        {
            if (password.Length < 8 || password.Length > 30)
            {
                error = "Password must be 8-30 characters";
                return false;
            }
            bool hasUpper = false, hasLower = false, hasDigit = false, hasSymbol = false;
            foreach (char c in password)
            {
                if      (char.IsUpper(c)) hasUpper  = true;
                else if (char.IsLower(c)) hasLower  = true;
                else if (char.IsDigit(c)) hasDigit  = true;
                else                      hasSymbol = true;
            }
            if (!hasUpper || !hasLower || !hasDigit || !hasSymbol)
            {
                error = "Password must include uppercase, lowercase, a number, and a symbol";
                return false;
            }
            error = null;
            return true;
        }

        private static bool ValidateEmail(string email, out string error)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                error = "Email is required";
                return false;
            }
            int at  = email.IndexOf('@');
            int dot = email.LastIndexOf('.');
            if (at < 1 || dot <= at + 1 || dot == email.Length - 1)
            {
                error = "Enter a valid email address";
                return false;
            }
            error = null;
            return true;
        }

        private static bool ValidateUsername(string username, out string error)
        {
            if (string.IsNullOrWhiteSpace(username) || username.Length < 2)
            {
                error = "Username must be at least 2 characters";
                return false;
            }
            if (username.Length > 20)
            {
                error = "Username must be 20 characters or fewer";
                return false;
            }
            error = null;
            return true;
        }

        private static string FriendlyError(Exception ex)
        {
            string msg = ex.Message;
            if (msg.Contains("already exists") || msg.Contains("already taken") || msg.Contains("EntityExists") || msg.Contains("ENTITY_EXISTS"))
                return "An account with that email already exists";
            if (msg.Contains("INVALID_PASSWORD") || msg.Contains("wrong password") || msg.Contains("Incorrect"))
                return "Incorrect email or password";
            if (msg.Contains("No password reset") || msg.Contains("No reset"))
                return "No reset was requested for this email — click Send Reset Code first";
            if (msg.Contains("Invalid reset code"))
                return "Incorrect reset code — check your email and try again";
            if (msg.Contains("No email on file"))
                return "No email is on file for this account — contact support";
            if (msg.Contains("No verification code"))
                return "No verification code was sent — click Resend Code";
            if (msg.Contains("Verification code has expired"))
                return "Verification code expired — click Resend Code to get a new one";
            if (msg.Contains("Invalid verification code"))
                return "Incorrect verification code — check your email and try again";
            if (msg.Contains("network") || msg.Contains("Network") || msg.Contains("unreachable"))
                return "Network error — check your connection";
            return msg;
        }
    }
}
```

Notes on what changed vs. the old file (for the reviewer, not the code):
- `_guestButton` field, `OnGuestClicked`, and its listener/SetBusy line are gone.
- `AuthPanel` gained `ForgotPasswordEmail` / `ForgotPasswordReset` / `VerifyEmail`; `_forgotPasswordPanel`/`_forgotStatusText` replaced by per-panel equivalents.
- `EnsureSessionAsync` now preserves the caller's suppression state (the old version reset it to `false` in `finally`, which would have unmasked the registration flow mid-way).
- `HandleSignedIn` refuses anonymous sessions outright.
- Registration no longer relies on the SignedIn event at all — entry into the game happens exclusively in `OnVerifyClicked`.
- `OnResetPasswordClicked` reads the email captured by the send-code panel (`_pendingResetEmail`) and signs the anonymous session out after a successful reset (fixes the latent "already signed in" bug).

- [ ] **Step 2: Verify compile**

Unity MCP `read_console`: zero errors. (Scene wiring is intentionally still stale — Task 5.)

- [ ] **Step 3: Commit**

```bash
git add Assets/_Project/Scripts/UI/AuthScreen.cs
git commit -m "Rework AuthScreen: verify-email panel, two-panel forgot password, remove guest play"
```

---

### Task 5: Auth.unity scene changes

**Files:**
- Modify: `Assets/_Project/Scenes/Auth.unity` (via Unity MCP — do not hand-edit the YAML for new objects; only wiring diffs are safe to eyeball)

**Interfaces:**
- Consumes: the exact serialized field names produced by Task 4 (see its Interfaces block).
- Produces: a fully wired Auth scene. No code contracts.

- [ ] **Step 1: Open the scene and survey the existing layout**

Unity MCP: `manage_scene` (load `Assets/_Project/Scenes/Auth.unity`), then `find_gameobjects`/`manage_gameobject` to dump the hierarchy under the Canvas — note the existing `LoginPanel`, `RegisterPanel`, `ForgotPasswordPanel` structure, and the visual conventions (background image, fonts, colors, button styles) to copy.

- [ ] **Step 2: Delete the Guest button**

Find the login panel's Guest button GameObject and delete it. If the login panel uses a layout group the remaining buttons reflow automatically; otherwise close the gap manually.

- [ ] **Step 3: Restructure the forgot-password panels**

- Rename `ForgotPasswordPanel` → `ForgotEmailPanel`. Keep: email InputField, Send Reset Code button, Back-to-login button, status Text. Delete from it: code field, new-password field, confirm field, Reset Password button.
- Duplicate `ForgotEmailPanel` → rename `ForgotResetPanel`. Replace its content with: code InputField (placeholder "Reset code"), new-password InputField (Password content type), confirm InputField (Password content type), Reset Password button, Back button, status Text. Match the original panel's styling.
- Both panels inactive by default (AuthScreen activates them).

- [ ] **Step 4: Create the Verify Email panel**

Duplicate `ForgotResetPanel` → rename `VerifyEmailPanel`; prune to: title Text ("Verify your email"), instruction Text ("Enter the 6-digit code we sent to your email"), code InputField (placeholder "Verification code"), Verify button, Resend Code button, Cancel button, status Text. Inactive by default.

- [ ] **Step 5: Rewire the AuthScreen component**

Wire every serialized field listed in Task 4's Interfaces block via `manage_components` (set references by fileID/path). Renamed fields (`_forgotEmailPanel`, `_forgotEmailStatusText`, `_forgotResetStatusText`) and all new Verify/Reset panel fields must be non-null; `_guestButton` no longer exists on the component.

- [ ] **Step 6: Verify wiring**

Dump the AuthScreen component's serialized references (`manage_components` get) and confirm none of the fields in Task 4's Interfaces list is `None`/`{fileID: 0}` — this scene has shipped a `{fileID: 0}` regression before (`_forgotPasswordButton`, found 2026-07-16).

- [ ] **Step 7: Save the scene and commit**

Unity MCP `manage_scene` save, then:

```bash
git add Assets/_Project/Scenes/Auth.unity
git commit -m "Auth scene: verify-email panel, split forgot-password panels, remove guest button"
```

---

### Task 6: Full verification and ship

**Files:** none new — verification, push, PR.

- [ ] **Step 1: Run the full EditMode suite**

Unity MCP `run_tests` (EditMode, no filter). Expected: all tests pass (117 pre-existing + 5 new from Task 1). Fix anything red before proceeding.

- [ ] **Step 2: Console sweep**

`read_console` — zero errors/warnings introduced by this work.

- [ ] **Step 3: Play Mode smoke test (editor, LocalMock services)**

Enter Play Mode from the Boot scene via Unity MCP `manage_editor`, then confirm in the console/scene: (1) register with a fresh email → Verify Email panel appears, code `123456` verifies and advances to the game; (2) register + Cancel → back at Login, re-registering the same email succeeds (account was deleted); (3) forgot password → send code auto-advances to the reset panel, code `123456` + new password returns to Login with the success message; (4) no Guest button anywhere. If Play Mode automation is impractical, flag the smoke test as pending for the user instead of skipping silently.

- [ ] **Step 4: Push and open a draft PR**

```bash
git push -u origin worktree-fix-auth-forgot-password-verify-email
gh pr create --draft --title "Auth flow: registration email verification, two-panel forgot password, anonymous-session lockdown" --body "..."
```

PR body must include the **manual UGS deployment checklist** (this repo has no deploy tooling): deploy `CheckEmailAvailable` alongside the already-pending `RequestPasswordReset`, `ConfirmPasswordReset`, `SaveEmail`, `RequestEmailVerificationCode`, `ConfirmEmailVerificationCode`; prerequisites unchanged (Cloud Code secrets `RESEND_API_KEY`, `RESET_FROM_EMAIL`, `UGS_SERVICE_ACCOUNT_KEY`, `UGS_SERVICE_ACCOUNT_SECRET`; Cloud Save index on `email_lookup`; verified Resend sender domain). End the body with the standard Claude Code attribution line.
