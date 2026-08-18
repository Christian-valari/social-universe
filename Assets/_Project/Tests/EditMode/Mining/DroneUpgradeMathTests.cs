using NUnit.Framework;
using UnityEngine;
using SocialUniverse.Config;
using SocialUniverse.Mining;

namespace SocialUniverse.Tests
{
    public class DroneUpgradeMathTests
    {
        private static UpgradeDefinition Upgrade(int baseCost, float growth, float delta, int maxLevel)
        {
            var u = ScriptableObject.CreateInstance<UpgradeDefinition>();
            void Set(string f, object v) => typeof(UpgradeDefinition)
                .GetField(f, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(u, v);
            Set("_baseCost", baseCost); Set("_costGrowth", growth); Set("_deltaPerLevel", delta); Set("_maxLevel", maxLevel);
            return u;
        }

        [Test]
        public void NextCost_grows_geometrically_from_level_zero()
        {
            var u = Upgrade(baseCost: 100, growth: 2f, delta: 5f, maxLevel: 10);
            Assert.AreEqual(100, DroneUpgradeMath.NextCost(u, 0)); // level 0 -> 1
            Assert.AreEqual(200, DroneUpgradeMath.NextCost(u, 1)); // level 1 -> 2
            Assert.AreEqual(400, DroneUpgradeMath.NextCost(u, 2)); // level 2 -> 3
            Object.DestroyImmediate(u);
        }

        [Test]
        public void EffectiveStat_is_base_plus_level_times_delta()
        {
            var u = Upgrade(100, 2f, delta: 5f, maxLevel: 10);
            Assert.AreEqual(50f, DroneUpgradeMath.EffectiveStat(50f, u, 0));
            Assert.AreEqual(65f, DroneUpgradeMath.EffectiveStat(50f, u, 3));
            Assert.AreEqual(50f, DroneUpgradeMath.EffectiveStat(50f, null, 3)); // null track -> base
            Object.DestroyImmediate(u);
        }

        [Test]
        public void SlotUnlockCost_scales_from_start_slots()
        {
            // baseCost 500, growth 2, start 2 slots: first extra (currentSlots=2) = 500, next (3) = 1000
            Assert.AreEqual(500,  DroneUpgradeMath.SlotUnlockCost(500, 2f, currentSlots: 2, startSlots: 2));
            Assert.AreEqual(1000, DroneUpgradeMath.SlotUnlockCost(500, 2f, currentSlots: 3, startSlots: 2));
        }
    }
}
