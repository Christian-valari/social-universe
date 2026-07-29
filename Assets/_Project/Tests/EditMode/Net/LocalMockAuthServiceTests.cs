using System.Threading.Tasks;
using NUnit.Framework;
using SocialUniverse.Net;

namespace SocialUniverse.Tests
{
    // Exercises the in-memory LocalMockAuthService against the Firebase + UGS OIDC
    // contract: email-verification link state, the "already signed in" guard that
    // mirrors UGS (a live session blocks email/SSO sign-in until SignOutAsync),
    // account deletion, session restore, and deterministic Google identity. The
    // UGS/Firebase-backed AuthService is a thin SDK wrapper verified in PlayMode.
    public class LocalMockAuthServiceTests
    {
        private LocalMockAuthService _auth;

        [SetUp]
        public void SetUp() => _auth = new LocalMockAuthService();

        [TearDown]
        public void TearDown()
        {
            if (_auth.IsSignedIn) _auth.SignOutAsync();
        }

        [Test]
        public async Task Registered_user_starts_unverified_then_verifies()
        {
            await _auth.RegisterAsync("Player1", "Passw0rd!", "v@example.com");
            Assert.IsFalse(_auth.IsEmailVerified);
            Assert.IsTrue(await _auth.ReloadAndCheckVerifiedAsync());
            Assert.IsTrue(_auth.IsEmailVerified);
        }

        [Test]
        public void Apple_sign_in_is_not_supported()
        {
            Assert.ThrowsAsync<System.NotSupportedException>(async () =>
                await _auth.SignInWithAppleAsync("token"));
        }

        [Test]
        public async Task First_google_sign_in_has_no_display_name()
        {
            await _auth.SignInWithGoogleAsync();
            Assert.IsNull(_auth.DisplayName);
        }

        [Test]
        public async Task Email_sign_in_over_a_live_session_throws()
        {
            // Await the async call directly rather than wrapping it in a blocking
            // assertion helper: those helpers block the Unity main thread while
            // the mock's Task.Delay continuation is posted back to it, deadlocking
            // the EditMode run.
            await _auth.SignInWithGoogleAsync();
            try
            {
                await _auth.SignInWithEmailAsync("someone@example.com", "Passw0rd!");
                Assert.Fail("Expected InvalidOperationException for sign-in over a live session");
            }
            catch (System.InvalidOperationException ex)
            {
                StringAssert.Contains("already signed in", ex.Message); // mirrors UGS
            }
        }

        [Test]
        public async Task Signing_out_lets_email_sign_in_succeed()
        {
            // Establish a real account, then sign out so its record persists.
            await _auth.RegisterAsync("Player1", "Passw0rd!", "back@example.com");
            await _auth.SignOutAsync();

            // Await directly — an unexpected exception fails the test on its own,
            // and a blocking assertion helper would deadlock the run.
            await _auth.SignInWithEmailAsync("back@example.com", "Passw0rd!");
            Assert.IsTrue(_auth.IsSignedIn);
        }

        [Test]
        public async Task Deleting_account_frees_the_email_and_signs_out()
        {
            await _auth.RegisterAsync("Player1", "Passw0rd!", "del@example.com");
            await _auth.DeleteAccountAsync();
            Assert.IsFalse(_auth.IsSignedIn);

            // The email record is freed: re-registering the same address succeeds.
            // Awaited directly — a blocking assertion wrapper around a call with an
            // internal Task.Delay would deadlock the EditMode main thread.
            await _auth.RegisterAsync("Player2", "Passw0rd!", "del@example.com");
            Assert.IsTrue(_auth.IsSignedIn);
        }

        [Test]
        public async Task Choosing_a_name_then_signing_back_in_with_google_recalls_it()
        {
            await _auth.SignInWithGoogleAsync();
            await _auth.UpdateDisplayNameAsync("Nova");
            await _auth.SignOutAsync();

            await _auth.SignInWithGoogleAsync();
            Assert.AreEqual("Nova", _auth.DisplayName);
        }
    }
}
