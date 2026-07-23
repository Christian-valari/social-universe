using System;
using System.Threading.Tasks;

namespace SocialUniverse.Core
{
    public interface IAuthService
    {
        bool   IsSignedIn        { get; }
        bool   SessionTokenExists { get; }
        string PlayerId          { get; }
        string Username          { get; }     // cosmetic handle, not used for sign-in; null for anonymous/SSO accounts
        string DisplayName       { get; }     // in-game display name shown to other players
        string Email             { get; }     // null for anonymous/SSO-only accounts; the sign-in identity for credential accounts

        event Action            OnSignedIn;
        event Action<Exception> OnSignInFailed;

        Task InitializeAsync();

        // Attempts to resume a previously authenticated session using the cached
        // session token. Returns true if the player is signed in afterwards.
        Task<bool> TryAutoSignInAsync();

        Task SignInAnonymouslyAsync();
        Task SignInWithEmailAsync(string email, string password);
        Task RegisterAsync(string username, string password, string email);
        Task SignInWithAppleAsync(string idToken);
        // Google Play Games sign-in. The string is now a Play Games *v2 server
        // auth code* (from GoogleAuthHandler.GetIdTokenAsync), exchanged by UGS's
        // SignInWithGooglePlayGamesAsync — v1 ID-token sign-in is deprecated and
        // blocked at Play upload. Name kept stable so existing callers (AuthScreen)
        // are untouched.
        Task SignInWithGoogleAsync(string authCode);
        Task SignOutAsync();

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

        // Updates the display name stored in the auth layer (UGS PlayerName / local prefs).
        // The ProfileService also persists this to the game profile for cross-player visibility.
        Task UpdateDisplayNameAsync(string displayName);

        // Password reset: client sends email; Cloud Code handles OTP generation/delivery/validation.
        Task RequestPasswordResetAsync(string email);
        Task ConfirmPasswordResetAsync(string email, string resetCode, string newPassword);

        // Post-login email verification: the server reads the caller's own saved
        // email (player_profile.email) rather than trusting a client-supplied
        // address, since the caller is already an authenticated player by the
        // time this is called — see EmailVerificationModal.
        Task RequestEmailVerificationCodeAsync();
        Task ConfirmEmailVerificationCodeAsync(string code);
    }
}
