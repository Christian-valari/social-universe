using System;
using VContainer.Unity;
using SocialUniverse.Core;
using SocialUniverse.Mining;

namespace SocialUniverse.App
{
    public class MineralSaleHandler : IStartable, IDisposable
    {
        private readonly IMineralService _minerals;
        private bool _saleInFlight; // drops double-taps so one Sell can't send two SellMinerals calls (Known Issue #16)

        public MineralSaleHandler(IMineralService minerals) => _minerals = minerals;

        public void Start()   => EventBus.Subscribe<SellMineralsRequestedEvent>(OnSellRequested);
        public void Dispose() => EventBus.Unsubscribe<SellMineralsRequestedEvent>(OnSellRequested);

        private async void OnSellRequested(SellMineralsRequestedEvent e)
        {
            if (_saleInFlight) return;
            _saleInFlight = true;
            SellResult result;
            try
            {
                result = e.All ? await _minerals.SellAllAsync() : await _minerals.SellAsync(e.MineralId, e.Qty);
            }
            finally
            {
                _saleInFlight = false;
            }

            if (result is { Success: false })
            {
                SULog.Warn($"Sell minerals failed: {result.Reason}", SULog.Channel.Economy);
                EventBus.Publish(new ServerActionFailedEvent(ServerFailureMessages.For("Sale", result.Reason)));
            }
            // Wallet + MineralInventory events already fired by the service on success; the
            // view refreshes via MineralInventoryChangedEvent.
        }
    }
}
