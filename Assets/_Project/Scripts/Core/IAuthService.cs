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
