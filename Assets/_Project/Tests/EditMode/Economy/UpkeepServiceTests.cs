using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using SocialUniverse.Economy;
using SocialUniverse.Core;

namespace SocialUniverse.Tests
{
    public class UpkeepServiceTests
    {
        private class FakeBackendClient : IBackendClient
        {
            public UpkeepResult ApplyUpkeepResponse;

            public Task<T> CallAsync<T>(string function, Dictionary<string, object> args = null)
            {
                if (function == "ApplyUpkeep" && typeof(T) == typeof(UpkeepResult))
                {
                    object response = ApplyUpkeepResponse;
                    return Task.FromResult((T)response);
                }
                return Task.FromResult(default(T));
            }

            public Task CallAsync(string function, Dictionary<string, object> args = null) =>
                Task.CompletedTask;
        }

        [Test]
        public async Task ApplyUpkeepAsync_updates_wallet_and_removes_reverted_tiles()
        {
            var backend = new FakeBackendClient
            {
                ApplyUpkeepResponse = new UpkeepResult
                {
                    NewBalance    = 480,
                    ChargedTiles  = new List<string> { "3" },
                    RevertedTiles = new List<string> { "7" }
                }
            };
            var wallet              = new Wallet();
            var landRegistryService = new LandRegistryService(backend);
            landRegistryService.SetOwner("3", "local_player");
            landRegistryService.SetOwner("7", "local_player");

            var upkeepService = new UpkeepService(backend, wallet, landRegistryService);

            var result = await upkeepService.ApplyUpkeepAsync("Planet_Earth");

            Assert.AreEqual(480, wallet.Coins);
            Assert.IsNotNull(landRegistryService.GetEntry("3"));
            Assert.IsNull(landRegistryService.GetEntry("7"));
            CollectionAssert.AreEqual(new[] { "3" }, result.ChargedTiles);
            CollectionAssert.AreEqual(new[] { "7" }, result.RevertedTiles);
        }

        [Test]
        public async Task ApplyUpkeepAsync_with_no_due_tiles_leaves_wallet_and_registry_unchanged()
        {
            var backend = new FakeBackendClient
            {
                ApplyUpkeepResponse = new UpkeepResult { NewBalance = 500 }
            };
            var wallet              = new Wallet();
            var landRegistryService = new LandRegistryService(backend);
            landRegistryService.SetOwner("3", "local_player");

            var upkeepService = new UpkeepService(backend, wallet, landRegistryService);

            var result = await upkeepService.ApplyUpkeepAsync("Planet_Earth");

            Assert.AreEqual(500, wallet.Coins);
            Assert.IsNotNull(landRegistryService.GetEntry("3"));
            Assert.IsEmpty(result.RevertedTiles);
        }
    }
}
