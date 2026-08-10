using NUnit.Framework;
using SocialUniverse.UI;

namespace SocialUniverse.Tests
{
    public class BuildFeedbackTests
    {
        [Test] public void EaseOutBack_is_zero_at_start() =>
            Assert.AreEqual(0f, BuildFeedback.EaseOutBack(0f), 1e-4f);

        [Test] public void EaseOutBack_is_one_at_end() =>
            Assert.AreEqual(1f, BuildFeedback.EaseOutBack(1f), 1e-4f);

        [Test] public void EaseOutBack_overshoots_above_one_near_end() =>
            Assert.Greater(BuildFeedback.EaseOutBack(0.8f), 1f);

        [Test] public void EaseInBack_is_zero_at_start() =>
            Assert.AreEqual(0f, BuildFeedback.EaseInBack(0f), 1e-4f);

        [Test] public void EaseInBack_is_one_at_end() =>
            Assert.AreEqual(1f, BuildFeedback.EaseInBack(1f), 1e-4f);

        [Test] public void EaseInBack_dips_below_zero_near_start() =>
            Assert.Less(BuildFeedback.EaseInBack(0.2f), 0f);
    }
}
