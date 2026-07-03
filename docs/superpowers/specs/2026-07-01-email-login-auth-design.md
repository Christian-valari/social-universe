# Email/Username/Password Auth Design

**Date:** 2026-07-01
**Status:** Approved (pending spec self-review)

## Purpose

Change signup to collect email, username, and password, and change login to
use email + password instead of username + password. This is a login-identity
change, not a net-new feature — signup already collects username, email, and
password (plus a separate display name); login currently uses username +
password.

## Current State (as of this design)

- `IAuthService.RegisterAsync(username, password, email, displayName)` —
  calls UGS `SignUpWithUsernamePasswordAsync(username, password)`, then
  `UpdatePlayerNameAsync(displayName)`, then the `SaveEmail` Cloud Code
  function (writes the email to the player's Cloud Save profile and an
  `idx_email_<hash>` reverse-lookup index used by password reset).
- `IAuthService.SignInWithCredentialsAsync(username, password)` — calls UGS
  `SignInWithUsernamePasswordAsync(username, password)` directly.
- `LocalMockAuthService` mirrors this in-memory, keyed by username.
- Forgot-password (`RequestPasswordResetAsync`/`ConfirmPasswordResetAsync`)
  is already email-based and unaffected by this change.
- No automated tests currently reference any of the auth types being changed.
- Pre-launch: no real UGS accounts exist yet, so no data migration is needed.

## Key Constraint

UGS's built-in username/password authentication has no native "sign in by
email" call — only sign-in-by-username. UGS usernames must be 3-20 characters
from `[a-zA-Z0-9.\-@_]` and are enforced unique per project. Real email
addresses often exceed 20 characters, so an email cannot be passed directly
into the username slot.

## Decisions

1. **Username becomes a purely cosmetic display handle.** It plays no role
   in authentication and is not required to be unique. It replaces the
   previous separate "Display Name" field — one field, not two.
2. **Client-side password validation is updated to match UGS's real policy**:
   8-30 characters, at least one uppercase, one lowercase, one number, one
   symbol. (Previously only checked 6+ characters, which could pass client
   validation and still be rejected server-side.)
3. **Login identity is a deterministic hash of the email**, computed
   client-side, used as the opaque "username" UGS's SDK requires — see
   Approach below. No server-side migration is needed since there are no
   real accounts yet.

## Approach: Deterministic Email-Hash as UGS Login Key

```
loginKey = HexEncode(SHA256(email.Trim().ToLowerInvariant()))[:20]
```

This fits UGS's username constraints (3-20 chars, allowed charset is a
subset of hex) and is fully deterministic: login recomputes it from the
typed email with no network lookup required. As a side effect, UGS's own
uniqueness enforcement on this key gives free "email already registered"
enforcement at signup, with negligible collision risk (80 bits of entropy
truncated from SHA-256).

Two alternative approaches were considered and rejected:
- **Cloud Code email→username lookup before login** (sign in anonymously,
  resolve email→username via the existing index, then sign in for real) —
  rejected: 3 network round-trips per login instead of 1, creates a
  throwaway anonymous UGS identity per attempt, and only preserves "real
  usernames as the login key," which no longer matters once username is
  cosmetic-only.
- **Fully custom email/password auth** (bypass UGS's built-in password auth,
  implement server-side credential storage/verification) — rejected:
  reimplements password security (salting, rate-limiting, breach handling)
  that UGS provides for free, for no benefit given the constraints here.

## Interface Changes

`Core/IAuthService.cs`:
- `SignInWithCredentialsAsync(string username, string password)` →
  `SignInWithEmailAsync(string email, string password)`
- `RegisterAsync(string username, string password, string email, string displayName)`
  → `RegisterAsync(string username, string password, string email)`
- `Username` and `DisplayName` properties are unchanged in shape but are now
  always equal (both reflect the cosmetic handle).

## Data Flow

**Signup:**
1. `AuthScreen` collects username, email, password → calls
   `RegisterAsync(username, password, email)`.
2. `AuthService` derives `loginKey` from email, calls UGS
   `SignUpWithUsernamePasswordAsync(loginKey, password)`.
3. Calls `UpdatePlayerNameAsync(username)` (existing call; now carries the
   cosmetic name).
4. Calls the existing `SaveEmail` Cloud Code function, unchanged.

**Login:**
1. `AuthScreen` collects email + password → calls
   `SignInWithEmailAsync(email, password)`.
2. `AuthService` derives the same `loginKey`, calls UGS
   `SignInWithUsernamePasswordAsync(loginKey, password)`.

**Forgot password:** unchanged — already keyed by email via `SaveEmail`'s
reverse index.

## Components

| File | Change |
|---|---|
| `Core/IAuthService.cs` | Rename/update method signatures per Interface Changes above. |
| `Net/AuthService.cs` | Add private `DeriveLoginKey(email)` (SHA-256 → 20 hex chars). Implement updated methods per the data flow above. |
| `Net/LocalMockAuthService.cs` | Mirror the same behavior: `_users` dictionary keyed by **email** instead of username. Register throws if the email is already taken; username is stored purely as the cosmetic name. |
| `UI/AuthScreen.cs` | Login panel: replace the username input with an email input, validate as email, call `SignInWithEmailAsync`. Register panel: drop the separate Display Name field; the existing username field becomes the sole cosmetic-name input; `OnRegisterClicked` calls the 3-arg `RegisterAsync`. Replace the username-length check in `ValidateCredentials` with the existing `ValidateDisplayName` rules (2-20 chars) applied to username. Replace the password length check with the new UGS-matching rule (8-30 chars, upper+lower+number+symbol). |
| `Assets/Scenes/Auth.unity` | Login panel's username `InputField` GameObject relabeled/repurposed as an email field (placeholder text, field name) — a scene edit, done via Unity MCP tools during implementation, not a code change. |
| `ServerCode/*.js` | **No changes.** `SaveEmail`, `RequestPasswordReset`, `ConfirmPasswordReset` already operate on email. |

## Error Handling

- **Login failure** (wrong password, or email never registered): both map
  to the same generic **"Incorrect email or password"** — deliberately
  indistinguishable, so a failed login can't be used to enumerate registered
  emails.
- **Signup with an already-registered email**: UGS's uniqueness check on the
  derived login key rejects it naturally (`EntityExists`/"already taken"
  style error) → mapped to **"An account with that email already exists."**
- **Signup with invalid password format**: caught client-side before
  submission; if it somehow reaches the server, mapped to **"Password must
  be 8-30 characters with uppercase, lowercase, a number, and a symbol."**
- **Hash collision** (two different emails hashing to the same key): not
  handled specially — negligible probability at 80 bits of entropy; would
  surface as the ordinary "account already exists" error on signup if it
  ever occurred.
- **Forgot-password flow**: untouched, keeps its existing enumeration-safe
  behavior (`RequestPasswordReset` always returns success regardless of
  whether the email is registered).

## Testing

No automated tests currently exist for auth, and this design does not
introduce new test infrastructure wholesale. Verification plan:

- **Manual verification** in the Editor using `LocalMockAuthService`: sign
  up with username+email+password → confirm account created and signed in;
  sign out; log back in with email+password → succeeds; wrong password →
  generic error; duplicate email at signup → blocked with the friendly
  message.
- **One targeted unit test**: `DeriveLoginKey` is pure logic and easy to get
  subtly wrong (normalization, truncation length, charset) without it being
  obvious from reading the UI. Add an EditMode test asserting it's
  deterministic, case/whitespace-insensitive, and produces UGS-valid output
  (3-20 chars, allowed charset).

## Out of Scope

- Migrating existing real accounts (none exist yet).
- Any change to the forgot-password flow.
- Any change to Google/Apple/guest sign-in paths.
- Broader automated test coverage beyond the one targeted unit test above.
