using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using SocialUniverse.Config;
using SocialUniverse.UI;
using UnityEngine;

namespace SocialUniverse.Tests
{
    // Known Issue #15: the Cargo upgrade charged coins but had no gameplay effect, so the Garage no
    // longer offers it (Speed now shortens idle mining). The card prefab has a fixed meter per stat,
    // so a meter whose stat isn't offered must be hidden rather than shown with placeholder values.
    public class DroneUpgradeTracksTests
    {
        private readonly List<Object> _created = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _created) if (o != null) Object.DestroyImmediate(o);
            _created.Clear();
        }

        [Test]
        public void Garage_offers_yield_and_speed_but_not_cargo()
        {
            CollectionAssert.AreEqual(new[] { DroneStat.Yield, DroneStat.Speed }, DroneGarageView.OfferedUpgradeStats);
        }

        [Test]
        public void Owned_card_hides_meters_for_stats_that_are_not_offered()
        {
            var (row, roots) = MakeRow();

            row.BindOwned(null, "Scout", true, null, new List<DroneStatVm>
            {
                new DroneStatVm(DroneStat.Yield, 1, 10, 100, false, true, null),
                new DroneStatVm(DroneStat.Speed, 0, 10, 40,  false, true, null),
            });

            Assert.IsFalse(roots[DroneStat.Cargo].activeSelf);
            Assert.IsTrue(roots[DroneStat.Yield].activeSelf);
            Assert.IsTrue(roots[DroneStat.Speed].activeSelf);
        }

        [Test]
        public void Acquirable_card_hides_comparison_rows_for_stats_that_are_not_offered()
        {
            var (row, roots) = MakeRow();

            row.BindAcquirable(null, "Titan", true, null, new List<DroneStatDeltaVm>
            {
                new DroneStatDeltaVm("Yield", "1.0x", "3.0x", DeltaDirection.Up),
                new DroneStatDeltaVm("Speed", "5", "6", DeltaDirection.Up),
            }, "Tier 5", DeltaDirection.Up);

            Assert.IsFalse(roots[DroneStat.Cargo].activeSelf);
            Assert.IsTrue(roots[DroneStat.Yield].activeSelf);
            Assert.IsTrue(roots[DroneStat.Speed].activeSelf);
        }

        private (DroneRowView row, Dictionary<DroneStat, GameObject> roots) MakeRow()
        {
            var go = new GameObject("DroneRow");
            _created.Add(go);
            var row = go.AddComponent<DroneRowView>();

            var roots  = new Dictionary<DroneStat, GameObject>();
            var meters = new List<DroneRowView.StatMeter>();
            foreach (var stat in new[] { DroneStat.Cargo, DroneStat.Yield, DroneStat.Speed })
            {
                var root = new GameObject(stat + "Meter");
                _created.Add(root);
                roots[stat] = root;
                meters.Add(new DroneRowView.StatMeter { Stat = stat, Root = root });
            }
            typeof(DroneRowView).GetField("_statMeters", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(row, meters.ToArray());
            return (row, roots);
        }
    }
}
