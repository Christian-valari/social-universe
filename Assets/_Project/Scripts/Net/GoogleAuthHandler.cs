using System;
using System.Threading.Tasks;
using SocialUniverse.Config;

// Acquires a Google Play Games *server auth code* (Play Games Services v2 flow),
// ready to pass to IAuthService.SignInWithGoogleAsync(authCode). NOTE: the method
// is still named GetIdTokenAsync and the interface method SignInWithGoogleAsync
// for caller stability (AuthScreen is untouched), but under v2 the string is a
// server auth code, not an ID token. Follows Unity's Google Play Games sign-in docs:
// https://docs.unity.com/en-us/authentication/platform-signin/google-play-games
//
// v2 (play-services-games-v2) is mandatory — Google blocks the v1 SDK
// (com.google.android.gms:play-services-games) at upload. Unlike v1, v2 returns
// a one-time server AUTH CODE via RequestServerSideAccess (not an ID token);
// UGS exchanges it server-side using the Web client ID + secret configured in
// the Authentication dashboard.
//
// Throws NotSupportedException in the Editor / on non-Android — AuthScreen
// catches this and substitutes a mock auth code, so the mock flow still works
// in dev. The Android device path needs the Google Play Games plugin for Unity
// v11.01+ imported + Play Console setup before it compiles for Android — see
// docs/google-signin-setup-checklist.md and
// docs/superpowers/specs/2026-07-23-google-signin-play-games-plugin-design.md.
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
            return GetServerAuthCodeAndroidAsync();
#else
            return Task.FromException<string>(
                new NotSupportedException("Google Play Games sign-in is unavailable in the Unity Editor or on this platform"));
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        // v2 has no PlayGamesClientConfiguration — Activate() once per session is
        // the whole init. Guarded so a retry after cancel/failure doesn't repeat it.
        private static bool _activated;

        private static Task<string> GetServerAuthCodeAndroidAsync()
        {
            // The Web client ID isn't passed to Play Games in code (it's entered
            // in the plugin's Android setup dialog — see the checklist); this
            // placeholder guard stays as a "setup not done yet" tripwire and
            // keeps GoogleAuthConfig the single source of truth for the value.
            if (string.IsNullOrEmpty(_webClientId) || _webClientId.StartsWith("YOUR_"))
                return Task.FromException<string>(new InvalidOperationException(
                    "GoogleAuthConfig.WebClientId is still the placeholder — see docs/google-signin-setup-checklist.md"));

            if (!_activated)
            {
                GooglePlayGames.PlayGamesPlatform.Activate();
                _activated = true;
            }

            // Play Games auth is callback-based; bridge it to the Task the callers
            // (AuthScreen → AuthService) already await. Authenticate first, then
            // request the server auth code. A non-Success status (DeveloperError =
            // SHA-1/OAuth config; Canceled = user/config rejection; NetworkError)
            // or an empty code faults the task, surfacing via AuthScreen's catch.
            var tcs = new TaskCompletionSource<string>();
            GooglePlayGames.PlayGamesPlatform.Instance.Authenticate(status =>
            {
                if (status != GooglePlayGames.BasicApi.SignInStatus.Success)
                {
                    tcs.SetException(new InvalidOperationException(
                        $"Google Play Games sign-in failed: {status}"));
                    return;
                }

                // forceRefreshToken:false — reuse the cached grant when possible.
                GooglePlayGames.PlayGamesPlatform.Instance.RequestServerSideAccess(false, code =>
                {
                    if (string.IsNullOrEmpty(code))
                        tcs.SetException(new InvalidOperationException("Google Play Games returned no server auth code"));
                    else
                        tcs.SetResult(code);
                });
            });
            return tcs.Task;
        }
#endif
    }
}
