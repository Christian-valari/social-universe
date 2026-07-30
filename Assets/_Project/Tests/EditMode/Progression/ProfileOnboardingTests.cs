using NUnit.Framework;
using SocialUniverse.Progression;

namespace SocialUniverse.Tests
{
    public class ProfileOnboardingTests
    {
        [Test]
        public void Profile_name_present_does_not_need_onboarding()
        {
            Assert.IsFalse(ProfileOnboarding.NeedsOnboarding("Nova", null, null));
        }

        [Test]
        public void Auth_display_name_present_does_not_need_onboarding()
        {
            Assert.IsFalse(ProfileOnboarding.NeedsOnboarding(null, "Comet", null));
        }

        [Test]
        public void Auth_username_present_does_not_need_onboarding()
        {
            Assert.IsFalse(ProfileOnboarding.NeedsOnboarding(null, null, "Rover"));
        }

        [Test]
        public void All_empty_needs_onboarding()
        {
            Assert.IsTrue(ProfileOnboarding.NeedsOnboarding(null, "", "   "));
        }

        [Test]
        public void Whitespace_only_name_needs_onboarding()
        {
            Assert.IsTrue(ProfileOnboarding.NeedsOnboarding("   ", null, null));
        }

        [Test]
        public void Bare_hash_suffix_only_needs_onboarding()
        {
            // UGS appends "#1234"; a name that is *only* the suffix has no real part.
            Assert.IsTrue(ProfileOnboarding.NeedsOnboarding("#1234", null, null));
        }

        [Test]
        public void Name_with_hash_suffix_is_real()
        {
            Assert.IsFalse(ProfileOnboarding.NeedsOnboarding("Nova#1234", null, null));
        }
    }
}
