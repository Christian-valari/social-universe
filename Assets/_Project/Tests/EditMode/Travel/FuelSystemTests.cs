using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using SocialUniverse.Config;
using SocialUniverse.Core;
using SocialUniverse.Economy;
using SocialUniverse.Progression;
using SocialUniverse.Travel;
using UnityEngine;

namespace SocialUniverse.Tests
{
    public class FuelSystemTests
    {
        private class FakeBackendClient : IBackendClient
        {
            public FuelStateResult GetFuelStateResponse;
            public FuelStateResult SpendFuelResponse;
            public FuelStateResult RefillFuelResponse;
            public string LastFunction;
            public Dictionary<string, object> LastArgs;

            public Task<T> CallAsync<T>(string function, Dictionary<string, object> args = null)
            {
                LastFunction = function;
                LastArgs     = args;

                object response = function switch
                {
                    "GetFuelState" => GetFuelStateResponse,
                    "SpendFuel"    => SpendFuelResponse,
                    "RefillFuel"   => RefillFuelResponse,
                    _              => null
                };
                return Task.FromResult((T)response);
            }

            public Task CallAsync(string function, Dictionary<string, object> args = null) =>
                Task.CompletedTask;
        }

        private static EconomyConfig NewConfig() => ScriptableObject.CreateInstance<EconomyConfig>();

        [Test]
        public async Task RefreshAsync_applies_fuel_and_maxFuel_to_PlayerState()
        {
            var backend = new FakeBackendClient
            {
                GetFuelStateResponse = new FuelStateResult { Success = true, Fuel = 42f, MaxFuel = 120f }
            };
            var playerState = new PlayerState();
            var fuelSystem  = new FuelSystem(backend, playerState, new Wallet(), NewConfig());

            await fuelSystem.RefreshAsync();

            Assert.AreEqual(120f, playerState.MaxFuel);
            Assert.AreEqual(42f, playerState.Fuel);
        }

        [Test]
        public async Task TrySpendAsync_on_success_deducts_and_returns_true()
        {
            var backend = new FakeBackendClient
            {
                SpendFuelResponse = new FuelStateResult { Success = true, Fuel = 80f, MaxFuel = 100f }
            };
            var playerState = new PlayerState();
            var fuelSystem  = new FuelSystem(backend, playerState, new Wallet(), NewConfig());

            bool success = await fuelSystem.TrySpendAsync(20f);

            Assert.IsTrue(success);
            Assert.AreEqual(80f, playerState.Fuel);
            Assert.AreEqual("SpendFuel", backend.LastFunction);
        }

        [Test]
        public async Task TrySpendAsync_on_failure_still_resyncs_fuel_and_returns_false()
        {
            var backend = new FakeBackendClient
            {
                SpendFuelResponse = new FuelStateResult { Success = false, Fuel = 5f, MaxFuel = 100f }
            };
            var playerState = new PlayerState();
            var fuelSystem  = new FuelSystem(backend, playerState, new Wallet(), NewConfig());

            bool success = await fuelSystem.TrySpendAsync(50f);

            Assert.IsFalse(success);
            Assert.AreEqual(5f, playerState.Fuel);
        }

        [Test]
        public async Task TrySpendAsync_with_zero_or_negative_amount_short_circuits_without_calling_backend()
        {
            var backend     = new FakeBackendClient();
            var playerState = new PlayerState();
            var fuelSystem  = new FuelSystem(backend, playerState, new Wallet(), NewConfig());

            bool success = await fuelSystem.TrySpendAsync(0f);

            Assert.IsTrue(success);
            Assert.IsNull(backend.LastFunction);
        }

        [Test]
        public async Task RefillAsync_on_success_updates_fuel_and_wallet()
        {
            var backend = new FakeBackendClient
            {
                RefillFuelResponse = new FuelStateResult { Success = true, Fuel = 100f, MaxFuel = 100f, NewBalance = 450 }
            };
            var playerState = new PlayerState();
            var wallet       = new Wallet();
            var fuelSystem   = new FuelSystem(backend, playerState, wallet, NewConfig());

            bool success = await fuelSystem.RefillAsync();

            Assert.IsTrue(success);
            Assert.AreEqual(100f, playerState.Fuel);
            Assert.AreEqual(450, wallet.Coins);
        }

        [Test]
        public async Task RefillAsync_on_failure_leaves_wallet_unchanged()
        {
            var backend = new FakeBackendClient
            {
                RefillFuelResponse = new FuelStateResult { Success = false, NewBalance = 10 }
            };
            var playerState = new PlayerState();
            var wallet       = new Wallet();
            wallet.SetCoins(10);
            var fuelSystem = new FuelSystem(backend, playerState, wallet, NewConfig());

            bool success = await fuelSystem.RefillAsync();

            Assert.IsFalse(success);
            Assert.AreEqual(10, wallet.Coins);
        }
    }
}
