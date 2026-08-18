using System.Collections.Generic;
using System.Threading.Tasks;
using SocialUniverse.Config;
using SocialUniverse.Economy;

namespace SocialUniverse.Mining
{
    // Dev-mode mineral service: validates against the in-memory inventory + registry,
    // pays coins into the wallet locally. No server round-trip.
    public class LocalMockMineralService : IMineralService
    {
        private readonly MineralInventory _inventory;
        private readonly Wallet           _wallet;
        private readonly DatabaseRegistry _registry;

        public LocalMockMineralService(MineralInventory inventory, Wallet wallet, DatabaseRegistry registry)
        {
            _inventory = inventory;
            _wallet    = wallet;
            _registry  = registry;
        }

        public Task<SellResult> SellAsync(string mineralId, int qty)
        {
            int held = _inventory.Get(mineralId);
            var def  = _registry.GetMineral(mineralId);
            if (def == null || qty <= 0 || held < qty)
                return Task.FromResult(new SellResult { Success = false, Reason = "INSUFFICIENT_QTY" });

            _inventory.Add(mineralId, -qty);
            _wallet.SetCoins(_wallet.Coins + qty * def.SellValue);
            return Task.FromResult(Snapshot());
        }

        public Task<SellResult> SellAllAsync()
        {
            int payout = _inventory.TotalSellValue(_registry);
            _inventory.SetAll(new Dictionary<string, int>());
            _wallet.SetCoins(_wallet.Coins + payout);
            return Task.FromResult(Snapshot());
        }

        public Task<int> GrantMiningAsync(string mineralId, int qty, float sessionDurationSec, float unitsPerSec)
        {
            if (!string.IsNullOrEmpty(mineralId) && qty > 0) _inventory.Add(mineralId, qty);
            return Task.FromResult(qty);
        }

        private SellResult Snapshot() => new SellResult
        {
            Success = true,
            NewBalance = _wallet.Coins,
            RemainingInventory = new Dictionary<string, int>(_inventory.All)
        };
    }
}
