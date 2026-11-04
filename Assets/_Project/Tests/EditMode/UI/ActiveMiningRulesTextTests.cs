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

        private const string TwoTargetsLine = "New: two targets can be live at once.";

        [Test]
        public void Tier_3_rules_name_two_targets() =>
            StringAssert.Contains(TwoTargetsLine, ActiveMiningRulesText.For(Live(live: 2, previousLive: 1)));

        [Test]
        public void The_two_targets_line_follows_the_ring_rule() =>
            StringAssert.StartsWith(Rules, ActiveMiningRulesText.For(Live(live: 2, previousLive: 1)));

        [TestCase(2, 2, TestName = "A_tier_with_two_targets_like_the_one_below_does_not_repeat_it")]
        [TestCase(1, 1, TestName = "A_tier_with_one_target_does_not_name_two")]
        public void Two_targets_are_named_only_where_they_first_appear(int live, int previousLive) =>
            StringAssert.DoesNotContain("two targets", ActiveMiningRulesText.For(Live(live, previousLive)));

        private const string SpinLine = "New: the asteroid spins faster, so targets move.";

        [Test]
        public void Tier_4_rules_name_the_spin() =>
            StringAssert.Contains(SpinLine, ActiveMiningRulesText.For(Spin(maxSpin: 45f, previousMaxSpin: 15f)));

        [Test]
        public void The_spin_line_follows_the_ring_rule() =>
            StringAssert.StartsWith(Rules, ActiveMiningRulesText.For(Spin(maxSpin: 45f, previousMaxSpin: 15f)));

        [TestCase(45f, 45f, TestName = "A_tier_that_spins_like_the_one_below_does_not_repeat_it")]
        [TestCase(15f, 15f, TestName = "A_tier_at_the_base_spin_does_not_name_it")]
        public void The_spin_is_named_only_where_it_first_speeds_up(float maxSpin, float previousMaxSpin) =>
            StringAssert.DoesNotContain("spins", ActiveMiningRulesText.For(Spin(maxSpin, previousMaxSpin)));

        private const string VeinLine = "New: press and drag along glowing veins.";

        [Test]
        public void Tier_5_rules_name_the_veins() =>
            StringAssert.Contains(VeinLine, ActiveMiningRulesText.For(Veins(every: 4, previousEvery: 0)));

        [Test]
        public void The_vein_line_follows_the_ring_rule() =>
            StringAssert.StartsWith(Rules, ActiveMiningRulesText.For(Veins(every: 4, previousEvery: 0)));

        [TestCase(4, 4, TestName = "A_tier_with_veins_like_the_one_below_does_not_repeat_them")]
        [TestCase(0, 0, TestName = "A_tier_with_veins_turned_off_does_not_name_them")]
        public void Veins_are_named_only_where_they_first_appear(int every, int previousEvery) =>
            StringAssert.DoesNotContain("vein", ActiveMiningRulesText.For(Veins(every, previousEvery)));

        private static ActiveMiningRunSettings Veins(int every, int previousEvery) =>
            new ActiveMiningRunSettings(5, new ActiveMiningTierSettings(16, 0.9f, 0.6f, 0.75f, 3, veinEvery: every),
                new ActiveMiningTierSettings(14, 1f, 0.65f, 0.85f, 3, veinEvery: previousEvery),
                perfectShare: 1f, goodShare: 0.8f, lateShare: 0.6f, spawnGapSeconds: 0.25f, missGraceSeconds: 0.2f);

        private static ActiveMiningRunSettings Spin(float maxSpin, float previousMaxSpin) =>
            new ActiveMiningRunSettings(4, new ActiveMiningTierSettings(14, 1f, 0.65f, 0.85f, 3, 0f, 1, 5f, maxSpin),
                new ActiveMiningTierSettings(12, 1.1f, 0.7f, 0.9f, 3, 0f, 1, 5f, previousMaxSpin),
                perfectShare: 1f, goodShare: 0.8f, lateShare: 0.6f, spawnGapSeconds: 0.25f, missGraceSeconds: 0.2f);

        private static ActiveMiningRunSettings Live(int live, int previousLive) =>
            new ActiveMiningRunSettings(3, new ActiveMiningTierSettings(12, 1.1f, 0.7f, 0.9f, 3, 0f, live),
                new ActiveMiningTierSettings(10, 1.3f, 0.75f, 1.05f, 3, 0f, previousLive),
                perfectShare: 1f, goodShare: 0.8f, lateShare: 0.6f, spawnGapSeconds: 0.25f, missGraceSeconds: 0.2f);

        private static ActiveMiningRunSettings Tier(float chance, float previousChance) =>
            new ActiveMiningRunSettings(2, new ActiveMiningTierSettings(10, 1.3f, 0.75f, 1.05f, 3, chance),
                new ActiveMiningTierSettings(8, 1.6f, 0.8f, 1.2f, 3, previousChance),
                perfectShare: 1f, goodShare: 0.8f, lateShare: 0.6f, spawnGapSeconds: 0.25f, missGraceSeconds: 0.2f);

        private static ActiveMiningRunSettings TierOneSettings() =>
            new ActiveMiningRunSettings(1, new ActiveMiningTierSettings(), previousTier: null,
                perfectShare: 1f, goodShare: 0.8f, lateShare: 0.6f, spawnGapSeconds: 0.25f, missGraceSeconds: 0.2f);
    }
}
