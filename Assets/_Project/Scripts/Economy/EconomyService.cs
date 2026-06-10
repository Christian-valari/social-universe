using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unity.Services.Economy;
using SocialUniverse.Config;
using SocialUniverse.Core;
using SocialUniverse.Net;

namespace SocialUniverse.Economy
{
    // Server-authoritative economy backed by UGS Economy + Cloud Code.
    // Reads go directly to the UGS Economy SDK (cached balance fetch).
    // Writes (grant/spend) go through Cloud Code so the server validates before committing.
    public class EconomyService : IEconomyService
    {
        private readonly Wallet         _wallet;
        private readonly EconomyConfig  _config;
        private readonly IBackendClient _backend;

        public EconomyService(Wallet wallet, EconomyConfig config, IBackendClient backend)
        {
            _wallet  = wallet;
            _config  = config;
            _backend = backend;
        }

        public async Task<Wallet> GetWalletAsync()
        {
            var response = await Unity.Services.Economy.EconomyService.Instance
                .PlayerBalances.GetBalancesAsync();

            foreach (var balance in response.Balances)
            {
                if (balance.CurrencyId == _config.CoinsCurrencyId)
                    _wallet.SetCoins((int)balance.Balance);
                else if (balance.CurrencyId == _config.StardustCurrencyId)
                    _wallet.SetStardust((int)balance.Balance);
            }

            return _wallet;
        }

        public async Task<bool> SpendCoinsAsync(int amount)
        {
            if (!_wallet.CanAfford(amount)) return false;

            var result = await _backend.CallAsync<SpendResult>(
                "SpendCoins",
                new Dictionary<string, object> { { "amount", amount } });

            if (result.Success)
                _wallet.SetCoins(result.NewBalance);

            return result.Success;
        }

        public async Task GrantCoinsAsync(int amount)
        {
            var result = await _backend.CallAsync<GrantResult>(
                "GrantCoins",
                new Dictionary<string, object> { { "amount", amount } });

            _wallet.SetCoins(result.NewBalance);
        }

        public async Task GrantStardustAsync(int amount)
        {
            var result = await _backend.CallAsync<GrantResult>(
                "GrantStardust",
                new Dictionary<string, object> { { "amount", amount } });

            _wallet.SetStardust(result.NewBalance);
        }

        // Thin DTOs for Cloud Code responses.
        private struct SpendResult  { public bool Success; public int NewBalance; }
        private struct GrantResult  { public int NewBalance; }
    }
}
