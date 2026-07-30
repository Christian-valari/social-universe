# First-Time Profile Onboarding (Name + Avatar) for Nameless Accounts

**Date:** 2026-07-30
**Branch:** `feature/firebase-auth-oidc`
**Status:** Approved design — pending implementation plan

## Problem

Email registration collects a username up front (`AuthService.RegisterAsync` →
`UpdatePlayerNameAsync(username)`), so email players always enter the game with an
in-game display name. **Google sign-in does not.** `SignInWithGoogleAsync` bridges the
Firebase token into a UGS session but never sets a player name, so a first-time Google
player lands in the social game with:

- no display name — `IAuthService.Username`/`DisplayName` both map to UGS
  `AuthenticationService.Instance.PlayerName`, which is `null` for a Google/SSO account
  (`GetPlayerNameAsync(autoGenerate: false)` won't invent one), so
  `ChatDisplayNameResolver.Resolve(...)` yields the `"Player"` fallback; and
- no chosen avatar (a random one is now auto-assigned during hydration — see the
  companion avatar-null fix — but the player never got to pick).

They should be required to set a display name (and may change the avatar) before playing.

## Goal

A first-time nameless account (Google today, any future OAuth/SSO provider) is shown a
**mandatory, non-dismissable** onboarding modal on entering the Hub/Planet scene, where
they set their display name and optionally change their avatar, then proceed into the game.
Returning players who already have a name are never prompted. Email registrants are never
prompted (they already chose a username).

## Non-Goals

- No new onboarding UI in the Auth scene (we reuse the existing in-game modals).
- No changes to the email registration or email-verification flows.
- No server/Cloud Code changes — name/avatar already commit through `ProfileService`.

## Detection

In `PlanetSceneScope.HydrateServerStateAsync` (`SocialUniverse.App`), after the display
name is resolved from profile → auth, compute whether the player has **no real name
anywhere**. The decision is extracted into a pure, Unity-free helper so it is unit-testable
(same pattern as `AvatarAssignment.ResolveAvatarId`):

```csharp
// SocialUniverse.Progression
public static class ProfileOnboarding
{
    // True when no real display name exists on the profile or the auth session,
    // i.e. the player has never chosen an in-game name (a fresh Google/SSO account).
    public static bool NeedsOnboarding(string profileDisplayName, string authDisplayName, string authUsername)
        => ChatDisplayNameResolver.Resolve(
               FirstNonEmpty(profileDisplayName, authDisplayName), authUsername)
           == ChatDisplayNameResolver.Fallback;
}
```

The helper reuses `ChatDisplayNameResolver`'s sanitize rules (trims, strips the UGS
`#1234` suffix, ignores whitespace-only), so a name of only a `#suffix` or spaces counts
as "no name". Accepted edge case: a player who literally types `"Player"` as their name is
treated as nameless — acceptable and rare.

If `NeedsOnboarding(...)` is true, `PlanetSceneScope` publishes `ShowProfileOnboardingEvent`
after the scene is marked ready (so the modal appears over a loaded game, consistent with
how `ShowEmailVerificationPromptEvent` is timed).

## Event

`SocialUniverse.Core.ShowProfileOnboardingEvent` — a parameterless event mirroring the
existing `ShowEmailVerificationPromptEvent`. Decouples the App layer (detection) from the
UI layer (presentation) via `EventBus`, per Architecture Rule #4.

## Presentation

`HUDController` (`SocialUniverse.UI`) subscribes to `ShowProfileOnboardingEvent` in `Start`
(unsubscribes in `OnDestroy`) and, on receipt, opens the avatar/name modal in **onboarding
mode**:

```csharp
private void OnShowProfileOnboarding(ShowProfileOnboardingEvent _)
{
    _avatarSelectionModal?.OpenForOnboarding();
    _displayNameModal?.OpenForOnboarding();
}
```

The HUD already holds serialized references to both modals.

### Onboarding mode on the modals

`AvatarSelectionModal` and `DisplayNameModal` gain an `OpenForOnboarding()` entry alongside
the existing `Open()` (which the HUD avatar/username buttons keep using unchanged). In
onboarding mode:

- **Non-dismissable:** the Cancel/close button is hidden and tap-out/`Close()` is a no-op,
  so the player cannot leave without setting a name.
- **Name required:** confirm runs the existing `DisplayNameValidator` rules and refuses to
  commit an empty/too-short/too-long/rejected name, showing the existing status text.
- **Commit path unchanged:** on a valid name, the existing
  `DisplayNameModal.OnConfirmClicked` flow commits the name via
  `ProfileService.UpdateDisplayNameAsync` + `IAuthService.UpdateDisplayNameAsync`, then
  calls `_avatarSelectionModal.UpdateAvatar()` to persist the chosen avatar, and closes.
- **Avatar default:** pre-selected to the random avatar assigned during hydration; the
  player may pick a different one from the grid before confirming.

A shared `_onboarding` flag on each modal gates the Cancel-hidden / no-dismiss behavior and
is cleared on successful commit.

## Interaction With the Email-Verification Prompt

No collision in practice: the verify-email prompt (`ShowEmailVerificationPromptEvent`) only
fires for **unverified** accounts, and Google accounts report a verified email. An
onboarding-eligible player (nameless Google account) is therefore always verified, so the
two prompts do not overlap. If a future provider were both unverified and nameless,
onboarding is published only when `NeedsOnboarding` is true and the verify prompt retains
its own independent guard; the two modals are separate GameObjects and would simply stack —
acceptable, and out of scope to sequence here.

## Error Handling

- **Mock/local backend:** `UpdateDisplayNameAsync` returns null → treated as success with
  the local name (existing behavior); onboarding still completes.
- **Name commit failure (server):** the modal stays open and shows the existing error
  status; the player retries. Because the modal is non-dismissable, they cannot enter the
  game with the fallback name after a failure.
- **Detection failure:** if profile fetch throws, hydration already logs a warning and
  proceeds; `NeedsOnboarding` is evaluated on whatever auth data is available, so a
  transient profile-fetch failure at worst prompts a player who has an auth name — a
  low-cost, self-correcting outcome (they confirm the pre-filled name).

## Testing

**EditMode unit tests** — `ProfileOnboardingTests` for `NeedsOnboarding`:

- profile has a name → false
- no profile name, auth has a name → false
- no profile name, auth username only → false
- all empty/null → true
- name is whitespace only → true
- name is a bare `#1234` suffix → true

**Manual PlayMode pass:**

- Google first login → onboarding modal appears over the loaded Hub, cannot be dismissed,
  requires a valid name, commits name + avatar, enters game.
- Sign out / Google login again → no onboarding modal (name now set).
- Email registrant → no onboarding modal (unchanged).

## Files

**New**
- `Assets/_Project/Scripts/Core/ShowProfileOnboardingEvent.cs`
- `Assets/_Project/Scripts/Progression/ProfileOnboarding.cs`
- `Assets/_Project/Tests/EditMode/Progression/ProfileOnboardingTests.cs` (alongside the existing `AvatarAssignmentTests.cs`, under the `SocialUniverse.Tests` asmdef)

**Edited**
- `Assets/_Project/Scripts/App/PlanetSceneScope.cs` — detect + publish after scene ready
- `Assets/_Project/Scripts/UI/HUDController.cs` — subscribe + open in onboarding mode
- `Assets/_Project/Scripts/UI/DisplayNameModal.cs` — `OpenForOnboarding()` + non-dismiss gate
- `Assets/_Project/Scripts/UI/AvatarSelectionModal.cs` — `OpenForOnboarding()` + hide Cancel

**Scene (verify during implementation)**
- `Assets/Scenes/Planet.unity` — confirm the modals' Cancel/close buttons can be hidden in
  onboarding mode; HUD already references both modals, so no new refs expected.

## Architecture Compliance

- **Server-authoritative:** name and avatar commit through `ProfileService` (Rule #1).
- **Decoupled via events:** App→UI handoff over `EventBus` (Rule #4).
- **No backend SDK in UI:** UI uses `IAuthService`/`ProfileService` abstractions (Rule #2).
- **Namespaces:** new types placed per the Project Structure table
  (`Core`, `Progression`, `UI`).
