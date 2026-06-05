using NUnit.Framework;
using SocialUniverse.Economy;

namespace SocialUniverse.Tests
{
    public class WalletTests
    {
        private Wallet _wallet;

        [SetUp]
        public void SetUp() => _wallet = new Wallet();

        [Test]
        public void CanAfford_returns_true_when_sufficient_coins()
        {
            _wallet.SetCoins(100);
            Assert.IsTrue(_wallet.CanAfford(100));
        }

        [Test]
        public void CanAfford_returns_false_when_insufficient_coins()
        {
            _wallet.SetCoins(50);
            Assert.IsFalse(_wallet.CanAfford(51));
        }

        [Test]
        public void SetCoins_fires_OnCoinsChanged()
        {
            int received = -1;
            _wallet.OnCoinsChanged += v => received = v;
            _wallet.SetCoins(200);
            Assert.AreEqual(200, received);
        }

        [Test]
        public void SetStardust_fires_OnStardustChanged()
        {
            int received = -1;
            _wallet.OnStardustChanged += v => received = v;
            _wallet.SetStardust(5);
            Assert.AreEqual(5, received);
        }
    }
}
