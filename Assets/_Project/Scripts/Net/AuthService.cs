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
        private Task _playerNameFetch;

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
            // Hydrate the player name BEFORE raising OnSignedIn: AuthScreen
            // publishes PlayerReadyEvent from that callback, and
            // SocialServicesInitializer immediately bakes DisplayName into the
            // Vivox login, which is session-locked — a name that arrives late
            // can never be applied.
            AuthenticationService.Instance.SignedIn += async () =>
            {
                await HydratePlayerNameAsync();
                OnSignedIn?.Invoke();
            };
            AuthenticationService.Instance.SignInFailed += e => OnSignInFailed?.Invoke(e);
            return Task.CompletedTask;
        }

        // UGS's PlayerName property is a lazy local cache: it returns null until
        // GetPlayerNameAsync or UpdatePlayerNameAsync runs in the current
        // session, so without this fetch every launch after the registration
        // session sees a null name. autoGenerate: false keeps guests who never
        // chose a name on the "Player" placeholder instead of a random
        // UGS-generated one (the API returns null on 404 in that case).
        // Non-fatal: on failure the existing fallback chain still applies.
        private Task HydratePlayerNameAsync()
        {
            if (_playerNameFetch == null || _playerNameFetch.IsCompleted)
                _playerNameFetch = FetchPlayerNameAsync();
            return _playerNameFetch;
        }

        private async Task FetchPlayerNameAsync()
        {
            try
            {
                await AuthenticationService.Instance.GetPlayerNameAsync(autoGenerate: false);
            }
            catch (Exception ex)
            {
                SULog.Warn($"AuthService: player name fetch failed ({ex.Message})", SULog.Channel.Net);
            }
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
                // BootState publishes PlayerReadyEvent as soon as this returns
                // (the Auth scene is skipped, so the SignedIn-callback hydration
                // above may still be in flight) — await it here so the name is
                // cached before the Vivox login reads it.
                await HydratePlayerNameAsync();
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
            // The SignedIn-callback hydration is in flight for this brand-new
            // account and will 404 (no name yet), which clears the UGS name
            // cache — await it before UpdatePlayerNameAsync so its stale
            // response can't land afterwards and wipe the freshly-set name.
            await HydratePlayerNameAsync();
            if (!string.IsNullOrEmpty(username))
                await AuthenticationService.Instance.UpdatePlayerNameAsync(username);

            if (string.IsNullOrEmpty(PlayerId))
                throw new InvalidOperationException(
                    "PlayerId is null after sign-up — UGS auth token not yet available; cannot call SaveEmail");

            _email = email;
            try
            {
                await _backend.CallAsync("SaveEmail",
                    new Dictionary<string, object> { { "email", email } });
            }
            catch (Exception ex)
            {
                // The UGS account already exists at this point (sign-up above succeeded),
                // so a SaveEmail failure must not fail the whole registration — that would
                // strand the account (a retry hits ENTITY_EXISTS, but email/profile/reset-index
                // never get saved). player_profile.email and the reset index are only needed
                // for forgot-password; sign-in/sign-up remain fully functional without them.
                SULog.Warn($"SaveEmail failed after registration (playerId: {PlayerId}): {ex.Message}", SULog.Channel.Net);
            }

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

        public async Task RequestEmailVerificationCodeAsync()
        {
            await _backend.CallAsync("RequestEmailVerificationCode");
            SULog.Info($"Email verification code requested (playerId: {PlayerId})", SULog.Channel.Net);
        }

        public async Task ConfirmEmailVerificationCodeAsync(string code)
        {
            await _backend.CallAsync("ConfirmEmailVerificationCode",
                new Dictionary<string, object> { { "code", code } });
            SULog.Info("Email verification code confirmed", SULog.Channel.Net);
        }
    }
}
