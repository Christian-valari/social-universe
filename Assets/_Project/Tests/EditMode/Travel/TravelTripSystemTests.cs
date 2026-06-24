using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
using SocialUniverse.Config;
using SocialUniverse.Core;
using SocialUniverse.Progression;
using SocialUniverse.Travel;
using UnityEngine;

namespace SocialUniverse.Tests
{
    public class TravelTripSystemTests
    {
        private class FakeBackendClient : IBackendClient
        {
            public TravelTripResult StartTravelResponse;
            public TravelTripResult GetTravelStateResponse;
            public TravelTripResult LandTravelResponse;
            public string LastFunction;

            public Task<T> CallAsync<T>(string function, Dictionary<string, object> args = null)
            {
                LastFunction = function;
                object response = function switch
                {
                    "StartTravel"    => StartTravelResponse,
                    "GetTravelState" => GetTravelStateResponse,
                    "LandTravel"     => LandTravelResponse,
                    _                => null
                };
                return Task.FromResult((T)response);
            }

            public Task CallAsync(string function, Dictionary<string, object> args = null) =>
                Task.CompletedTask;
        }

        private static PlanetDefinition NewPlanet(string id)
        {
            var planet = ScriptableObject.CreateInstance<PlanetDefinition>();
            var field = planet.GetType().GetField("_planetId", BindingFlags.NonPublic | BindingFlags.Instance);
            field.SetValue(planet, id);
            return planet;
        }

        [Test]
        public async Task StartTravelAsync_applies_traveling_state_on_success()
        {
            var backend = new FakeBackendClient
            {
                StartTravelResponse = new TravelTripResult
                {
                    Success = true, Traveling = true, TargetPlanetId = "mars", ArrivalTs = 123456L,
                    Fuel = 80f, MaxFuel = 100f
                }
            };
            var playerState = new PlayerState();
            var trips = new TravelTripSystem(backend, playerState, NewPlanet("current"));

            var result = await trips.StartTravelAsync(NewPlanet("mars"));

            Assert.IsTrue(result.Success);
            Assert.IsTrue(playerState.IsTraveling);
            Assert.AreEqual("mars", playerState.TravelTargetId);
            Assert.AreEqual(123456L, playerState.TravelArrivalTsMs);
            Assert.AreEqual(80f, playerState.Fuel);
        }

        [Test]
        public async Task StartTravelAsync_does_not_set_traveling_when_backend_returns_failure()
        {
            var backend = new FakeBackendClient
            {
                StartTravelResponse = new TravelTripResult { Success = false, Reason = "insufficient_fuel", Fuel = 5f, MaxFuel = 100f }
            };
            var playerState = new PlayerState();
            var trips = new TravelTripSystem(backend, playerState, NewPlanet("current"));

            var result = await trips.StartTravelAsync(NewPlanet("mars"));

            Assert.IsFalse(result.Success);
            Assert.IsFalse(playerState.IsTraveling);
            Assert.AreEqual(5f, playerState.Fuel);
        }

        [Test]
        public async Task LandAsync_clears_traveling_state_on_success()
        {
            var backend = new FakeBackendClient
            {
                LandTravelResponse = new TravelTripResult { Success = true, TargetPlanetId = "mars" }
            };
            var playerState = new PlayerState();
            playerState.SetTravelState(true, "mars", 999L);
            var trips = new TravelTripSystem(backend, playerState, NewPlanet("current"));

            var result = await trips.LandAsync();

            Assert.IsTrue(result.Success);
            Assert.IsFalse(playerState.IsTraveling);
            Assert.IsNull(playerState.TravelTargetId);
        }

        [Test]
        public async Task LandAsync_leaves_traveling_state_unchanged_on_failure()
        {
            var backend = new FakeBackendClient
            {
                LandTravelResponse = new TravelTripResult { Success = false, Reason = "not_arrived" }
            };
            var playerState = new PlayerState();
            playerState.SetTravelState(true, "mars", 999L);
            var trips = new TravelTripSystem(backend, playerState, NewPlanet("current"));

            var result = await trips.LandAsync();

            Assert.IsFalse(result.Success);
            Assert.IsTrue(playerState.IsTraveling);
        }

        [Test]
        public async Task RefreshAsync_applies_in_progress_trip_from_server()
        {
            var backend = new FakeBackendClient
            {
                GetTravelStateResponse = new TravelTripResult { Success = true, Traveling = true, TargetPlanetId = "pluto", ArrivalTs = 555L }
            };
            var playerState = new PlayerState();
            var trips = new TravelTripSystem(backend, playerState, NewPlanet("current"));

            await trips.RefreshAsync();

            Assert.IsTrue(playerState.IsTraveling);
            Assert.AreEqual("pluto", playerState.TravelTargetId);
            Assert.AreEqual(555L, playerState.TravelArrivalTsMs);
        }

        [Test]
        public async Task RefreshAsync_with_no_trip_leaves_player_state_not_traveling()
        {
            var backend = new FakeBackendClient
            {
                GetTravelStateResponse = new TravelTripResult { Success = true, Traveling = false }
            };
            var playerState = new PlayerState();
            var trips = new TravelTripSystem(backend, playerState, NewPlanet("current"));

            await trips.RefreshAsync();

            Assert.IsFalse(playerState.IsTraveling);
        }
    }
}
