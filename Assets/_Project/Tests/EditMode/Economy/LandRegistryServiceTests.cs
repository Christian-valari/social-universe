using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using SocialUniverse.Economy;
using SocialUniverse.Core;

namespace SocialUniverse.Tests
{
    public class LandRegistryServiceTests
    {
        private class FakeBackendClient : IBackendClient
        {
            public Dictionary<string, LandTileEntry> Tiles = new();

            public Task<T> CallAsync<T>(string function, Dictionary<string, object> args = null)
            {
                if (function == "GetLandRegistry" && typeof(T) == typeof(LandRegistryData))
                {
                    object response = new LandRegistryData { Tiles = Tiles };
                    return Task.FromResult((T)response);
                }
                return Task.FromResult(default(T));
            }

            public Task CallAsync(string function, Dictionary<string, object> args = null) =>
                Task.CompletedTask;
        }

        [Test]
        public async Task RefreshAsync_populates_TileOwners_from_backend_response()
        {
            var backend = new FakeBackendClient
            {
                Tiles = new Dictionary<string, LandTileEntry>
                {
                    { "12", new LandTileEntry { OwnerId = "player_a" } },
                    { "45", new LandTileEntry { OwnerId = "player_b" } }
                }
            };
            var registry = new LandRegistryService(backend);

            await registry.RefreshAsync("Planet_Earth");

            Assert.AreEqual("player_a", registry.GetOwner("12"));
            Assert.AreEqual("player_b", registry.GetOwner("45"));
            Assert.AreEqual(2, registry.TileOwners.Count);
        }

        [Test]
        public void GetOwner_returns_null_for_unknown_tile()
        {
            var registry = new LandRegistryService(new FakeBackendClient());

            Assert.IsNull(registry.GetOwner("999"));
        }

        [Test]
        public void SetOwner_updates_local_cache_immediately()
        {
            var registry = new LandRegistryService(new FakeBackendClient());

            registry.SetOwner("7", "local_player");

            Assert.AreEqual("local_player", registry.GetOwner("7"));
            Assert.AreEqual(0, registry.GetEntry("7").BuildLevel);
        }

        [Test]
        public void SetBuildLevel_updates_existing_entry()
        {
            var registry = new LandRegistryService(new FakeBackendClient());
            registry.SetOwner("7", "local_player");

            registry.SetBuildLevel("7", 2);

            Assert.AreEqual(2, registry.GetEntry("7").BuildLevel);
        }

        [Test]
        public void ResetYieldState_resets_visit_count()
        {
            var registry = new LandRegistryService(new FakeBackendClient());
            registry.SetOwner("7", "local_player");
            registry.GetEntry("7").VisitCount = 5;

            registry.ResetYieldState("7");

            Assert.AreEqual(0, registry.GetEntry("7").VisitCount);
        }

        [Test]
        public void RemoveTile_clears_entry()
        {
            var registry = new LandRegistryService(new FakeBackendClient());
            registry.SetOwner("7", "local_player");

            registry.RemoveTile("7");

            Assert.IsNull(registry.GetOwner("7"));
        }
    }
}
