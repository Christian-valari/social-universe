using System.Collections.Generic;
using System.Reflection;
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
    public class TravelServiceTests
    {
        private class FakeBackendClient : IBackendClient
        {
            public FuelStateResult SpendFuelResponse;

            public Task<T> CallAsync<T>(string function, Dictionary<string, object> args = null)
            {
                object response = function == "SpendFuel" ? SpendFuelResponse : null;
                return Task.FromResult((T)response);
            }

            public Task CallAsync(string function, Dictionary<string, object> args = null) =>
                Task.CompletedTask;
        }

        private static PlanetDefinition NewPlanet(string id, int travelFuelCost, int orbitOrder = 0)
        {
            var planet = ScriptableObject.CreateInstance<PlanetDefinition>();
            SetPrivateField(planet, "_planetId", id);
            SetPrivateField(planet, "_displayName", id);
            SetPrivateField(planet, "_travelFuelCost", travelFuelCost);
            SetPrivateField(planet, "_orbitOrder", orbitOrder);
            return planet;
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            field.SetValue(target, value);
        }

        [Test]
        public void GetFuelCost_for_home_planet_is_always_zero_regardless_of_configured_cost()
        {
            var current = NewPlanet("current", 0);
            var travelService = new TravelService(new FuelSystem(new FakeBackendClient(), new PlayerState(), new Wallet(), ScriptableObject.CreateInstance<EconomyConfig>()), current);
            var earth = NewPlanet(Constants.PlanetIds.Earth, 999);

            Assert.AreEqual(0, travelService.GetFuelCost(earth));
        }

        [Test]
        public void GetFuelCost_for_other_planets_returns_configured_cost()
        {
            var current = NewPlanet("current", 0);
            var travelService = new TravelService(new FuelSystem(new FakeBackendClient(), new PlayerState(), new Wallet(), ScriptableObject.CreateInstance<EconomyConfig>()), current);
            var mars = NewPlanet("mars", 20);

            Assert.AreEqual(20, travelService.GetFuelCost(mars));
        }

        [Test]
        public async Task TravelToPlanetAsync_succeeds_when_fuel_spend_succeeds()
        {
            var backend = new FakeBackendClient
            {
                SpendFuelResponse = new FuelStateResult { Success = true, Fuel = 80f, MaxFuel = 100f }
            };
            var fuelSystem    = new FuelSystem(backend, new PlayerState(), new Wallet(), ScriptableObject.CreateInstance<EconomyConfig>());
            var earth         = NewPlanet("earth", 0, orbitOrder: 3);
            var travelService = new TravelService(fuelSystem, earth);
            var mars          = NewPlanet("mars", 20, orbitOrder: 4);

            var result = await travelService.TravelToPlanetAsync(mars);

            Assert.IsTrue(result.Success);
        }

        [Test]
        public async Task TravelToPlanetAsync_fails_when_fuel_insufficient()
        {
            var backend = new FakeBackendClient
            {
                SpendFuelResponse = new FuelStateResult { Success = false, Fuel = 5f, MaxFuel = 100f }
            };
            var fuelSystem    = new FuelSystem(backend, new PlayerState(), new Wallet(), ScriptableObject.CreateInstance<EconomyConfig>());
            var neptune       = NewPlanet("neptune", 0, orbitOrder: 8);
            var travelService = new TravelService(fuelSystem, neptune);
            var pluto         = NewPlanet("pluto", 100, orbitOrder: 9);

            var result = await travelService.TravelToPlanetAsync(pluto);

            Assert.IsFalse(result.Success);
            Assert.AreEqual("InsufficientFuel", result.FailureReason);
        }

        [Test]
        public async Task TravelToPlanetAsync_fails_when_target_out_of_range()
        {
            var backend       = new FakeBackendClient
            {
                SpendFuelResponse = new FuelStateResult { Success = true, Fuel = 80f, MaxFuel = 100f }
            };
            var fuelSystem    = new FuelSystem(backend, new PlayerState(), new Wallet(), ScriptableObject.CreateInstance<EconomyConfig>());
            var mercury       = NewPlanet("mercury", 0, orbitOrder: 1);
            var travelService = new TravelService(fuelSystem, mercury);
            var pluto         = NewPlanet("pluto", 100, orbitOrder: 9);

            var result = await travelService.TravelToPlanetAsync(pluto);

            Assert.IsFalse(result.Success);
            Assert.AreEqual("OutOfRange", result.FailureReason);
        }

        [Test]
        public async Task TravelToPlanetAsync_to_home_never_calls_backend()
        {
            var backend       = new FakeBackendClient();
            var fuelSystem    = new FuelSystem(backend, new PlayerState(), new Wallet(), ScriptableObject.CreateInstance<EconomyConfig>());
            // current planet is far away (orbit order 9) to prove "free trip home" also bypasses the range check.
            var farAway       = NewPlanet("pluto", 0, orbitOrder: 9);
            var travelService = new TravelService(fuelSystem, farAway);
            var earth         = NewPlanet(Constants.PlanetIds.Earth, 999, orbitOrder: 3);

            var result = await travelService.TravelToPlanetAsync(earth);

            Assert.IsTrue(result.Success);
        }
    }
}
