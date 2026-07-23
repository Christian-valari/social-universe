using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using SocialUniverse.Core;

namespace SocialUniverse.Net
{
    // Instant-success mock used when running without a live UGS project.
    // Stores registered users in-memory so login/register behave realistically.
    public class LocalMockAuthService : IAuthService
    {
        private struct UserRecord
        {
            public string Password;
            public string Username; // cosmetic handle only, not used for sign-in
            public UserRecord(string password, string username) { Password = password; Username = username; }
        }

        // Keyed by normalized email — email is the sign-in identity now, mirroring
        // AuthService's UGS login-key derivation (see DeriveLoginKey there).
        private readonly Dictionary<string, UserRecord> _users          = new();
        private readonly HashSet<string>                 _pendingResets = new(); // normalized emails awaiting reset
        // Keyed by mock playerId ("mock_google"/"mock_apple"). Lets a repeat
        // mock SSO sign-in within this run recall a previously chosen name,
        // mirroring a real OAuth provider always resolving to the same
        // linked UGS account.
        private readonly Dictionary<string, string> _ssoDisplayNames = new();
        private bool _pendingEmailVerificationCode; // an outstanding verification code exists (mock code: 123456)

        private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

        private bool   _isSignedIn;
        private bool   _isAnonymous;
        private string _playerId     = "";
        private string _username     = "";
        private string _displayName  = "";
        private string _email        = "";

        public bool   IsSignedIn         => _isSignedIn;
        public bool   IsAnonymous        => _isAnonymous;
        public bool   SessionTokenExists => PlayerPrefs.HasKey(SaveKeys.AuthSession);
        public string PlayerId           => _playerId;
        public string Username           => string.IsNullOrEmpty(_username) ? null : _username;
        public string DisplayName        => string.IsNullOrEmpty(_displayName) ? Username : _displayName;
        public string Email              => string.IsNullOrEmpty(_email) ? null : _email;

        public event Action            OnSignedIn;
        public event Action<Exception> OnSignInFailed;

        public Task InitializeAsync() => Task.CompletedTask;

        // Mirrors the real AuthService's session-token resume: restores the playerId
        // persisted by the last successful sign-in, without prompting for credentials.
        public Task<bool> TryAutoSignInAsync()
        {
            if (_isSignedIn) return Task.FromResult(true);
            if (!SessionTokenExists) return Task.FromResult(false);

            _playerId    = PlayerPrefs.GetString(SaveKeys.AuthSession);
            _username    = PlayerPrefs.GetString(SaveKeys.AuthSession + "_name", "");
            _displayName = PlayerPrefs.GetString(SaveKeys.AuthSession + "_display_name", "");
            _email       = PlayerPrefs.GetString(SaveKeys.AuthSession + "_email", "");
            _isAnonymous = PlayerPrefs.GetInt(SaveKeys.AuthSession + "_anon", 0) == 1;
            _isSignedIn  = true;
            SULog.Info($"[MOCK] Restored session ({_playerId})", SULog.Channel.Net);
            OnSignedIn?.Invoke();
            return Task.FromResult(true);
        }

        public async Task SignInAnonymouslyAsync()
        {
            if (_isSignedIn) { OnSignedIn?.Invoke(); return; }
            await Task.Delay(900);
            _playerId    = "guest_" + UnityEngine.Random.Range(10000, 99999);
            _isSignedIn  = true;
            _isAnonymous = true;
            PersistSession();
            SULog.Info($"[MOCK] Signed in as guest ({_playerId})", SULog.Channel.Net);
            OnSignedIn?.Invoke();
        }

        public async Task SignInWithEmailAsync(string email, string password)
        {
            // Mirror UGS: SignInWithUsernamePasswordAsync throws over any live
            // session (anonymous or not). Callers must SignOutAsync first — this
            // is exactly the bricked-login path Fix 1 guards against in AuthScreen.
            if (_isSignedIn)
                throw new InvalidOperationException("A player is already signed in — sign out before signing in again.");
            await Task.Delay(800);
            string key = NormalizeEmail(email);
            if (!_users.TryGetValue(key, out var record) || record.Password != password)
                throw new InvalidOperationException("Incorrect email or password");
            _playerId    = "mock_" + key;
            _username    = record.Username;
            _email       = email;
            _isSignedIn  = true;
            _isAnonymous = false;
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
            _users[key] = new UserRecord(password, username);
            if (!_isSignedIn)
                _playerId = "mock_" + key;   // fresh account; anonymous upgrade keeps its id
            _username    = username;
            _email       = email;
            _isAnonymous = false;
            _isSignedIn  = true;
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

        public Task SignInWithAppleAsync(string idToken)  => MockSsoSignInAsync("apple");
        public Task SignInWithGoogleAsync(string authCode) => MockSsoSignInAsync("google");

        private async Task MockSsoSignInAsync(string provider)
        {
            // Mirror UGS: SSO sign-in throws over any live session (anonymous or
            // not) — same "already signed in" contract as SignInWithEmailAsync.
            if (_isSignedIn)
                throw new InvalidOperationException("A player is already signed in — sign out before signing in again.");
            await Task.Delay(900);
            // Deterministic per-provider identity (not random): a real OAuth
            // provider always resolves to the same linked UGS account, so
            // repeat mock sign-ins with the same provider must be detected as
            // the same returning player once a name has been chosen.
            _playerId    = "mock_" + provider;
            _isAnonymous = false;
            _isSignedIn  = true;
            _displayName = _ssoDisplayNames.TryGetValue(_playerId, out string name) ? name : "";
            PersistSession();
            SULog.Info($"[MOCK] Signed in with {provider} ({_playerId})", SULog.Channel.Net);
            OnSignedIn?.Invoke();
        }

        public Task SignOutAsync()
        {
            _isSignedIn  = false;
            _isAnonymous = false;
            _playerId    = "";
            _email       = "";
            PlayerPrefs.DeleteKey(SaveKeys.AuthSession);
            PlayerPrefs.DeleteKey(SaveKeys.AuthSession + "_anon");
            PlayerPrefs.Save();
            return Task.CompletedTask;
        }

        public async Task<bool> IsEmailAvailableAsync(string email)
        {
            await Task.Delay(300);
            return !_users.ContainsKey(NormalizeEmail(email));
        }

        public Task DeleteAccountAsync()
        {
            if (!_isSignedIn) throw new InvalidOperationException("Not signed in");
            if (!string.IsNullOrEmpty(_email))
                _users.Remove(NormalizeEmail(_email));
            _pendingEmailVerificationCode = false;
            SULog.Info($"[MOCK] Account deleted ({_playerId})", SULog.Channel.Net);
            return SignOutAsync();
        }

        // Always "succeeds" — never confirms whether the email is registered (prevents enumeration).
        // If the email is registered, stores a pending reset keyed by email. Mock code is always "123456".
        public Task RequestPasswordResetAsync(string email)
        {
            string key = NormalizeEmail(email);
            if (_users.ContainsKey(key))
                _pendingResets.Add(key);
            SULog.Info($"[MOCK] Password reset requested for {email} (mock code: 123456)", SULog.Channel.Net);
            return Task.CompletedTask;
        }

        public async Task ConfirmPasswordResetAsync(string email, string resetCode, string newPassword)
        {
            await Task.Delay(500);
            string key = NormalizeEmail(email);
            if (!_pendingResets.Contains(key))
                throw new InvalidOperationException("No password reset is pending for this email");
            if (resetCode != "123456")
                throw new InvalidOperationException("Invalid reset code");
            _users[key] = new UserRecord(newPassword, _users[key].Username);
            _pendingResets.Remove(key);
            SULog.Info($"[MOCK] Password reset confirmed for {key}", SULog.Channel.Net);
        }

        // Always "succeeds" — mirrors RequestPasswordResetAsync's mock style.
        // Mock code is always "123456".
        public Task RequestEmailVerificationCodeAsync()
        {
            _pendingEmailVerificationCode = true;
            SULog.Info("[MOCK] Email verification code sent (mock code: 123456)", SULog.Channel.Net);
            return Task.CompletedTask;
        }

        public Task ConfirmEmailVerificationCodeAsync(string code)
        {
            if (!_pendingEmailVerificationCode)
                throw new InvalidOperationException("No verification code requested");
            if (code != "123456")
                throw new InvalidOperationException("Invalid verification code");
            _pendingEmailVerificationCode = false;
            SULog.Info("[MOCK] Email verified", SULog.Channel.Net);
            return Task.CompletedTask;
        }

        private void PersistSession()
        {
            PlayerPrefs.SetString(SaveKeys.AuthSession, _playerId);
            PlayerPrefs.SetString(SaveKeys.AuthSession + "_name", _username);
            PlayerPrefs.SetString(SaveKeys.AuthSession + "_display_name", _displayName);
            PlayerPrefs.SetString(SaveKeys.AuthSession + "_email", _email);
            PlayerPrefs.SetInt(SaveKeys.AuthSession + "_anon", _isAnonymous ? 1 : 0);
            PlayerPrefs.Save();
        }
    }
}
