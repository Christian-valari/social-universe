# Post-Login Email Verification Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let registration succeed immediately (no code required at signup), then prompt the player once, right after first login, to verify their email — skippable, and re-triggerable later from the HUD.

**Architecture:** This replaces the pre-registration email verification design (which required a Custom-Data-keyed OTP scheme since no player existed yet). Because verification now always happens against an already-authenticated real player, the Cloud Code side collapses to the same player-scoped Cloud Save pattern `RequestPasswordReset.js`/`ConfirmPasswordReset.js` already use — no client-supplied email (the server reads the player's own saved `player_profile.email`), no shared item, no cross-request races. `AuthScreen` reverts to a single-step registration; a new `EmailVerificationModal` (mirroring `DisplayNameModal`) is shown once from `PlanetSceneScope` after the player's profile hydrates, if `emailVerified` is false and they haven't been prompted before (tracked via a local PlayerPrefs flag).

**Tech Stack:** Unity 6 / C# (VContainer DI), Unity Gaming Services Cloud Code (Node.js) + Cloud Save player-scoped API, Resend (existing email provider), NUnit EditMode tests.

## Global Constraints

- This plan **replaces** the prior pre-registration email verification feature. It removes: `AuthScreen`'s Register-panel code field/Send-Code button and gating logic, and the Custom-Data-keyed storage in the two Cloud Code functions. It **keeps unchanged**: `AuthScreen.EnsureSessionAsync`/`_suppressAutoTransition` and their use in the Forgot Password handlers (`OnSendResetCodeClicked`/`OnResetPasswordClicked`) — that fix is independent, since a signed-out "forgot password" user still needs a session for those two Cloud Code calls.
- `AuthService.RegisterAsync` reverts from `AddUsernamePasswordAsync` back to `SignUpWithUsernamePasswordAsync` — the swap to `Add` existed only to accommodate the anonymous pre-auth session the old pre-registration verification required, which no longer exists.
- `IAuthService.RequestEmailVerificationCodeAsync`/`ConfirmEmailVerificationCodeAsync` drop their `email` parameter entirely (the server derives the email from the caller's own `context.playerId`).
- Cloud Code throw messages: `"No verification code requested"`, `"Verification code has expired — request a new one"`, `"Invalid verification code"`, `"No email on file for this account"` (new — the edge case where `SaveEmail` failed at registration). No message references "for this email" anymore since there's no client-supplied email.
- Follow the codebase's "Known Issue #6" convention: `new DataApi(context)` + positional `getItems`/`setItem` args — never `new DataApi({ headers: ... })` or an options-object call.
- No `deleteItem`-style call — clear the OTP via the null-sentinel overwrite pattern already used in `ConfirmPasswordReset.js`/`ConfirmEmailVerificationCode.js`.
- The one-time "has the player been prompted" state is a **local PlayerPrefs flag**, not server-side — a deliberate simplification (the cost of re-prompting on a reinstall/new device is low, and it avoids a second Cloud Code function purely for UI nagging state).
- `AuthService`/`LocalMockAuthService`/`AuthScreen`/`EmailVerificationModal`/`HUDController` have no existing unit test coverage in this codebase except where mock state-machine logic is meaningfully testable (`LocalMockAuthServiceTests.cs`/`LocalMockFriendsServiceTests.cs` precedent) — update that mock test file for the new signatures; don't invent test infrastructure for the UGS-SDK-bound or MonoBehaviour-bound pieces.
- `ServerCode/*.js` files have no automated test runner — verify with `node --check` only, plus manual Play Mode verification.
- `docs/` is intentionally untracked in this repo (kept on disk, not committed) — this plan file does not need to be committed to git.
- The connected Unity Editor in this environment is bound to a different checkout than the git worktree used for this plan's commits — any task requiring live compile/test/scene verification must be mirrored into that checkout first (established pattern from the prior plan's execution).

---

### Task 1: Cloud Code — rewrite RequestEmailVerificationCode.js and ConfirmEmailVerificationCode.js for player-scoped storage

**Files:**
- Modify (full rewrite): `ServerCode/RequestEmailVerificationCode.js`
- Modify (full rewrite): `ServerCode/ConfirmEmailVerificationCode.js`

**Interfaces:**
- Consumes: nothing from other tasks (Cloud Code functions are independent modules).
- Produces: `RequestEmailVerificationCode` — no params, returns `{ success: true }` or throws `"No email on file for this account"` / `"Please wait a moment before requesting another code"`. `ConfirmEmailVerificationCode` — params `{ code: string }`, returns `{ success: true }` or throws `"Verification code must be 6 digits"` / `"No verification code requested"` / `"Verification code has expired — request a new one"` / `"Invalid verification code"`. Task 3's `AuthService` calls both by these exact names with these exact param shapes.

- [ ] **Step 1: Replace the full content of `ServerCode/RequestEmailVerificationCode.js`**

```js
// RequestEmailVerificationCode — generates a 6-digit OTP to prove the
// calling player's registered email is reachable. The player already has an
// account by this point (verification happens post-login, not pre-account),
// so this reads their own saved email from player_profile (set by
// SaveEmail.js at registration) rather than trusting a client-supplied
// address, and stores the pending OTP under a player-scoped Cloud Save key
// — same pattern as RequestPasswordReset.js. No Custom Data, no email
// hashing: the caller's own context.playerId is the only key needed.
//
// SETUP REQUIRED: reuses the same Cloud Code secrets as RequestPasswordReset:
//   RESEND_API_KEY   — your Resend API key
//   RESET_FROM_EMAIL — verified sender address (e.g. noreply@yourgame.com)
const { DataApi } = require("@unity-services/cloud-save-1.4");

const PROFILE_KEY = "player_profile";
const OTP_KEY      = "email_verify_otp";
const OTP_TTL_MS    = 15 * 60 * 1000; // 15 minutes, matches RequestPasswordReset
const COOLDOWN_MS   = 60 * 1000;      // 60 seconds — cost/accidental-double-click guard;
                                      // the caller is an identified account now, so this
                                      // is no longer an abuse mitigant like it was pre-account.

function generateOtp() {
  return String(Math.floor(100000 + Math.random() * 900000));
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
      text:    `Your verification code is: ${otp}\n\nThis code expires in 15 minutes.`
    })
  });

  if (!res.ok) throw new Error(`Resend error: ${res.status}`);
}

/**
 * No parameters — the caller's own saved email (player_profile.email) is used.
 */
module.exports = async ({ context, logger, secrets }) => {
  const { projectId, playerId } = context;
  const saveApi = new DataApi(context);

  let profile = null;
  try {
    const res  = await saveApi.getItems(projectId, playerId, [PROFILE_KEY]);
    const item = res.data.results.find(r => r.key === PROFILE_KEY);
    if (item?.value) profile = typeof item.value === "string" ? JSON.parse(item.value) : item.value;
  } catch (_) { /* no profile saved */ }

  const email = profile?.email;
  if (!email) {
    throw new Error("No email on file for this account");
  }

  let existing = null;
  try {
    const res  = await saveApi.getItems(projectId, playerId, [OTP_KEY]);
    const item = res.data.results.find(r => r.key === OTP_KEY);
    if (item?.value) existing = item.value;
  } catch (_) { /* nothing pending yet */ }

  if (existing && Date.now() - existing.requestedAt < COOLDOWN_MS) {
    throw new Error("Please wait a moment before requesting another code");
  }

  const otp = generateOtp();
  await saveApi.setItem(projectId, playerId, {
    key: OTP_KEY,
    value: { otp, expiresAt: Date.now() + OTP_TTL_MS, requestedAt: Date.now() }
  });

  await sendVerificationEmail(email, otp, secrets.RESEND_API_KEY, secrets.RESET_FROM_EMAIL);

  logger.info(`RequestEmailVerificationCode: code sent to player ${playerId}`);
  return { success: true };
};
```

- [ ] **Step 2: Replace the full content of `ServerCode/ConfirmEmailVerificationCode.js`**

```js
// ConfirmEmailVerificationCode — validates the OTP sent by
// RequestEmailVerificationCode against the caller's own player-scoped Cloud
// Save record. On success, marks player_profile.emailVerified = true and
// clears the OTP (null-sentinel overwrite — no deleteItem precedent in this
// codebase, see LandTravel.js).
const { DataApi } = require("@unity-services/cloud-save-1.4");

const PROFILE_KEY = "player_profile";
const OTP_KEY      = "email_verify_otp";

/**
 * @param {string} code - The 6-digit OTP from the verification email.
 */
module.exports = async ({ params, context, logger }) => {
  const code = (params.code ?? "").trim();
  if (code.length !== 6) throw new Error("Verification code must be 6 digits");

  const { projectId, playerId } = context;
  const saveApi = new DataApi(context);

  let entry = null;
  try {
    const res  = await saveApi.getItems(projectId, playerId, [OTP_KEY]);
    const item = res.data.results.find(r => r.key === OTP_KEY);
    if (item?.value) entry = item.value;
  } catch (_) { /* nothing pending */ }

  if (!entry)                       throw new Error("No verification code requested");
  if (Date.now() > entry.expiresAt) throw new Error("Verification code has expired — request a new one");
  if (code !== entry.otp)           throw new Error("Invalid verification code");

  let profile = {};
  try {
    const res  = await saveApi.getItems(projectId, playerId, [PROFILE_KEY]);
    const item = res.data.results.find(r => r.key === PROFILE_KEY);
    if (item?.value) profile = typeof item.value === "string" ? JSON.parse(item.value) : item.value;
  } catch (_) { /* no profile yet — shouldn't happen if RequestEmailVerificationCode already ran */ }

  profile.emailVerified = true;
  await saveApi.setItem(projectId, playerId, { key: PROFILE_KEY, value: profile });
  await saveApi.setItem(projectId, playerId, { key: OTP_KEY, value: null });

  logger.info(`ConfirmEmailVerificationCode: verified player ${playerId}`);
  return { success: true };
};
```

- [ ] **Step 3: Verify syntax**

Run: `node --check ServerCode/RequestEmailVerificationCode.js && node --check ServerCode/ConfirmEmailVerificationCode.js`
Expected: no output (exit code 0).

- [ ] **Step 4: Commit**

```bash
git add ServerCode/RequestEmailVerificationCode.js ServerCode/ConfirmEmailVerificationCode.js
git commit -m "Rewrite email verification Cloud Code for post-login, player-scoped storage"
```

---

### Task 2: Add emailVerified to GetPlayerProfile.js and PlayerProfile.cs

**Files:**
- Modify: `ServerCode/GetPlayerProfile.js`
- Modify: `Assets/_Project/Scripts/Social/PlayerProfile.cs`

**Interfaces:**
- Consumes: `player_profile.emailVerified` written by Task 1's `ConfirmEmailVerificationCode.js`.
- Produces: `PlayerProfile.EmailVerified` (bool), populated from `GetPlayerProfile`'s `emailVerified` field. Task 6 (`PlanetSceneScope`) reads this.

- [ ] **Step 1: Add `emailVerified` to the return value in `ServerCode/GetPlayerProfile.js`**

The current file's return statement is:

```js
  return {
    playerId:    targetId,
    displayName: profile?.displayName ?? null,
    level:       profile?.level ?? 1,
    xp:          profile?.xp ?? 0,
    badges:      profile?.badges ?? [],
    tilesOwned
  };
};
```

Replace it with:

```js
  return {
    playerId:     targetId,
    displayName:  profile?.displayName ?? null,
    level:        profile?.level ?? 1,
    xp:           profile?.xp ?? 0,
    badges:       profile?.badges ?? [],
    tilesOwned,
    emailVerified: profile?.emailVerified ?? false
  };
};
```

- [ ] **Step 2: Verify syntax**

Run: `node --check ServerCode/GetPlayerProfile.js`
Expected: no output (exit code 0).

- [ ] **Step 3: Add `EmailVerified` to the `PlayerProfile` C# DTO**

The current file `Assets/_Project/Scripts/Social/PlayerProfile.cs` is:

```csharp
namespace SocialUniverse.Social
{
    // A player's public profile as returned by the "GetPlayerProfile" Cloud
    // Code function (field names match its lowercase JSON keys — the backend
    // deserializer is case-insensitive, same as the Economy result DTOs).
    // Public top-level class so tests can construct it for a fake
    // IBackendClient.
    public class PlayerProfile
    {
        public string   PlayerId;
        public string   DisplayName;
        public int      Level;
        public int      Xp;
        public string[] Badges;
        public int      TilesOwned;
    }
}
```

Replace it with:

```csharp
namespace SocialUniverse.Social
{
    // A player's public profile as returned by the "GetPlayerProfile" Cloud
    // Code function (field names match its lowercase JSON keys — the backend
    // deserializer is case-insensitive, same as the Economy result DTOs).
    // Public top-level class so tests can construct it for a fake
    // IBackendClient.
    public class PlayerProfile
    {
        public string   PlayerId;
        public string   DisplayName;
        public int      Level;
        public int      Xp;
        public string[] Badges;
        public int      TilesOwned;
        public bool     EmailVerified;
    }
}
```

- [ ] **Step 4: Verify compile**

```
mcp__UnityMCP__refresh_unity  (compile: "request", scope: "scripts")
mcp__UnityMCP__read_console   (types: ["error"], count: 20)
```

Expected: no new `error CS...` entries. (Remember: mirror `PlayerProfile.cs` into the main checkout first if the connected Unity Editor is bound there — see Global Constraints.)

- [ ] **Step 5: Commit**

```bash
git add ServerCode/GetPlayerProfile.js Assets/_Project/Scripts/Social/PlayerProfile.cs
git commit -m "Add EmailVerified to PlayerProfile and GetPlayerProfile"
```

---

### Task 3: Simplify IAuthService/AuthService/LocalMockAuthService signatures; revert RegisterAsync

**Files:**
- Modify: `Assets/_Project/Scripts/Core/IAuthService.cs`
- Modify: `Assets/_Project/Scripts/Net/AuthService.cs`
- Modify: `Assets/_Project/Scripts/Net/LocalMockAuthService.cs`

**Interfaces:**
- Consumes: Cloud Code functions from Task 1 (`RequestEmailVerificationCode` with no params, `ConfirmEmailVerificationCode` with `{ code }`).
- Produces: `Task RequestEmailVerificationCodeAsync()` and `Task ConfirmEmailVerificationCodeAsync(string code)` on `IAuthService` (dropping the `email` parameter). Task 7/8's `EmailVerificationModal` calls these by these exact names.

- [ ] **Step 1: Update `IAuthService.cs`**

The current interface has:

```csharp
        // Registration email verification: client sends email; Cloud Code handles OTP
        // generation/delivery/validation. Call ConfirmEmailVerificationCodeAsync
        // successfully before calling RegisterAsync — see AuthScreen.OnRegisterClicked.
        Task RequestEmailVerificationCodeAsync(string email);
        Task ConfirmEmailVerificationCodeAsync(string email, string code);
    }
}
```

Replace it with:

```csharp
        // Post-login email verification: the server reads the caller's own saved
        // email (player_profile.email) rather than trusting a client-supplied
        // address, since the caller is already an authenticated player by the
        // time this is called — see EmailVerificationModal.
        Task RequestEmailVerificationCodeAsync();
        Task ConfirmEmailVerificationCodeAsync(string code);
    }
}
```

- [ ] **Step 2: Update `AuthService.cs`**

The current implementation has:

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

Replace it with:

```csharp
        public async Task RequestEmailVerificationCodeAsync()
        {
            await _backend.CallAsync("RequestEmailVerificationCode");
            SULog.Info($"Email verification code requested (playerId: {PlayerId})", SULog.Channel.Net);
        }

        public async Task ConfirmEmailVerificationCodeAsync(string code)
        {
            await _backend.CallAsync("ConfirmEmailVerificationCode",
                new Dictionary<string, object> { { "code", code } });
            SULog.Info("Email verification code confirmed", SULog.Channel.Net);
        }
```

Then find `RegisterAsync` in the same file — it currently reads:

```csharp
        public async Task RegisterAsync(string username, string password, string email)
        {
            string loginKey = EmailLoginKey.Derive(email);

            // Uses AddUsernamePasswordAsync, not SignUpWithUsernamePasswordAsync: by
            // this point AuthScreen.EnsureSessionAsync has already established an
            // anonymous session (Cloud Code's RequestEmailVerificationCode/
            // ConfirmEmailVerificationCode require an authenticated player even
            // pre-registration, so the email-verification step signs one in first).
            // SignUp throws ClientInvalidUserState against an already-signed-in
            // caller; Add upgrades that same anonymous session into a full account
            // instead of creating a new, disconnected one.
            await AuthenticationService.Instance.AddUsernamePasswordAsync(loginKey, password);
            if (!string.IsNullOrEmpty(username))
                await AuthenticationService.Instance.UpdatePlayerNameAsync(username);
```

Replace it with (this reverts the `Add`→`SignUp` swap — email verification no longer happens before registration, so there's no anonymous pre-auth session to worry about):

```csharp
        public async Task RegisterAsync(string username, string password, string email)
        {
            string loginKey = EmailLoginKey.Derive(email);
            await AuthenticationService.Instance.SignUpWithUsernamePasswordAsync(loginKey, password);
            if (!string.IsNullOrEmpty(username))
                await AuthenticationService.Instance.UpdatePlayerNameAsync(username);
```

The rest of `RegisterAsync` (the `PlayerId` null check, the `SaveEmail` try/catch, the final log line) is unchanged.

- [ ] **Step 3: Update `LocalMockAuthService.cs`**

The current mock has:

```csharp
        private readonly HashSet<string>                 _pendingRegistrationCodes = new(); // normalized emails with an outstanding verification code (mock code: 123456)
```

Replace it with (no email keying needed — the mock only ever represents one signed-in player at a time):

```csharp
        private bool _pendingEmailVerificationCode; // an outstanding verification code exists (mock code: 123456)
```

Then find the two mock methods:

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

Replace them with:

```csharp
        // Always "succeeds" — mirrors RequestPasswordResetAsync's mock style.
        // Mock code is always "123456".
        public Task RequestEmailVerificationCodeAsync()
        {
            _pendingEmailVerificationCode = true;
            SULog.Info("[MOCK] Email verification code sent (mock code: 123456)", SULog.Channel.Net);
            return Task.CompletedTask;
        }

        public Task ConfirmEmailVerificationCodeAsync(string code)
        {
            if (!_pendingEmailVerificationCode)
                throw new InvalidOperationException("No verification code requested");
            if (code != "123456")
                throw new InvalidOperationException("Invalid verification code");
            _pendingEmailVerificationCode = false;
            SULog.Info("[MOCK] Email verified", SULog.Channel.Net);
            return Task.CompletedTask;
        }
```

- [ ] **Step 4: Verify compile**

```
mcp__UnityMCP__refresh_unity  (compile: "request", scope: "scripts")
mcp__UnityMCP__read_console   (types: ["error"], count: 30)
```

Expected: no new `error CS...` entries. This step will surface a compile error in `LocalMockAuthServiceTests.cs` (Task 4 hasn't updated it yet) — that's expected; confirm the error is specifically about the test file's outdated 2-arg call sites (e.g. `CS1501: No overload for method 'ConfirmEmailVerificationCodeAsync' takes 2 arguments`), not about `IAuthService.cs`/`AuthService.cs`/`LocalMockAuthService.cs` themselves.

- [ ] **Step 5: Commit**

```bash
git add Assets/_Project/Scripts/Core/IAuthService.cs Assets/_Project/Scripts/Net/AuthService.cs Assets/_Project/Scripts/Net/LocalMockAuthService.cs
git commit -m "Simplify email verification to no-email signatures; revert RegisterAsync to SignUp"
```

---

### Task 4: Update LocalMockAuthServiceTests.cs for the new signatures

**Files:**
- Modify: `Assets/_Project/Tests/EditMode/Net/LocalMockAuthServiceTests.cs`

**Interfaces:**
- Consumes: `LocalMockAuthService.RequestEmailVerificationCodeAsync()`/`ConfirmEmailVerificationCodeAsync(string code)` from Task 3.
- Produces: nothing consumed by later tasks — this is the terminal test coverage for the mock's verification logic.

This task fixes the compile error Task 3 intentionally left behind (the test file's old 2-arg call sites). It's a mechanical signature update, not new TDD — the behavior being tested doesn't change now, one verification code exists at a time per mock instance, so there's no need to keep the four separate test cases from testing per-email keys.

- [ ] **Step 1: Replace the full content of the test file**

```csharp
using System.Threading.Tasks;
using NUnit.Framework;
using SocialUniverse.Net;

namespace SocialUniverse.Tests
{
    // Exercises the post-login email-verification mock (the UGS-backed
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
            await _auth.RequestEmailVerificationCodeAsync();

            Assert.DoesNotThrowAsync(async () =>
                await _auth.ConfirmEmailVerificationCodeAsync("123456"));
        }

        [Test]
        public void Confirming_without_requesting_first_throws()
        {
            Assert.ThrowsAsync<System.InvalidOperationException>(async () =>
                await _auth.ConfirmEmailVerificationCodeAsync("123456"));
        }

        [Test]
        public async Task Confirming_with_wrong_code_throws()
        {
            await _auth.RequestEmailVerificationCodeAsync();

            Assert.ThrowsAsync<System.InvalidOperationException>(async () =>
                await _auth.ConfirmEmailVerificationCodeAsync("000000"));
        }

        [Test]
        public async Task Code_is_single_use()
        {
            await _auth.RequestEmailVerificationCodeAsync();
            await _auth.ConfirmEmailVerificationCodeAsync("123456");

            Assert.ThrowsAsync<System.InvalidOperationException>(async () =>
                await _auth.ConfirmEmailVerificationCodeAsync("123456"));
        }
    }
}
```

- [ ] **Step 2: Run the test file's tests**

```
mcp__UnityMCP__run_tests  (mode: "EditMode", assembly_names: ["SocialUniverse.Tests"], test_names: ["SocialUniverse.Tests.LocalMockAuthServiceTests"])
```

Expected: all 4 tests PASS (the compile error from Task 3 is now resolved, and the mock logic itself is unchanged in behavior — just re-keyed to a single flag instead of a per-email set).

- [ ] **Step 3: Run the full EditMode suite to confirm no regressions**

```
mcp__UnityMCP__run_tests  (mode: "EditMode")
```

Expected: 121/121 passing (same total as before this plan — this task doesn't add or remove test count, just updates 4 existing tests' call sites).

- [ ] **Step 4: Commit**

```bash
git add Assets/_Project/Tests/EditMode/Net/LocalMockAuthServiceTests.cs
git commit -m "Update LocalMockAuthServiceTests for no-email verification signatures"
```

---

### Task 5: Add IsEmailVerified to PlayerState

**Files:**
- Modify: `Assets/_Project/Scripts/Progression/PlayerState.cs`

**Interfaces:**
- Consumes: nothing from other tasks.
- Produces: `PlayerState.IsEmailVerified` (bool, default `false`), `PlayerState.SetEmailVerified(bool)`, `PlayerState.OnEmailVerifiedChanged` (`event Action<bool>`). Task 6 calls `SetEmailVerified`; Task 7's `EmailVerificationModal` reads `IsEmailVerified`.

- [ ] **Step 1: Add the new property, event, and setter**

The current file is:

```csharp
using System;

namespace SocialUniverse.Progression
{
    public class PlayerState
    {
        public string PlayerId    { get; set; } = "local_player";
        public string DisplayName { get; private set; } = "Player";
        public int    Level       { get; private set; } = 1;
        public int    XP          { get; private set; }
        public float  Fuel        { get; private set; } = 100f;
        public float  MaxFuel     { get; private set; } = 100f;
        public bool   IsTraveling       { get; private set; }
        public string TravelTargetId    { get; private set; }
        public long   TravelArrivalTsMs { get; private set; }

        public event Action<string> OnDisplayNameChanged;
        public event Action<int>    OnLevelChanged;
        public event Action<float>  OnFuelChanged;
        public event Action<float>  OnMaxFuelChanged;
        public event Action<bool, string, long> OnTravelStateChanged;

        public void SetDisplayName(string name)
        {
            DisplayName = name;
            OnDisplayNameChanged?.Invoke(name);
        }
```

Replace it with:

```csharp
using System;

namespace SocialUniverse.Progression
{
    public class PlayerState
    {
        public string PlayerId    { get; set; } = "local_player";
        public string DisplayName { get; private set; } = "Player";
        public bool   IsEmailVerified { get; private set; }
        public int    Level       { get; private set; } = 1;
        public int    XP          { get; private set; }
        public float  Fuel        { get; private set; } = 100f;
        public float  MaxFuel     { get; private set; } = 100f;
        public bool   IsTraveling       { get; private set; }
        public string TravelTargetId    { get; private set; }
        public long   TravelArrivalTsMs { get; private set; }

        public event Action<string> OnDisplayNameChanged;
        public event Action<bool>   OnEmailVerifiedChanged;
        public event Action<int>    OnLevelChanged;
        public event Action<float>  OnFuelChanged;
        public event Action<float>  OnMaxFuelChanged;
        public event Action<bool, string, long> OnTravelStateChanged;

        public void SetDisplayName(string name)
        {
            DisplayName = name;
            OnDisplayNameChanged?.Invoke(name);
        }

        public void SetEmailVerified(bool verified)
        {
            IsEmailVerified = verified;
            OnEmailVerifiedChanged?.Invoke(verified);
        }
```

(The rest of the file — `AddXP`, `SetLevel`, `SetFuel`, `SetMaxFuel`, `SetTravelState` — is unchanged.)

- [ ] **Step 2: Verify compile**

```
mcp__UnityMCP__refresh_unity  (compile: "request", scope: "scripts")
mcp__UnityMCP__read_console   (types: ["error"], count: 20)
```

Expected: no new `error CS...` entries.

- [ ] **Step 3: Commit**

```bash
git add Assets/_Project/Scripts/Progression/PlayerState.cs
git commit -m "Add IsEmailVerified to PlayerState"
```

---

### Task 6: Hydrate IsEmailVerified and publish the one-time verification prompt

**Files:**
- Create: `Assets/_Project/Scripts/Core/ShowEmailVerificationPromptEvent.cs`
- Modify: `Assets/_Project/Scripts/Core/SaveKeys.cs`
- Modify: `Assets/_Project/Scripts/App/PlanetSceneScope.cs`

**Interfaces:**
- Consumes: `PlayerProfile.EmailVerified` (Task 2), `PlayerState.SetEmailVerified` (Task 5).
- Produces: `ShowEmailVerificationPromptEvent` (empty struct), published via `EventBus.Publish`. Task 8's `HUDController` subscribes to this.

- [ ] **Step 1: Create the new event**

```csharp
namespace SocialUniverse.Core
{
    // Published by PlanetSceneScope the first time a signed-in player's profile
    // hydrates with emailVerified == false and they haven't been prompted before
    // (tracked locally — see SaveKeys.EmailVerificationPromptedKey). HUDController
    // subscribes to open EmailVerificationModal.
    public readonly struct ShowEmailVerificationPromptEvent { }
}
```

Save as `Assets/_Project/Scripts/Core/ShowEmailVerificationPromptEvent.cs`.

- [ ] **Step 2: Add a key-generator method to `SaveKeys.cs`**

The current file ends with:

```csharp
        // Returns the Cloud Save key for a planet's owned-tile list.
        public static string OwnedTilesKey(string planetId) => $"owned_tiles_{planetId.ToLowerInvariant()}";
    }
}
```

Replace it with:

```csharp
        // Returns the Cloud Save key for a planet's owned-tile list.
        public static string OwnedTilesKey(string planetId) => $"owned_tiles_{planetId.ToLowerInvariant()}";

        // Local-only (PlayerPrefs) flag: has this player already been shown the
        // one-time email-verification prompt on this device? Deliberately not
        // server-side — see PlanetSceneScope.HydrateServerStateAsync.
        public static string EmailVerificationPromptedKey(string playerId) => $"email_verification_prompted_{playerId}";
    }
}
```

- [ ] **Step 3: Extend `PlanetSceneScope.HydrateServerStateAsync`**

The current file has this block (inside `HydrateServerStateAsync`):

```csharp
            try
            {
                var profile = await _profileService.GetProfileAsync(_auth.PlayerId);
                if (profile != null && !string.IsNullOrEmpty(profile.DisplayName))
                    _playerState.SetDisplayName(profile.DisplayName);
            }
            catch (Exception ex)
            {
                SULog.Warn($"PlanetSceneBootstrapper: profile fetch failed ({ex.Message}), using auth id", SULog.Channel.Net);
            }
```

Replace it with:

```csharp
            try
            {
                var profile = await _profileService.GetProfileAsync(_auth.PlayerId);
                if (profile != null)
                {
                    if (!string.IsNullOrEmpty(profile.DisplayName))
                        _playerState.SetDisplayName(profile.DisplayName);

                    _playerState.SetEmailVerified(profile.EmailVerified);

                    if (!profile.EmailVerified)
                    {
                        string promptedKey = SaveKeys.EmailVerificationPromptedKey(_auth.PlayerId);
                        if (!PlayerPrefs.HasKey(promptedKey))
                        {
                            PlayerPrefs.SetInt(promptedKey, 1);
                            PlayerPrefs.Save();
                            EventBus.Publish(new ShowEmailVerificationPromptEvent());
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                SULog.Warn($"PlanetSceneBootstrapper: profile fetch failed ({ex.Message}), using auth id", SULog.Channel.Net);
            }
```

This file already has `using UnityEngine;` (for `PlayerPrefs`, confirmed by its existing `PlayerPrefs.HasKey`/`GetString` calls a few lines below this block for `SaveKeys.TravelTargetId`/`SaveKeys.LastPlanetId`) and already references `SocialUniverse.Core` types (`EventBus`, `SaveKeys`) elsewhere in the file, so no new `using` statements are needed.

- [ ] **Step 4: Verify compile**

```
mcp__UnityMCP__refresh_unity  (compile: "request", scope: "scripts")
mcp__UnityMCP__read_console   (types: ["error"], count: 20)
```

Expected: no new `error CS...` entries.

- [ ] **Step 5: Commit**

```bash
git add Assets/_Project/Scripts/Core/ShowEmailVerificationPromptEvent.cs Assets/_Project/Scripts/Core/SaveKeys.cs Assets/_Project/Scripts/App/PlanetSceneScope.cs
git commit -m "Hydrate IsEmailVerified and publish a one-time verification prompt"
```

---

### Task 7: Create EmailVerificationModal

**Files:**
- Create: `Assets/_Project/Scripts/UI/EmailVerificationModal.cs`

**Interfaces:**
- Consumes: `IAuthService.RequestEmailVerificationCodeAsync()`/`ConfirmEmailVerificationCodeAsync(string code)` (Task 3), `PlayerState.IsEmailVerified`/`SetEmailVerified` (Task 5).
- Produces: `EmailVerificationModal.Open()`/`Close()` (public methods), and serialized fields `_codeInput` (`TMP_InputField`), `_sendCodeButton`/`_verifyButton`/`_closeButton` (`Button`), `_statusText` (`TMP_Text`) that Task 9's scene-wiring task assigns in the Inspector.

- [ ] **Step 1: Write the new file**

```csharp
using System;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using TMPro;
using SocialUniverse.Core;
using SocialUniverse.Progression;

namespace SocialUniverse.UI
{
    // Pop-up modal for post-login email verification. Auto-opened once by
    // HUDController (see ShowEmailVerificationPromptEvent) after first login if
    // the player hasn't verified yet; also reachable any time via the HUD's
    // verify-email button. Mirrors DisplayNameModal's structure.
    public class EmailVerificationModal : MonoBehaviour
    {
        [SerializeField] private TMP_InputField _codeInput;
        [SerializeField] private Button         _sendCodeButton;
        [SerializeField] private Button         _verifyButton;
        [SerializeField] private Button         _closeButton;
        [SerializeField] private TMP_Text       _statusText;

        [Inject] private IAuthService _auth;
        [Inject] private PlayerState  _playerState;

        private void Awake()
        {
            _sendCodeButton.onClick.AddListener(OnSendCodeClicked);
            _verifyButton  .onClick.AddListener(OnVerifyClicked);
            _closeButton   .onClick.AddListener(Close);
            gameObject.SetActive(false);
        }

        public void Open()
        {
            _codeInput.text  = "";
            _statusText.text = "";
            bool verified = _playerState.IsEmailVerified;
            _sendCodeButton.gameObject.SetActive(!verified);
            _verifyButton  .gameObject.SetActive(!verified);
            _codeInput     .gameObject.SetActive(!verified);
            _statusText.text = verified ? "Your email is verified." : "";
            gameObject.SetActive(true);
        }

        public void Close() => gameObject.SetActive(false);

        private async void OnSendCodeClicked()
        {
            SetBusy(true);
            _statusText.text = "Sending verification code…";
            try
            {
                await _auth.RequestEmailVerificationCodeAsync();
                _statusText.text = "Verification code sent — check your email (mock code: 123456)";
            }
            catch (Exception ex)
            {
                _statusText.text = FriendlyError(ex);
            }
            finally
            {
                SetBusy(false);
            }
        }

        private async void OnVerifyClicked()
        {
            string code = _codeInput.text.Trim();
            if (string.IsNullOrWhiteSpace(code))
            {
                _statusText.text = "Enter the verification code sent to your email";
                return;
            }

            SetBusy(true);
            _statusText.text = "Verifying…";
            try
            {
                await _auth.ConfirmEmailVerificationCodeAsync(code);
                _playerState.SetEmailVerified(true);
                _statusText.text = "Your email is verified.";
                _sendCodeButton.gameObject.SetActive(false);
                _verifyButton  .gameObject.SetActive(false);
                _codeInput     .gameObject.SetActive(false);
            }
            catch (Exception ex)
            {
                _statusText.text = FriendlyError(ex);
            }
            finally
            {
                SetBusy(false);
            }
        }

        private void SetBusy(bool busy)
        {
            _sendCodeButton.interactable = !busy;
            _verifyButton  .interactable = !busy;
        }

        private static string FriendlyError(Exception ex)
        {
            string msg = ex.Message;
            if (msg.Contains("No email on file"))
                return "No email is on file for this account — contact support";
            if (msg.Contains("Please wait a moment"))
                return "Please wait a moment before requesting another code";
            if (msg.Contains("No verification code"))
                return "No verification code was sent — click Send Code first";
            if (msg.Contains("Verification code has expired"))
                return "Verification code expired — click Send Code to get a new one";
            if (msg.Contains("Invalid verification code"))
                return "Incorrect verification code — check your email and try again";
            if (msg.Contains("network") || msg.Contains("Network") || msg.Contains("unreachable"))
                return "Network error — check your connection";
            return msg;
        }
    }
}
```

- [ ] **Step 2: Verify compile**

```
mcp__UnityMCP__refresh_unity  (compile: "request", scope: "scripts")
mcp__UnityMCP__read_console   (types: ["error"], count: 20)
```

Expected: no new `error CS...` entries.

- [ ] **Step 3: Commit**

```bash
git add Assets/_Project/Scripts/UI/EmailVerificationModal.cs
git commit -m "Add EmailVerificationModal"
```

---

### Task 8: Wire EmailVerificationModal into HUDController

**Files:**
- Modify: `Assets/_Project/Scripts/UI/HUDController.cs`

**Interfaces:**
- Consumes: `EmailVerificationModal` (Task 7), `ShowEmailVerificationPromptEvent` (Task 6).
- Produces: two new serialized fields (`_emailVerificationModal`, `_verifyEmailButton`) that Task 10's scene-wiring task assigns in the Inspector.

- [ ] **Step 1: Add serialized fields**

The current file has:

```csharp
        [SerializeField] private TMP_Text _usernameText;
        [SerializeField] private Button _usernameButton;
        [SerializeField] private DisplayNameModal _displayNameModal;
```

Replace it with:

```csharp
        [SerializeField] private TMP_Text _usernameText;
        [SerializeField] private Button _usernameButton;
        [SerializeField] private DisplayNameModal _displayNameModal;
        [SerializeField] private EmailVerificationModal _emailVerificationModal;
        [SerializeField] private Button _verifyEmailButton;
```

- [ ] **Step 2: Wire the button and event subscription in `Start()`**

The current `Start()` has:

```csharp
            _currency.Bind(_wallet);
            _chatButton.onClick.AddListener(_socialPanel.Open);
            _usernameButton?.onClick.AddListener(OnUsernameClicked);
            _launchButton?.onClick.AddListener(() => EventBus.Publish(new LaunchRequestedEvent()));
```

Replace it with:

```csharp
            _currency.Bind(_wallet);
            _chatButton.onClick.AddListener(_socialPanel.Open);
            _usernameButton?.onClick.AddListener(OnUsernameClicked);
            _launchButton?.onClick.AddListener(() => EventBus.Publish(new LaunchRequestedEvent()));
            if (_verifyEmailButton != null) _verifyEmailButton.onClick.AddListener(() => _emailVerificationModal?.Open());
            EventBus.Subscribe<ShowEmailVerificationPromptEvent>(OnShowEmailVerificationPrompt);
```

- [ ] **Step 3: Add the handler and unsubscribe in `OnDestroy()`**

The current `OnDestroy()` has:

```csharp
        private void OnDestroy()
        {
            _playerState.OnLevelChanged       -= SetLevel;
            _playerState.OnFuelChanged        -= SetFuel;
            _playerState.OnDisplayNameChanged -= SetUsername;
            _presence.PresenceChanged         -= RefreshExplorerCount;
        }
```

Replace it with:

```csharp
        private void OnDestroy()
        {
            _playerState.OnLevelChanged       -= SetLevel;
            _playerState.OnFuelChanged        -= SetFuel;
            _playerState.OnDisplayNameChanged -= SetUsername;
            _presence.PresenceChanged         -= RefreshExplorerCount;
            EventBus.Unsubscribe<ShowEmailVerificationPromptEvent>(OnShowEmailVerificationPrompt);
        }

        private void OnShowEmailVerificationPrompt(ShowEmailVerificationPromptEvent _)
        {
            _emailVerificationModal?.Open();
        }
```

- [ ] **Step 4: Verify compile**

```
mcp__UnityMCP__refresh_unity  (compile: "request", scope: "scripts")
mcp__UnityMCP__read_console   (types: ["error"], count: 20)
```

Expected: no new `error CS...` entries.

- [ ] **Step 5: Commit**

```bash
git add Assets/_Project/Scripts/UI/HUDController.cs
git commit -m "Wire EmailVerificationModal into HUDController"
```

---

### Task 9: Remove the pre-registration verification UI/logic from AuthScreen

**Files:**
- Modify: `Assets/_Project/Scripts/UI/AuthScreen.cs`

**Interfaces:**
- Consumes: nothing new.
- Produces: `OnRegisterClicked` reverts to calling `RegisterAsync` directly (no `ConfirmEmailVerificationCodeAsync` step, no `EnsureSessionAsync` call — registration is a genuinely signed-out → signed-up transition again).

- [ ] **Step 1: Remove the `_regCodeField`/`_sendVerificationCodeButton` serialized fields**

The current Register-panel field group is:

```csharp
        [SerializeField] private InputField _regConfirmField;
        [SerializeField] private InputField _regCodeField;
        [SerializeField] private Text       _regStatusText;
        [SerializeField] private Button     _registerButton;
        [SerializeField] private Button     _sendVerificationCodeButton;
        [SerializeField] private Button     _goToLoginButton;
```

Replace it with:

```csharp
        [SerializeField] private InputField _regConfirmField;
        [SerializeField] private Text       _regStatusText;
        [SerializeField] private Button     _registerButton;
        [SerializeField] private Button     _goToLoginButton;
```

- [ ] **Step 2: Remove the button wiring in `Start()`**

The current `Start()` has:

```csharp
            _registerButton    .onClick.AddListener(OnRegisterClicked);
            if (_sendVerificationCodeButton != null) _sendVerificationCodeButton.onClick.AddListener(OnSendVerificationCodeClicked);
            _goToLoginButton   .onClick.AddListener(() => ShowPanel(AuthPanel.Login));
```

Replace it with:

```csharp
            _registerButton    .onClick.AddListener(OnRegisterClicked);
            _goToLoginButton   .onClick.AddListener(() => ShowPanel(AuthPanel.Login));
```

- [ ] **Step 3: Remove `OnSendVerificationCodeClicked` and simplify `OnRegisterClicked`**

The current file has (in order) `OnSendVerificationCodeClicked` followed by `OnRegisterClicked`:

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
                await EnsureSessionAsync();
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
                await EnsureSessionAsync();
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

Replace both methods with a single `OnRegisterClicked`:

```csharp
        private async void OnRegisterClicked()
        {
            string username = _regUsernameField.text.Trim();
            string email     = _regEmailField.text.Trim();
            string password  = _regPasswordField.text;
            string confirm   = _regConfirmField.text;

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
            _regStatusText.text = "Creating account…";
            try   { await _auth.RegisterAsync(username, password, email); }
            catch (Exception ex) { _regStatusText.text = FriendlyError(ex); SetBusy(false); }
        }
```

- [ ] **Step 4: Remove the now-dead verification-code checks from `FriendlyError`**

The current `FriendlyError` has:

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

Replace it with (the verification-code checks moved to `EmailVerificationModal.FriendlyError` in Task 7 — `AuthScreen` no longer calls any verification-code method):

```csharp
            if (msg.Contains("No password reset") || msg.Contains("No reset"))
                return "No reset was requested for this email — click Send Reset Code first";
            if (msg.Contains("Invalid reset code"))
                return "Incorrect reset code — check your email and try again";
            if (msg.Contains("network") || msg.Contains("Network") || msg.Contains("unreachable"))
                return "Network error — check your connection";
```

Note: `EnsureSessionAsync`, `_suppressAutoTransition`, and its use in `OnSendResetCodeClicked`/`OnResetPasswordClicked` are **not** touched by this task — they remain exactly as they are, since the Forgot Password flow still needs them independently.

- [ ] **Step 5: Verify compile**

```
mcp__UnityMCP__refresh_unity  (compile: "request", scope: "scripts")
mcp__UnityMCP__read_console   (types: ["error"], count: 20)
```

Expected: no new `error CS...` entries.

- [ ] **Step 6: Commit**

```bash
git add Assets/_Project/Scripts/UI/AuthScreen.cs
git commit -m "Remove pre-registration email verification from AuthScreen"
```

---

### Task 10: Clean up Auth.unity (remove the old code field/Send-Code button)

**Files:**
- Modify: `Assets/Scenes/Auth.unity` (scene asset — edited live via UnityMCP, not by hand-editing YAML)

**Interfaces:**
- Consumes: Task 9's `AuthScreen.cs` (no longer has `_regCodeField`/`_sendVerificationCodeButton` fields to reference).
- Produces: a Register panel with no dangling references and no leftover verification UI.

- [ ] **Step 1: Inspect the current Register panel**

```
mcp__UnityMCP__find_gameobjects  (search for "RegCodeField" and "SendVerificationCodeButton" by name, under the Register panel — these were added in the prior plan's Task 6)
```

- [ ] **Step 2: Delete both GameObjects**

```
mcp__UnityMCP__manage_gameobject  (action: "delete" on the "RegCodeField" GameObject)
mcp__UnityMCP__manage_gameobject  (action: "delete" on the "SendVerificationCodeButton" GameObject)
```

- [ ] **Step 3: Restore the register button's label**

Find the `RegisterButton` GameObject's label text (child `Text (TMP)`), currently reading "Verify & Create Account". Set it back to "Create Account" via `mcp__UnityMCP__manage_components` (action: "set_property", targeting the label's `TMP_Text`/`Text` component, property `text`, value `"Create Account"`).

- [ ] **Step 4: Save the scene**

```
mcp__UnityMCP__manage_scene  (action: "save")
```

- [ ] **Step 5: Verify in the console**

```
mcp__UnityMCP__read_console  (types: ["error", "warning"], count: 20)
```

Expected: no new errors (e.g. no `MissingReferenceException` — there shouldn't be any, since Task 9 already removed the C# fields these GameObjects were wired to, so Unity will simply have already-nulled those particular serialized references before this task even runs).

- [ ] **Step 6: Copy the scene back into the worktree and commit**

```bash
cp "C:\Users\chris\UnityProjects\social-universe\Assets\Scenes\Auth.unity" "C:\Users\chris\UnityProjects\social-universe\.claude\worktrees\buzzing-floating-wall\Assets\Scenes\Auth.unity"
```

```bash
git add Assets/Scenes/Auth.unity
git commit -m "Remove pre-registration email verification UI from Auth.unity"
```

---

### Task 11: Wire EmailVerificationModal and the verify-email button into the Planet HUD scene

**Files:**
- Modify: `Assets/Scenes/Planet.unity` (scene asset — edited live via UnityMCP)

**Interfaces:**
- Consumes: `HUDController._emailVerificationModal`/`_verifyEmailButton` (Task 8), `EmailVerificationModal`'s serialized fields (Task 7).
- Produces: a new modal GameObject in the Planet scene's HUD canvas, wired to `HUDController`, plus a visible "Verify Email" button.

This task is inherently interactive (exact GameObject instance IDs only exist once the scene is inspected live) — the steps below are the concrete recipe to follow.

- [ ] **Step 1: Inspect the HUD's existing modal pattern**

```
mcp__UnityMCP__find_gameobjects  (search for the existing "DisplayNameModal" GameObject in Planet.unity's HUD canvas — its structure/style is the template for the new modal)
```

Read its full hierarchy (via the `mcpforunity://scene/gameobject/{id}` resource) to see its panel background, input field, buttons, and status text layout.

- [ ] **Step 2: Duplicate the DisplayNameModal GameObject**

```
mcp__UnityMCP__manage_gameobject  (action: "duplicate" on the DisplayNameModal GameObject)
```

Rename the duplicate to `EmailVerificationModal`. It will have a `DisplayNameModal` component attached (since it was duplicated from that GameObject) — remove that component (`mcp__UnityMCP__manage_components`, action: "remove", component_type: "DisplayNameModal") and add an `EmailVerificationModal` component instead (action: "add", component_type: "EmailVerificationModal").

- [ ] **Step 3: Adjust the duplicated hierarchy's child elements**

The duplicated modal will have DisplayNameModal's child structure (one input field, confirm/cancel buttons, status text). Add a second button (duplicate one of the existing buttons) so the modal has three buttons total (Send Code, Verify, Close) instead of two — relabel all three and the existing input field/status text appropriately: input field placeholder "Verification code", buttons labeled "Send Code" / "Verify" / "Close".

- [ ] **Step 4: Wire the `EmailVerificationModal` component's serialized fields**

Assign (via `mcp__UnityMCP__manage_components`, action: "set_property"):
- `_codeInput` → the input field's `TMP_InputField` component
- `_sendCodeButton` → the "Send Code" button's `Button` component
- `_verifyButton` → the "Verify" button's `Button` component
- `_closeButton` → the "Close" button's `Button` component
- `_statusText` → the status text's `TMP_Text` component

- [ ] **Step 5: Add a "Verify Email" button to the HUD**

Duplicate an existing simple HUD button (e.g. the username button) to create a new `VerifyEmailButton`, label it "Verify Email", and position it somewhere sensible in the HUD (near the username display).

- [ ] **Step 6: Wire `HUDController`'s new serialized fields**

Assign:
- `_emailVerificationModal` → the `EmailVerificationModal` component from Step 2
- `_verifyEmailButton` → the `VerifyEmailButton` GameObject's `Button` component from Step 5

- [ ] **Step 7: Set the modal inactive by default**

Confirm the `EmailVerificationModal` GameObject's active state is `false` by default in the scene (matching `DisplayNameModal`'s convention — `Awake()` also calls `gameObject.SetActive(false)` defensively, but the scene should start with it hidden too).

- [ ] **Step 8: Save the scene**

```
mcp__UnityMCP__manage_scene  (action: "save")
```

- [ ] **Step 9: Verify in the console**

```
mcp__UnityMCP__read_console  (types: ["error", "warning"], count: 20)
```

Expected: no new errors.

- [ ] **Step 10: Copy the scene back into the worktree and commit**

```bash
cp "C:\Users\chris\UnityProjects\social-universe\Assets\Scenes\Planet.unity" "C:\Users\chris\UnityProjects\social-universe\.claude\worktrees\buzzing-floating-wall\Assets\Scenes\Planet.unity"
```

```bash
git add Assets/Scenes/Planet.unity
git commit -m "Wire EmailVerificationModal and verify-email button into Planet.unity"
```

---

### Task 12: Manual end-to-end verification

**Files:** none (verification only)

- [ ] **Step 1: Enter Play Mode with `LocalMockAuthService` active.**

- [ ] **Step 2: Golden path — registration** — Register a new account (username/email/password/confirm, no code field should exist anymore). Confirm the account is created immediately and the player lands in the Planet scene.

- [ ] **Step 3: First-login prompt (real backend only — see note)** — Confirm `EmailVerificationModal` opens automatically once, right after landing (since `emailVerified` is false and this is the first hydration for this player).

  **Note found during final review:** `PlanetSceneScope.HydrateServerStateAsync` fetches the profile via `IBackendClient.CallAsync<PlayerProfile>("GetPlayerProfile", ...)`. In dev/mock mode, `LocalMockBackendClient.CallAsync<T>` returns `default(T)` — `null` for `PlayerProfile` — since it doesn't know about specific Cloud Code functions. The hydration block is gated on `if (profile != null)`, so in mock mode the profile fetch always "misses," `SetEmailVerified`/the prompt-publish logic never run, and **the auto-prompt cannot be exercised against the mock backend at all**. This is inherited mock-backend behavior (display-name hydration has the same gap, but falls back to the auth username instead), not something this plan can fix without changing shared mock infrastructure used by other features — out of scope here. Steps 3–5 can only be verified against Step 9's real-backend pass. Do not sign off Steps 3–5 as "passed" based on mock-mode testing.

- [ ] **Step 4: Skip (real backend only)** — Click "Close" without verifying. Confirm the modal closes and the player can play normally.

- [ ] **Step 5: No re-prompt (real backend only)** — Reload/re-enter the Planet scene (or restart Play Mode with the same session) and confirm the modal does **not** reopen automatically a second time.

- [ ] **Step 6: Manual verification from the HUD (works in mock mode)** — Click the new "Verify Email" button. This does NOT depend on the profile-hydration gap above — `PlayerState.IsEmailVerified` defaults to `false` either way, and `EmailVerificationModal.OnVerifyClicked` sets it directly via `_playerState.SetEmailVerified(true)` without needing a fresh profile fetch. Confirm the modal opens with the send/verify flow available. Click "Send Code", confirm the mock status message appears, enter `123456`, click "Verify". Confirm the status changes to "Your email is verified." and the send/verify UI hides.

- [ ] **Step 7: Already-verified state** — Close and reopen the modal (via the HUD button). Confirm it now shows "Your email is verified." immediately, with no send/verify UI.

- [ ] **Step 8: Forgot Password regression check** — From the Login panel, use Forgot Password (Send Reset Code, then Reset Password) and confirm this still works — it should be completely unaffected by this plan's changes (Task 9 explicitly does not touch `EnsureSessionAsync`/the Forgot Password handlers).

- [ ] **Step 9 (requires a live UGS project + deployed Cloud Code + configured secrets):** repeat Steps 2–7 against the real backend. Deploy the two rewritten Cloud Code functions and confirm `RESEND_API_KEY`/`RESET_FROM_EMAIL` are configured in the project's Cloud Code secrets (a prerequisite already identified as blocking testing of the prior design — this plan doesn't change that requirement, just where in the flow it's exercised).

- [ ] **Step 10: Record results** — Note the pass/fail outcome of Steps 2–9 in this session's follow-up.
