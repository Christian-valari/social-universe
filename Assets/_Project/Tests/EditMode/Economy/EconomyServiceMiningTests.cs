using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using SocialUniverse.Config;
using SocialUniverse.Economy;
using SocialUniverse.Core;
using UnityEngine;

namespace SocialUniverse.Tests
{
    public class EconomyServiceMiningTests
    {
        private class FakeBackendClient : IBackendClient
        {
            public string LastFunction;
            public Dictionary<string, object> LastArgs;
            public int  GrantedToReturn = 100;
            public int? NewBalanceToReturn = 600;

            public Task<T> CallAsync<T>(string function, Dictionary<string, object> args = null)
            {
                LastFunction = function;
                LastArgs     = args;

                if (function == "ValidateMining" && typeof(T) == typeof(EconomyService.MiningGrantResult))
                {
                    object response = new EconomyService.MiningGrantResult { Granted = GrantedToReturn, NewBalance = NewBalanceToReturn };
                    return Task.FromResult((T)response);
                }
                return Task.FromResult(default(T));
            }

            public Task CallAsync(string function, Dictionary<string, object> args = null) => Task.CompletedTask;
        }

        private EconomyConfig      _config;
        private Wallet             _wallet;
        private FakeBackendClient  _backend;
        private EconomyService     _economy;

        [SetUp]
        public void SetUp()
        {
            _config  = ScriptableObject.CreateInstance<EconomyConfig>();
            _wallet  = new Wallet();
            _backend = new FakeBackendClient();
            _economy = new EconomyService(_wallet, _config, _backend);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_config);

        [Test]
        public async Task GrantMiningRewardAsync_calls_ValidateMining_with_the_given_params()
        {
            await _economy.GrantMiningRewardAsync(claimedCoins: 200, sessionDurationSec: 300f, coinsPerSec: 0.667f);

            Assert.AreEqual("ValidateMining", _backend.LastFunction);
            Assert.AreEqual(200, _backend.LastArgs["claimedCoins"]);
            Assert.AreEqual(300f, _backend.LastArgs["sessionDurationSec"]);
            Assert.AreEqual(0.667f, _backend.LastArgs["coinsPerSec"]);
        }

        [Test]
        public async Task GrantMiningRewardAsync_returns_granted_amount_and_updates_wallet_from_newBalance()
        {
            _backend.GrantedToReturn    = 150;
            _backend.NewBalanceToReturn = 650;

            int granted = await _economy.GrantMiningRewardAsync(200, 300f, 0.667f);

            Assert.AreEqual(150, granted);
            Assert.AreEqual(650, _wallet.Coins);
        }

        [Test]
        public async Task GrantMiningRewardAsync_does_not_touch_wallet_when_newBalance_is_null()
        {
            _wallet.SetCoins(500);
            _backend.GrantedToReturn    = 0;
            _backend.NewBalanceToReturn = null; // ValidateMining.js returns this when grantAmount <= 0

            int granted = await _economy.GrantMiningRewardAsync(0, 300f, 0.667f);

            Assert.AreEqual(0, granted);
            Assert.AreEqual(500, _wallet.Coins, "wallet must not be zeroed out when the server reports no new balance");
        }
    }
}
