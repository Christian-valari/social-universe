using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using SocialUniverse.Economy;
using SocialUniverse.Core;

namespace SocialUniverse.Tests
{
    public class YieldServiceTests
    {
        private class FakeBackendClient : IBackendClient
        {
            public YieldClaimResult ClaimYieldResponse;

            public Task<T> CallAsync<T>(string function, Dictionary<string, object> args = null)
            {
                if (function == "ClaimYield" && typeof(T) == typeof(YieldClaimResult))
                {
                    object response = ClaimYieldResponse;
                    return Task.FromResult((T)response);
                }
                return Task.FromResult(default(T));
            }

            public Task CallAsync(string function, Dictionary<string, object> args = null) =>
                Task.CompletedTask;
        }

        private FakeBackendClient    _backend;
        private Wallet               _wallet;
        private LandRegistryService  _landRegistryService;
        private YieldService         _yieldService;

        [SetUp]
        public void SetUp()
        {
            _backend             = new FakeBackendClient();
            _wallet              = new Wallet();
            _landRegistryService = new LandRegistryService(_backend);
            _yieldService        = new YieldService(_backend, _wallet, _landRegistryService);

            _landRegistryService.SetOwner("7", "local_player");
            _landRegistryService.GetEntry("7").VisitCount = 5;
        }

        [Test]
        public async Task ClaimYieldAsync_on_success_updates_wallet_and_resets_yield_state()
        {
            _backend.ClaimYieldResponse = new YieldClaimResult { Success = true, Granted = 12, NewBalance = 512 };

            var result = await _yieldService.ClaimYieldAsync("7", "Planet_Earth");

            Assert.IsTrue(result.Success);
            Assert.AreEqual(512, _wallet.Coins);
            Assert.AreEqual(0, _landRegistryService.GetEntry("7").VisitCount);
        }

        [Test]
        public async Task ClaimYieldAsync_on_failure_does_not_modify_wallet_or_registry()
        {
            _backend.ClaimYieldResponse = new YieldClaimResult { Success = false, Reason = "NOT_OWNER" };

            var result = await _yieldService.ClaimYieldAsync("7", "Planet_Earth");

            Assert.IsFalse(result.Success);
            Assert.AreEqual(0, _wallet.Coins);
            Assert.AreEqual(5, _landRegistryService.GetEntry("7").VisitCount);
        }
    }
}
