using System;
using System.Threading.Tasks;

// Google Sign-In has been deferred to the 'feature/google-signin' branch (plugin
// assets, Android/iOS native glue, and the full GoogleAuthHandler implementation
// live there). This stub keeps AuthScreen/IAuthService compiling on main without
// requiring the Google Sign-In Unity plugin to be imported. AuthScreen already
// catches NotSupportedException from GetIdTokenAsync and falls back to a mock
// token, so the "Sign in with Google" button degrades gracefully rather than
// breaking the build.
namespace SocialUniverse.Net
{
    public static class GoogleAuthHandler
    {
        public static Task<string> GetIdTokenAsync()
        {
            return Task.FromException<string>(
                new NotSupportedException("Google Sign-In is deferred — see the feature/google-signin branch"));
        }
    }
}
