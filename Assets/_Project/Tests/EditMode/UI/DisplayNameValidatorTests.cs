using NUnit.Framework;
using SocialUniverse.UI;

namespace SocialUniverse.Tests
{
    public class DisplayNameValidatorTests
    {
        [Test]
        public void Empty_name_is_rejected()
        {
            Assert.IsFalse(DisplayNameValidator.Validate("", out string error));
            StringAssert.Contains("at least", error);
        }

        [Test]
        public void Whitespace_only_name_is_rejected()
        {
            Assert.IsFalse(DisplayNameValidator.Validate("   ", out string error));
            StringAssert.Contains("at least", error);
        }

        [Test]
        public void Single_character_name_is_rejected()
        {
            Assert.IsFalse(DisplayNameValidator.Validate("A", out _));
        }

        [Test]
        public void Two_character_name_is_accepted()
        {
            Assert.IsTrue(DisplayNameValidator.Validate("Al", out string error));
            Assert.IsNull(error);
        }

        [Test]
        public void Twenty_character_name_is_accepted()
        {
            string name = new string('A', 20);
            Assert.IsTrue(DisplayNameValidator.Validate(name, out _));
        }

        [Test]
        public void Twenty_one_character_name_is_rejected()
        {
            string name = new string('A', 21);
            Assert.IsFalse(DisplayNameValidator.Validate(name, out string error));
            StringAssert.Contains("20 characters", error);
        }

        [Test]
        public void Name_with_a_space_is_rejected()
        {
            Assert.IsFalse(DisplayNameValidator.Validate("Star Fox", out string error));
            StringAssert.Contains("spaces", error);
        }

        [Test]
        public void Leading_and_trailing_whitespace_is_trimmed_before_validating()
        {
            Assert.IsTrue(DisplayNameValidator.Validate("  Nova  ", out string error));
            Assert.IsNull(error);
        }
    }
}
