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
            return Task.FromException<string>(
                new NotSupportedException("Google Sign-In is unavailable in the Unity Editor or on this platform"));
        }
    }
}
