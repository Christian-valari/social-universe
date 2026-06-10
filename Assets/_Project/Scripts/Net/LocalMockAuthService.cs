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
        private readonly Dictionary<string, string> _users = new();

        private bool   _isSignedIn;
        private string _playerId = "";

        public bool   IsSignedIn         => _isSignedIn;
        public bool   SessionTokenExists => PlayerPrefs.HasKey(SaveKeys.AuthSession);
        public string PlayerId           => _playerId;

        public event Action            OnSignedIn;
        public event Action<Exception> OnSignInFailed;

        public Task InitializeAsync() => Task.CompletedTask;

        // Mirrors the real AuthService's session-token resume: restores the playerId
        // persisted by the last successful sign-in, without prompting for credentials.
        public Task<bool> TryAutoSignInAsync()
        {
            if (_isSignedIn) return Task.FromResult(true);
            if (!SessionTokenExists) return Task.FromResult(false);

            _playerId   = PlayerPrefs.GetString(SaveKeys.AuthSession);
            _isSignedIn = true;
            SULog.Info($"[MOCK] Restored session ({_playerId})", SULog.Channel.Net);
            OnSignedIn?.Invoke();
            return Task.FromResult(true);
        }

        public async Task SignInAnonymouslyAsync()
        {
            if (_isSignedIn) { OnSignedIn?.Invoke(); return; }
            await Task.Delay(900);
            _playerId   = "guest_" + UnityEngine.Random.Range(10000, 99999);
            _isSignedIn = true;
            PersistSession();
            SULog.Info($"[MOCK] Signed in as guest ({_playerId})", SULog.Channel.Net);
            OnSignedIn?.Invoke();
        }

        public async Task SignInWithCredentialsAsync(string username, string password)
        {
            await Task.Delay(800);
            if (_users.TryGetValue(username, out var stored) && stored != password)
                throw new InvalidOperationException("Incorrect username or password");
            _playerId   = "mock_" + username;
            _isSignedIn = true;
            PersistSession();
            SULog.Info($"[MOCK] Signed in as {username}", SULog.Channel.Net);
            OnSignedIn?.Invoke();
        }

        public async Task RegisterAsync(string username, string password)
        {
            await Task.Delay(1200);
            if (_users.ContainsKey(username))
                throw new InvalidOperationException("Username already taken");
            _users[username] = password;
            _playerId        = "mock_" + username;
            _isSignedIn      = true;
            PersistSession();
            SULog.Info($"[MOCK] Registered {username}", SULog.Channel.Net);
            OnSignedIn?.Invoke();
        }

        public Task SignInWithAppleAsync(string idToken)  => SignInAnonymouslyAsync();
        public Task SignInWithGoogleAsync(string idToken) => SignInAnonymouslyAsync();

        public Task SignOutAsync()
        {
            _isSignedIn = false;
            _playerId   = "";
            PlayerPrefs.DeleteKey(SaveKeys.AuthSession);
            PlayerPrefs.Save();
            return Task.CompletedTask;
        }

        private void PersistSession()
        {
            PlayerPrefs.SetString(SaveKeys.AuthSession, _playerId);
            PlayerPrefs.Save();
        }
    }
}
