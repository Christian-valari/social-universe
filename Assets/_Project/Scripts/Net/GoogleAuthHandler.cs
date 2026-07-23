using System;
using System.Threading.Tasks;
using SocialUniverse.Config;

// Acquires a Google ID token via the Google Play Games plugin, ready to pass
// to IAuthService.SignInWithGoogleAsync(idToken). Follows the official Unity
// Authentication docs:
// https://docs.unity.com/en-us/authentication/platform-signin/google
//
// Throws NotSupportedException in the Unity Editor and on non-Android
// platforms — AuthScreen catches this and falls back to a mock token, so the
// mock auth flow still works in dev mode. The Android device path needs the
// Google Play Games plugin (v0.10.14) imported and Play Console setup done
// before it compiles for the Android target — see
// docs/google-signin-setup-checklist.md and
// docs/superpowers/specs/2026-07-23-google-signin-play-games-plugin-design.md.
// Configure lets the app wire up the OAuth Web Client ID at bootstrap.
namespace SocialUniverse.Net
{
    public static class GoogleAuthHandler
    {
        private static string _webClientId;

        // Called once from RootLifetimeScope.Configure before any sign-in
        // attempt. Never touches GooglePlayGames.* types, so it's safe to call
        // even in the Editor or on platforms where the native plugin isn't
        // present.
        public static void Configure(GoogleAuthConfig config)
        {
            _webClientId = config != null ? config.WebClientId : null;
        }

        public static Task<string> GetIdTokenAsync()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            return GetIdTokenAndroidAsync();
#else
            return Task.FromException<string>(
                new NotSupportedException("Google Sign-In is unavailable in the Unity Editor or on this platform"));
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        // InitializeInstance throws if called more than once per session, so
        // initialise the Play Games platform exactly once — retrying after a
        // cancel/failure must not re-initialise.
        private static bool _initialized;

        private static Task<string> GetIdTokenAndroidAsync()
        {
            // The Web client ID isn't passed to Play Games in code (it's entered
            // in the plugin's Android setup dialog — see the checklist); this
            // placeholder guard stays as a "setup not done yet" tripwire and
            // keeps GoogleAuthConfig the single source of truth for the value.
            if (string.IsNullOrEmpty(_webClientId) || _webClientId.StartsWith("YOUR_"))
                return Task.FromException<string>(new InvalidOperationException(
                    "GoogleAuthConfig.WebClientId is still the placeholder — see docs/google-signin-setup-checklist.md"));

            EnsureInitialized();

            // Play Games authentication is callback-based; bridge it to the
            // Task the callers (AuthScreen → AuthService) already await. Use the
            // SignInStatus overload (not the bool one) so a failure carries the
            // actual reason — DeveloperError almost always means the signing-key
            // SHA-1 / OAuth / Play Games config doesn't match; Canceled means
            // the user dismissed the prompt; NetworkError is connectivity. A
            // faulted task surfaces via AuthScreen's existing catch (FriendlyError
            // + busy cleared).
            var tcs = new TaskCompletionSource<string>();
            GooglePlayGames.PlayGamesPlatform.Instance.Authenticate(
                GooglePlayGames.BasicApi.SignInInteractivity.CanPromptAlways,
                (GooglePlayGames.BasicApi.SignInStatus status) =>
                {
                    if (status != GooglePlayGames.BasicApi.SignInStatus.Success)
                    {
                        tcs.SetException(new InvalidOperationException(
                            $"Google Play Games sign-in failed: {status}"));
                        return;
                    }

                    string idToken = GooglePlayGames.PlayGamesPlatform.Instance.GetIdToken();
                    if (string.IsNullOrEmpty(idToken))
                        tcs.SetException(new InvalidOperationException("Google Play Games returned no ID token"));
                    else
                        tcs.SetResult(idToken);
                });
            return tcs.Task;
        }

        private static void EnsureInitialized()
        {
            if (_initialized) return;

            // RequestIdToken() makes the plugin mint the OAuth ID token that
            // UGS's SignInWithGoogleAsync validates against the Web client ID
            // (the Web client ID is configured in the plugin's Android setup
            // dialog, not here).
            var config = new GooglePlayGames.BasicApi.PlayGamesClientConfiguration.Builder()
                .RequestIdToken()
                .Build();
            GooglePlayGames.PlayGamesPlatform.InitializeInstance(config);
            GooglePlayGames.PlayGamesPlatform.DebugLogEnabled = true;
            GooglePlayGames.PlayGamesPlatform.Activate();
            _initialized = true;
        }
#endif
    }
}
