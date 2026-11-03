using NUnit.Framework;
using SocialUniverse.Config;
using SocialUniverse.UI;

namespace SocialUniverse.Tests
{
    // The line on the pre-game panel that tells the player how a closing-ring run works.
    public class ActiveMiningRulesTextTests
    {
        private const string Rules = "Tap each target before its ring closes. Earlier taps bank more.";

        [Test]
        public void Rules_explain_the_ring() =>
            Assert.AreEqual(Rules, ActiveMiningRulesText.For(TierOneSettings()));

        // A scene opened on its own has no run begun, so the handoff holds no settings.
        [Test]
        public void A_run_without_settings_gets_the_same_rules() =>
            Assert.AreEqual(Rules, ActiveMiningRulesText.For(null));

        private static ActiveMiningRunSettings TierOneSettings() =>
            new ActiveMiningRunSettings(1, new ActiveMiningTierSettings(), previousTier: null,
                perfectShare: 1f, goodShare: 0.8f, lateShare: 0.6f, spawnGapSeconds: 0.25f, missGraceSeconds: 0.2f);
    }
}
