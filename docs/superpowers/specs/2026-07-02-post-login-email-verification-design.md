# Post-Login Email Verification — Design

## Context

Email verification on registration was originally built as a pre-account gate: the player had to request and confirm a 6-digit code before `RegisterAsync` (account creation) would run. That design required inventing a Cloud Save Custom Data scheme keyed by an email hash, because no player account existed yet to scope a normal player-level Cloud Save key to — and that scheme accumulated real complexity (a 60-second per-email cooldown to bound abuse against arbitrary third-party addresses, then a further revision to per-email-keyed Custom Data items after a final review found the original shared-map design could grow unbounded and lose data under concurrent registrations).

The project owner has decided to move verification to **after** login instead: registration succeeds immediately, and a one-time prompt offers to verify the player's email post-login. This is a product/UX decision (lower signup friction), not a technical requirement — Cloud Code secret access is unrelated to whether the calling player is anonymous or fully registered, so this change does not affect (and was not motivated by) the separate `RESEND_API_KEY`/`RESET_FROM_EMAIL` dashboard configuration issue found during testing.

## Goal

Prove a player's email address is reachable, without blocking account creation. Prompt once, right after first login; let them skip; let them verify later from the HUD if they skipped.

## Key Simplification

Because verification now always happens against an **already-authenticated real player**, the Cloud Code side collapses back to the same player-scoped Cloud Save pattern already used by `RequestPasswordReset.js`/`ConfirmPasswordReset.js` — no Custom Data, no email-hash keys, no shared-item concurrency concerns, and no client-supplied email (the server reads the player's own saved email from `player_profile`, which is more correct than trusting a client-supplied address for an already-identified account). All three problems the final review found in the old design (unbounded growth, cross-request races, plan-mandated cooldown-as-abuse-mitigant) are structurally eliminated, not patched.

## What's Removed

- `AuthScreen`'s Register-panel code field (`_regCodeField`), "Send Code" button (`_sendVerificationCodeButton`), and the `ConfirmEmailVerificationCodeAsync`-then-`RegisterAsync` gating in `OnRegisterClicked`. Registration reverts to a single direct call to `RegisterAsync`.
- `AuthService.RegisterAsync`'s use of `AddUsernamePasswordAsync` reverts to `SignUpWithUsernamePasswordAsync` — that swap existed only to accommodate the anonymous pre-auth session the old pre-registration verification step required. A fresh registration is once again a genuinely signed-out → signed-up transition.
- The Custom Data storage design in `RequestEmailVerificationCode.js`/`ConfirmEmailVerificationCode.js` (customId `"email_verification"`, per-email keys) is replaced entirely.

## What's Kept Unchanged

- `AuthScreen.EnsureSessionAsync` / `_suppressAutoTransition` and its use in `OnSendResetCodeClicked`/`OnResetPasswordClicked` (Forgot Password). That fix is independent of this redesign — a player who forgot their password is definitionally signed out, and Cloud Code still requires *some* authenticated session for those two calls.
- `RequestPasswordReset.js`/`ConfirmPasswordReset.js` — untouched.
- `IAuthService.RequestPasswordResetAsync`/`ConfirmPasswordResetAsync` — untouched.

## Components

### 1. Cloud Code (rewritten)

**`RequestEmailVerificationCode.js`** — no params. Reads the calling player's own `player_profile.email` (set by `SaveEmail.js` at registration) rather than trusting a client-supplied address. Generates a 6-digit OTP, stores it under a new player-scoped Cloud Save key `email_verify_otp` (`{ otp, expiresAt, requestedAt }`, same shape/TTL as before — 15 minutes), keeps a lightweight 60-second per-player cooldown (now purely a cost/accidental-double-click guard, not an abuse mitigant, since the caller is an identified account). Throws `"No email on file for this account"` if `player_profile.email` is somehow unset (shouldn't happen in practice — `SaveEmail.js` runs at registration — but `AuthService.RegisterAsync` already tolerates `SaveEmail` failing without blocking registration, so this is a real, reachable edge case).

**`ConfirmEmailVerificationCode.js`** — params `{ code }` only. Reads `email_verify_otp` from the same player-scoped key, validates (same three failure messages as before: no pending code / expired / wrong code), and on success writes `player_profile.emailVerified = true` (merge-write, same pattern as `SaveEmail.js`/`UpdateProfile.js`) and clears `email_verify_otp` via the null-sentinel pattern (no `deleteItem` — same convention already established in this codebase).

Both files keep the "Known Issue #6" `DataApi(context)` + positional-args convention.

### 2. `IAuthService` / `AuthService` / `LocalMockAuthService` (simplified signatures)

```csharp
Task RequestEmailVerificationCodeAsync();          // was Task RequestEmailVerificationCodeAsync(string email)
Task ConfirmEmailVerificationCodeAsync(string code); // was Task ConfirmEmailVerificationCodeAsync(string email, string code)
```

`AuthService`'s implementations become simpler thin passthroughs (no `email` arg to forward). `LocalMockAuthService`'s mock logic keeps the same pending/single-use/mock-code-"123456" behavior, just without the email keying (a single pending-code flag is enough since the mock only ever represents one signed-in player at a time).

### 3. `PlayerProfile` (client DTO) + `GetPlayerProfile.js`

Add `public bool EmailVerified;` to `PlayerProfile.cs`. `GetPlayerProfile.js` returns `emailVerified: profile?.emailVerified ?? false` alongside its existing fields.

### 4. `PlayerState`

Add `IsEmailVerified` (bool, default false) + `SetEmailVerified(bool)` + `OnEmailVerifiedChanged` event, mirroring the existing `DisplayName`/`SetDisplayName`/`OnDisplayNameChanged` triplet exactly.

### 5. `PlanetSceneScope.HydrateServerStateAsync` (extended)

After the existing `GetPlayerProfile` call (which already hydrates display name), also call `_playerState.SetEmailVerified(profile.EmailVerified)`. Then: if `!profile.EmailVerified` and a local PlayerPrefs flag `email_verification_prompted_<playerId>` is not set, publish a new `ShowEmailVerificationPromptEvent` and set that PlayerPrefs flag (so the prompt fires at most once ever, per player, per device — a deliberate, acceptable simplification over server-side "prompted" tracking, since the cost of showing it again on a reinstall/new device is low and it avoids a second server round-trip and a second Cloud Code function just for UI nagging state).

### 6. `EmailVerificationModal` (new, mirrors `DisplayNameModal.cs`'s exact pattern)

```csharp
[SerializeField] private TMP_InputField _codeInput;
[SerializeField] private Button         _sendCodeButton;
[SerializeField] private Button         _verifyButton;
[SerializeField] private Button         _closeButton;   // labeled "Close" always — doubles as "skip" on the auto-prompt and "cancel" when opened manually; no dynamic relabeling
[SerializeField] private TMP_Text       _statusText;

[Inject] private IAuthService _auth;
[Inject] private PlayerState  _playerState;
```

`Open()`: if `_playerState.IsEmailVerified`, show a "Your email is verified" status with only the close button enabled (code input/send/verify hidden or disabled). Otherwise show the normal flow: "Send Code" → status update → enter code → "Verify" → on success, `_playerState.SetEmailVerified(true)` and close. Same `FriendlyError`-style message mapping as `AuthScreen` for the three throw strings (no verification code / expired / invalid) — duplicated locally (small, self-contained modal) rather than sharing `AuthScreen`'s private static method across namespaces.

### 7. `HUDController` (extended)

- New serialized fields: `_emailVerificationModal` (the new modal), `_verifyEmailButton` (always-visible HUD button, mirrors `_usernameButton`'s existing wiring style) that calls `_emailVerificationModal.Open()`.
- Subscribe to `ShowEmailVerificationPromptEvent` in `Start()` (alongside its existing subscriptions) to auto-open the modal once; unsubscribe in `OnDestroy()`.

### 8. `ShowEmailVerificationPromptEvent` (new, empty struct — same shape as `PlayerReadyEvent`)

Lives alongside `PlayerReadyEvent.cs` in `Core/`.

## Data Flow

**First login after registration:** `AuthScreen.OnRegisterClicked` → `RegisterAsync` (no verification step) → `PlayerReadyEvent` → `AuthState` → Planet scene → `PlanetSceneScope.HydrateServerStateAsync` fetches profile, sees `emailVerified: false`, no local "prompted" flag yet → publishes `ShowEmailVerificationPromptEvent` and sets the flag → `HUDController` opens `EmailVerificationModal` → player sends/confirms code or clicks Skip/Close.

**Later, from the HUD:** player clicks `_verifyEmailButton` at any time → same modal, same flow, but never auto-triggered again once the one-time flag is set.

## Error Handling

Three Cloud Code throw messages, updated to drop the email reference now that there's no `email` param: `"No verification code requested"`, `"Verification code has expired — request a new one"`, `"Invalid verification code"`. Mapped to friendly UI copy inside `EmailVerificationModal` the same way `AuthScreen.FriendlyError` does today. New failure mode: `"No email on file for this account"` (the edge case where `SaveEmail` failed at registration) — mapped to something like "We don't have an email on file — please contact support" (no self-service recovery path exists for this edge case in scope; it's rare and already logged server-side via `SULog.Warn` in `RegisterAsync`).

## Testing

- `LocalMockAuthServiceTests.cs` (already exists from the prior design) gets updated for the new no-email signatures — same four cases (correct code succeeds, no-request throws, wrong code throws, single-use), same mock-code-"123456" convention.
- No automated coverage for the Cloud Code files or `EmailVerificationModal`/`HUDController` changes (consistent with this codebase's existing convention — Cloud Code and MonoBehaviour UI pieces are manually/PlayMode-verified, not unit tested).
- Manual verification checklist (Play Mode, mock backend): register a new account → confirm no code is required → confirm the verification modal auto-opens once → skip it → confirm it does NOT reopen on a subsequent HUD load → open it manually from the HUD button → complete verification with the mock code → confirm `IsEmailVerified` flips and the modal shows the "verified" state on next open. Then a second pass against the real backend (once `RESEND_API_KEY`/`RESET_FROM_EMAIL` are configured in the dashboard) for the actual email delivery and real error strings.
