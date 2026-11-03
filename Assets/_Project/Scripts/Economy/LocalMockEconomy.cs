using System.Threading.Tasks;
using SocialUniverse.Config;

namespace SocialUniverse.Economy
{
    public class LocalMockEconomy : IEconomyService
    {
        private readonly Wallet        _wallet;
        private readonly EconomyConfig _config;

        public LocalMockEconomy(Wallet wallet, EconomyConfig config)
        {
            _wallet = wallet;
            _config = config;
            _wallet.SetCoins(_config.StartingCoins);
            _wallet.SetStardust(_config.StartingStardust);
        }

        public Task<Wallet> GetWalletAsync() => Task.FromResult(_wallet);

        public Task<bool> SpendCoinsAsync(int amount)
        {
            if (!_wallet.CanAfford(amount)) return Task.FromResult(false);
            _wallet.SetCoins(_wallet.Coins - amount);
            return Task.FromResult(true);
        }
    }
}
