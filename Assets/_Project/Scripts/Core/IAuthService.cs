using System;
using System.Threading.Tasks;

namespace SocialUniverse.Core
{
    public interface IAuthService
    {
        bool   IsSignedIn        { get; }
        bool   SessionTokenExists { get; }
        string PlayerId          { get; }

        event Action            OnSignedIn;
        event Action<Exception> OnSignInFailed;

        Task InitializeAsync();

        // Attempts to resume a previously authenticated session using the cached
        // session token. Returns true if the player is signed in afterwards.
        Task<bool> TryAutoSignInAsync();

        Task SignInAnonymouslyAsync();
        Task SignInWithCredentialsAsync(string username, string password);
        Task RegisterAsync(string username, string password);
        Task SignInWithAppleAsync(string idToken);
        Task SignInWithGoogleAsync(string idToken);
        Task SignOutAsync();
    }
}
