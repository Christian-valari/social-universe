using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SocialUniverse.Core;
using SocialUniverse.Economy;

namespace SocialUniverse.Mining
{
    public class MineralService : IMineralService
    {
        private readonly IBackendClient   _backend;
        private readonly MineralInventory _inventory;
        private readonly Wallet           _wallet;

        public MineralService(IBackendClient backend, MineralInventory inventory, Wallet wallet)
        {
            _backend   = backend;
            _inventory = inventory;
            _wallet    = wallet;
        }

        public Task<SellResult> SellAsync(string mineralId, int qty) =>
            SellInternalAsync(new Dictionary<string, object> { { "mineralId", mineralId }, { "qty", qty } });

        public Task<SellResult> SellAllAsync() =>
            SellInternalAsync(new Dictionary<string, object> { { "all", true } });

        private async Task<SellResult> SellInternalAsync(Dictionary<string, object> args)
        {
            SellResult res;
            try
            {
                res = await _backend.CallAsync<SellResult>("SellMinerals", args);
            }
            catch (Exception ex)
            {
                SULog.Error($"MineralService.Sell failed — {ex.Message}", SULog.Channel.Economy);
                return new SellResult { Success = false, Reason = "Network error" };
            }

            if (res != null && res.Success)
            {
                if (res.NewBalance >= 0) _wallet.SetCoins(res.NewBalance);
                if (res.RemainingInventory != null) _inventory.SetAll(res.RemainingInventory);
            }
            return res ?? new SellResult { Success = false, Reason = "Empty response" };
        }

        public async Task<int> GrantMiningAsync(string mineralId, int qty, float sessionDurationSec, float unitsPerSec)
        {
            if (string.IsNullOrEmpty(mineralId) || qty <= 0) return 0;

            var res = await _backend.CallAsync<GrantResponse>("ValidateMining", new Dictionary<string, object>
            {
                { "mineralId",          mineralId },
                { "claimedQty",         qty },
                { "sessionDurationSec", sessionDurationSec },
                { "unitsPerSec",        unitsPerSec }
            });

            int granted = res?.granted ?? 0;
            if (granted > 0) _inventory.Add(mineralId, granted);
            return granted;
        }

        // MUST MATCH the return shape of ServerCode/ValidateMining.js.
        private class GrantResponse
        {
            public int granted;
            public string mineralId;
        }
    }
}
