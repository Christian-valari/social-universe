using System;
using System.Collections.Generic;
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
        private readonly IBackendClient _backend;
        private readonly EconomyConfig  _config;
        private readonly LandRegistry   _registry;
        private readonly Wallet         _wallet;

        public LandPurchaseService(IBackendClient backend, EconomyConfig config,
            LandRegistry registry, Wallet wallet)
        {
            _backend  = backend;
            _config   = config;
            _registry = registry;
            _wallet   = wallet;
        }

        public async Task<LandPurchaseResult> PurchaseAsync(LandPurchaseRequest request, PlanetDefinition planet)
        {
            int price = (int)Math.Round(_config.BaseLandPrice * planet.LandPriceMultiplier);

            PurchaseLandResponse response;
            try
            {
                response = await _backend.CallAsync<PurchaseLandResponse>("PurchaseLand",
                    new Dictionary<string, object>
                    {
                        { "tileId",   request.TileId  },
                        { "planetId", planet.name      },
                        { "price",    price            }
                    });
            }
            catch (Exception ex)
            {
                SULog.Error($"LandPurchaseService: server call failed — {ex.Message}", SULog.Channel.Economy);
                return new LandPurchaseResult { Success = false, FailureReason = "Network error" };
            }

            if (!response.Success)
            {
                string reason = response.Reason switch
                {
                    "ALREADY_OWNED"      => "Tile is already owned",
                    "INSUFFICIENT_FUNDS" => "Insufficient coins",
                    _                    => response.Reason ?? "Server rejected"
                };
                return new LandPurchaseResult { Success = false, FailureReason = reason };
            }

            _registry.MarkOwned(request.TileId);
            if (response.NewBalance >= 0) _wallet.SetCoins(response.NewBalance);

            SULog.Info($"Land purchased: {request.TileId} for {price} coins (balance: {response.NewBalance})", SULog.Channel.Economy);
            return new LandPurchaseResult { Success = true };
        }

        private class PurchaseLandResponse
        {
            public bool   Success;
            public string Reason;
            public int    NewBalance = -1;
        }
    }
}
