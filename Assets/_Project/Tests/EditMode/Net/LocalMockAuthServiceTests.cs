using System.Threading.Tasks;
using NUnit.Framework;
using SocialUniverse.Net;

namespace SocialUniverse.Tests
{
    // Exercises the in-memory LocalMockAuthService: email-verification codes,
    // anonymous-session semantics and the "already signed in" guard that mirrors
    // UGS (a live session blocks email/SSO sign-in until SignOutAsync),
    // anonymous-to-account upgrade on register, email availability, account
    // deletion, and session restore. The UGS-backed AuthService is a thin
    // SDK/Cloud-Code wrapper verified in PlayMode.
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
        public async Task Confirming_with_correct_code_after_request_succeeds()
        {
            await _auth.RequestEmailVerificationCodeAsync();

            Assert.DoesNotThrowAsync(async () =>
                await _auth.ConfirmEmailVerificationCodeAsync("123456"));
        }

        [Test]
        public void Confirming_without_requesting_first_throws()
        {
            Assert.ThrowsAsync<System.InvalidOperationException>(async () =>
                await _auth.ConfirmEmailVerificationCodeAsync("123456"));
        }

        [Test]
        public async Task Confirming_with_wrong_code_throws()
        {
            await _auth.RequestEmailVerificationCodeAsync();

            Assert.ThrowsAsync<System.InvalidOperationException>(async () =>
                await _auth.ConfirmEmailVerificationCodeAsync("000000"));
        }

        [Test]
        public async Task Code_is_single_use()
        {
            await _auth.RequestEmailVerificationCodeAsync();
            await _auth.ConfirmEmailVerificationCodeAsync("123456");

            Assert.ThrowsAsync<System.InvalidOperationException>(async () =>
                await _auth.ConfirmEmailVerificationCodeAsync("123456"));
        }

        [Test]
        public async Task Anonymous_sign_in_is_reported_anonymous()
        {
            await _auth.SignInAnonymouslyAsync();
            Assert.IsTrue(_auth.IsAnonymous);
        }

        [Test]
        public async Task Email_sign_in_over_a_live_anonymous_session_throws()
        {
            // Await the async call directly rather than wrapping it in a blocking
            // assertion helper: those helpers block the Unity main thread while
            // the mock's Task.Delay continuation is posted back to it, deadlocking
            // the EditMode run.
            await _auth.SignInAnonymouslyAsync();
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
        public async Task Signing_out_the_anonymous_session_lets_email_sign_in_succeed()
        {
            // Establish a real account, then sign out so its record persists.
            await _auth.RegisterAsync("Player1", "Passw0rd!", "back@example.com");
            await _auth.SignOutAsync();

            // A throwaway anonymous transport session (as a registration pre-check
            // or forgot-password flow leaves behind), then the Fix-1 cleanup
            // AuthScreen performs before signing in for real.
            await _auth.SignInAnonymouslyAsync();
            await _auth.SignOutAsync();

            // Await directly — an unexpected exception fails the test on its own,
            // and a blocking assertion helper would deadlock the run.
            await _auth.SignInWithEmailAsync("back@example.com", "Passw0rd!");
            Assert.IsTrue(_auth.IsSignedIn);
            Assert.IsFalse(_auth.IsAnonymous);
        }

        [Test]
        public async Task Email_availability_reflects_registration()
        {
            Assert.IsTrue(await _auth.IsEmailAvailableAsync("new@example.com"));
            await _auth.RegisterAsync("Player1", "Passw0rd!", "new@example.com");
            Assert.IsFalse(await _auth.IsEmailAvailableAsync("New@Example.com")); // case-insensitive
        }

        [Test]
        public async Task Registering_over_an_anonymous_session_upgrades_it()
        {
            await _auth.SignInAnonymouslyAsync();
            string anonId = _auth.PlayerId;
            await _auth.RegisterAsync("Player1", "Passw0rd!", "up@example.com");
            Assert.AreEqual(anonId, _auth.PlayerId);   // same account, upgraded
            Assert.IsFalse(_auth.IsAnonymous);
        }

        [Test]
        public async Task Deleting_account_frees_the_email_and_signs_out()
        {
            await _auth.RegisterAsync("Player1", "Passw0rd!", "del@example.com");
            await _auth.DeleteAccountAsync();
            Assert.IsFalse(_auth.IsSignedIn);
            Assert.IsTrue(await _auth.IsEmailAvailableAsync("del@example.com"));
        }

        [Test]
        public async Task Restored_session_remembers_it_was_anonymous()
        {
            await _auth.SignInAnonymouslyAsync();
            var restored = new LocalMockAuthService();
            await restored.TryAutoSignInAsync();
            Assert.IsTrue(restored.IsAnonymous);
            await restored.SignOutAsync();
        }
    }
}
