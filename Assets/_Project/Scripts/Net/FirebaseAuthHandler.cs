using System;
using System.Threading.Tasks;
using Firebase.Auth;
using UnityEngine;
using SocialUniverse.Config;
using SocialUniverse.Core;

// Owns every Firebase Auth call and nothing else. AuthService consumes this
// to get a Firebase ID token, then bridges it into UGS via OpenID Connect.
// Email/password works in the Editor; Google (FederatedOAuthProvider web
// flow) does not — SignInGoogleAsync throws NotSupportedException at RUNTIME
// when Application.isEditor is true (not via #if UNITY_EDITOR), so the real
// Google API below still compiles and is Editor-verifiable. AuthScreen
// catches the exception and substitutes a mock, mirroring the retired
// GoogleAuthHandler.
//
// Discovered against the imported Firebase Unity SDK via reflection (see
// task-4-report.md "Discovered Firebase API"): the brief's placeholder
// `Auth.CurrentUser_SignInWithProviderAsync(...)` does not exist. The real
// call is the INSTANCE method `FirebaseAuth.SignInWithProviderAsync(
// FederatedAuthProvider)` -> `Task<AuthResult>`, and `AuthResult.User` yields
// the `FirebaseUser`. `FederatedOAuthProvider` (a `FederatedAuthProvider`)
// is configured via `SetProviderData(FederatedOAuthProviderData)`, whose
// `ProviderId` (inherited from `FederatedProviderData`) is set to
// "google.com".
namespace SocialUniverse.Net
{
    public static class FirebaseAuthHandler
    {
        private static FirebaseAuthConfig _config;
        private static FirebaseAuth Auth => FirebaseAuth.DefaultInstance;

        public static void Configure(FirebaseAuthConfig config) => _config = config;

        public static bool   HasCurrentUser  => Auth.CurrentUser != null;
        public static string CurrentEmail    => Auth.CurrentUser?.Email;
        public static bool   IsEmailVerified => Auth.CurrentUser?.IsEmailVerified ?? false;

        public static async Task<string> RegisterEmailAsync(string email, string password)
        {
            var result = await Auth.CreateUserWithEmailAndPasswordAsync(email, password);
            string token = await result.User.TokenAsync(true);
            SULog.Info("Firebase account registered", SULog.Channel.Net);
            return token;
        }

        public static async Task<string> SignInEmailAsync(string email, string password)
        {
            var result = await Auth.SignInWithEmailAndPasswordAsync(email, password);
            string token = await result.User.TokenAsync(true);
            SULog.Info("Signed in to Firebase with email", SULog.Channel.Net);
            return token;
        }

        // Android web flow (Chrome Custom Tabs consent), no native Google
        // Sign-In dependency. Guarded at runtime rather than with
        // #if UNITY_EDITOR: the Firebase managed API below is available in
        // the Editor, so throwing here (instead of excluding the code from
        // compilation) lets the Editor compile-verify the real Google call
        // while still preventing it from actually running there.
        public static async Task<string> SignInGoogleAsync()
        {
            if (Application.isEditor)
                throw new NotSupportedException("Google sign-in is unavailable in the Unity Editor");

            var provider = new FederatedOAuthProvider();
            provider.SetProviderData(new FederatedOAuthProviderData { ProviderId = "google.com" });
            var result = await Auth.SignInWithProviderAsync(provider);
            string token = await result.User.TokenAsync(true);
            SULog.Info("Signed in to Firebase with Google", SULog.Channel.Net);
            return token;
        }

        public static Task<string> GetFreshIdTokenAsync() => Auth.CurrentUser.TokenAsync(true);

        public static Task SendEmailVerificationAsync() => Auth.CurrentUser.SendEmailVerificationAsync();

        public static Task SendPasswordResetAsync(string email) => Auth.SendPasswordResetEmailAsync(email);

        public static async Task<bool> ReloadAndCheckVerifiedAsync()
        {
            if (Auth.CurrentUser == null) return false;
            await Auth.CurrentUser.ReloadAsync();
            return Auth.CurrentUser.IsEmailVerified;
        }

        public static Task DeleteCurrentUserAsync() => Auth.CurrentUser.DeleteAsync();

        public static void SignOut() => Auth.SignOut();
    }
}
