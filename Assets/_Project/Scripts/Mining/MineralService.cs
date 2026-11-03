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

        // Never adds minerals, whatever the reply says: a forfeit grants nothing.
        public async Task<MiningGrantResult> ForfeitMiningAsync(string planetId, string mineralId, string slotId)
        {
            var args = new Dictionary<string, object>
            {
                { "planetId",  planetId },
                { "mineralId", mineralId },
                { "slotId",    slotId },
                { "forfeit",   true }
            };
            return await _backend.CallAsync<MiningGrantResult>("ValidateMining", args)
                ?? new MiningGrantResult { MineralId = mineralId, Reason = "Empty response" };
        }

        public async Task<MiningGrantResult> GrantMiningAsync(string planetId, string mineralId, int qty, string idleSlotId = null,
            string slotId = null)
        {
            // Nothing to grant — unless the claim ends a server-tracked idle run, which only
            // ValidateMining can end (it answers INVALID_PARAMS and still ends the run).
            if ((string.IsNullOrEmpty(mineralId) || qty <= 0) && string.IsNullOrEmpty(idleSlotId))
                return new MiningGrantResult { MineralId = mineralId };

            var args = new Dictionary<string, object>
            {
                { "planetId",   planetId },
                { "mineralId",  mineralId },
                { "claimedQty", qty }
            };
            if (!string.IsNullOrEmpty(idleSlotId)) args["idleSlotId"] = idleSlotId;
            if (!string.IsNullOrEmpty(slotId))     args["slotId"]     = slotId;

            var res = await _backend.CallAsync<MiningGrantResult>("ValidateMining", args)
                ?? new MiningGrantResult { MineralId = mineralId, Reason = "Empty response" };

            if (res.Granted > 0) _inventory.Add(mineralId, res.Granted);
            return res;
        }
    }
}
