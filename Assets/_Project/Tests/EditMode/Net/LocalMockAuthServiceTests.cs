using System.Threading.Tasks;
using NUnit.Framework;
using SocialUniverse.Net;

namespace SocialUniverse.Tests
{
    // Exercises the post-login email-verification mock (the UGS-backed
    // AuthService is verified in PlayMode — it is a thin SDK/Cloud-Code
    // wrapper with no branching logic of its own).
    public class LocalMockAuthServiceTests
    {
        private LocalMockAuthService _auth;

        [SetUp]
        public void SetUp() => _auth = new LocalMockAuthService();

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
    }
}
