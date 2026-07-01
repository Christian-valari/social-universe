using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using SocialUniverse.Core;

namespace SocialUniverse.Net
{
    public class AuthService : IAuthService
    {
        private readonly IBackendClient _backend;
        private string _email;

        public bool   IsSignedIn         => AuthenticationService.Instance.IsSignedIn;
        public bool   SessionTokenExists => AuthenticationService.Instance.SessionTokenExists;
        public string PlayerId           => AuthenticationService.Instance.PlayerId;
        public string Username           => AuthenticationService.Instance.PlayerName;
        public string DisplayName        => AuthenticationService.Instance.PlayerName;
        public string Email              => _email;

        public event Action            OnSignedIn;
        public event Action<Exception> OnSignInFailed;

        public AuthService(IBackendClient backend)
        {
            _backend = backend;
        }

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

        public async Task SignInWithEmailAsync(string email, string password)
        {
            string loginKey = EmailLoginKey.Derive(email);
            await AuthenticationService.Instance.SignInWithUsernamePasswordAsync(loginKey, password);
            _email = email;
            SULog.Info($"Signed in with email (playerId: {PlayerId})", SULog.Channel.Net);
        }

        public async Task RegisterAsync(string username, string password, string email)
        {
            string loginKey = EmailLoginKey.Derive(email);
            await AuthenticationService.Instance.SignUpWithUsernamePasswordAsync(loginKey, password);
            if (!string.IsNullOrEmpty(username))
                await AuthenticationService.Instance.UpdatePlayerNameAsync(username);

            if (string.IsNullOrEmpty(PlayerId))
                throw new InvalidOperationException(
                    "PlayerId is null after sign-up — UGS auth token not yet available; cannot call SaveEmail");
            await _backend.CallAsync("SaveEmail",
                new Dictionary<string, object> { { "email", email } });
            _email = email;

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
            _email = null;
            SULog.Info("Signed out", SULog.Channel.Net);
            return Task.CompletedTask;
        }

        public async Task RequestPasswordResetAsync(string email)
        {
            await _backend.CallAsync("RequestPasswordReset",
                new Dictionary<string, object> { { "email", email } });
            SULog.Info($"Password reset requested for {email}", SULog.Channel.Net);
        }

        public async Task ConfirmPasswordResetAsync(string email, string resetCode, string newPassword)
        {
            await _backend.CallAsync("ConfirmPasswordReset",
                new Dictionary<string, object> { { "email", email }, { "code", resetCode }, { "newPassword", newPassword } });
            SULog.Info("Password reset confirmed", SULog.Channel.Net);
        }
    }
}
