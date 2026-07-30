# First-Time Profile Onboarding (Name + Avatar) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Require a first-time nameless account (Google/SSO) to set a display name (and optionally change its avatar) via a mandatory, non-dismissable modal on entering the Hub/Planet scene.

**Architecture:** A pure `ProfileOnboarding.NeedsOnboarding` helper decides, during `PlanetSceneScope` hydration, whether the signed-in player has no real name anywhere; if so it publishes `ShowProfileOnboardingEvent` over `EventBus`. `HUDController` subscribes and opens the existing `AvatarSelectionModal` + `DisplayNameModal` in a new onboarding mode that hides Cancel, forbids dismissal, and requires a valid name before committing name + avatar through `ProfileService`.

**Tech Stack:** Unity 6 (URP), C#, VContainer DI, Unity Test Framework (NUnit EditMode), UGS Authentication, Firebase (OIDC bridge), Unity MCP for compile/test feedback.

## Global Constraints

- Namespaces mirror folders per the Project Structure table: events in `SocialUniverse.Core`, the pure helper in `SocialUniverse.Progression`, UI in `SocialUniverse.UI`, scene-scope glue in `SocialUniverse.App`.
- Server-authoritative: name/avatar commit only through `ProfileService` / `IAuthService` — never mint state client-side (Architecture Rule #1).
- Decouple via events: App→UI handoff goes over `EventBus`, not a direct call (Architecture Rule #4).
- No backend SDK types in UI: UI depends on `IAuthService` / `ProfileService` abstractions only (Architecture Rule #2).
- `SocialUniverse.Progression` must NOT gain a reference to `SocialUniverse.Social` — keep `ProfileOnboarding` self-contained.
- Display-name validation rules already in the codebase: min 2 chars, max = `SocialConfig.MaxDisplayNameLength` (default 20). Do not change them.
- The "no name" sentinel is `ChatDisplayNameResolver.Fallback` == `"Player"`.
- One public type per file; file named after the type.
- After every C# change, verify compilation via Unity MCP `refresh_unity` (compile=request, mode=force, scope=scripts) then `read_console` (types=["error"]) and confirm **0 errors** before committing.

## Preconditions (do first)

The working tree currently has uncommitted edits from the avatar-null-guard fix in
`Assets/_Project/Scripts/UI/AvatarSelectionModal.cs` and the hydration `else`-branch in
`Assets/_Project/Scripts/App/PlanetSceneScope.cs`, plus the Firebase error-message work.
Commit those separately BEFORE starting, so each task below can `git add` only its own files
without bundling unrelated changes:

```bash
git add Assets/_Project/Scripts/UI/AvatarSelectionModal.cs \
        Assets/_Project/Scripts/App/PlanetSceneScope.cs \
        Assets/_Project/Scripts/Net/FirebaseAuthHandler.cs \
        Assets/_Project/Scripts/UI/AuthScreen.cs \
        Assets/_Project/Scripts/UI/EmailVerificationModal.cs
git commit -m "fix: guard avatar picker null + assign avatar to new players; friendly Firebase auth errors"
```

(Scene/asset edits — `Auth.unity`, `Planet.unity`, materials, config — can be committed by the user separately; they are not part of this plan except the optional `_closeButton` wiring in Task 6.)

---

### Task 1: `ProfileOnboarding` pure decision helper

**Files:**
- Create: `Assets/_Project/Scripts/Progression/ProfileOnboarding.cs`
- Test: `Assets/_Project/Tests/EditMode/Progression/ProfileOnboardingTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces: `bool SocialUniverse.Progression.ProfileOnboarding.NeedsOnboarding(string profileDisplayName, string authDisplayName, string authUsername)` — returns `true` when none of the three arguments contains a real name (after trimming and stripping a UGS `#1234` suffix). Order-independent.

- [ ] **Step 1: Write the failing tests**

Create `Assets/_Project/Tests/EditMode/Progression/ProfileOnboardingTests.cs`:

```csharp
using NUnit.Framework;
using SocialUniverse.Progression;

namespace SocialUniverse.Tests
{
    public class ProfileOnboardingTests
    {
        [Test]
        public void Profile_name_present_does_not_need_onboarding()
        {
            Assert.IsFalse(ProfileOnboarding.NeedsOnboarding("Nova", null, null));
        }

        [Test]
        public void Auth_display_name_present_does_not_need_onboarding()
        {
            Assert.IsFalse(ProfileOnboarding.NeedsOnboarding(null, "Comet", null));
        }

        [Test]
        public void Auth_username_present_does_not_need_onboarding()
        {
            Assert.IsFalse(ProfileOnboarding.NeedsOnboarding(null, null, "Rover"));
        }

        [Test]
        public void All_empty_needs_onboarding()
        {
            Assert.IsTrue(ProfileOnboarding.NeedsOnboarding(null, "", "   "));
        }

        [Test]
        public void Whitespace_only_name_needs_onboarding()
        {
            Assert.IsTrue(ProfileOnboarding.NeedsOnboarding("   ", null, null));
        }

        [Test]
        public void Bare_hash_suffix_only_needs_onboarding()
        {
            // UGS appends "#1234"; a name that is *only* the suffix has no real part.
            Assert.IsTrue(ProfileOnboarding.NeedsOnboarding("#1234", null, null));
        }

        [Test]
        public void Name_with_hash_suffix_is_real()
        {
            Assert.IsFalse(ProfileOnboarding.NeedsOnboarding("Nova#1234", null, null));
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run the EditMode suite via Unity MCP `run_tests` (mode=`EditMode`, test_filter=`ProfileOnboardingTests`).
Expected: FAIL / compile error — `ProfileOnboarding` does not exist.

- [ ] **Step 3: Write minimal implementation**

Create `Assets/_Project/Scripts/Progression/ProfileOnboarding.cs`:

```csharp
namespace SocialUniverse.Progression
{
    // Pure decision for PlanetSceneScope: does this account still need to choose an
    // in-game name? True when no real name exists on the profile or the auth session
    // (a fresh Google/SSO account, whose UGS PlayerName is null). The sanitize rules
    // mirror SocialUniverse.Social.ChatDisplayNameResolver (trim, strip the UGS
    // "#1234" suffix, ignore whitespace-only) but are kept local so Progression need
    // not reference SocialUniverse.Social.
    public static class ProfileOnboarding
    {
        public static bool NeedsOnboarding(string profileDisplayName, string authDisplayName, string authUsername)
            => !HasRealName(profileDisplayName)
            && !HasRealName(authDisplayName)
            && !HasRealName(authUsername);

        private static bool HasRealName(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;

            int hash = name.IndexOf('#');
            if (hash >= 0) name = name.Substring(0, hash);

            return name.Trim().Length > 0;
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run the EditMode suite via Unity MCP `run_tests` (mode=`EditMode`, test_filter=`ProfileOnboardingTests`).
Expected: PASS — all 7 tests green.

- [ ] **Step 5: Commit**

```bash
git add Assets/_Project/Scripts/Progression/ProfileOnboarding.cs \
        Assets/_Project/Tests/EditMode/Progression/ProfileOnboardingTests.cs
git commit -m "feat: add ProfileOnboarding.NeedsOnboarding decision helper + tests"
```

---

### Task 2: `ShowProfileOnboardingEvent` + publish from hydration

**Files:**
- Create: `Assets/_Project/Scripts/Core/ShowProfileOnboardingEvent.cs`
- Modify: `Assets/_Project/Scripts/App/PlanetSceneScope.cs` (inside `HydrateServerStateAsync`, the profile try/catch block around lines 300-345)

**Interfaces:**
- Consumes: `ProfileOnboarding.NeedsOnboarding(string, string, string)` (Task 1); `IAuthService.DisplayName`, `IAuthService.Username`; `PlayerProfile.DisplayName`.
- Produces: `SocialUniverse.Core.ShowProfileOnboardingEvent` (parameterless `readonly struct`).

- [ ] **Step 1: Create the event type**

Create `Assets/_Project/Scripts/Core/ShowProfileOnboardingEvent.cs`:

```csharp
namespace SocialUniverse.Core
{
    // Published by PlanetSceneScope when a signed-in player has no real display name
    // on their profile or auth session (a fresh Google/SSO account). HUDController
    // subscribes to open the avatar/name modal in mandatory onboarding mode.
    public readonly struct ShowProfileOnboardingEvent { }
}
```

- [ ] **Step 2: Capture the profile display name across the try/catch**

In `PlanetSceneScope.HydrateServerStateAsync`, declare a captured variable just before the profile `try` and record the profile's display name inside the `if (profile != null)` branch.

Change the opening of the block from:

```csharp
            try
            {
                var profile = await _profileService.GetProfileAsync(_auth.PlayerId);
                if (profile != null)
                {
                    if (!string.IsNullOrEmpty(profile.DisplayName))
                        _playerState.SetDisplayName(profile.DisplayName);
```

to:

```csharp
            string profileDisplayName = null;
            try
            {
                var profile = await _profileService.GetProfileAsync(_auth.PlayerId);
                if (profile != null)
                {
                    profileDisplayName = profile.DisplayName;

                    if (!string.IsNullOrEmpty(profile.DisplayName))
                        _playerState.SetDisplayName(profile.DisplayName);
```

- [ ] **Step 3: Publish the onboarding event after the try/catch**

Immediately after the closing `}` of the `catch (Exception ex) { ... "profile fetch failed" ... }` block (currently ending around line 345), add:

```csharp

            // A first-time nameless account (e.g. Google/SSO, whose UGS PlayerName is
            // null) has never chosen an in-game name. Prompt them to before they play.
            // Published in the same hydration phase as ShowEmailVerificationPromptEvent,
            // so HUDController (subscribed in Start) receives it the same way.
            if (ProfileOnboarding.NeedsOnboarding(profileDisplayName, _auth.DisplayName, _auth.Username))
            {
                EventBus.Publish(new ShowProfileOnboardingEvent());
            }
```

Note: `PlanetSceneScope` already has `using SocialUniverse.Progression;` (it calls `AvatarAssignment`) and `using SocialUniverse.Core;` (it calls `EventBus`), so no new usings are required. Verify both are present; add whichever is missing.

- [ ] **Step 4: Verify compilation**

Unity MCP: `refresh_unity` (compile=request, mode=force, scope=scripts), then `read_console` (types=["error"]).
Expected: 0 errors.

- [ ] **Step 5: Commit**

```bash
git add Assets/_Project/Scripts/Core/ShowProfileOnboardingEvent.cs \
        Assets/_Project/Scripts/App/PlanetSceneScope.cs
git commit -m "feat: publish ShowProfileOnboardingEvent for nameless first-time accounts"
```

---

### Task 3: `AvatarSelectionModal` onboarding mode

**Files:**
- Modify: `Assets/_Project/Scripts/UI/AvatarSelectionModal.cs`

**Interfaces:**
- Consumes: nothing new.
- Produces: `void AvatarSelectionModal.OpenForOnboarding()` (opens the modal and hides the optional close button, locking dismissal); `void AvatarSelectionModal.EndOnboarding()` (releases the lock and restores the close button). `Close()` becomes a no-op while onboarding.

- [ ] **Step 1: Add the onboarding fields**

In the serialized-field block (currently `_gridContainer`, `_avatarPreview`, `_avatarButtonPrefab`, `_statusText`), add an optional close-button reference; and add a private flag near `_selectedAvatarId`:

```csharp
        [SerializeField] private Button    _closeButton;          // optional; hidden during onboarding
```

```csharp
        private bool _onboarding;
```

(`using UnityEngine.UI;` is already present for `Button`/`Image`.)

- [ ] **Step 2: Add OpenForOnboarding / EndOnboarding and guard Close**

Replace the existing `Close`:

```csharp
        public void Close() => gameObject.SetActive(false);
```

with:

```csharp
        public void OpenForOnboarding()
        {
            _onboarding = true;
            if (_closeButton != null) _closeButton.gameObject.SetActive(false);
            Open();
        }

        // Releases the non-dismiss lock so Close() (called by UpdateAvatar on a
        // successful commit) can actually hide the modal. Safe to call when not onboarding.
        public void EndOnboarding()
        {
            _onboarding = false;
            if (_closeButton != null) _closeButton.gameObject.SetActive(true);
        }

        public void Close()
        {
            if (_onboarding) return;
            gameObject.SetActive(false);
        }
```

- [ ] **Step 3: Verify compilation**

Unity MCP: `refresh_unity` (compile=request, mode=force, scope=scripts), then `read_console` (types=["error"]).
Expected: 0 errors.

- [ ] **Step 4: Commit**

```bash
git add Assets/_Project/Scripts/UI/AvatarSelectionModal.cs
git commit -m "feat: AvatarSelectionModal onboarding mode (non-dismissable)"
```

---

### Task 4: `DisplayNameModal` onboarding mode

**Files:**
- Modify: `Assets/_Project/Scripts/UI/DisplayNameModal.cs`

**Interfaces:**
- Consumes: `AvatarSelectionModal.EndOnboarding()` (Task 3), `AvatarSelectionModal.UpdateAvatar()` (existing).
- Produces: `void DisplayNameModal.OpenForOnboarding()` — activates the modal, hides Cancel, clears the name field, and locks dismissal until a valid name is committed.

- [ ] **Step 1: Add the onboarding flag**

Add near the injected fields:

```csharp
        private bool _onboarding;
```

- [ ] **Step 2: Add OpenForOnboarding and guard Close**

Replace the existing `Close`:

```csharp
        public void Close()
        {
            _audio.PlaySfx(SfxId.Cancel);
            gameObject.SetActive(false);
        }
```

with:

```csharp
        public void OpenForOnboarding()
        {
            _onboarding = true;
            if (_cancelButton != null) _cancelButton.gameObject.SetActive(false);
            gameObject.SetActive(true);
            _audio.PlaySfx(SfxId.OpenPanel);
            _nameInput.text  = "";   // force the player to choose a name
            _statusText.text = "Choose a display name to get started";
        }

        public void Close()
        {
            if (_onboarding) return;
            _audio.PlaySfx(SfxId.Cancel);
            gameObject.SetActive(false);
        }
```

- [ ] **Step 3: Skip the "name unchanged" shortcut during onboarding**

The early-return that treats an unchanged name as a no-op must not fire during onboarding
(the field starts empty and the player must commit a real name). Change:

```csharp
            if (name == _playerState.DisplayName)
            {
                _audio.PlaySfx(SfxId.Confirm);
                _avatarSelectionModal.UpdateAvatar();
                return;
            }
```

to:

```csharp
            if (!_onboarding && name == _playerState.DisplayName)
            {
                _audio.PlaySfx(SfxId.Confirm);
                _avatarSelectionModal.UpdateAvatar();
                return;
            }
```

- [ ] **Step 4: Release the onboarding lock on a successful commit**

In `OnConfirmClicked`, the success branch currently reads:

```csharp
                if (result == null || result.Success)
                {
                    string committed = result?.DisplayName ?? name;
                    _playerState.SetDisplayName(committed);
                    await _auth.UpdateDisplayNameAsync(committed);

                    _avatarSelectionModal.UpdateAvatar();
                    _audio.PlaySfx(SfxId.Confirm);

                    Close();
                }
```

Replace it with:

```csharp
                if (result == null || result.Success)
                {
                    string committed = result?.DisplayName ?? name;
                    _playerState.SetDisplayName(committed);
                    await _auth.UpdateDisplayNameAsync(committed);

                    // Onboarding commits name + avatar together, then releases the
                    // non-dismiss lock on BOTH modals so they can close.
                    _avatarSelectionModal.EndOnboarding();
                    _avatarSelectionModal.UpdateAvatar();

                    if (_onboarding)
                    {
                        _onboarding = false;
                        if (_cancelButton != null) _cancelButton.gameObject.SetActive(true);
                    }

                    _audio.PlaySfx(SfxId.Confirm);
                    Close();
                }
```

- [ ] **Step 5: Verify compilation**

Unity MCP: `refresh_unity` (compile=request, mode=force, scope=scripts), then `read_console` (types=["error"]).
Expected: 0 errors.

- [ ] **Step 6: Commit**

```bash
git add Assets/_Project/Scripts/UI/DisplayNameModal.cs
git commit -m "feat: DisplayNameModal onboarding mode (name required, non-dismissable)"
```

---

### Task 5: `HUDController` opens the onboarding modal

**Files:**
- Modify: `Assets/_Project/Scripts/UI/HUDController.cs` (`Start` subscribe ~line 73; `OnDestroy` unsubscribe ~line 109; new handler beside `OnShowEmailVerificationPrompt` ~line 113)

**Interfaces:**
- Consumes: `ShowProfileOnboardingEvent` (Task 2), `AvatarSelectionModal.OpenForOnboarding()` (Task 3), `DisplayNameModal.OpenForOnboarding()` (Task 4).
- Produces: nothing.

- [ ] **Step 1: Subscribe in Start**

After the existing line:

```csharp
            EventBus.Subscribe<ShowEmailVerificationPromptEvent>(OnShowEmailVerificationPrompt);
```

add:

```csharp
            EventBus.Subscribe<ShowProfileOnboardingEvent>(OnShowProfileOnboarding);
```

- [ ] **Step 2: Unsubscribe in OnDestroy**

After the existing line:

```csharp
            EventBus.Unsubscribe<ShowEmailVerificationPromptEvent>(OnShowEmailVerificationPrompt);
```

add:

```csharp
            EventBus.Unsubscribe<ShowProfileOnboardingEvent>(OnShowProfileOnboarding);
```

- [ ] **Step 3: Add the handler**

Beside `OnShowEmailVerificationPrompt`, add:

```csharp
        private void OnShowProfileOnboarding(ShowProfileOnboardingEvent _)
        {
            // Mirrors the HUD avatar-button flow (opens both modals together), but in
            // mandatory onboarding mode: Cancel hidden, a valid name required.
            _avatarSelectionModal?.OpenForOnboarding();
            _displayNameModal?.OpenForOnboarding();
        }
```

- [ ] **Step 4: Verify compilation**

Unity MCP: `refresh_unity` (compile=request, mode=force, scope=scripts), then `read_console` (types=["error"]).
Expected: 0 errors.

- [ ] **Step 5: Commit**

```bash
git add Assets/_Project/Scripts/UI/HUDController.cs
git commit -m "feat: HUDController shows onboarding modal for nameless first-time players"
```

---

### Task 6: Scene wiring + verification

**Files:**
- Modify (scene): `Assets/Scenes/Planet.unity` — assign `AvatarSelectionModal._closeButton` (optional but recommended)

**Interfaces:**
- Consumes: all of the above.
- Produces: a verified, working feature.

- [ ] **Step 1: Wire the optional close button**

Open `Assets/Scenes/Planet.unity`. On the GameObject holding `AvatarSelectionModal`, drag the panel's existing close/X button (if one exists) into the new `Close Button` field. If the panel has no dedicated close button, leave it unassigned — dismissal is already blocked by the `Close()` guard; this field only hides a visible button during onboarding. Save the scene.

- [ ] **Step 2: Confirm EditMode tests still pass**

Run the full EditMode suite via Unity MCP `run_tests` (mode=`EditMode`).
Expected: PASS, including `ProfileOnboardingTests` (7) — no regressions in the existing suite.

- [ ] **Step 3: Manual UX verification (temporary debug trigger)**

Google sign-in throws in the Unity Editor, so trigger the modal directly instead. Enter Play mode into the Planet scene, then publish the event once from a temporary hook — e.g. add a throwaway `[MenuItem("Debug/Show Onboarding")]` that calls `EventBus.Publish(new ShowProfileOnboardingEvent());`, invoke it, and confirm:
  - The avatar grid + name field appear together.
  - Cancel is hidden and the panel cannot be dismissed by tapping out or the close button.
  - Confirming an empty/1-char name shows the "at least 2 characters" error and does not close.
  - Confirming a valid name commits, the modal closes, and the HUD username/avatar update.
Remove the temporary debug hook afterward.

- [ ] **Step 4: Device smoke test (deferred, user-owned)**

On a real device (Google sign-in is unavailable in-editor): register/sign in with a **new** Google account → onboarding modal appears, requires a name, enters the game; sign out and sign in again → no modal (name now set). This is the same device-test pass already tracked for the Firebase auth branch.

- [ ] **Step 5: Commit the scene wiring**

```bash
git add Assets/Scenes/Planet.unity
git commit -m "chore: wire AvatarSelectionModal close button for onboarding mode"
```

---

## Notes / Risks

- **EventBus timing:** the onboarding event is published inside `HydrateServerStateAsync`, the same phase where `ShowEmailVerificationPromptEvent` is already published and reliably received by `HUDController`. If a future refactor changes when the HUD subscribes, both prompts would be affected together.
- **No email-verify collision:** onboarding-eligible accounts are Google (email-verified), and the verify prompt only fires for unverified accounts, so the two modals never open together in practice. If they ever did, they are separate GameObjects and would stack — acceptable, out of scope.
- **`"Player"` edge case:** a player who literally types `Player` is treated as nameless by `NeedsOnboarding`. Accepted in the spec.
- **`_closeButton` is nullable by design:** the feature is correct without it (the `Close()` guard blocks dismissal); the reference only improves the visuals by hiding a button.
