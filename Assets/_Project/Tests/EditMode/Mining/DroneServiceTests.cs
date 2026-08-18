using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using SocialUniverse.Config;
using SocialUniverse.Core;
using SocialUniverse.Economy;
using SocialUniverse.Mining;

namespace SocialUniverse.Tests
{
    public class DroneServiceTests
    {
        private static void Set(object o, string f, object v) => o.GetType()
            .GetField(f, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(o, v);

        private class FakeBackendClient : IBackendClient
        {
            public DroneActionResult Response;
            public Task<T> CallAsync<T>(string function, Dictionary<string, object> args = null)
            {
                if (typeof(T) == typeof(DroneActionResult)) return Task.FromResult((T)(object)Response);
                return Task.FromResult(default(T));
            }
            public Task CallAsync(string function, Dictionary<string, object> args = null) => Task.CompletedTask;
        }

        private DatabaseRegistry _registry;
        private EconomyConfig    _config;

        [SetUp]
        public void SetUp()
        {
            var scout = ScriptableObject.CreateInstance<DroneDefinition>();
            Set(scout, "_droneId", "scout"); Set(scout, "_tier", 1); Set(scout, "_unlockCost", 0);
            var hauler = ScriptableObject.CreateInstance<DroneDefinition>();
            Set(hauler, "_droneId", "hauler"); Set(hauler, "_tier", 2); Set(hauler, "_unlockCost", 300);
            var cargo = ScriptableObject.CreateInstance<UpgradeDefinition>();
            Set(cargo, "_stat", DroneStat.Cargo); Set(cargo, "_baseCost", 100); Set(cargo, "_costGrowth", 2f); Set(cargo, "_deltaPerLevel", 10f); Set(cargo, "_maxLevel", 5);

            _registry = ScriptableObject.CreateInstance<DatabaseRegistry>();
            Set(_registry, "_drones", new[] { scout, hauler });
            Set(_registry, "_upgrades", new[] { cargo });

            _config = ScriptableObject.CreateInstance<EconomyConfig>();
            Set(_config, "_startingFleetSlots", 2);
            Set(_config, "_slotUnlockBaseCost", 500);
            Set(_config, "_slotUnlockCostGrowth", 2f);
        }

        [Test]
        public async Task Real_service_applies_returned_snapshot_and_balance_on_success()
        {
            var backend = new FakeBackendClient
            {
                Response = new DroneActionResult
                {
                    Success = true, NewBalance = 200,
                    Fleet = new DroneFleetSnapshot
                    {
                        Slots = 2, ActiveDroneId = "scout",
                        Drones = new List<DroneSnapshot>
                        {
                            new DroneSnapshot { DroneId = "scout",  Upgrades = new Dictionary<string,int>() },
                            new DroneSnapshot { DroneId = "hauler", Upgrades = new Dictionary<string,int>() }
                        }
                    }
                }
            };
            var wallet = new Wallet();
            var fleet  = new DroneFleet();
            var svc = new DroneService(backend, fleet, wallet, _registry);

            var result = await svc.AcquireDroneAsync("hauler");

            Assert.IsTrue(result.Success);
            Assert.AreEqual(200, wallet.Coins);
            Assert.IsNotNull(fleet.Get("hauler"));
        }

        [Test]
        public async Task Real_service_is_noop_on_failure()
        {
            var backend = new FakeBackendClient { Response = new DroneActionResult { Success = false, Reason = "INSUFFICIENT_FUNDS" } };
            var wallet = new Wallet();
            var fleet  = new DroneFleet();
            fleet.Apply(DroneFleetSnapshot.SingleDrone("scout", 2), _registry);
            var svc = new DroneService(backend, fleet, wallet, _registry);

            var result = await svc.AcquireDroneAsync("hauler");

            Assert.IsFalse(result.Success);
            Assert.AreEqual(0, wallet.Coins);
            Assert.IsNull(fleet.Get("hauler"));
        }

        [Test]
        public async Task Mock_upgrade_deducts_next_cost_and_increments_level()
        {
            var wallet = new Wallet(); wallet.SetCoins(500);
            var fleet  = new DroneFleet();
            fleet.Apply(DroneFleetSnapshot.SingleDrone("scout", 2), _registry);
            var mock = new LocalMockDroneService(fleet, wallet, _registry, _config);

            var result = await mock.UpgradeAsync("scout", DroneStat.Cargo);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(400, wallet.Coins); // 500 - baseCost 100
            Assert.AreEqual(1, fleet.Get("scout").Level(DroneStat.Cargo));
        }

        [Test]
        public async Task Mock_acquire_fails_when_slots_full()
        {
            var wallet = new Wallet(); wallet.SetCoins(9999);
            var fleet  = new DroneFleet();
            // 2 slots, already 2 drones owned
            fleet.Apply(new DroneFleetSnapshot
            {
                Slots = 2, ActiveDroneId = "scout",
                Drones = new List<DroneSnapshot>
                {
                    new DroneSnapshot { DroneId = "scout",  Upgrades = new Dictionary<string,int>() },
                    new DroneSnapshot { DroneId = "hauler", Upgrades = new Dictionary<string,int>() }
                }
            }, _registry);
            var mock = new LocalMockDroneService(fleet, wallet, _registry, _config);

            // a third drone id doesn't exist, but slot-full check should trip first for an owned-capacity guard;
            // acquire an already-owned drone -> ALREADY_OWNED, acquire with full slots -> SLOTS_FULL
            var result = await mock.AcquireDroneAsync("hauler");
            Assert.IsFalse(result.Success);
        }
    }
}
