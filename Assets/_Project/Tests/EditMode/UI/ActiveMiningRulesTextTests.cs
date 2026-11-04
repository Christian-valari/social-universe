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

        private const string DecoyLine = "New: red X spots are decoys, so leave them alone.";

        [Test]
        public void The_first_tier_with_decoys_names_them() =>
            StringAssert.Contains(DecoyLine, ActiveMiningRulesText.For(Tier(chance: 0.25f, previousChance: 0f)));

        [Test]
        public void The_decoy_line_follows_the_ring_rule() =>
            StringAssert.StartsWith(Rules, ActiveMiningRulesText.For(Tier(chance: 0.25f, previousChance: 0f)));

        [TestCase(0.25f, 0.25f, TestName = "A_tier_with_the_same_decoy_chance_as_the_one_below_does_not_repeat_it")]
        [TestCase(0.35f, 0.25f, TestName = "A_tier_with_more_decoys_than_the_one_below_does_not_repeat_it")]
        public void Decoys_are_named_only_where_they_first_appear(float chance, float previousChance) =>
            StringAssert.DoesNotContain("decoy", ActiveMiningRulesText.For(Tier(chance, previousChance)));

        [Test]
        public void A_tier_without_decoys_does_not_name_them() =>
            Assert.AreEqual(Rules, ActiveMiningRulesText.For(Tier(chance: 0f, previousChance: 0f)));

        private static ActiveMiningRunSettings Tier(float chance, float previousChance) =>
            new ActiveMiningRunSettings(2, new ActiveMiningTierSettings(10, 1.3f, 0.75f, 1.05f, 3, chance),
                new ActiveMiningTierSettings(8, 1.6f, 0.8f, 1.2f, 3, previousChance),
                perfectShare: 1f, goodShare: 0.8f, lateShare: 0.6f, spawnGapSeconds: 0.25f, missGraceSeconds: 0.2f);

        private static ActiveMiningRunSettings TierOneSettings() =>
            new ActiveMiningRunSettings(1, new ActiveMiningTierSettings(), previousTier: null,
                perfectShare: 1f, goodShare: 0.8f, lateShare: 0.6f, spawnGapSeconds: 0.25f, missGraceSeconds: 0.2f);
    }
}
