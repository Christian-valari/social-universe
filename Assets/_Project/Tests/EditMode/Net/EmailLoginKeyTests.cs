using NUnit.Framework;
using SocialUniverse.Net;

namespace SocialUniverse.Tests.Net
{
    public class EmailLoginKeyTests
    {
        [Test]
        public void Derive_is_deterministic_for_the_same_email()
        {
            Assert.AreEqual(EmailLoginKey.Derive("player@example.com"), EmailLoginKey.Derive("player@example.com"));
        }

        [Test]
        public void Derive_ignores_case_and_surrounding_whitespace()
        {
            string key = EmailLoginKey.Derive("Player@Example.com");

            Assert.AreEqual(key, EmailLoginKey.Derive("player@example.com"));
            Assert.AreEqual(key, EmailLoginKey.Derive("  player@example.com  "));
        }

        [Test]
        public void Derive_differs_for_different_emails()
        {
            Assert.AreNotEqual(EmailLoginKey.Derive("a@example.com"), EmailLoginKey.Derive("b@example.com"));
        }

        [Test]
        public void Derive_produces_a_key_valid_for_UGS_username_rules()
        {
            // UGS username/password auth requires 3-20 chars of [a-zA-Z0-9.\-@_].
            string key = EmailLoginKey.Derive("a.very.long.address+that+exceeds+twenty+chars@example.com");

            Assert.GreaterOrEqual(key.Length, 3);
            Assert.LessOrEqual(key.Length, 20);
            foreach (char c in key)
                Assert.IsTrue(char.IsLetterOrDigit(c) || c == '.' || c == '-' || c == '@' || c == '_',
                    $"Character '{c}' is not valid in a UGS username");
        }
    }
}
