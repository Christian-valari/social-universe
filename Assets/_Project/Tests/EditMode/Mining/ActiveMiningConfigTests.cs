using NUnit.Framework;
using SocialUniverse.Config;
using UnityEditor;
using UnityEngine;

namespace SocialUniverse.Tests
{
    public class ActiveMiningConfigTests
    {
        private static EconomyConfig Real() =>
            AssetDatabase.LoadAssetAtPath<EconomyConfig>("Assets/_Project/ScriptableObjects/EconomyConfig.asset");

        [TestCase(1, 8, 1.6f, 0.8f, 1.2f)]
        [TestCase(2, 10, 1.3f, 0.75f, 1.05f)]
        [TestCase(3, 12, 1.1f, 0.7f, 0.9f)]
        [TestCase(4, 14, 1.0f, 0.65f, 0.85f)]
        [TestCase(5, 16, 0.9f, 0.6f, 0.75f)]
        public void Real_asset_holds_the_tier_table(int tier, int targets, float ring, float perfect, float good)
        {
            var s = Real().ActiveRunSettings(tier);
            Assert.AreEqual(tier, s.TierNumber);
            Assert.AreEqual(targets, s.Tier.Targets);
            Assert.AreEqual(ring, s.Tier.RingSeconds, 0.001f);
            Assert.AreEqual(perfect, s.Tier.PerfectBySeconds, 0.001f);
            Assert.AreEqual(good, s.Tier.GoodBySeconds, 0.001f);
            Assert.AreEqual(3, s.Tier.MaxMisses);
            Assert.Less(s.Tier.PerfectBySeconds, s.Tier.GoodBySeconds);
            Assert.Less(s.Tier.GoodBySeconds, s.Tier.RingSeconds);
        }

        [TestCase(1, 0f)]
        [TestCase(2, 0.25f)]
        [TestCase(3, 0.25f)]
        [TestCase(4, 0.35f)]
        [TestCase(5, 0.35f)]
        public void Real_asset_holds_decoy_chances(int tier, float chance) =>
            Assert.AreEqual(chance, Real().ActiveRunSettings(tier).Tier.DecoyChance, 0.001f);

        [Test] public void A_tier_has_no_decoys_unless_it_is_given_a_chance()
        {
            Assert.AreEqual(0f, new ActiveMiningTierSettings().DecoyChance);
            Assert.AreEqual(0f, new ActiveMiningTierSettings(8, 1.6f, 0.8f, 1.2f, 3).DecoyChance);
        }

        [Test] public void Real_asset_holds_the_shared_values()
        {
            var s = Real().ActiveRunSettings(1);
            Assert.AreEqual((1f, 0.8f, 0.6f, 0.25f, 0.2f),
                (s.PerfectShare, s.GoodShare, s.LateShare, s.SpawnGapSeconds, s.MissGraceSeconds));
            Assert.AreEqual(0.7f, Real().IdleYieldFraction, 0.001f);
        }

        [Test] public void Real_asset_holds_the_first_target_lead_in()
        {
            Assert.AreEqual(0.6f, Real().ActiveRunSettings(1).FirstTargetDelaySeconds, 0.001f);
        }

        [Test] public void Previous_tier_is_null_at_tier_1_and_set_above_it()
        {
            Assert.IsNull(Real().ActiveRunSettings(1).PreviousTier);
            Assert.AreEqual(8, Real().ActiveRunSettings(2).PreviousTier.Targets);
        }

        [Test] public void A_tier_outside_the_table_uses_the_nearest_row()
        {
            Assert.AreEqual(8, Real().ActiveRunSettings(0).Tier.Targets);
            Assert.AreEqual(16, Real().ActiveRunSettings(9).Tier.Targets);
        }

        [Test] public void An_empty_table_falls_back_to_tier_1_defaults()
        {
            var config = ScriptableObject.CreateInstance<EconomyConfig>();
            Assert.AreEqual(8, config.ActiveRunSettings(3).Tier.Targets);
            Object.DestroyImmediate(config);
        }
    }
}
