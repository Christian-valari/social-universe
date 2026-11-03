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

            // The sale popup shows the result. On success the service has already fired the wallet
            // and MineralInventoryChangedEvent updates. Always publishes, even for a missing reply,
            // so the popup never waits forever.
            bool   ok      = result is { Success: true };
            string message = "";
            if (!ok)
            {
                string reason = result?.Reason ?? "Empty response";
                SULog.Warn($"Sell minerals failed: {reason}", SULog.Channel.Economy);
                message = ServerFailureMessages.For("Sale", reason);
            }
            EventBus.Publish(new MineralSaleCompletedEvent { Success = ok, Message = message });
        }
    }
}
