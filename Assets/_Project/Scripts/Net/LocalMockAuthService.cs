using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using SocialUniverse.Core;

namespace SocialUniverse.Net
{
    // Instant-success mock used when running without a live UGS/Firebase project.
    // Stores registered users in-memory so login/register behave realistically.
    public class LocalMockAuthService : IAuthService
    {
        private struct UserRecord
        {
            public string Password;
            public string Username; // cosmetic handle only, not used for sign-in
            public bool   Verified; // simulates the Firebase email-verification flag
            public UserRecord(string password, string username, bool verified)
            {
                Password = password; Username = username; Verified = verified;
            }
        }

        // Keyed by normalized email — email is the sign-in identity, mirroring
        // AuthService's Firebase email/password sign-in.
        private readonly Dictionary<string, UserRecord> _users = new();
        // Keyed by mock playerId ("mock_google"). Lets a repeat mock SSO sign-in
        // within this run recall a previously chosen name, mirroring a real OAuth
        // provider always resolving to the same linked account.
        private readonly Dictionary<string, string> _ssoDisplayNames = new();

        private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

        private bool   _isSignedIn;
        private bool   _isEmailVerified;
        private string _playerId     = "";
        private string _username     = "";
        private string _displayName  = "";
        private string _email        = "";

        public bool   IsSignedIn         => _isSignedIn;
        public bool   IsEmailVerified    => _isEmailVerified;
        public bool   SessionTokenExists => PlayerPrefs.HasKey(SaveKeys.AuthSession);
        public string PlayerId           => _playerId;
        public string Username           => string.IsNullOrEmpty(_username) ? null : _username;
        public string DisplayName        => string.IsNullOrEmpty(_displayName) ? Username : _displayName;
        public string Email              => string.IsNullOrEmpty(_email) ? null : _email;

        public event Action            OnSignedIn;
        public event Action<Exception> OnSignInFailed;

        public Task InitializeAsync() => Task.CompletedTask;

        // Mirrors the real AuthService's session resume: restores the playerId
        // persisted by the last successful sign-in, without prompting for credentials.
        public Task<bool> TryAutoSignInAsync()
        {
            if (_isSignedIn) return Task.FromResult(true);
            if (!SessionTokenExists) return Task.FromResult(false);

            _playerId    = PlayerPrefs.GetString(SaveKeys.AuthSession);
            _username    = PlayerPrefs.GetString(SaveKeys.AuthSession + "_name", "");
            _displayName = PlayerPrefs.GetString(SaveKeys.AuthSession + "_display_name", "");
            _email       = PlayerPrefs.GetString(SaveKeys.AuthSession + "_email", "");
            _isEmailVerified = PlayerPrefs.GetInt(SaveKeys.AuthSession + "_verified", 0) == 1;
            _isSignedIn  = true;
            SULog.Info($"[MOCK] Restored session ({_playerId})", SULog.Channel.Net);
            OnSignedIn?.Invoke();
            return Task.FromResult(true);
        }

        public async Task SignInWithEmailAsync(string email, string password)
        {
            // Mirror UGS: sign-in throws over any live session. Callers must
            // SignOutAsync first — the bricked-login path AuthScreen guards against.
            if (_isSignedIn)
                throw new InvalidOperationException("A player is already signed in — sign out before signing in again.");
            await Task.Delay(800);
            string key = NormalizeEmail(email);
            if (!_users.TryGetValue(key, out var record) || record.Password != password)
                throw new InvalidOperationException("Incorrect email or password");
            _playerId        = "mock_" + key;
            _username        = record.Username;
            _email           = email;
            _isEmailVerified = record.Verified;
            _isSignedIn      = true;
            PersistSession();
            SULog.Info($"[MOCK] Signed in as {record.Username}", SULog.Channel.Net);
            OnSignedIn?.Invoke();
        }

        public async Task RegisterAsync(string username, string password, string email)
        {
            await Task.Delay(1200);
            string key = NormalizeEmail(email);
            if (_users.ContainsKey(key))
                throw new InvalidOperationException("An account with that email already exists");
            _users[key]      = new UserRecord(password, username, verified: false);
            _playerId        = "mock_" + key;
            _username        = username;
            _email           = email;
            _isEmailVerified = false;   // Firebase sends a verification link; unverified until clicked
            _isSignedIn      = true;
            PersistSession();
            SULog.Info($"[MOCK] Registered {username} (email: {_email})", SULog.Channel.Net);
            OnSignedIn?.Invoke();
        }

        public Task UpdateDisplayNameAsync(string displayName)
        {
            _displayName = displayName;
            if (!string.IsNullOrEmpty(_playerId))
                _ssoDisplayNames[_playerId] = displayName;
            PersistSession();
            SULog.Info($"[MOCK] Display name updated to '{displayName}'", SULog.Channel.Net);
            return Task.CompletedTask;
        }

        public Task SignInWithAppleAsync(string idToken) =>
            throw new NotSupportedException("Apple sign-in is not yet implemented via Firebase");

        public async Task SignInWithGoogleAsync()
        {
            // Mirror UGS: SSO sign-in throws over any live session.
            if (_isSignedIn)
                throw new InvalidOperationException("A player is already signed in — sign out before signing in again.");
            await Task.Delay(900);
            // Deterministic identity (not random): a real OAuth provider always
            // resolves to the same linked account, so repeat mock sign-ins are
            // detected as the same returning player once a name has been chosen.
            _playerId        = "mock_google";
            _isEmailVerified = true; // Google accounts report a verified email
            _isSignedIn      = true;
            _displayName     = _ssoDisplayNames.TryGetValue(_playerId, out string name) ? name : "";
            PersistSession();
            SULog.Info($"[MOCK] Signed in with google ({_playerId})", SULog.Channel.Net);
            OnSignedIn?.Invoke();
        }

        public Task SignOutAsync()
        {
            _isSignedIn      = false;
            _isEmailVerified = false;
            _playerId        = "";
            _email           = "";
            PlayerPrefs.DeleteKey(SaveKeys.AuthSession);
            PlayerPrefs.DeleteKey(SaveKeys.AuthSession + "_verified");
            PlayerPrefs.Save();
            return Task.CompletedTask;
        }

        public Task DeleteAccountAsync()
        {
            if (!_isSignedIn) throw new InvalidOperationException("Not signed in");
            if (!string.IsNullOrEmpty(_email))
                _users.Remove(NormalizeEmail(_email));
            SULog.Info($"[MOCK] Account deleted ({_playerId})", SULog.Channel.Net);
            return SignOutAsync();
        }

        // Always "succeeds" — never reveals whether the email is registered
        // (prevents enumeration). Mirrors Firebase SendPasswordResetEmailAsync.
        public Task RequestPasswordResetAsync(string email)
        {
            SULog.Info($"[MOCK] Password reset link requested for {email}", SULog.Channel.Net);
            return Task.CompletedTask;
        }

        // Mirrors Firebase SendEmailVerificationAsync — fire-and-forget log.
        public Task SendEmailVerificationAsync()
        {
            SULog.Info("[MOCK] Verification email sent (click the link to verify)", SULog.Channel.Net);
            return Task.CompletedTask;
        }

        // Simulates the user having clicked the emailed link: reloading the
        // Firebase user now reports the account as verified.
        public Task<bool> ReloadAndCheckVerifiedAsync()
        {
            _isEmailVerified = true;
            if (!string.IsNullOrEmpty(_email))
            {
                string key = NormalizeEmail(_email);
                if (_users.TryGetValue(key, out var record))
                {
                    record.Verified = true;
                    _users[key] = record;
                }
            }
            PersistSession();
            SULog.Info("[MOCK] Email verified", SULog.Channel.Net);
            return Task.FromResult(true);
        }

        private void PersistSession()
        {
            PlayerPrefs.SetString(SaveKeys.AuthSession, _playerId);
            PlayerPrefs.SetString(SaveKeys.AuthSession + "_name", _username);
            PlayerPrefs.SetString(SaveKeys.AuthSession + "_display_name", _displayName);
            PlayerPrefs.SetString(SaveKeys.AuthSession + "_email", _email);
            PlayerPrefs.SetInt(SaveKeys.AuthSession + "_verified", _isEmailVerified ? 1 : 0);
            PlayerPrefs.Save();
        }
    }
}
