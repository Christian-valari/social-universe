using System;
using VContainer.Unity;
using SocialUniverse.Core;
using SocialUniverse.Mining;

namespace SocialUniverse.App
{
    public class MineralSaleHandler : IStartable, IDisposable
    {
        private readonly IMineralService _minerals;

        public MineralSaleHandler(IMineralService minerals) => _minerals = minerals;

        public void Start()   => EventBus.Subscribe<SellMineralsRequestedEvent>(OnSellRequested);
        public void Dispose() => EventBus.Unsubscribe<SellMineralsRequestedEvent>(OnSellRequested);

        private async void OnSellRequested(SellMineralsRequestedEvent e)
        {
            var result = e.All ? await _minerals.SellAllAsync() : await _minerals.SellAsync(e.MineralId, e.Qty);
            if (result is { Success: false })
                SULog.Warn($"Sell minerals failed: {result.Reason}", SULog.Channel.Economy);
            // Wallet + MineralInventory events already fired by the service on success; the
            // view refreshes via MineralInventoryChangedEvent.
        }
    }
}
