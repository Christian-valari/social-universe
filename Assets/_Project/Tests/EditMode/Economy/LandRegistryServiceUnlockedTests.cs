using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using SocialUniverse.Core;
using SocialUniverse.Economy;

namespace SocialUniverse.Tests
{
    public class LandRegistryServiceUnlockedTests
    {
        private class FakeBackend : IBackendClient
        {
            public object NextResult;
            public Task<T> CallAsync<T>(string function, Dictionary<string, object> args = null) =>
                Task.FromResult((T)NextResult);
            public Task CallAsync(string function, Dictionary<string, object> args = null) => Task.CompletedTask;
        }

        [Test]
        public async Task RefreshAsync_maps_unlocked_from_response()
        {
            var backend = new FakeBackend
            {
                NextResult = new LandRegistryData
                {
                    Tiles = new Dictionary<string, LandTileEntry>
                    {
                        ["t1"] = new LandTileEntry { OwnerId = "p", Unlocked = new[] { true, false, true } }
                    }
                }
            };
            var svc = new LandRegistryService(backend);
            await svc.RefreshAsync("Planet_Earth");
            Assert.AreEqual(new[] { true, false, true }, svc.GetEntry("t1").Unlocked);
        }
    }
}
