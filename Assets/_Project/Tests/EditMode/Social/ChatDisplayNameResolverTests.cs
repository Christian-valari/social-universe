using NUnit.Framework;
using SocialUniverse.Social;

namespace SocialUniverse.Tests
{
    public class ChatDisplayNameResolverTests
    {
        [Test]
        public void Display_name_wins_when_present()
        {
            Assert.AreEqual("Chris", ChatDisplayNameResolver.Resolve("Chris", "Backup"));
        }

        [Test]
        public void Username_wins_when_display_name_is_null_or_empty()
        {
            Assert.AreEqual("Backup", ChatDisplayNameResolver.Resolve(null, "Backup"));
            Assert.AreEqual("Backup", ChatDisplayNameResolver.Resolve("", "Backup"));
        }

        [Test]
        public void Falls_back_to_placeholder_when_both_are_null_or_empty()
        {
            Assert.AreEqual("Player", ChatDisplayNameResolver.Resolve(null, null));
            Assert.AreEqual("Player", ChatDisplayNameResolver.Resolve("", ""));
        }

        [Test]
        public void Strips_ugs_hash_suffix()
        {
            Assert.AreEqual("Chris", ChatDisplayNameResolver.Resolve("Chris#1234", null));
            Assert.AreEqual("Chris", ChatDisplayNameResolver.Resolve(null, "Chris#1234"));
        }

        [Test]
        public void Suffix_only_name_falls_back_to_placeholder()
        {
            Assert.AreEqual("Player", ChatDisplayNameResolver.Resolve("#1234", null));
        }

        [Test]
        public void Whitespace_only_name_falls_back_to_placeholder()
        {
            Assert.AreEqual("Player", ChatDisplayNameResolver.Resolve("   ", null));
        }

        [Test]
        public void Trims_surrounding_whitespace()
        {
            Assert.AreEqual("Chris", ChatDisplayNameResolver.Resolve(" Chris #1234", null));
        }
    }
}
