using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using SocialUniverse.Config;
using SocialUniverse.Mining;

namespace SocialUniverse.Tests
{
    public class DroneRuntimeTests
    {
        private static void Set(object o, string f, object v) => o.GetType()
            .GetField(f, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(o, v);

        [Test]
        public void Effective_stats_reflect_upgrade_levels()
        {
            var def = ScriptableObject.CreateInstance<DroneDefinition>();
            Set(def, "_cargoCap", 50); Set(def, "_yieldMultiplier", 1f); Set(def, "_travelSpeed", 5f);

            var cargo = ScriptableObject.CreateInstance<UpgradeDefinition>();
            Set(cargo, "_stat", DroneStat.Cargo); Set(cargo, "_deltaPerLevel", 10f);
            var yield = ScriptableObject.CreateInstance<UpgradeDefinition>();
            Set(yield, "_stat", DroneStat.Yield); Set(yield, "_deltaPerLevel", 0.5f);

            var upgrades = new Dictionary<DroneStat, UpgradeDefinition> { { DroneStat.Cargo, cargo }, { DroneStat.Yield, yield } };
            var levels   = new Dictionary<DroneStat, int> { { DroneStat.Cargo, 2 }, { DroneStat.Yield, 3 } };

            var drone = new DroneRuntime(def, levels, upgrades);

            Assert.AreEqual(70, drone.EffectiveCargoCap);          // 50 + 2*10
            Assert.AreEqual(2.5f, drone.EffectiveYieldMult, 1e-4); // 1 + 3*0.5
            Assert.AreEqual(5f, drone.EffectiveTravelSpeed);       // no Speed upgrade -> base

            Object.DestroyImmediate(def); Object.DestroyImmediate(cargo); Object.DestroyImmediate(yield);
        }

        [Test]
        public void Unknown_stat_level_defaults_to_zero_and_base()
        {
            var def = ScriptableObject.CreateInstance<DroneDefinition>();
            Set(def, "_cargoCap", 50);
            var drone = new DroneRuntime(def);
            Assert.AreEqual(0, drone.Level(DroneStat.Cargo));
            Assert.AreEqual(50, drone.EffectiveCargoCap);
            Object.DestroyImmediate(def);
        }
    }
}
