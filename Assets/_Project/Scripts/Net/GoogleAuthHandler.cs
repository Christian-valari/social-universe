using System;
using System.Threading.Tasks;

// SETUP REQUIRED before enabling Google Sign-In on device:
//   1. Import the Google Sign-In Unity Plugin (.unitypackage) from:
//      https://github.com/googlesamples/google-signin-unity/releases
//   2. In Google Cloud Console, create OAuth 2.0 client IDs for Android and iOS.
//   3. Replace WebClientId below with your OAuth web application client ID.
//   4. Android: add SHA-1 fingerprint to the Google Cloud Console OAuth client.
//   5. iOS: add the reversed client ID as a URL scheme in Xcode.

namespace SocialUniverse.Net
{
    // Acquires a Google ID token via platform-specific OAuth, ready to pass to
    // IAuthService.SignInWithGoogleAsync(idToken). Throws NotSupportedException
    // in the Unity Editor — AuthScreen catches this and falls back to a mock token
    // so the mock auth service flow still works in dev mode.
    public static class GoogleAuthHandler
    {
        private const string WebClientId = "YOUR_WEB_CLIENT_ID.apps.googleusercontent.com";

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
            var config = new Google.GoogleSignInConfiguration
            {
                WebClientId    = WebClientId,
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
