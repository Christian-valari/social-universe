using System.Threading.Tasks;
using NUnit.Framework;
using SocialUniverse.Config;
using SocialUniverse.Economy;
using UnityEngine;

namespace SocialUniverse.Tests
{
    public class LocalMockEconomyTests
    {
        private EconomyConfig    _config;
        private Wallet           _wallet;
        private LocalMockEconomy _economy;

        [SetUp]
        public void SetUp()
        {
            _config  = ScriptableObject.CreateInstance<EconomyConfig>();
            _wallet  = new Wallet();
            _economy = new LocalMockEconomy(_wallet, _config);
        }

        [TearDown]
        public void TearDown() => UnityEngine.Object.DestroyImmediate(_config);

        [Test]
        public async Task GetWalletAsync_returns_wallet_with_starting_balance()
        {
            var w = await _economy.GetWalletAsync();
            Assert.AreEqual(_config.StartingCoins, w.Coins);
        }

        [Test]
        public async Task SpendCoinsAsync_deducts_coins_on_success()
        {
            bool ok = await _economy.SpendCoinsAsync(100);
            Assert.IsTrue(ok);
            Assert.AreEqual(_config.StartingCoins - 100, _wallet.Coins);
        }

        [Test]
        public async Task SpendCoinsAsync_fails_when_insufficient()
        {
            bool ok = await _economy.SpendCoinsAsync(999999);
            Assert.IsFalse(ok);
            Assert.AreEqual(_config.StartingCoins, _wallet.Coins);
        }
    }
}
