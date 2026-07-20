# Google Sign-In First-Time Name Panel + Device Restore Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship a mandatory "choose your display name" panel that appears the first time a player signs in with Google (returning players skip it), and restore real on-device Google Sign-In so the button actually works on Android hardware instead of always falling back to a mock token.

**Architecture:** `AuthScreen` gains a sixth panel (`ChooseName`) in its existing panel-switch pattern, gated by the same `_suppressAutoTransition` mechanism the verify-email flow already uses — `OnGoogleClicked` checks `IAuthService.DisplayName` immediately after sign-in and either shows the panel (empty name) or advances straight into the game (name already set). A `DisplayNameValidator` pure static helper enforces the 2–20 char, no-spaces rule. Separately, the Google Sign-In Unity plugin (deleted from `main` in commit `309f83ef` for lack of an asmdef) is recovered from git history, given its own asmdef, referenced from `SocialUniverse.Net`, and wired behind a new `GoogleAuthConfig` ScriptableObject holding the OAuth Web Client ID.

**Tech Stack:** Unity 6 (legacy UGUI in the Auth scene — `InputField`/`Text`/`Button`, not TMP), UGS Authentication SDK, VContainer, NUnit EditMode tests, Unity MCP for scene edits/asset creation/test runs, Google Sign-In Unity Plugin v1.0.4 (recovered from git history).

**Spec:** `docs/superpowers/specs/2026-07-17-google-signin-display-name-design.md`

## Global Constraints

- Namespaces mirror folders: `SocialUniverse.Core`, `SocialUniverse.Net`, `SocialUniverse.UI`, `SocialUniverse.Config`, `SocialUniverse.App`. One public type per file, file named after the type.
- Gameplay code depends on `I*Service` abstractions only. `GoogleAuthHandler` stays a plain static utility outside DI (it holds no state a service needs — only a configured Web Client ID), matching its existing shape.
- Auth scene UI uses **legacy UGUI** (`InputField`, `Text`, `Button` from `UnityEngine.UI`) — not TMP, despite what the design spec's original wording said; `DisplayNameModal` (Planet scene) is the only TMP-based name UI in this codebase and is out of scope here.
- Tunable/config values live in `*Config` ScriptableObjects under `Assets/_Project/ScriptableObjects/` (rule already followed by `SocialConfig`/`AudioConfig` — `GoogleAuthConfig` follows the same pattern: a `[SerializeField]` field on `RootLifetimeScope`, not a DI-container registration, since nothing else needs to resolve it).
- Tests: EditMode suite must stay green. Run via Unity MCP `run_tests` (testMode: EditMode); CLI fallback: `"C:\Program Files\Unity\Hub\Editor\<version>\Editor\Unity.exe" -runTests -projectPath . -testResults results.xml -testPlatform EditMode`.
- After any script change, use Unity MCP `read_console` to confirm zero compile errors before proceeding.
- Unity MCP notes (from project memory): `execute_code` is unusable (no Roslyn); if the bridge goes unresponsive it self-recovers — wait and retry, don't restart Unity. Use a temp `MenuItem` script for anything `execute_code` would normally do.
- OAuth credentials do not exist yet. `GoogleAuthConfig.WebClientId` ships with a placeholder value (`YOUR_WEB_CLIENT_ID.apps.googleusercontent.com`) and `GoogleAuthHandler` throws a clear error if it's still the placeholder when the Android path is invoked. Real device sign-in stays blocked until the user completes the checklist in Task 6.
- Commit messages end with `Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>`.

---

### Task 1: `DisplayNameValidator` pure static helper

**Files:**
- Create: `Assets/_Project/Scripts/UI/DisplayNameValidator.cs`
- Test: `Assets/_Project/Tests/EditMode/UI/DisplayNameValidatorTests.cs`

**Interfaces:**
- Produces (Task 3 relies on this exact signature): `SocialUniverse.UI.DisplayNameValidator.Validate(string name, out string error) : bool`, plus public constants `MinLength = 2`, `MaxLength = 20`.

- [ ] **Step 1: Write the failing tests**

Create `Assets/_Project/Tests/EditMode/UI/DisplayNameValidatorTests.cs`:

```csharp
using NUnit.Framework;
using SocialUniverse.UI;

namespace SocialUniverse.Tests
{
    public class DisplayNameValidatorTests
    {
        [Test]
        public void Empty_name_is_rejected()
        {
            Assert.IsFalse(DisplayNameValidator.Validate("", out string error));
            StringAssert.Contains("at least", error);
        }

        [Test]
        public void Whitespace_only_name_is_rejected()
        {
            Assert.IsFalse(DisplayNameValidator.Validate("   ", out string error));
            StringAssert.Contains("at least", error);
        }

        [Test]
        public void Single_character_name_is_rejected()
        {
            Assert.IsFalse(DisplayNameValidator.Validate("A", out _));
        }

        [Test]
        public void Two_character_name_is_accepted()
        {
            Assert.IsTrue(DisplayNameValidator.Validate("Al", out string error));
            Assert.IsNull(error);
        }

        [Test]
        public void Twenty_character_name_is_accepted()
        {
            string name = new string('A', 20);
            Assert.IsTrue(DisplayNameValidator.Validate(name, out _));
        }

        [Test]
        public void Twenty_one_character_name_is_rejected()
        {
            string name = new string('A', 21);
            Assert.IsFalse(DisplayNameValidator.Validate(name, out string error));
            StringAssert.Contains("20 characters", error);
        }

        [Test]
        public void Name_with_a_space_is_rejected()
        {
            Assert.IsFalse(DisplayNameValidator.Validate("Star Fox", out string error));
            StringAssert.Contains("spaces", error);
        }

        [Test]
        public void Leading_and_trailing_whitespace_is_trimmed_before_validating()
        {
            Assert.IsTrue(DisplayNameValidator.Validate("  Nova  ", out string error));
            Assert.IsNull(error);
        }
    }
}
```

- [ ] **Step 2: Verify the tests fail**

Unity MCP `read_console` after script reload. Expected: compile error — `The type or namespace name 'DisplayNameValidator' could not be found`.

- [ ] **Step 3: Implement `DisplayNameValidator`**

Create `Assets/_Project/Scripts/UI/DisplayNameValidator.cs`:

```csharp
namespace SocialUniverse.UI
{
    // Pure validation for the Google Sign-In first-time display-name panel
    // (AuthScreen.ChooseName). Kept separate from AuthScreen's private
    // ValidateUsername (email registration) and DisplayNameModal's
    // SocialConfig-driven validation (in-game rename) so each flow's rules
    // can evolve independently — see
    // docs/superpowers/specs/2026-07-17-google-signin-display-name-design.md.
    public static class DisplayNameValidator
    {
        public const int MinLength = 2;
        public const int MaxLength = 20;

        // Trims before checking length/spaces; callers should use the same
        // trimmed value when committing the name.
        public static bool Validate(string name, out string error)
        {
            string trimmed = (name ?? string.Empty).Trim();

            if (trimmed.Length < MinLength)
            {
                error = $"Name must be at least {MinLength} characters";
                return false;
            }
            if (trimmed.Length > MaxLength)
            {
                error = $"Name must be {MaxLength} characters or fewer";
                return false;
            }
            if (trimmed.Contains(' '))
            {
                error = "Name cannot contain spaces";
                return false;
            }
            error = null;
            return true;
        }
    }
}
```

- [ ] **Step 4: Run the tests and verify they pass**

Unity MCP `run_tests` (EditMode, filter `DisplayNameValidatorTests`). Expected: 8/8 pass.

- [ ] **Step 5: Commit**

```bash
git add Assets/_Project/Scripts/UI/DisplayNameValidator.cs Assets/_Project/Scripts/UI/DisplayNameValidator.cs.meta Assets/_Project/Tests/EditMode/UI/DisplayNameValidatorTests.cs Assets/_Project/Tests/EditMode/UI/DisplayNameValidatorTests.cs.meta
git commit -m "$(cat <<'EOF'
ui: add DisplayNameValidator for the Google sign-in name panel

Pure static helper (2-20 chars, no spaces) so the upcoming choose-name
panel can validate without depending on SocialConfig, which isn't
DI-registered in the standalone Auth scene.

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 2: `LocalMockAuthService` — deterministic per-provider mock identity

**Files:**
- Modify: `Assets/_Project/Scripts/Net/LocalMockAuthService.cs`
- Test: `Assets/_Project/Tests/EditMode/Net/LocalMockAuthServiceTests.cs`

**Interfaces:**
- Consumes: existing `IAuthService` members (`SignInWithGoogleAsync`, `SignInWithAppleAsync`, `UpdateDisplayNameAsync`, `SignOutAsync`, `DisplayName`).
- Produces: no new public members — only changes `MockSsoSignInAsync`'s internal `_playerId` scheme and `UpdateDisplayNameAsync`'s bookkeeping, both already part of `IAuthService`. Task 3/8's manual Play Mode smoke test relies on this behavior: first mock Google/Apple sign-in yields an empty `DisplayName`; after `UpdateDisplayNameAsync` + sign-out, a repeat sign-in with the same mock provider recalls the chosen name.

Today `MockSsoSignInAsync` assigns `_playerId = provider + "_" + Random.Range(...)` — a fresh random identity every call, so the mock can never model "the same Google account signing in twice." Fix: make the identity deterministic per provider and remember the chosen name against it in-memory (cleared per test/app-session, same as the rest of this mock's state).

- [ ] **Step 1: Write the failing tests**

Append inside the existing `LocalMockAuthServiceTests` class (after `Restored_session_remembers_it_was_anonymous`, before the closing brace):

```csharp
        [Test]
        public async Task First_google_sign_in_has_no_display_name()
        {
            await _auth.SignInWithGoogleAsync("token");
            Assert.IsNull(_auth.DisplayName);
        }

        [Test]
        public async Task Choosing_a_name_then_signing_back_in_with_google_recalls_it()
        {
            await _auth.SignInWithGoogleAsync("token");
            await _auth.UpdateDisplayNameAsync("Nova");
            await _auth.SignOutAsync();

            await _auth.SignInWithGoogleAsync("token");
            Assert.AreEqual("Nova", _auth.DisplayName);
        }

        [Test]
        public async Task Google_and_apple_mock_identities_are_independent()
        {
            await _auth.SignInWithGoogleAsync("token");
            await _auth.UpdateDisplayNameAsync("Nova");
            await _auth.SignOutAsync();

            await _auth.SignInWithAppleAsync("token");
            Assert.IsNull(_auth.DisplayName);
        }
```

- [ ] **Step 2: Verify the tests fail**

Unity MCP `run_tests` (EditMode, filter `LocalMockAuthServiceTests`). Expected: `First_google_sign_in_has_no_display_name` passes by accident (fresh instance), but `Choosing_a_name_then_signing_back_in_with_google_recalls_it` FAILS — `_auth.DisplayName` is `null`/empty instead of `"Nova"`, because today's random `_playerId` means the second sign-in never looks up the name chosen on the first.

- [ ] **Step 3: Add the per-identity name lookup**

In `Assets/_Project/Scripts/Net/LocalMockAuthService.cs`, add a field next to `_pendingResets`:

```csharp
        // Keyed by mock playerId ("mock_google"/"mock_apple"). Lets a repeat
        // mock SSO sign-in within this run recall a previously chosen name,
        // mirroring a real OAuth provider always resolving to the same
        // linked UGS account.
        private readonly Dictionary<string, string> _ssoDisplayNames = new();
```

- [ ] **Step 4: Update `UpdateDisplayNameAsync` to remember the name per identity**

Replace:

```csharp
        public Task UpdateDisplayNameAsync(string displayName)
        {
            _displayName = displayName;
            PersistSession();
            SULog.Info($"[MOCK] Display name updated to '{displayName}'", SULog.Channel.Net);
            return Task.CompletedTask;
        }
```

with:

```csharp
        public Task UpdateDisplayNameAsync(string displayName)
        {
            _displayName = displayName;
            if (!string.IsNullOrEmpty(_playerId))
                _ssoDisplayNames[_playerId] = displayName;
            PersistSession();
            SULog.Info($"[MOCK] Display name updated to '{displayName}'", SULog.Channel.Net);
            return Task.CompletedTask;
        }
```

- [ ] **Step 5: Make `MockSsoSignInAsync`'s identity deterministic**

Replace:

```csharp
        private async Task MockSsoSignInAsync(string provider)
        {
            // Mirror UGS: SSO sign-in throws over any live session (anonymous or
            // not) — same "already signed in" contract as SignInWithEmailAsync.
            if (_isSignedIn)
                throw new InvalidOperationException("A player is already signed in — sign out before signing in again.");
            await Task.Delay(900);
            _playerId    = provider + "_" + UnityEngine.Random.Range(10000, 99999);
            _isAnonymous = false;
            _isSignedIn  = true;
            PersistSession();
            SULog.Info($"[MOCK] Signed in with {provider} ({_playerId})", SULog.Channel.Net);
            OnSignedIn?.Invoke();
        }
```

with:

```csharp
        private async Task MockSsoSignInAsync(string provider)
        {
            // Mirror UGS: SSO sign-in throws over any live session (anonymous or
            // not) — same "already signed in" contract as SignInWithEmailAsync.
            if (_isSignedIn)
                throw new InvalidOperationException("A player is already signed in — sign out before signing in again.");
            await Task.Delay(900);
            // Deterministic per-provider identity (not random): a real OAuth
            // provider always resolves to the same linked UGS account, so
            // repeat mock sign-ins with the same provider must be detected as
            // the same returning player once a name has been chosen.
            _playerId    = "mock_" + provider;
            _isAnonymous = false;
            _isSignedIn  = true;
            _displayName = _ssoDisplayNames.TryGetValue(_playerId, out string name) ? name : "";
            PersistSession();
            SULog.Info($"[MOCK] Signed in with {provider} ({_playerId})", SULog.Channel.Net);
            OnSignedIn?.Invoke();
        }
```

- [ ] **Step 6: Run the tests and verify they pass**

Unity MCP `run_tests` (EditMode, filter `LocalMockAuthServiceTests`). Expected: all pass, including the 3 new ones (10 total in this file).

- [ ] **Step 7: Commit**

```bash
git add Assets/_Project/Scripts/Net/LocalMockAuthService.cs Assets/_Project/Tests/EditMode/Net/LocalMockAuthServiceTests.cs
git commit -m "$(cat <<'EOF'
net: give mock SSO sign-in a deterministic per-provider identity

MockSsoSignInAsync previously assigned a random playerId on every call,
so the mock could never represent "the same Google account signing in
twice" — the upcoming first-time name panel needs that to test returning
vs. first-time detection without a live UGS project.

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 3: `AuthService` hydration fix + `AuthScreen` choose-name panel logic

**Files:**
- Modify: `Assets/_Project/Scripts/Net/AuthService.cs`
- Modify: `Assets/_Project/Scripts/UI/AuthScreen.cs`
- Modify: `Assets/_Project/Scripts/Core/BootState.cs`

**Interfaces:**
- Consumes: `DisplayNameValidator.Validate` (Task 1), `IAuthService.DisplayName`/`UpdateDisplayNameAsync`/`SignInWithGoogleAsync` (Task 2 makes the mock behave correctly), `EventBus.Publish(new PlayerReadyEvent())`.
- Produces: new `AuthScreen` serialized fields `_chooseNamePanel` (GameObject), `_chooseNameInput` (InputField), `_chooseNameConfirmButton` (Button), `_chooseNameStatusText` (Text) — Task 4 wires these in the scene.

No automated test for the `AuthService`/`BootState` fixes (both only touch a live `AuthenticationService.Instance` singleton or VContainer/scene-loading infrastructure, same as the rest of those classes — see `AuthServiceTests.cs`'s header comment; there is no `BootStateTests.cs`). Verified manually in Task 8's Play Mode smoke test.

**Why `BootState` needs a change here:** the spec claims a player who quits while gated at the choose-name panel is "self-healing" — the next Google sign-in re-detects the empty name and shows the panel again. That's only true if the player goes through `AuthScreen.OnGoogleClicked` again. `BootState.RunAsync` (`Assets/_Project/Scripts/Core/BootState.cs:58`) currently publishes `PlayerReadyEvent` and skips the Auth scene entirely for *any* signed-in, non-anonymous restored session — including a Google account with no display name yet. Left unfixed, that account would sail straight into the game on relaunch, never seeing the panel, which breaks the "mandatory, no skip" design decision. Steps 8-9 below close this.

- [ ] **Step 1: Fix the `AuthService.SignInWithGoogleAsync` hydration race**

In `Assets/_Project/Scripts/Net/AuthService.cs`, replace:

```csharp
        public async Task SignInWithGoogleAsync(string idToken)
        {
            await AuthenticationService.Instance.SignInWithGoogleAsync(idToken);
            _isAnonymous = false;
            SULog.Info($"Signed in with Google (playerId: {PlayerId})", SULog.Channel.Net);
        }
```

with:

```csharp
        public async Task SignInWithGoogleAsync(string idToken)
        {
            await AuthenticationService.Instance.SignInWithGoogleAsync(idToken);
            _isAnonymous = false;
            // The SignedIn-callback hydration (see InitializeAsync) runs
            // fire-and-forget and isn't guaranteed to finish before this call
            // returns — AuthScreen checks DisplayName immediately afterwards
            // to decide first-time vs. returning player, so a returning
            // player could otherwise be misdetected as first-time. Mirrors
            // the same await already in RegisterAsync.
            await HydratePlayerNameAsync();
            SULog.Info($"Signed in with Google (playerId: {PlayerId})", SULog.Channel.Net);
        }
```

- [ ] **Step 2: Verify it compiles**

Unity MCP `read_console`. Expected: zero errors.

- [ ] **Step 3: Add the choose-name panel fields to `AuthScreen`**

In `Assets/_Project/Scripts/UI/AuthScreen.cs`, extend the panel enum:

```csharp
        private enum AuthPanel { Login, Register, ForgotPasswordEmail, ForgotPasswordReset, VerifyEmail, ChooseName }
```

Add to the `--- Panels ---` block:

```csharp
        [SerializeField] private GameObject _chooseNamePanel;
```

Add a new field block after the `--- Verify email panel ---` fields (after `_verifyCancelButton`):

```csharp

        // --- Choose display name panel (first Google sign-in) ---
        [SerializeField] private InputField _chooseNameInput;
        [SerializeField] private Button     _chooseNameConfirmButton;
        [SerializeField] private Text       _chooseNameStatusText;
```

- [ ] **Step 4: Wire the confirm button in `Start`**

Add after the existing `_verifyCancelButton` listener line:

```csharp
            if (_chooseNameConfirmButton != null) _chooseNameConfirmButton.onClick.AddListener(OnChooseNameConfirmed);
```

- [ ] **Step 5: Extend `ShowPanel`**

Add to the panel-toggle block (after the `_verifyEmailPanel` line):

```csharp
            if (_chooseNamePanel  != null) _chooseNamePanel .SetActive(panel == AuthPanel.ChooseName);
```

Add to the status-clear block (after the `_verifyStatusText` line):

```csharp
            if (_chooseNameStatusText  != null) _chooseNameStatusText .text = "";
```

- [ ] **Step 6: Rewrite `OnGoogleClicked` to detect first-time vs. returning**

Replace:

```csharp
        private async void OnGoogleClicked()
        {
            SetBusy(true);
            _loginStatusText.text = "Signing in with Google…";
            try
            {
                // Same anonymous-transport cleanup as OnLoginClicked: UGS rejects
                // an SSO sign-in over a live anonymous session with "already
                // signed in".
                if (_auth.IsSignedIn && _auth.IsAnonymous) await _auth.SignOutAsync();
                string idToken;
                try   { idToken = await GoogleAuthHandler.GetIdTokenAsync(); }
                catch (NotSupportedException) { idToken = "mock_google_token"; }
                await _auth.SignInWithGoogleAsync(idToken);
            }
            catch (Exception ex) { _loginStatusText.text = FriendlyError(ex); SetBusy(false); }
        }
```

with:

```csharp
        private async void OnGoogleClicked()
        {
            SetBusy(true);
            _loginStatusText.text = "Signing in with Google…";
            // Suppress HandleSignedIn's auto-publish: the SignedIn event can
            // fire before this method gets a chance to check below whether
            // this is a first-time sign-in — same mechanism the verify-email
            // flow uses. Cleared explicitly once we know which path to take.
            _suppressAutoTransition = true;
            try
            {
                // Same anonymous-transport cleanup as OnLoginClicked: UGS rejects
                // an SSO sign-in over a live anonymous session with "already
                // signed in".
                if (_auth.IsSignedIn && _auth.IsAnonymous) await _auth.SignOutAsync();
                string idToken;
                try   { idToken = await GoogleAuthHandler.GetIdTokenAsync(); }
                catch (NotSupportedException) { idToken = "mock_google_token"; }
                await _auth.SignInWithGoogleAsync(idToken);

                if (string.IsNullOrEmpty(_auth.DisplayName))
                {
                    // First Google sign-in: hold at Auth until a name is chosen.
                    SetBusy(false);
                    ShowPanel(AuthPanel.ChooseName);
                }
                else
                {
                    _suppressAutoTransition = false;
                    SetBusy(false);
                    SULog.Info("Auth: Google sign-in (returning player) — advancing to game", SULog.Channel.Net);
                    EventBus.Publish(new PlayerReadyEvent());
                }
            }
            catch (Exception ex)
            {
                _suppressAutoTransition = false;
                _loginStatusText.text = FriendlyError(ex);
                SetBusy(false);
            }
        }
```

- [ ] **Step 7: Add `OnChooseNameConfirmed`**

Add immediately after `OnGoogleClicked`:

```csharp
        private async void OnChooseNameConfirmed()
        {
            string name = _chooseNameInput.text;
            if (!DisplayNameValidator.Validate(name, out string err))
            {
                _chooseNameStatusText.text = err;
                return;
            }

            SetBusy(true);
            _chooseNameStatusText.text = "Saving…";
            try
            {
                await _auth.UpdateDisplayNameAsync(name.Trim());
                _suppressAutoTransition = false;
                SetBusy(false);
                SULog.Info("Auth: display name chosen — advancing to game", SULog.Channel.Net);
                EventBus.Publish(new PlayerReadyEvent());
            }
            catch (Exception ex)
            {
                _chooseNameStatusText.text = FriendlyError(ex);
                SetBusy(false);
            }
        }
```

- [ ] **Step 8: Extend `SetBusy` and `SetActiveStatus`**

Add to `SetBusy` (after the `_verifyCancelButton` line):

```csharp
            if (_chooseNameConfirmButton != null) _chooseNameConfirmButton.interactable = !busy;
```

Add to `SetActiveStatus` (after the `_verifyEmailPanel` branch):

```csharp
            else if (_chooseNamePanel != null && _chooseNamePanel.activeSelf && _chooseNameStatusText != null)
                _chooseNameStatusText.text = message;
```

- [ ] **Step 9: Gate a restored nameless session in `HandleSignedIn`**

`AuthScreen.Start()` calls `HandleSignedIn()` directly when `_auth.IsSignedIn` is already true (a session BootState decided to route through the Auth scene — see Step 10). Today `HandleSignedIn` only checks `IsAnonymous` before publishing; it needs the same empty-name check `OnGoogleClicked` uses, or a restored nameless account would auto-publish `PlayerReadyEvent` on `Start()` before ever reaching the panel.

Replace:

```csharp
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
```

with:

```csharp
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
            // A restored SSO session with no display name yet (the app was
            // quit while gated at the choose-name panel — see BootState) is
            // shown the panel again instead of entering the game nameless.
            if (string.IsNullOrEmpty(_auth.DisplayName))
            {
                SetBusy(false);
                ShowPanel(AuthPanel.ChooseName);
                return;
            }
            SetBusy(false);
            SULog.Info("Auth: signed in — advancing to game", SULog.Channel.Net);
            EventBus.Publish(new PlayerReadyEvent());
        }
```

- [ ] **Step 10: Stop `BootState` from skipping the Auth scene for a nameless session**

In `Assets/_Project/Scripts/Core/BootState.cs`, replace:

```csharp
            if (_auth.IsSignedIn)
            {
                SULog.Info("Boot: session restored, skipping Auth scene");
```

with:

```csharp
            // A restored SSO session with no display name yet means the app
            // was quit while gated at AuthScreen's choose-name panel — route
            // through the Auth scene (below) instead of publishing
            // PlayerReadyEvent directly, so HandleSignedIn can show the panel
            // again rather than letting a nameless account into the game.
            if (_auth.IsSignedIn && !string.IsNullOrEmpty(_auth.DisplayName))
            {
                SULog.Info("Boot: session restored, skipping Auth scene");
```

No other change is needed in this method — the existing "loading Auth scene" path at the bottom already runs unconditionally for anything that doesn't return early above, and it loads `Auth.unity` with the root scope as parent, so `AuthScreen.Start()` sees `_auth.IsSignedIn == true` and calls the just-fixed `HandleSignedIn()`.

- [ ] **Step 11: Verify it compiles**

Unity MCP `read_console`. Expected: zero errors. `AuthSceneWiringTests.Every_AuthScreen_serialized_reference_is_wired` will now FAIL (the three new fields are `{fileID: 0}` in the scene) — this is expected and fixed by Task 4, not by editing the test.

- [ ] **Step 12: Commit**

```bash
git add Assets/_Project/Scripts/Net/AuthService.cs Assets/_Project/Scripts/UI/AuthScreen.cs Assets/_Project/Scripts/Core/BootState.cs
git commit -m "$(cat <<'EOF'
auth: first-time Google sign-in now holds at a choose-name panel

Fixes a hydration race in AuthService.SignInWithGoogleAsync that could
misdetect a returning player as first-time, adds the ChooseName panel
to AuthScreen's existing panel-switch/suppression pattern (mirrors the
verify-email flow), and stops BootState from skipping the Auth scene
for a restored session that has no display name yet — otherwise a
player who quit mid choose-name-panel would slip into the game
nameless on relaunch instead of seeing the panel again. Scene wiring
is intentionally still stale — next task.

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 4: `Auth.unity` scene — add and wire the `ChooseNamePanel`

**Files:**
- Modify: `Assets/Scenes/Auth.unity` (via Unity MCP — do not hand-edit the YAML for new objects; only wiring diffs are safe to eyeball)

**Interfaces:**
- Consumes: the exact field names from Task 3's Interfaces block (`_chooseNamePanel`, `_chooseNameInput`, `_chooseNameConfirmButton`, `_chooseNameStatusText`).
- Produces: a fully wired Auth scene. No code contracts.

- [ ] **Step 1: Open the scene and find the duplication template**

Unity MCP: `manage_scene` (load `Assets/Scenes/Auth.unity`), then `find_gameobjects` for `VerifyEmailPanel` — it already has the closest-matching structure (title `Text`, instruction `Text`, one input field, a primary action button, a status `Text`, no back-navigation needed for `ChooseName` since it's mandatory). Note its exact child hierarchy and styling (background image, fonts, colors, button sprites) via `manage_gameobject`/`find_gameobjects` before duplicating.

- [ ] **Step 2: Duplicate `VerifyEmailPanel` → `ChooseNamePanel`**

Duplicate the `VerifyEmailPanel` GameObject, rename to `ChooseNamePanel`, and prune its children to:
- `ChooseNameTitle` (Text) — "Choose your display name"
- `ChooseNameInstruction` (Text) — "This is what other players will see"
- `ChooseNameInput` (InputField, placeholder "Display name", no Password content type)
- `ChooseNameConfirmButton` (Button, label "Confirm")
- `ChooseNameStatusText` (Text)

Delete the duplicated `VerifyButton`/`ResendCodeButton`/`VerifyCancelButton` equivalents — there is no cancel/skip/resend on this panel (design decision: mandatory, matches email signup always collecting a username). Keep the panel inactive by default (`AuthScreen` activates it via `ShowPanel`).

- [ ] **Step 3: Wire the `AuthScreen` component**

Via `manage_components`, set on the `AuthScreen` component:
- `_chooseNamePanel` → the `ChooseNamePanel` GameObject
- `_chooseNameInput` → `ChooseNamePanel/ChooseNameInput`'s `InputField`
- `_chooseNameConfirmButton` → `ChooseNamePanel/ChooseNameConfirmButton`'s `Button`
- `_chooseNameStatusText` → `ChooseNamePanel/ChooseNameStatusText`'s `Text`

- [ ] **Step 4: Verify wiring**

Run `AuthSceneWiringTests.Every_AuthScreen_serialized_reference_is_wired` via Unity MCP `run_tests` (EditMode, filter `AuthSceneWiringTests`). Expected: PASS — no field is `{fileID: 0}`.

- [ ] **Step 5: Save the scene and commit**

Unity MCP `manage_scene` save, then:

```bash
git add Assets/Scenes/Auth.unity
git commit -m "$(cat <<'EOF'
Auth scene: add and wire the ChooseNamePanel

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 5: Recover the Google Sign-In plugin and fix the asmdef gap

**Files:**
- Restore (from `309f83ef^`): `Assets/GoogleSignIn/**`, `Assets/GeneratedLocalRepo/**`, `Assets/Parse/**`, `Assets/PlayServicesResolver.meta`, `Assets/Plugins/Android/mainTemplate.gradle(.meta)`, `Assets/Plugins/iOS/GoogleSignIn/**` (+ folder `.meta`), `ProjectSettings/AndroidResolverDependencies.xml`, `ProjectSettings/GvhProjectSettings.xml`
- Create: `Assets/GoogleSignIn/GoogleSignIn.asmdef`
- Modify: `Assets/_Project/Scripts/Net/SocialUniverse.Net.asmdef`, `Packages/manifest.json`, `ProjectSettings/PackageManagerSettings.asset`

**Interfaces:** No C# API changes — this task only makes `Google.*` types (namespace `Google`, from `Assets/GoogleSignIn/GoogleSignIn.cs` etc.) resolvable from `SocialUniverse.Net`. Task 7 is the first to actually reference them.

Commit `309f83ef` ("Defer Google Sign-In to feature/google-signin branch") deleted this whole plugin because `GoogleAuthHandler.cs` referenced `Google.*` types from `Assembly-CSharp` with no asmdef bridging them into `SocialUniverse.Net`, breaking Android Player builds with CS0246/CS0103. The full plugin is untouched at `309f83ef`'s parent commit — this task restores it and adds the missing asmdef so the same failure can't recur.

- [ ] **Step 1: Confirm there are no on-disk leftovers to reconcile**

```bash
ls Assets/GoogleSignIn Assets/Plugins/Android Assets/Plugins/iOS/GoogleSignIn 2>&1
```

Expected: "No such file or directory" for all three (confirmed clean during brainstorming — nothing untracked to reconcile). If anything DOES exist, diff it against `309f83ef^`'s version before overwriting rather than blindly restoring.

- [ ] **Step 2: Restore the plugin assets from git history**

```bash
git checkout 309f83ef^ -- \
  Assets/GeneratedLocalRepo \
  Assets/GoogleSignIn \
  Assets/Parse \
  Assets/PlayServicesResolver.meta \
  "Assets/Plugins/Android/mainTemplate.gradle" \
  "Assets/Plugins/Android/mainTemplate.gradle.meta" \
  Assets/Plugins/iOS/GoogleSignIn \
  Assets/Plugins/iOS/GoogleSignIn.meta \
  ProjectSettings/AndroidResolverDependencies.xml \
  ProjectSettings/GvhProjectSettings.xml
git status --porcelain=v1
```

Expected: all the paths above show as newly added (`A`) or modified. Do **not** restore `Assets/_Project/Scripts/Net/GoogleAuthHandler.cs` this way — Task 6/7 hand-edit the current stub in place. Do **not** restore `Packages/packages-lock.json` or `ProjectSettings/PackageManagerSettings.asset` this way either — both are Package Manager-owned derived files; Step 4 below edits their one source-of-truth line by hand / lets Unity regenerate the rest.

- [ ] **Step 3: Create the plugin's asmdef**

The plugin's runtime scripts (`Assets/GoogleSignIn/*.cs` and `Assets/GoogleSignIn/Impl/*.cs`, namespace `Google`) have no nested asmdef of their own — `Assets/GoogleSignIn/Editor/` contains only XML/AAR resolver config and a readme .txt, no C#, so no separate Editor asmdef is needed.

Create `Assets/GoogleSignIn/GoogleSignIn.asmdef`:

```json
{
    "name": "GoogleSignIn",
    "rootNamespace": "",
    "references": [],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": true,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false
}
```

Use Unity MCP (`manage_asset` or direct file creation followed by `refresh_unity`) so Unity generates the matching `.meta` file rather than hand-authoring a GUID.

- [ ] **Step 4: Reference it from `SocialUniverse.Net.asmdef`**

In `Assets/_Project/Scripts/Net/SocialUniverse.Net.asmdef`, add `"GoogleSignIn"` to the `references` array (alongside the existing `"SocialUniverse.Social"` entry):

```json
    "references": [
        "VContainer",
        "SocialUniverse.Core",
        "SocialUniverse.Config",
        "Unity.Services.Core",
        "Unity.Services.Core.Environments",
        "Unity.Services.Authentication",
        "Unity.Services.CloudCode",
        "Unity.Services.CloudSave",
        "Unity.Services.Vivox",
        "NaughtyAttributes.Core",
        "SocialUniverse.Social",
        "GoogleSignIn"
    ],
```

- [ ] **Step 5: Restore the EDM4U package dependency**

In `Packages/manifest.json`, add the dependency (alphabetically among the `com.*` entries, right after `com.coplaydev.unity-mcp`):

```json
    "com.google.external-dependency-manager": "1.2.187",
```

and add `"com.google"` to the existing OpenUPM `scopedRegistries` entry's `scopes` array:

```json
      "scopes": [
        "jp.hadashikick",
        "com.google"
      ]
```

- [ ] **Step 6: Sync `PackageManagerSettings.asset`**

Unity normally re-syncs `ProjectSettings/PackageManagerSettings.asset` from `manifest.json` automatically on the next package resolve (Editor focus / domain reload after Step 5's `refresh_unity`). Confirm via Unity MCP `read_console` that a package resolve ran with no errors; if the scope hasn't synced, add `- com.google` under the existing `- jp.hadashikick` line in that file's `m_Scopes` list by hand, matching `309f83ef`'s exact reverse diff.

- [ ] **Step 7: Refresh and verify compilation**

Unity MCP `refresh_unity`, then `read_console`. Expected: zero compile errors — `Assets/GoogleSignIn/**` compiles standalone in its new asmdef, and `SocialUniverse.Net` compiles with the new reference even though nothing in it uses `Google.*` yet.

- [ ] **Step 8: Commit**

```bash
git add Assets/GeneratedLocalRepo Assets/GoogleSignIn Assets/Parse Assets/PlayServicesResolver.meta \
  "Assets/Plugins/Android/mainTemplate.gradle" "Assets/Plugins/Android/mainTemplate.gradle.meta" \
  Assets/Plugins/iOS/GoogleSignIn Assets/Plugins/iOS/GoogleSignIn.meta \
  ProjectSettings/AndroidResolverDependencies.xml ProjectSettings/GvhProjectSettings.xml \
  ProjectSettings/PackageManagerSettings.asset Packages/manifest.json Packages/packages-lock.json \
  Assets/_Project/Scripts/Net/SocialUniverse.Net.asmdef
git commit -m "$(cat <<'EOF'
Restore Google Sign-In plugin from 309f83ef^ with a proper asmdef

309f83ef deleted the plugin because GoogleAuthHandler referenced
Google.* types from Assembly-CSharp with no asmdef bridging them into
SocialUniverse.Net, breaking Android Player builds. This restores the
plugin under its own GoogleSignIn.asmdef, referenced from
SocialUniverse.Net, so that gap can't recur. GoogleAuthHandler itself
is untouched here — next two tasks.

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 6: `GoogleAuthConfig` ScriptableObject + setup checklist

**Files:**
- Create: `Assets/_Project/Scripts/Config/GoogleAuthConfig.cs`
- Modify: `Assets/_Project/Scripts/Net/GoogleAuthHandler.cs`
- Modify: `Assets/_Project/Scripts/App/RootLifetimeScope.cs`
- Create: `Assets/_Project/ScriptableObjects/GoogleAuthConfig.asset` (Unity MCP)
- Create: `docs/google-signin-setup-checklist.md`

**Interfaces:**
- Produces: `SocialUniverse.Config.GoogleAuthConfig.WebClientId : string`; `SocialUniverse.Net.GoogleAuthHandler.Configure(GoogleAuthConfig config)` — Task 7's Android branch reads the value this stores.

- [ ] **Step 1: Create `GoogleAuthConfig`**

`Assets/_Project/Scripts/Config/GoogleAuthConfig.cs`:

```csharp
using UnityEngine;

namespace SocialUniverse.Config
{
    // Holds the OAuth Web Client ID Google Sign-In needs on Android — UGS's
    // SignInWithGoogleAsync verifies the device-acquired ID token against
    // this same Web client. Placeholder until the user completes the Google
    // Cloud Console / UGS dashboard setup — see
    // docs/google-signin-setup-checklist.md.
    [CreateAssetMenu(menuName = "SocialUniverse/Config/GoogleAuthConfig", fileName = "GoogleAuthConfig")]
    public class GoogleAuthConfig : ScriptableObject
    {
        [SerializeField] private string _webClientId = "YOUR_WEB_CLIENT_ID.apps.googleusercontent.com";

        public string WebClientId => _webClientId;
    }
}
```

- [ ] **Step 2: Add `Configure` to `GoogleAuthHandler`**

Replace the full contents of `Assets/_Project/Scripts/Net/GoogleAuthHandler.cs` with:

```csharp
using System;
using System.Threading.Tasks;
using SocialUniverse.Config;

// Acquires a Google ID token via platform-specific OAuth, ready to pass to
// IAuthService.SignInWithGoogleAsync(idToken). Throws NotSupportedException
// in the Unity Editor and on non-Android platforms — AuthScreen catches this
// and falls back to a mock token, so the mock auth flow still works in dev
// mode. The Android device path is restored in a later pass (see
// docs/superpowers/specs/2026-07-17-google-signin-display-name-design.md);
// Configure lets the app wire up the OAuth Web Client ID ahead of that so
// RootLifetimeScope only needs to change once.
namespace SocialUniverse.Net
{
    public static class GoogleAuthHandler
    {
        private static string _webClientId;

        // Called once from RootLifetimeScope.Configure before any sign-in
        // attempt. Never touches Google.* types, so it's safe to call even in
        // the Editor or on platforms where the native plugin isn't present.
        public static void Configure(GoogleAuthConfig config)
        {
            _webClientId = config != null ? config.WebClientId : null;
        }

        public static Task<string> GetIdTokenAsync()
        {
            return Task.FromException<string>(
                new NotSupportedException("Google Sign-In is unavailable in the Unity Editor or on this platform"));
        }
    }
}
```

- [ ] **Step 3: Wire it into `RootLifetimeScope`**

In `Assets/_Project/Scripts/App/RootLifetimeScope.cs`, add a serialized field alongside `_audioCatalog`:

```csharp
        [SerializeField] private GoogleAuthConfig _googleAuthConfig;
```

and at the top of `Configure`, right after `base.Configure(builder);`:

```csharp
            GoogleAuthHandler.Configure(_googleAuthConfig);
```

(`SocialUniverse.Net` and `SocialUniverse.Config` are already `using`d in this file.)

- [ ] **Step 4: Verify it compiles**

Unity MCP `read_console`. Expected: zero errors.

- [ ] **Step 5: Create the `GoogleAuthConfig` asset and wire it into Bootstrap**

Unity MCP `manage_asset` (or `manage_scriptable_object`, whichever the project's prior AudioCatalog task found working — `action=create type_name=SocialUniverse.Config.GoogleAuthConfig`) to create `Assets/_Project/ScriptableObjects/GoogleAuthConfig.asset` (leave `_webClientId` at its placeholder default). Then open `Assets/Scenes/Bootstrap.unity`, find the `RootLifetimeScope` component, and assign the new asset to `_googleAuthConfig` via `manage_components`. Save the scene.

- [ ] **Step 6: Write the setup checklist**

Create `docs/google-signin-setup-checklist.md`:

```markdown
# Google Sign-In — Device Setup Checklist

Code-complete; these are the account/console steps only a project owner can
do. Nothing in the app works on a real Android device until all four are
done.

1. **Google Cloud Console** (https://console.cloud.google.com/):
   - Configure the OAuth consent screen for the project.
   - Create OAuth 2.0 credentials → **Web application** client ID. Copy it —
     this is the value that goes in step 3.
   - Create OAuth 2.0 credentials → **Android** client ID:
     - Package name: `com.ValariSolutions.socialuniverse` (from
       `ProjectSettings/AndroidResolverDependencies.xml`'s `bundleId`).
     - SHA-1 fingerprint of the signing keystore:
       ```
       keytool -list -v -keystore zKeystore/user.keystore -alias <alias> -storepass <password>
       ```
       Copy the `SHA1:` fingerprint line into the Android client's config.

2. **Unity Gaming Services dashboard**: Authentication → enable **Google**
   as an identity provider, pasting in the **Web** client ID from step 1
   (not the Android one — UGS validates ID tokens against the Web client).

3. **This repo**: open `Assets/_Project/ScriptableObjects/GoogleAuthConfig.asset`
   in the Inspector and replace the placeholder `_webClientId` with the same
   Web client ID from step 1.

4. **Device smoke test** (after 1-3 are done and a Development Build is
   installed on an Android device signed with the same keystore as step 1):
   - First Google sign-in → the choose-name panel appears → enter a name →
     enters the game.
   - Reinstall (or clear app data) and sign in again with the same Google
     account → goes straight into the game, no name panel.
```

- [ ] **Step 7: Commit**

```bash
git add Assets/_Project/Scripts/Config/GoogleAuthConfig.cs Assets/_Project/Scripts/Net/GoogleAuthHandler.cs \
  Assets/_Project/Scripts/App/RootLifetimeScope.cs Assets/_Project/ScriptableObjects/GoogleAuthConfig.asset \
  Assets/Scenes/Bootstrap.unity docs/google-signin-setup-checklist.md
git commit -m "$(cat <<'EOF'
config: add GoogleAuthConfig + OAuth setup checklist

WebClientId ships as a placeholder — real device sign-in is blocked
until the user completes the Google Cloud Console / UGS steps in
docs/google-signin-setup-checklist.md.

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 7: Restore the real Android `GoogleAuthHandler` implementation

**Files:**
- Modify: `Assets/_Project/Scripts/Net/GoogleAuthHandler.cs`

**Interfaces:**
- Consumes: `Google.GoogleSignInConfiguration`, `Google.GoogleSignIn`, `Google.GoogleSignInUser` (from Task 5's restored plugin, now resolvable via the `GoogleSignIn` asmdef reference), `GoogleAuthConfig.WebClientId` (Task 6, via the `_webClientId` field `Configure` already populates).
- Produces: no new public members — `GetIdTokenAsync()`'s Android branch now actually calls the plugin instead of always throwing.

- [ ] **Step 1: Replace `GetIdTokenAsync` with the platform-branching version**

In `Assets/_Project/Scripts/Net/GoogleAuthHandler.cs`, replace:

```csharp
        public static Task<string> GetIdTokenAsync()
        {
            return Task.FromException<string>(
                new NotSupportedException("Google Sign-In is unavailable in the Unity Editor or on this platform"));
        }
```

with:

```csharp
        public static Task<string> GetIdTokenAsync()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            return GetIdTokenAndroidAsync();
#else
            return Task.FromException<string>(
                new NotSupportedException("Google Sign-In is unavailable in the Unity Editor or on this platform"));
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private static async Task<string> GetIdTokenAndroidAsync()
        {
            if (string.IsNullOrEmpty(_webClientId) || _webClientId.StartsWith("YOUR_"))
                throw new InvalidOperationException(
                    "GoogleAuthConfig.WebClientId is still the placeholder — see docs/google-signin-setup-checklist.md");

            var config = new Google.GoogleSignInConfiguration
            {
                WebClientId    = _webClientId,
                RequestIdToken = true,
            };
            Google.GoogleSignIn.Configuration = config;

            Google.GoogleSignInUser user = await Google.GoogleSignIn.DefaultInstance.SignIn();
            if (string.IsNullOrEmpty(user.IdToken))
                throw new InvalidOperationException("Google Sign-In returned no ID token");
            return user.IdToken;
        }
#endif
```

- [ ] **Step 2: Verify Editor compilation**

Unity MCP `read_console`. Expected: zero errors — in the Editor, `UNITY_ANDROID && !UNITY_EDITOR` is false, so `GetIdTokenAndroidAsync` and its `Google.*` references aren't even compiled into this configuration, but the `#if` block itself must still parse cleanly (Unity type-checks all `#if` branches for the currently-targeted platform's compiler pass — switch the Editor's active build target to Android via Unity MCP `manage_editor` if available, then back, to force at least one compile pass through the Android branch and confirm `Google.*` resolves).

- [ ] **Step 3: Commit**

```bash
git add Assets/_Project/Scripts/Net/GoogleAuthHandler.cs
git commit -m "$(cat <<'EOF'
net: restore real on-device Google Sign-In in GoogleAuthHandler

The Android branch now calls the recovered Google Sign-In plugin
instead of unconditionally throwing NotSupportedException. Guarded by
a placeholder check against GoogleAuthConfig.WebClientId so a
misconfigured build fails with a clear message instead of a native
crash.

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 8: Full verification and hand-off

**Files:** none new — verification only.

- [ ] **Step 1: Run the full EditMode suite**

Unity MCP `run_tests` (EditMode, no filter). Expected: all pass — 233 pre-existing + 8 new from Task 1 + 3 new from Task 2 = 244 total. Fix anything red before proceeding.

- [ ] **Step 2: Console sweep**

Unity MCP `read_console` — zero errors/warnings introduced by this work.

- [ ] **Step 3: Play Mode smoke test (editor, `LocalMock` services, `_devMode` enabled)**

Enter Play Mode from the Boot scene via Unity MCP `manage_editor`, then confirm in the console/scene:
1. Click "Sign in with Google" → since `GoogleAuthHandler.GetIdTokenAsync()` throws `NotSupportedException` in the Editor, `AuthScreen` falls back to `"mock_google_token"` → `LocalMockAuthService` reports an empty `DisplayName` → the `ChooseNamePanel` appears (not straight into the game).
2. Try invalid names on the panel: empty → "Name must be at least 2 characters"; 21 chars → "Name must be 20 characters or fewer"; contains a space → "Name cannot contain spaces". Panel stays up each time.
3. Enter a valid name (e.g. "Nova") → Confirm → advances into the game.
4. From the Planet scene, sign out (existing debug/settings path) back to Auth, click "Sign in with Google" again → since `LocalMockAuthService`'s mock Google identity is deterministic (Task 2), it recalls `DisplayName = "Nova"` → advances straight into the game, no panel.
5. Sign in with Google as a *new* mock identity (e.g. temporarily change the `MockSsoSignInAsync` provider string, or sign out and use a fresh `LocalMockAuthService` instance by re-entering Play Mode) to reach the `ChooseNamePanel` again, then exit Play Mode without confirming a name — simulating an app quit mid-panel. Re-enter Play Mode from Boot: `TryAutoSignInAsync` restores the nameless session, `BootState` (Task 3 Step 10) must NOT skip the Auth scene, and `AuthScreen.HandleSignedIn` (Task 3 Step 9) must show the `ChooseNamePanel` again rather than publishing `PlayerReadyEvent`.
6. Existing email login/register/forgot-password flows still work unchanged (regression check on the panel-switch and `BootState` changes) — a fully registered/verified email account still skips straight to the game on relaunch as before.

If Play Mode automation is impractical in this environment, flag the smoke test as pending for the user instead of skipping silently.

- [ ] **Step 4: Flag remaining manual work**

Real on-device Google Sign-In cannot be verified in this pass — it depends on the user completing `docs/google-signin-setup-checklist.md` (Google Cloud Console OAuth clients, UGS dashboard config, pasting the real Web Client ID into `GoogleAuthConfig.asset`, then a signed Android build). State this plainly when reporting completion; do not claim device sign-in works.

- [ ] **Step 5: Hand off**

Invoke `superpowers:finishing-a-development-branch` to decide how this branch (`worktree-google-signin-display-name`) gets integrated — do not push or open a PR unilaterally. If a PR is opened, its body must state the manual OAuth setup checklist from Task 6 as an explicit blocker for device testing.
