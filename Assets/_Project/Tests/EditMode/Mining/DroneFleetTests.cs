using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using SocialUniverse.Config;
using SocialUniverse.Mining;

namespace SocialUniverse.Tests
{
    public class DroneFleetTests
    {
        private static void Set(object o, string f, object v) => o.GetType()
            .GetField(f, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(o, v);

        private DatabaseRegistry _registry;

        [SetUp]
        public void SetUp()
        {
            var scout = ScriptableObject.CreateInstance<DroneDefinition>();
            Set(scout, "_droneId", "scout"); Set(scout, "_tier", 1);
            var hauler = ScriptableObject.CreateInstance<DroneDefinition>();
            Set(hauler, "_droneId", "hauler"); Set(hauler, "_tier", 2);
            var cargo = ScriptableObject.CreateInstance<UpgradeDefinition>();
            Set(cargo, "_stat", DroneStat.Cargo); Set(cargo, "_deltaPerLevel", 10f);

            _registry = ScriptableObject.CreateInstance<DatabaseRegistry>();
            Set(_registry, "_drones", new[] { scout, hauler });
            Set(_registry, "_upgrades", new[] { cargo });
        }

        [Test]
        public void Apply_rebuilds_runtimes_and_resolves_active_and_levels()
        {
            var snap = new DroneFleetSnapshot
            {
                Slots = 2, ActiveDroneId = "hauler",
                Drones = new List<DroneSnapshot>
                {
                    new DroneSnapshot { DroneId = "scout",  Upgrades = new Dictionary<string,int>() },
                    new DroneSnapshot { DroneId = "hauler", Upgrades = new Dictionary<string,int> { { "Cargo", 3 } } }
                }
            };

            var fleet = new DroneFleet();
            fleet.Apply(snap, _registry);

            Assert.AreEqual(2, fleet.UnlockedSlots);
            Assert.AreEqual("hauler", fleet.Active.Definition.DroneId);
            Assert.AreEqual(2, fleet.Active.Definition.Tier);
            Assert.AreEqual(3, fleet.Get("hauler").Level(DroneStat.Cargo));
            Assert.AreEqual(80, fleet.Get("hauler").EffectiveCargoCap); // 50 base default + 3*10
        }

        [Test]
        public void SingleDrone_snapshot_seeds_one_active_drone()
        {
            var fleet = new DroneFleet();
            fleet.Apply(DroneFleetSnapshot.SingleDrone("scout", slots: 2), _registry);
            Assert.AreEqual("scout", fleet.Active.Definition.DroneId);
            Assert.AreEqual(1, fleet.Drones.Count);
        }

        [Test]
        public void ToSnapshot_round_trips_through_Apply()
        {
            var fleet = new DroneFleet();
            fleet.Apply(DroneFleetSnapshot.SingleDrone("scout", 2), _registry);
            fleet.Get("scout").SetLevel(DroneStat.Cargo, 4);

            var snap = fleet.ToSnapshot();
            Assert.AreEqual("scout", snap.ActiveDroneId);
            Assert.AreEqual(4, snap.Drones[0].Upgrades["Cargo"]);
        }
    }
}
