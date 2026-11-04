using NUnit.Framework;
using SocialUniverse.Config;
using SocialUniverse.UI;

namespace SocialUniverse.Tests
{
    // The lines on the pre-game panel that tell the player how a run works. A tier names every
    // challenge it has, because a player can reach it without having met the tiers below.
    public class ActiveMiningRulesTextTests
    {
        private const string Rules = "Tap each target before its ring closes. Earlier taps bank more.";

        [Test]
        public void Rules_explain_the_ring() =>
            Assert.AreEqual(Rules, ActiveMiningRulesText.For(Settings(new ActiveMiningTierSettings())));

        // A scene opened on its own has no run begun, so the handoff holds no settings.
        [Test]
        public void A_run_without_settings_gets_the_same_rules() =>
            Assert.AreEqual(Rules, ActiveMiningRulesText.For(null));

        [Test]
        public void Tier_1_is_one_line() =>
            Assert.AreEqual(1, ActiveMiningRulesText.For(Settings(new ActiveMiningTierSettings(8, 1.6f, 0.8f, 1.2f, 3))).Split('\n').Length);

        [Test]
        public void Tier_2_names_decoys() =>
            Assert.AreEqual(Rules + "\nWatch for: red X decoys", ActiveMiningRulesText.For(Settings(Tier(decoys: 0.25f))));

        [Test]
        public void Tier_3_names_decoys_and_two_targets() =>
            Assert.AreEqual(Rules + "\nWatch for: red X decoys, two targets at once", ActiveMiningRulesText.For(Settings(Tier(decoys: 0.25f, live: 2))));

        [Test]
        public void Tier_4_names_the_spin_too() =>
            Assert.AreEqual(Rules + "\nWatch for: red X decoys, two targets at once, faster spin",
                ActiveMiningRulesText.For(Settings(Tier(decoys: 0.3f, live: 2, maxSpin: 45f))));

        [Test]
        public void Tier_5_names_every_challenge() =>
            Assert.AreEqual(Rules + "\nWatch for: red X decoys, two targets at once, faster spin, dragging along lines",
                ActiveMiningRulesText.For(Settings(Tier(decoys: 0.35f, live: 2, maxSpin: 45f, veinEvery: 4))));

        // A tier needs no lower tier to be named: a challenge is listed wherever it is on.
        [Test]
        public void A_challenge_is_named_without_the_ones_below_it() =>
            Assert.AreEqual(Rules + "\nWatch for: two targets at once", ActiveMiningRulesText.For(Settings(Tier(live: 2))));

        [Test]
        public void Veins_alone_are_named() =>
            Assert.AreEqual(Rules + "\nWatch for: dragging along lines", ActiveMiningRulesText.For(Settings(Tier(veinEvery: 4))));

        [TestCase(15f, TestName = "The_base_spin_is_not_named")]
        [TestCase(5f, TestName = "A_slower_spin_is_not_named")]
        public void Spin_is_named_only_above_the_base_range(float maxSpin) =>
            StringAssert.DoesNotContain("spin", ActiveMiningRulesText.For(Settings(Tier(maxSpin: maxSpin))));

        [Test]
        public void Veins_turned_off_are_not_named() =>
            StringAssert.DoesNotContain("dragging", ActiveMiningRulesText.For(Settings(Tier(decoys: 0.35f, live: 2, maxSpin: 45f, veinEvery: 0))));

        [Test]
        public void The_text_does_not_call_the_line_glowing() =>
            StringAssert.DoesNotContain("glow", ActiveMiningRulesText.For(Settings(Tier(veinEvery: 4))));

        private static ActiveMiningTierSettings Tier(float decoys = 0f, int live = 1, float maxSpin = 15f, int veinEvery = 0) =>
            new ActiveMiningTierSettings(16, 0.9f, 0.6f, 0.75f, 3, decoys, live, 5f, maxSpin, veinEvery);

        private static ActiveMiningRunSettings Settings(ActiveMiningTierSettings tier) =>
            new ActiveMiningRunSettings(1, tier,
                perfectShare: 1f, goodShare: 0.8f, lateShare: 0.6f, spawnGapSeconds: 0.25f, missGraceSeconds: 0.2f);
    }
}
