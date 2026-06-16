using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using SocialUniverse.Config;
using SocialUniverse.Economy;
using SocialUniverse.Core;
using UnityEngine;

namespace SocialUniverse.Tests
{
    public class LandSaleServiceTests
    {
        private class FakeBackendClient : IBackendClient
        {
            public LandSaleResult SellLandResponse;

            public Task<T> CallAsync<T>(string function, Dictionary<string, object> args = null)
            {
                if (function == "SellLand" && typeof(T) == typeof(LandSaleResult))
                {
                    object response = SellLandResponse;
                    return Task.FromResult((T)response);
                }
                return Task.FromResult(default(T));
            }

            public Task CallAsync(string function, Dictionary<string, object> args = null) =>
                Task.CompletedTask;
        }

        private EconomyConfig    _config;
        private PlanetDefinition _planet;

        [SetUp]
        public void SetUp()
        {
            _config = ScriptableObject.CreateInstance<EconomyConfig>();
            _planet = ScriptableObject.CreateInstance<PlanetDefinition>();
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_config);
            UnityEngine.Object.DestroyImmediate(_planet);
        }

        [Test]
        public async Task SellAsync_on_success_updates_wallet_and_clears_ownership()
        {
            var backend = new FakeBackendClient
            {
                SellLandResponse = new LandSaleResult { Success = true, NewBalance = 550 }
            };
            var registry            = new LandRegistry();
            var wallet              = new Wallet();
            var landRegistryService = new LandRegistryService(backend);
            registry.MarkOwned("7");
            landRegistryService.SetOwner("7", "local_player");

            var saleService = new LandSaleService(backend, _config, registry, wallet, landRegistryService);

            var result = await saleService.SellAsync("7", _planet);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(550, wallet.Coins);
            Assert.IsFalse(registry.IsOwned("7"));
            Assert.IsNull(landRegistryService.GetOwner("7"));
        }

        [Test]
        public async Task SellAsync_on_failure_leaves_wallet_and_ownership_unchanged()
        {
            var backend = new FakeBackendClient
            {
                SellLandResponse = new LandSaleResult { Success = false, Reason = "NOT_OWNER" }
            };
            var registry            = new LandRegistry();
            var wallet              = new Wallet();
            var landRegistryService = new LandRegistryService(backend);
            registry.MarkOwned("7");
            landRegistryService.SetOwner("7", "local_player");

            var saleService = new LandSaleService(backend, _config, registry, wallet, landRegistryService);

            var result = await saleService.SellAsync("7", _planet);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(0, wallet.Coins);
            Assert.IsTrue(registry.IsOwned("7"));
            Assert.AreEqual("local_player", landRegistryService.GetOwner("7"));
        }
    }
}
