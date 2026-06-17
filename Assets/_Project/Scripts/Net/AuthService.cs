using System;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using SocialUniverse.Core;

namespace SocialUniverse.Net
{
    public class AuthService : IAuthService
    {
        public bool   IsSignedIn         => AuthenticationService.Instance.IsSignedIn;
        public bool   SessionTokenExists => AuthenticationService.Instance.SessionTokenExists;
        public string PlayerId           => AuthenticationService.Instance.PlayerId;
        public string Username           => AuthenticationService.Instance.PlayerName;
        public string DisplayName        => AuthenticationService.Instance.PlayerName;

        public event Action            OnSignedIn;
        public event Action<Exception> OnSignInFailed;

        public AuthService() { }

        public Task InitializeAsync()
        {
            AuthenticationService.Instance.SignedIn     += () => OnSignedIn?.Invoke();
            AuthenticationService.Instance.SignInFailed += e  => OnSignInFailed?.Invoke(e);
            return Task.CompletedTask;
        }

        // Resumes the player's previous session via UGS's cached session token, if one
        // exists. SignInAnonymouslyAsync transparently restores the cached account
        // (regardless of how it was originally created — anonymous, username/password,
        // Apple, Google, …) instead of creating a new identity.
        public async Task<bool> TryAutoSignInAsync()
        {
            if (IsSignedIn) return true;
            if (!SessionTokenExists) return false;

            try
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
                SULog.Info($"Restored session (playerId: {PlayerId})", SULog.Channel.Net);
                return true;
            }
            catch (Exception ex)
            {
                SULog.Warn($"Failed to restore session: {ex.Message}", SULog.Channel.Net);
                return false;
            }
        }

        public async Task SignInAnonymouslyAsync()
        {
            if (IsSignedIn) return;
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
            SULog.Info($"Signed in anonymously (playerId: {PlayerId})", SULog.Channel.Net);
        }

        public async Task SignInWithCredentialsAsync(string username, string password)
        {
            await AuthenticationService.Instance.SignInWithUsernamePasswordAsync(username, password);
            SULog.Info($"Signed in with credentials (playerId: {PlayerId})", SULog.Channel.Net);
        }

        public async Task RegisterAsync(string username, string password, string displayName)
        {
            await AuthenticationService.Instance.SignUpWithUsernamePasswordAsync(username, password);
            if (!string.IsNullOrEmpty(displayName))
                await AuthenticationService.Instance.UpdatePlayerNameAsync(displayName);
            SULog.Info($"Registered new account (playerId: {PlayerId})", SULog.Channel.Net);
        }

        public async Task UpdateDisplayNameAsync(string displayName)
        {
            await AuthenticationService.Instance.UpdatePlayerNameAsync(displayName);
            SULog.Info($"Display name updated to '{displayName}'", SULog.Channel.Net);
        }

        public async Task SignInWithAppleAsync(string idToken)
        {
            await AuthenticationService.Instance.SignInWithAppleAsync(idToken);
            SULog.Info($"Signed in with Apple (playerId: {PlayerId})", SULog.Channel.Net);
        }

        public async Task SignInWithGoogleAsync(string idToken)
        {
            await AuthenticationService.Instance.SignInWithGoogleAsync(idToken);
            SULog.Info($"Signed in with Google (playerId: {PlayerId})", SULog.Channel.Net);
        }

        public Task SignOutAsync()
        {
            // clearCredentials: true so the cached session token is discarded too —
            // otherwise TryAutoSignInAsync would silently restore this session on next launch.
            AuthenticationService.Instance.SignOut(clearCredentials: true);
            SULog.Info("Signed out", SULog.Channel.Net);
            return Task.CompletedTask;
        }
    }
}
