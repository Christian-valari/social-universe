using System;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using SocialUniverse.Core;

namespace SocialUniverse.Net
{
    // Firebase is the identity provider; UGS is bridged in via OpenID Connect.
    // Every sign-in path acquires a fresh Firebase ID token, then exchanges it
    // for a UGS session through SignInWithOpenIdConnectAsync.
    public class AuthService : IAuthService
    {
        private const string OidcProvider = "oidc-firebase";

        private Task _playerNameFetch;

        public bool   IsSignedIn         => AuthenticationService.Instance.IsSignedIn;
        public bool   SessionTokenExists => AuthenticationService.Instance.SessionTokenExists;
        public string PlayerId           => AuthenticationService.Instance.PlayerId;
        public string Username           => AuthenticationService.Instance.PlayerName;
        public string DisplayName        => AuthenticationService.Instance.PlayerName;
        public string Email              => FirebaseAuthHandler.CurrentEmail;
        public bool   IsEmailVerified    => FirebaseAuthHandler.IsEmailVerified;

        public event Action            OnSignedIn;
        public event Action<Exception> OnSignInFailed;

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

        private async Task BridgeToUgsAsync(string firebaseIdToken)
        {
            await AuthenticationService.Instance.SignInWithOpenIdConnectAsync(OidcProvider, firebaseIdToken);
            await HydratePlayerNameAsync();
        }

        public async Task SignInWithEmailAsync(string email, string password)
        {
            string idToken = await FirebaseAuthHandler.SignInEmailAsync(email, password);
            await BridgeToUgsAsync(idToken);
            SULog.Info($"Signed in with Firebase email (playerId: {PlayerId})", SULog.Channel.Net);
        }

        public async Task RegisterAsync(string username, string password, string email)
        {
            string idToken = await FirebaseAuthHandler.RegisterEmailAsync(email, password);
            await BridgeToUgsAsync(idToken);
            if (!string.IsNullOrEmpty(username))
                await AuthenticationService.Instance.UpdatePlayerNameAsync(username);
            await FirebaseAuthHandler.SendEmailVerificationAsync();
            SULog.Info($"Registered Firebase account (playerId: {PlayerId})", SULog.Channel.Net);
        }

        public async Task SignInWithGoogleAsync()
        {
            string idToken = await FirebaseAuthHandler.SignInGoogleAsync();
            await BridgeToUgsAsync(idToken);
            SULog.Info($"Signed in with Google via Firebase (playerId: {PlayerId})", SULog.Channel.Net);
        }

        public Task SignInWithAppleAsync(string idToken) =>
            throw new NotSupportedException("Apple sign-in is not yet implemented via Firebase");

        // Resumes a persisted Firebase session (if the SDK still holds a user)
        // and re-bridges it into a fresh UGS session via OIDC.
        public async Task<bool> TryAutoSignInAsync()
        {
            if (IsSignedIn) return true;
            if (!FirebaseAuthHandler.HasCurrentUser) return false;
            try
            {
                string idToken = await FirebaseAuthHandler.GetFreshIdTokenAsync();
                await BridgeToUgsAsync(idToken);
                // Freshen verification status now so IsEmailVerified is accurate
                // when BootState reads it right after — avoids sending someone
                // who verified out-of-band into the Verify-panel detour.
                await FirebaseAuthHandler.ReloadAndCheckVerifiedAsync();
                SULog.Info($"Restored Firebase session (playerId: {PlayerId})", SULog.Channel.Net);
                return true;
            }
            catch (Exception ex)
            {
                SULog.Warn($"Failed to restore session: {ex.Message}", SULog.Channel.Net);
                return false;
            }
        }

        public async Task UpdateDisplayNameAsync(string displayName)
        {
            await AuthenticationService.Instance.UpdatePlayerNameAsync(displayName);
            SULog.Info($"Display name updated to '{displayName}'", SULog.Channel.Net);
        }

        public Task RequestPasswordResetAsync(string email) => FirebaseAuthHandler.SendPasswordResetAsync(email);
        public Task SendEmailVerificationAsync()            => FirebaseAuthHandler.SendEmailVerificationAsync();
        public Task<bool> ReloadAndCheckVerifiedAsync()     => FirebaseAuthHandler.ReloadAndCheckVerifiedAsync();

        public async Task DeleteAccountAsync()
        {
            // Delete both sides: Firebase identity and UGS account/data. If the
            // Firebase delete fails (e.g. requires a recent login), sign the
            // Firebase user out anyway so a live Firebase session can never
            // survive a failed delete and get re-bridged into the now-deleted
            // UGS account on next launch (TryAutoSignInAsync would mint a
            // duplicate PlayerId).
            try { await FirebaseAuthHandler.DeleteCurrentUserAsync(); }
            catch (Exception ex)
            {
                SULog.Warn($"Firebase delete failed: {ex.Message}", SULog.Channel.Net);
                FirebaseAuthHandler.SignOut();
            }
            await AuthenticationService.Instance.DeleteAccountAsync();
            AuthenticationService.Instance.SignOut(clearCredentials: true);
            SULog.Info("Account deleted (Firebase + UGS)", SULog.Channel.Net);
        }

        public Task SignOutAsync()
        {
            FirebaseAuthHandler.SignOut();
            AuthenticationService.Instance.SignOut(clearCredentials: true);
            SULog.Info("Signed out (Firebase + UGS)", SULog.Channel.Net);
            return Task.CompletedTask;
        }
    }
}
