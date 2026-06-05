using System;
using System.Threading.Tasks;
using SocialUniverse.Config;
using SocialUniverse.Core;

namespace SocialUniverse.Economy
{
    public class LandPurchaseRequest
    {
        public string TileId;
        public string PlayerId;
    }

    public class LandPurchaseResult
    {
        public bool   Success;
        public string FailureReason;
    }

    public class LandPurchaseService
    {
        private readonly IEconomyService _economy;
        private readonly EconomyConfig   _config;

        public LandPurchaseService(IEconomyService economy, EconomyConfig config)
        {
            _economy = economy;
            _config  = config;
        }

        public async Task<LandPurchaseResult> PurchaseAsync(LandPurchaseRequest request, PlanetDefinition planet)
        {
            int  price   = (int)Math.Round(_config.BaseLandPrice * planet.LandPriceMultiplier);
            bool success = await _economy.SpendCoinsAsync(price);

            if (!success)
                return new LandPurchaseResult { Success = false, FailureReason = "Insufficient coins" };

            SULog.Info($"Land purchased: {request.TileId} by {request.PlayerId} for {price} coins", SULog.Channel.Economy);
            return new LandPurchaseResult { Success = true };
        }
    }
}
