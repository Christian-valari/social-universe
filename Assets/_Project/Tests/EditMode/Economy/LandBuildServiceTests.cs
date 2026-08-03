using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using SocialUniverse.Core;
using SocialUniverse.Economy;

namespace SocialUniverse.Tests
{
    public class LandBuildServiceTests
    {
        // Captures the last call and returns a pre-set object cast to the requested type.
        private class FakeBackendClient : IBackendClient
        {
            public string LastFunction;
            public Dictionary<string, object> LastArgs;
            public object NextResult;

            public Task<T> CallAsync<T>(string function, Dictionary<string, object> args = null)
            {
                LastFunction = function;
                LastArgs = args;
                return Task.FromResult((T)NextResult);
            }

            public Task CallAsync(string function, Dictionary<string, object> args = null) => Task.CompletedTask;
        }

        [Test]
        public async Task PlaceAsync_sends_expected_params_and_maps_result()
        {
            var backend = new FakeBackendClient
            {
                NextResult = new PlaceBuildResult { Success = true, NewBalance = 420, BuildLevel = 3 }
            };
            var service = new LandBuildService(backend);

            var result = await service.PlaceAsync("12", "earth", 2, "item_tree", 50);

            Assert.AreEqual("PlaceBuild", backend.LastFunction);
            Assert.AreEqual("12",        backend.LastArgs["tileId"]);
            Assert.AreEqual("earth",     backend.LastArgs["planetId"]);
            Assert.AreEqual(2,           backend.LastArgs["slotIndex"]);
            Assert.AreEqual("item_tree", backend.LastArgs["itemId"]);
            Assert.AreEqual(50,          backend.LastArgs["cost"]);
            Assert.IsTrue(result.Success);
            Assert.AreEqual(420, result.NewBalance);
            Assert.AreEqual(3,   result.BuildLevel);
        }

        [Test]
        public async Task PlaceAsync_returns_failure_on_null_response()
        {
            var service = new LandBuildService(new FakeBackendClient { NextResult = null });
            var result = await service.PlaceAsync("12", "earth", 0, "x", 10);
            Assert.IsFalse(result.Success);
        }

        [Test]
        public async Task RemoveAsync_sends_expected_params_and_maps_result()
        {
            var backend = new FakeBackendClient
            {
                NextResult = new RemoveBuildResult { Success = true, BuildLevel = 1 }
            };
            var service = new LandBuildService(backend);

            var result = await service.RemoveAsync("12", "earth", 1);

            Assert.AreEqual("RemoveBuild", backend.LastFunction);
            Assert.AreEqual(1, backend.LastArgs["slotIndex"]);
            Assert.IsTrue(result.Success);
            Assert.AreEqual(1, result.BuildLevel);
        }

        [Test]
        public async Task MoveAsync_sends_from_and_to_slots()
        {
            var backend = new FakeBackendClient
            {
                NextResult = new MoveBuildResult { Success = true }
            };
            var service = new LandBuildService(backend);

            var result = await service.MoveAsync("12", "earth", 0, 3);

            Assert.AreEqual("MoveBuild", backend.LastFunction);
            Assert.AreEqual(0, backend.LastArgs["fromSlot"]);
            Assert.AreEqual(3, backend.LastArgs["toSlot"]);
            Assert.IsTrue(result.Success);
        }
    }
}
