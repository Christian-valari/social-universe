using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SocialUniverse.Config;
using SocialUniverse.Core;

namespace SocialUniverse.Economy
{
    // Response shape for the "SellLand" Cloud Code function. Public so tests
    // can construct it for a fake IBackendClient.
    public class LandSaleResult
    {
        public bool   Success;
        public string Reason;
        public int    NewBalance = -1;
    }

    // Sells an owned tile back to the planet for a fraction of its base price.
    public class LandSaleService
    {
        private readonly IBackendClient      _backend;
        private readonly EconomyConfig       _config;
        private readonly LandRegistry        _registry;
        private readonly Wallet              _wallet;
        private readonly LandRegistryService _landRegistryService;

        public LandSaleService(IBackendClient backend, EconomyConfig config,
            LandRegistry registry, Wallet wallet, LandRegistryService landRegistryService)
        {
            _backend             = backend;
            _config              = config;
            _registry            = registry;
            _wallet              = wallet;
            _landRegistryService = landRegistryService;
        }

        public async Task<LandSaleResult> SellAsync(string tileId, PlanetDefinition planet)
        {
            int refund = (int)Math.Round(_config.BaseLandPrice * planet.LandPriceMultiplier * _config.LandResaleRate);

            var result = await _backend.CallAsync<LandSaleResult>("SellLand",
                new Dictionary<string, object>
                {
                    { "tileId",   tileId      },
                    { "planetId", planet.name },
                    { "refund",   refund      }
                });

            if (result is { Success: true })
            {
                _registry.RemoveOwned(tileId);
                _landRegistryService.RemoveTile(tileId);
                if (result.NewBalance >= 0) _wallet.SetCoins(result.NewBalance);
            }

            return result;
        }
    }
}
