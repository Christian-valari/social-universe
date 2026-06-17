using System;
using System.Threading.Tasks;

namespace SocialUniverse.Core
{
    public interface IAuthService
    {
        bool   IsSignedIn        { get; }
        bool   SessionTokenExists { get; }
        string PlayerId          { get; }
        string Username          { get; }     // null for anonymous/SSO accounts (login credential)
        string DisplayName       { get; }     // in-game display name shown to other players

        event Action            OnSignedIn;
        event Action<Exception> OnSignInFailed;

        Task InitializeAsync();

        // Attempts to resume a previously authenticated session using the cached
        // session token. Returns true if the player is signed in afterwards.
        Task<bool> TryAutoSignInAsync();

        Task SignInAnonymouslyAsync();
        Task SignInWithCredentialsAsync(string username, string password);
        Task RegisterAsync(string username, string password, string displayName);
        Task SignInWithAppleAsync(string idToken);
        Task SignInWithGoogleAsync(string idToken);
        Task SignOutAsync();

        // Updates the display name stored in the auth layer (UGS PlayerName / local prefs).
        // The ProfileService also persists this to the game profile for cross-player visibility.
        Task UpdateDisplayNameAsync(string displayName);
    }
}
