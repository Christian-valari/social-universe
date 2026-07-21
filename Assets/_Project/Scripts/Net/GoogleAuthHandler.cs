using System;
using System.Threading.Tasks;
using SocialUniverse.Config;

// Acquires a Google ID token via platform-specific OAuth, ready to pass to
// IAuthService.SignInWithGoogleAsync(idToken). Throws NotSupportedException
// in the Unity Editor and on non-Android platforms — AuthScreen catches this
// and falls back to a mock token, so the mock auth flow still works in dev
// mode. The Android device path is restored in a later pass (see
// docs/superpowers/specs/2026-07-17-google-signin-display-name-design.md);
// Configure lets the app wire up the OAuth Web Client ID ahead of that so
// RootLifetimeScope only needs to change once.
namespace SocialUniverse.Net
{
    public static class GoogleAuthHandler
    {
        private static string _webClientId;

        // Called once from RootLifetimeScope.Configure before any sign-in
        // attempt. Never touches Google.* types, so it's safe to call even in
        // the Editor or on platforms where the native plugin isn't present.
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
        private static async Task<string> GetIdTokenAndroidAsync()
        {
            if (string.IsNullOrEmpty(_webClientId) || _webClientId.StartsWith("YOUR_"))
                throw new InvalidOperationException(
                    "GoogleAuthConfig.WebClientId is still the placeholder — see docs/google-signin-setup-checklist.md");

            var config = new Google.GoogleSignInConfiguration
            {
                WebClientId    = _webClientId,
                RequestIdToken = true,
            };
            Google.GoogleSignIn.Configuration = config;

            Google.GoogleSignInUser user = await Google.GoogleSignIn.DefaultInstance.SignIn();
            if (string.IsNullOrEmpty(user.IdToken))
                throw new InvalidOperationException("Google Sign-In returned no ID token");
            return user.IdToken;
        }
#endif
    }
}
