using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using SocialUniverse.Config;
using SocialUniverse.Mining;

namespace SocialUniverse.Tests
{
    public class MineralInventoryTests
    {
        private static void SetField(object o, string f, object v) =>
            o.GetType().GetField(f, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(o, v);

        private static MineralDefinition Mineral(string id, int sellValue)
        {
            var m = ScriptableObject.CreateInstance<MineralDefinition>();
            SetField(m, "_mineralId", id);
            SetField(m, "_sellValue", sellValue);
            return m;
        }

        [Test]
        public void Add_and_Get_accumulate()
        {
            var inv = new MineralInventory();
            inv.Add("iron", 5);
            inv.Add("iron", 3);
            Assert.AreEqual(8, inv.Get("iron"));
            Assert.AreEqual(0, inv.Get("platinum"));
        }

        [Test]
        public void SetAll_replaces_contents()
        {
            var inv = new MineralInventory();
            inv.Add("iron", 5);
            inv.SetAll(new Dictionary<string, int> { { "platinum", 2 } });
            Assert.AreEqual(0, inv.Get("iron"));
            Assert.AreEqual(2, inv.Get("platinum"));
        }

        [Test]
        public void TotalSellValue_sums_qty_times_sellValue_over_registry()
        {
            var iron     = Mineral("iron", 2);
            var platinum = Mineral("platinum", 20);
            var reg = ScriptableObject.CreateInstance<DatabaseRegistry>();
            SetField(reg, "_minerals", new[] { iron, platinum });

            var inv = new MineralInventory();
            inv.SetAll(new Dictionary<string, int> { { "iron", 10 }, { "platinum", 3 } });

            Assert.AreEqual(10 * 2 + 3 * 20, inv.TotalSellValue(reg));

            Object.DestroyImmediate(iron); Object.DestroyImmediate(platinum); Object.DestroyImmediate(reg);
        }
    }
}
