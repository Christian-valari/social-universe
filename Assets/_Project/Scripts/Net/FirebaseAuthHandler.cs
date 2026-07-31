using System;
using System.Threading.Tasks;
using Firebase;
using Firebase.Auth;
using UnityEngine;
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
        private static FirebaseAuth Auth => FirebaseAuth.DefaultInstance;

        public static bool   HasCurrentUser  => Auth.CurrentUser != null;
        public static string CurrentEmail    => Auth.CurrentUser?.Email;
        public static bool   IsEmailVerified => Auth.CurrentUser?.IsEmailVerified ?? false;

        public static async Task<string> RegisterEmailAsync(string email, string password)
        {
            try
            {
                var result = await Auth.CreateUserWithEmailAndPasswordAsync(email, password);
                string token = await result.User.TokenAsync(true);
                SULog.Info("Firebase account registered", SULog.Channel.Net);
                return token;
            }
            catch (Exception ex) { throw Normalize(ex, "Couldn't create your account — please try again."); }
        }

        public static async Task<string> SignInEmailAsync(string email, string password)
        {
            try
            {
                var result = await Auth.SignInWithEmailAndPasswordAsync(email, password);
                string token = await result.User.TokenAsync(true);
                SULog.Info("Signed in to Firebase with email", SULog.Channel.Net);
                return token;
            }
            catch (Exception ex) { throw Normalize(ex, "Incorrect email or password"); }
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

            try
            {
                var provider = new FederatedOAuthProvider();
                provider.SetProviderData(new FederatedOAuthProviderData { ProviderId = "google.com" });
                var result = await Auth.SignInWithProviderAsync(provider);
                string token = await result.User.TokenAsync(true);
                SULog.Info("Signed in to Firebase with Google", SULog.Channel.Net);
                return token;
            }
            catch (Exception ex) { throw Normalize(ex, "Google sign-in failed — please try again."); }
        }

        public static Task<string> GetFreshIdTokenAsync() => Auth.CurrentUser.TokenAsync(true);

        public static async Task SendEmailVerificationAsync()
        {
            try { await Auth.CurrentUser.SendEmailVerificationAsync(); }
            catch (Exception ex) { throw Normalize(ex, "Couldn't send the verification email — please try again."); }
        }

        public static async Task SendPasswordResetAsync(string email)
        {
            try { await Auth.SendPasswordResetEmailAsync(email); }
            catch (Exception ex) { throw Normalize(ex, "Couldn't send the reset email — please try again."); }
        }

        public static async Task<bool> ReloadAndCheckVerifiedAsync()
        {
            if (Auth.CurrentUser == null) return false;
            await Auth.CurrentUser.ReloadAsync();
            return Auth.CurrentUser.IsEmailVerified;
        }

        public static Task DeleteCurrentUserAsync() => Auth.CurrentUser.DeleteAsync();

        public static void SignOut() => Auth.SignOut();

        // ---- Error translation ------------------------------------------------
        // Firebase's Unity SDK reports auth failures as a FirebaseException whose
        // Message is the opaque "An internal error has occurred." — the actionable
        // reason lives in ErrorCode (a Firebase.Auth.AuthError). Translate it here,
        // inside SocialUniverse.Net where the SDK types are allowed, so the UI
        // (which must not reference Firebase types — Architecture Rule #2) receives
        // a specific, user-facing message. `fallback` is the operation-appropriate
        // default for a Firebase error we don't map explicitly: projects with email
        // enumeration protection collapse wrong-password / no-such-user into a
        // single generic "invalid login credentials" that carries no distinct code.
        private static Exception Normalize(Exception ex, string fallback)
        {
            var fe = FindFirebaseException(ex);
            if (fe == null) return ex;   // not a Firebase error (e.g. NotSupported) — leave it

            var error = (AuthError)fe.ErrorCode;
            SULog.Warn($"Firebase auth error: {error} ({fe.ErrorCode}) — {fe.Message}", SULog.Channel.Net);

            string friendly = error switch
            {
                AuthError.WrongPassword        => "Incorrect email or password",
                AuthError.InvalidEmail         => "Enter a valid email address",
                AuthError.MissingPassword      => "Enter your password",
                AuthError.UserDisabled         => "This account has been disabled",
                AuthError.EmailAlreadyInUse    => "An account with that email already exists",
                AuthError.WeakPassword         => "Password is too weak",
                AuthError.TooManyRequests      => "Too many attempts — please wait a moment and try again",
                AuthError.NetworkRequestFailed => "Network error — check your connection",
                AuthError.OperationNotAllowed  => "This sign-in method isn't enabled",
                _                              => fallback
            };
            return friendly != null ? new Exception(friendly, ex) : ex;
        }

        // The faulted Firebase Task may surface the FirebaseException directly, or
        // wrapped in an AggregateException / as an InnerException — dig it out.
        private static FirebaseException FindFirebaseException(Exception ex)
        {
            for (var e = ex; e != null; e = e.InnerException)
            {
                if (e is FirebaseException fe) return fe;
                if (e is AggregateException agg)
                    foreach (var inner in agg.Flatten().InnerExceptions)
                        if (inner is FirebaseException ife) return ife;
            }
            return null;
        }
    }
}
