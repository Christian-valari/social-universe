using System.Linq;
using NUnit.Framework;
using UnityEngine;
using SocialUniverse.Config;

namespace SocialUniverse.Tests
{
    public class DatabaseRegistryM6Tests
    {
        private static void SetField(object o, string f, object v) =>
            o.GetType().GetField(f, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(o, v);

        [Test]
        public void GetMineral_finds_by_id_and_GetUpgrade_finds_by_stat()
        {
            var iron = ScriptableObject.CreateInstance<MineralDefinition>();
            SetField(iron, "_mineralId", "iron");
            var cargo = ScriptableObject.CreateInstance<UpgradeDefinition>();
            SetField(cargo, "_stat", DroneStat.Cargo);

            var reg = ScriptableObject.CreateInstance<DatabaseRegistry>();
            SetField(reg, "_minerals", new[] { iron });
            SetField(reg, "_upgrades", new[] { cargo });

            Assert.AreSame(iron, reg.GetMineral("iron"));
            Assert.IsNull(reg.GetMineral("nope"));
            Assert.AreSame(cargo, reg.GetUpgrade(DroneStat.Cargo));
            Assert.AreEqual(1, reg.AllMinerals.Count());

            Object.DestroyImmediate(iron); Object.DestroyImmediate(cargo); Object.DestroyImmediate(reg);
        }
    }
}
