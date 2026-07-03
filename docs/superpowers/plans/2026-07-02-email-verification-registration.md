# Email Verification on Registration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Before a new account is created, prove the player owns the email address they typed by sending it a 6-digit code and requiring them to enter it back correctly.

**Architecture:** Two new Cloud Code functions (`RequestEmailVerificationCode`, `ConfirmEmailVerificationCode`) mirror the existing password-reset OTP pattern (`RequestPasswordReset.js`/`ConfirmPasswordReset.js`) but store the pending code in a shared Custom Data item keyed by an email hash, since no player account exists yet to scope a player-level Cloud Save key to. `IAuthService` gets two new methods; `AuthScreen.OnRegisterClicked` calls `ConfirmEmailVerificationCodeAsync` and only proceeds to the existing `RegisterAsync` (UGS account creation) if it succeeds. Client-side gating only — the same trust model already used for password reset (client sequences the calls; nothing server-side re-checks that verification happened before account creation, since sign-up itself isn't a privileged economy action).

**Tech Stack:** Unity 6 / C# (VContainer DI), Unity Gaming Services Cloud Code (Node.js) + Cloud Save Custom Data API, Resend (existing email provider), NUnit EditMode tests.

## Global Constraints

- Reuse the existing Cloud Code secrets — no new secrets to configure: `RESEND_API_KEY`, `RESET_FROM_EMAIL` (already required for `RequestPasswordReset`).
- OTP format: 6 digits, 15-minute TTL — matches `RequestPasswordReset.js`/`ConfirmPasswordReset.js` exactly.
- `RequestEmailVerificationCode` has no existing-account check to bound abuse (unlike `RequestPasswordReset`, which only emails addresses tied to a real account) — it enforces a 60-second per-email cooldown instead, to bound email-bombing/Resend-quota abuse against arbitrary third-party addresses. Added post-review (Task 1); see that task's code for the exact mechanism.
- Follow the codebase's existing "Known Issue #6" convention correctly from the start: `new DataApi(context)` + positional `getCustomItems`/`setCustomItem` args — never `new DataApi({ headers: ... })` or an options-object call. (See `ServerCode/CLOUD_CODE_FUNCTIONS.md` and `ServerCode/GetLandRegistry.js` for the correct reference pattern.)
- `AuthService`/`LocalMockAuthService`/`AuthScreen` have no existing unit test coverage in this codebase except where mock state-machine logic is meaningfully testable (see `LocalMockFriendsServiceTests.cs` for precedent) — don't invent test infrastructure for the UGS-SDK-bound or MonoBehaviour-bound pieces; do write tests for new mock logic, matching that precedent.
- `ServerCode/*.js` files in this codebase have no automated test runner — verify with `node --check` only, plus manual Play Mode verification.
- `docs/` is intentionally untracked in this repo (kept on disk, not committed) — this plan file itself does not need to be committed to git.

---

### Task 1: Cloud Code — RequestEmailVerificationCode.js

**Files:**
- Create: `ServerCode/RequestEmailVerificationCode.js`

**Interfaces:**
- Consumes: nothing from other tasks.
- Produces: a deployable Cloud Code function callable as `RequestEmailVerificationCode` with params `{ email: string }`, returning `{ success: true }` or throwing on invalid email / send failure. Task 3's `AuthService.RequestEmailVerificationCodeAsync` calls this by name.

- [ ] **Step 1: Write the function**

```js
// RequestEmailVerificationCode — generates a 6-digit OTP to prove a
// registration email address is reachable, before the account is created.
// There's no player yet to scope a Cloud Save key to, so the pending code is
// stored in a shared Custom Data item keyed by an email hash. Emailed via
// Resend, same as RequestPasswordReset.js.
//
// SETUP REQUIRED: reuses the same Cloud Code secrets as RequestPasswordReset:
//   RESEND_API_KEY   — your Resend API key
//   RESET_FROM_EMAIL — verified sender address (e.g. noreply@yourgame.com)
//
// NOTE: the read-modify-write against the shared "pending_codes" Custom Data
// item is not transactional — same caveat already documented for
// land_registry writes (PurchaseLand.js etc.). Acceptable here: the only
// failure mode is two different emails' codes being requested in the same
// instant clobbering each other's map entry, which just means one of the two
// players has to click "Send Code" again.
const { DataApi } = require("@unity-services/cloud-save-1.4");

const CUSTOM_ID   = "email_verification";
const CODES_KEY   = "pending_codes";
const OTP_TTL_MS  = 15 * 60 * 1000; // 15 minutes, matches RequestPasswordReset
const COOLDOWN_MS = 60 * 1000;      // 60 seconds between sends to the same email —
                                     // there's no existing-account check to bound abuse
                                     // here (unlike RequestPasswordReset), so this bounds
                                     // email-bombing/Resend-quota abuse against arbitrary
                                     // third-party addresses instead.
const EMAIL_REGEX = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

function generateOtp() {
  return String(Math.floor(100000 + Math.random() * 900000));
}

function emailToKey(email) {
  // Deterministic short key from the email without storing PII as a Cloud
  // Save key. Simple djb2 hash — no crypto module needed. Duplicated from
  // SaveEmail.js/RequestPasswordReset.js (Cloud Code modules deploy as
  // standalone files, so small helpers are duplicated rather than shared).
  let h = 5381;
  for (let i = 0; i < email.length; i++) h = ((h << 5) + h) ^ email.charCodeAt(i);
  return (h >>> 0).toString(16).padStart(8, "0");
}

async function sendVerificationEmail(email, otp, apiKey, fromEmail) {
  const res = await fetch("https://api.resend.com/emails", {
    method:  "POST",
    headers: {
      "Authorization": `Bearer ${apiKey}`,
      "Content-Type":  "application/json"
    },
    body: JSON.stringify({
      from:    fromEmail,
      to:      [email],
      subject: "Social Universe — Verify Your Email",
      text:    `Your verification code is: ${otp}\n\nEnter this code to finish creating your account. This code expires in 15 minutes.`
    })
  });

  if (!res.ok) throw new Error(`Resend error: ${res.status}`);
}

/**
 * @param {string} email - The email address to verify.
 */
module.exports = async ({ params, context, logger, secrets }) => {
  const email = (params.email ?? "").trim().toLowerCase();

  if (!EMAIL_REGEX.test(email)) {
    throw new Error("Invalid email address");
  }

  const { projectId } = context;
  const customDataApi = new DataApi(context);
  const emailKey = emailToKey(email);

  let codes = {};
  try {
    const res  = await customDataApi.getCustomItems(projectId, CUSTOM_ID, [CODES_KEY]);
    const item = res.data.results.find(r => r.key === CODES_KEY);
    if (item?.value) codes = item.value;
  } catch (_) { /* nothing pending yet */ }

  const existing = codes[emailKey];
  if (existing && Date.now() - existing.requestedAt < COOLDOWN_MS) {
    throw new Error("Please wait a moment before requesting another code");
  }

  const otp = generateOtp();
  codes[emailKey] = { otp, expiresAt: Date.now() + OTP_TTL_MS, requestedAt: Date.now() };
  await customDataApi.setCustomItem(projectId, CUSTOM_ID, { key: CODES_KEY, value: codes });

  // Unlike RequestPasswordReset, don't swallow a send failure — there's no
  // enumeration risk pre-registration, and a silent failure would strand the
  // player with no code and no way to know why.
  await sendVerificationEmail(email, otp, secrets.RESEND_API_KEY, secrets.RESET_FROM_EMAIL);

  logger.info(`RequestEmailVerificationCode: code sent to ${email}`);
  return { success: true };
};
```

- [ ] **Step 2: Verify syntax**

Run: `node --check ServerCode/RequestEmailVerificationCode.js`
Expected: no output (exit code 0).

- [ ] **Step 3: Commit**

```bash
git add ServerCode/RequestEmailVerificationCode.js
git commit -m "Add RequestEmailVerificationCode Cloud Code function"
```

---

### Task 2: Cloud Code — ConfirmEmailVerificationCode.js

**Files:**
- Create: `ServerCode/ConfirmEmailVerificationCode.js`

**Interfaces:**
- Consumes: the `{ [emailHash]: { otp, expiresAt } }` map shape written by Task 1's `RequestEmailVerificationCode.js` under Custom Data `customId: "email_verification"`, `key: "pending_codes"`.
- Produces: a deployable Cloud Code function callable as `ConfirmEmailVerificationCode` with params `{ email: string, code: string }`, returning `{ success: true }` or throwing one of: `"Invalid email address"`, `"Verification code must be 6 digits"`, `"No verification code requested for this email"`, `"Verification code has expired — request a new one"`, `"Invalid verification code"`. Task 3's `AuthService.ConfirmEmailVerificationCodeAsync` calls this by name; Task 5's `AuthScreen.FriendlyError` maps these exact throw messages to UI copy.

- [ ] **Step 1: Write the function**

```js
// ConfirmEmailVerificationCode — validates the OTP sent by
// RequestEmailVerificationCode. The client calls this immediately before
// RegisterAsync (AuthenticationService.SignUpWithUsernamePasswordAsync) —
// see AuthScreen.OnRegisterClicked. Single-use: the code is cleared from the
// shared Custom Data item on success.
const { DataApi } = require("@unity-services/cloud-save-1.4");

const CUSTOM_ID   = "email_verification";
const CODES_KEY   = "pending_codes";
const EMAIL_REGEX = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

function emailToKey(email) {
  let h = 5381;
  for (let i = 0; i < email.length; i++) h = ((h << 5) + h) ^ email.charCodeAt(i);
  return (h >>> 0).toString(16).padStart(8, "0");
}

/**
 * @param {string} email - The email address being verified.
 * @param {string} code  - The 6-digit OTP from the verification email.
 */
module.exports = async ({ params, context, logger }) => {
  const email = (params.email ?? "").trim().toLowerCase();
  const code  = (params.code  ?? "").trim();

  if (!EMAIL_REGEX.test(email)) throw new Error("Invalid email address");
  if (code.length !== 6)        throw new Error("Verification code must be 6 digits");

  const { projectId } = context;
  const customDataApi = new DataApi(context);
  const emailKey = emailToKey(email);

  let codes = {};
  try {
    const res  = await customDataApi.getCustomItems(projectId, CUSTOM_ID, [CODES_KEY]);
    const item = res.data.results.find(r => r.key === CODES_KEY);
    if (item?.value) codes = item.value;
  } catch (_) { /* nothing pending */ }

  const entry = codes[emailKey];
  if (!entry)                       throw new Error("No verification code requested for this email");
  if (Date.now() > entry.expiresAt) throw new Error("Verification code has expired — request a new one");
  if (code !== entry.otp)           throw new Error("Invalid verification code");

  delete codes[emailKey];
  await customDataApi.setCustomItem(projectId, CUSTOM_ID, { key: CODES_KEY, value: codes });

  logger.info(`ConfirmEmailVerificationCode: verified ${email}`);
  return { success: true };
};
```

- [ ] **Step 2: Verify syntax**

Run: `node --check ServerCode/ConfirmEmailVerificationCode.js`
Expected: no output (exit code 0).

- [ ] **Step 3: Commit**

```bash
git add ServerCode/ConfirmEmailVerificationCode.js
git commit -m "Add ConfirmEmailVerificationCode Cloud Code function"
```

---

### Task 3: IAuthService interface + AuthService (real) + LocalMockAuthService (stub)

**Files:**
- Modify: `Assets/_Project/Scripts/Core/IAuthService.cs`
- Modify: `Assets/_Project/Scripts/Net/AuthService.cs`
- Modify: `Assets/_Project/Scripts/Net/LocalMockAuthService.cs`

**Interfaces:**
- Consumes: Cloud Code functions `RequestEmailVerificationCode`/`ConfirmEmailVerificationCode` from Tasks 1–2 (by string name, via `IBackendClient.CallAsync`).
- Produces: `Task RequestEmailVerificationCodeAsync(string email)` and `Task ConfirmEmailVerificationCodeAsync(string email, string code)` on `IAuthService`, real-implemented in `AuthService`, stubbed (throws `NotImplementedException`) in `LocalMockAuthService` pending Task 4. Task 5 (`AuthScreen`) calls both by these exact names.

Every C# class implementing `IAuthService` must implement every member for the project to compile — that's why the mock gets a stub in this task instead of being deferred to Task 4 outright. Task 4 replaces the stub with real logic via TDD.

- [ ] **Step 1: Add the interface methods**

In `Assets/_Project/Scripts/Core/IAuthService.cs`, add after the existing `ConfirmPasswordResetAsync` line:

```csharp
        // Password reset: client sends email; Cloud Code handles OTP generation/delivery/validation.
        Task RequestPasswordResetAsync(string email);
        Task ConfirmPasswordResetAsync(string email, string resetCode, string newPassword);

        // Registration email verification: client sends email; Cloud Code handles OTP
        // generation/delivery/validation. Call ConfirmEmailVerificationCodeAsync
        // successfully before calling RegisterAsync — see AuthScreen.OnRegisterClicked.
        Task RequestEmailVerificationCodeAsync(string email);
        Task ConfirmEmailVerificationCodeAsync(string email, string code);
    }
}
```

(This replaces the file's closing `}` `}` lines — the new methods go inside the interface body, right before its closing brace.)

- [ ] **Step 2: Implement in AuthService.cs**

In `Assets/_Project/Scripts/Net/AuthService.cs`, add after the existing `ConfirmPasswordResetAsync` method:

```csharp
        public async Task RequestEmailVerificationCodeAsync(string email)
        {
            await _backend.CallAsync("RequestEmailVerificationCode",
                new Dictionary<string, object> { { "email", email } });
            SULog.Info($"Email verification code requested for {email}", SULog.Channel.Net);
        }

        public async Task ConfirmEmailVerificationCodeAsync(string email, string code)
        {
            await _backend.CallAsync("ConfirmEmailVerificationCode",
                new Dictionary<string, object> { { "email", email }, { "code", code } });
            SULog.Info("Email verification code confirmed", SULog.Channel.Net);
        }
```

- [ ] **Step 3: Add stub implementations in LocalMockAuthService.cs**

In `Assets/_Project/Scripts/Net/LocalMockAuthService.cs`, add after the existing `ConfirmPasswordResetAsync` method (temporary — replaced with real logic in Task 4):

```csharp
        public Task RequestEmailVerificationCodeAsync(string email) =>
            throw new NotImplementedException();

        public Task ConfirmEmailVerificationCodeAsync(string email, string code) =>
            throw new NotImplementedException();
```

- [ ] **Step 4: Verify the project compiles**

```
mcp__UnityMCP__refresh_unity  (compile: "request", scope: "scripts")
mcp__UnityMCP__read_console   (types: ["error", "warning"], count: 30)
```

Expected: no new `error CS...` entries referencing `IAuthService.cs`, `AuthService.cs`, or `LocalMockAuthService.cs`.

- [ ] **Step 5: Commit**

```bash
git add Assets/_Project/Scripts/Core/IAuthService.cs Assets/_Project/Scripts/Net/AuthService.cs Assets/_Project/Scripts/Net/LocalMockAuthService.cs
git commit -m "Add email verification methods to IAuthService/AuthService; stub in LocalMockAuthService"
```

---

### Task 4: LocalMockAuthService — TDD the mock verification logic

**Files:**
- Create: `Assets/_Project/Tests/EditMode/Net/LocalMockAuthServiceTests.cs`
- Modify: `Assets/_Project/Scripts/Net/LocalMockAuthService.cs`

**Interfaces:**
- Consumes: `LocalMockAuthService` from Task 3 (currently stubbed).
- Produces: working mock behavior other tasks' manual verification (Task 7) relies on: `RequestEmailVerificationCodeAsync(email)` always succeeds and remembers the email as pending; `ConfirmEmailVerificationCodeAsync(email, code)` succeeds only if code `"123456"` was requested for that email and hasn't been consumed yet.

- [ ] **Step 1: Write the failing tests**

Create `Assets/_Project/Tests/EditMode/Net/LocalMockAuthServiceTests.cs`:

```csharp
using System.Threading.Tasks;
using NUnit.Framework;
using SocialUniverse.Net;

namespace SocialUniverse.Tests
{
    // Exercises the registration email-verification mock (the UGS-backed
    // AuthService is verified in PlayMode — it is a thin SDK/Cloud-Code
    // wrapper with no branching logic of its own).
    public class LocalMockAuthServiceTests
    {
        private LocalMockAuthService _auth;

        [SetUp]
        public void SetUp() => _auth = new LocalMockAuthService();

        [Test]
        public async Task Confirming_with_correct_code_after_request_succeeds()
        {
            await _auth.RequestEmailVerificationCodeAsync("Player@Example.com");

            Assert.DoesNotThrowAsync(async () =>
                await _auth.ConfirmEmailVerificationCodeAsync("player@example.com", "123456"));
        }

        [Test]
        public void Confirming_without_requesting_first_throws()
        {
            Assert.ThrowsAsync<System.InvalidOperationException>(async () =>
                await _auth.ConfirmEmailVerificationCodeAsync("nobody@example.com", "123456"));
        }

        [Test]
        public async Task Confirming_with_wrong_code_throws()
        {
            await _auth.RequestEmailVerificationCodeAsync("player@example.com");

            Assert.ThrowsAsync<System.InvalidOperationException>(async () =>
                await _auth.ConfirmEmailVerificationCodeAsync("player@example.com", "000000"));
        }

        [Test]
        public async Task Code_is_single_use()
        {
            await _auth.RequestEmailVerificationCodeAsync("player@example.com");
            await _auth.ConfirmEmailVerificationCodeAsync("player@example.com", "123456");

            Assert.ThrowsAsync<System.InvalidOperationException>(async () =>
                await _auth.ConfirmEmailVerificationCodeAsync("player@example.com", "123456"));
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```
mcp__UnityMCP__run_tests  (mode: "EditMode", assembly_names: ["SocialUniverse.Tests"], test_names: ["SocialUniverse.Tests.LocalMockAuthServiceTests"])
```

Expected: all 4 tests FAIL with `System.NotImplementedException` (from the Task 3 stub).

- [ ] **Step 3: Replace the stubs with real mock logic**

In `Assets/_Project/Scripts/Net/LocalMockAuthService.cs`, add a new field alongside the existing `_pendingResets` field:

```csharp
        private readonly HashSet<string>                 _pendingResets = new(); // normalized emails awaiting reset
        private readonly HashSet<string>                 _pendingRegistrationCodes = new(); // normalized emails with an outstanding verification code (mock code: 123456)
```

Then replace the Step 3 stub from Task 3 with:

```csharp
        // Always "succeeds" — mirrors RequestPasswordResetAsync's mock style.
        // Mock code is always "123456".
        public Task RequestEmailVerificationCodeAsync(string email)
        {
            string key = NormalizeEmail(email);
            _pendingRegistrationCodes.Add(key);
            SULog.Info($"[MOCK] Email verification code sent to {email} (mock code: 123456)", SULog.Channel.Net);
            return Task.CompletedTask;
        }

        public Task ConfirmEmailVerificationCodeAsync(string email, string code)
        {
            string key = NormalizeEmail(email);
            if (!_pendingRegistrationCodes.Contains(key))
                throw new InvalidOperationException("No verification code requested for this email");
            if (code != "123456")
                throw new InvalidOperationException("Invalid verification code");
            _pendingRegistrationCodes.Remove(key);
            SULog.Info($"[MOCK] Email verified for {email}", SULog.Channel.Net);
            return Task.CompletedTask;
        }
```

- [ ] **Step 4: Run tests to verify they pass**

```
mcp__UnityMCP__run_tests  (mode: "EditMode", assembly_names: ["SocialUniverse.Tests"], test_names: ["SocialUniverse.Tests.LocalMockAuthServiceTests"])
```

Expected: all 4 tests PASS.

- [ ] **Step 5: Run the full EditMode suite to check for regressions**

```
mcp__UnityMCP__run_tests  (mode: "EditMode")
```

Expected: same pass count as before this task, plus these 4 new passes; no new failures.

- [ ] **Step 6: Commit**

```bash
git add Assets/_Project/Tests/EditMode/Net/LocalMockAuthServiceTests.cs Assets/_Project/Scripts/Net/LocalMockAuthService.cs
git commit -m "Implement and test LocalMockAuthService email verification"
```

---

### Task 5: AuthScreen — gate registration behind email verification

**Files:**
- Modify: `Assets/_Project/Scripts/UI/AuthScreen.cs`

**Interfaces:**
- Consumes: `IAuthService.RequestEmailVerificationCodeAsync`/`ConfirmEmailVerificationCodeAsync` from Task 3/4.
- Produces: two new serialized fields (`_regCodeField`, `_sendVerificationCodeButton`) that Task 6's scene wiring must assign in the Inspector.

- [ ] **Step 1: Add serialized fields**

In `Assets/_Project/Scripts/UI/AuthScreen.cs`, add to the "Register panel" field group (after `_regConfirmField`):

```csharp
        [SerializeField] private InputField _regConfirmField;
        [SerializeField] private InputField _regCodeField;
        [SerializeField] private Text       _regStatusText;
        [SerializeField] private Button     _registerButton;
        [SerializeField] private Button     _sendVerificationCodeButton;
        [SerializeField] private Button     _goToLoginButton;
```

- [ ] **Step 2: Wire the new button in `Start()`**

In the `Start()` method, add alongside the existing `_registerButton.onClick.AddListener(OnRegisterClicked);` line:

```csharp
            _registerButton    .onClick.AddListener(OnRegisterClicked);
            if (_sendVerificationCodeButton != null) _sendVerificationCodeButton.onClick.AddListener(OnSendVerificationCodeClicked);
```

- [ ] **Step 3: Add `OnSendVerificationCodeClicked` and rewrite `OnRegisterClicked`**

Replace the existing `OnRegisterClicked` method with:

```csharp
        private async void OnSendVerificationCodeClicked()
        {
            string username = _regUsernameField.text.Trim();
            string email    = _regEmailField.text.Trim();
            string password = _regPasswordField.text;
            string confirm  = _regConfirmField.text;

            if (!ValidateUsername(username, out string nameErr)) { _regStatusText.text = nameErr; return; }
            if (!ValidateEmail(email, out string emailErr))       { _regStatusText.text = emailErr; return; }
            if (!ValidatePassword(password, out string passErr))  { _regStatusText.text = passErr; return; }
            if (password != confirm)
            {
                _regStatusText.text = "Passwords do not match";
                return;
            }

            if (_sendVerificationCodeButton != null) _sendVerificationCodeButton.interactable = false;
            _regStatusText.text = "Sending verification code…";
            try
            {
                await _auth.RequestEmailVerificationCodeAsync(email);
                _regStatusText.text = "Verification code sent — check your email (mock code: 123456)";
            }
            catch (Exception ex)
            {
                _regStatusText.text = FriendlyError(ex);
            }
            finally
            {
                if (_sendVerificationCodeButton != null) _sendVerificationCodeButton.interactable = true;
            }
        }

        private async void OnRegisterClicked()
        {
            string username = _regUsernameField.text.Trim();
            string email     = _regEmailField.text.Trim();
            string password  = _regPasswordField.text;
            string confirm   = _regConfirmField.text;
            string code      = _regCodeField != null ? _regCodeField.text.Trim() : "";

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
            if (string.IsNullOrWhiteSpace(code))
            {
                _regStatusText.text = "Enter the verification code sent to your email";
                return;
            }

            SetBusy(true);
            _regStatusText.text = "Verifying code…";
            try
            {
                await _auth.ConfirmEmailVerificationCodeAsync(email, code);
                _regStatusText.text = "Creating account…";
                await _auth.RegisterAsync(username, password, email);
            }
            catch (Exception ex)
            {
                _regStatusText.text = FriendlyError(ex);
                SetBusy(false);
            }
        }
```

- [ ] **Step 4: Extend `FriendlyError` for the new error messages**

In the `FriendlyError` method, add these checks before the final `network`/`Network` check:

```csharp
            if (msg.Contains("No password reset") || msg.Contains("No reset"))
                return "No reset was requested for this email — click Send Reset Code first";
            if (msg.Contains("Invalid reset code"))
                return "Incorrect reset code — check your email and try again";
            if (msg.Contains("No verification code"))
                return "No verification code was sent — click Send Code first";
            if (msg.Contains("Verification code has expired"))
                return "Verification code expired — click Send Code to get a new one";
            if (msg.Contains("Invalid verification code"))
                return "Incorrect verification code — check your email and try again";
            if (msg.Contains("network") || msg.Contains("Network") || msg.Contains("unreachable"))
                return "Network error — check your connection";
```

- [ ] **Step 5: Verify the project compiles**

```
mcp__UnityMCP__refresh_unity  (compile: "request", scope: "scripts")
mcp__UnityMCP__read_console   (types: ["error", "warning"], count: 30)
```

Expected: no new `error CS...` entries referencing `AuthScreen.cs`.

- [ ] **Step 6: Commit**

```bash
git add Assets/_Project/Scripts/UI/AuthScreen.cs
git commit -m "Gate registration behind email verification in AuthScreen"
```

---

### Task 6: Wire the new UI elements into Auth.unity

**Files:**
- Modify: `Assets/Scenes/Auth.unity` (scene asset — edited live via UnityMCP, not by hand-editing YAML)

**Interfaces:**
- Consumes: `AuthScreen._regCodeField` (InputField) and `AuthScreen._sendVerificationCodeButton` (Button) serialized fields from Task 5.
- Produces: a working Register panel in the Auth scene with a code entry field and a "Send Code" button, both correctly assigned on the `AuthScreen` component.

This task is inherently interactive (exact GameObject instance IDs only exist once the scene is inspected live) — the steps below are the concrete recipe to follow, not a placeholder.

- [ ] **Step 1: Inspect the existing Register panel**

```
mcp__UnityMCP__find_gameobjects  (search for the Register panel's existing InputField named "RegConfirmField" or similar, and the "RegisterButton"/"Create Account" button — read their names, RectTransform, and sibling index so the new elements can match their style and sit in a sensible position)
```

- [ ] **Step 2: Duplicate an existing InputField for the code field**

```
mcp__UnityMCP__manage_gameobject  (action: "duplicate" on the GameObject found in Step 1 for the confirm-password field)
```

Rename the duplicate to `RegCodeField`. Clear its placeholder text to read "Verification code". Position it directly below the existing confirm-password field (same X, Y offset by one row height — match the vertical spacing already used between sibling fields in the panel).

- [ ] **Step 3: Duplicate an existing Button for the send-code button**

```
mcp__UnityMCP__manage_gameobject  (action: "duplicate" on the "Create Account"/RegisterButton GameObject)
```

Rename the duplicate to `SendVerificationCodeButton`. Change its label text (TMP_Text/Text child) to "Send Code". Position it above `RegCodeField` (between the confirm-password field and the code field), so the flow reads top-to-bottom: username → email → password → confirm → [Send Code] → code → [Create Account].

- [ ] **Step 4: Update the existing register button's label**

Change the `RegisterButton`'s label text to "Verify & Create Account" (it now performs both steps — see Task 5).

- [ ] **Step 5: Wire the AuthScreen component's new serialized fields**

On the `AuthScreen` component (found via `find_gameobjects`), assign:
- `_regCodeField` → the `RegCodeField` GameObject's `InputField` component from Step 2.
- `_sendVerificationCodeButton` → the `SendVerificationCodeButton` GameObject's `Button` component from Step 3.

Use `mcp__UnityMCP__manage_components` to set these object-reference fields (inspect its schema via `ToolSearch` if the exact action/parameter names aren't already loaded — this tool is what the M2/M3 work used to wire `EmailField`→`AuthScreen._loginEmailField` previously, per this repo's own history).

- [ ] **Step 6: Save the scene**

```
mcp__UnityMCP__manage_scene  (action to save Auth.unity)
```

- [ ] **Step 7: Verify in the console**

```
mcp__UnityMCP__read_console  (types: ["error", "warning"], count: 20)
```

Expected: no new errors (e.g. no `MissingReferenceException` from a dangling serialized field).

- [ ] **Step 8: Commit**

```bash
git add Assets/Scenes/Auth.unity
git commit -m "Wire email verification UI into Auth.unity"
```

---

### Task 7: Manual end-to-end verification

**Files:** none (verification only)

- [ ] **Step 1: Enter Play Mode on the Bootstrap scene with `LocalMockAuthService` active** (default when running standalone without a live UGS project — see `Social_Universe_Architecture.md` / `PROGRESS.md` for how the mock is selected).

- [ ] **Step 2: Golden path** — On the Register panel: enter a new username/email/password/confirm, click **Send Code**. Confirm the status text shows "Verification code sent…". Enter `123456` in the code field, click **Verify & Create Account**. Confirm the app advances past Auth (i.e. `PlayerReadyEvent` fires — same signal `HandleSignedIn` already uses).

- [ ] **Step 3: Wrong code** — Repeat Step 2 but enter `000000` instead of `123456`. Confirm the status text reads "Incorrect verification code — check your email and try again" and the app does **not** advance.

- [ ] **Step 4: Skipped verification** — Fill the Register form and click **Verify & Create Account** directly without clicking Send Code first (leave the code field blank). Confirm the status text reads "Enter the verification code sent to your email". Then type any 6 digits and click again — confirm it reads "No verification code was sent — click Send Code first".

- [ ] **Step 5: Resend** — This step exercises the mock only (`LocalMockAuthService` has no cooldown — see note below). Click **Send Code** twice in a row for the same email, then confirm with `123456`. Confirm it still succeeds (the second request overwrites the first's pending code with a fresh one, per the mock's `HashSet.Add` — no double-send state issue). Against the **real backend** (Step 6), the 60-second cooldown added to `RequestEmailVerificationCode.js` after Task 1's review means an immediate second click there is expected to fail with "Please wait a moment before requesting another code" — confirm that message appears, then confirm a resend after waiting 60+ seconds succeeds.

- [ ] **Step 6 (requires a live UGS project + deployed Cloud Code):** repeat Steps 2–4 against the real backend. Before doing this, deploy `ServerCode/RequestEmailVerificationCode.js` and `ServerCode/ConfirmEmailVerificationCode.js` via the UGS CLI, and confirm the `RESEND_API_KEY`/`RESET_FROM_EMAIL` secrets are already configured (they are, per `RequestPasswordReset.js`'s existing setup — no new secrets needed). Confirm a real email arrives with a 6-digit code and that entering it completes registration.

- [ ] **Step 7: Record results** — Note the pass/fail outcome of Steps 2–6 in this session's follow-up (this codebase tracks manual verification status per feature; see how `EmailLoginKeyTests`/email-login-auth verification was tracked previously for the expected format).
