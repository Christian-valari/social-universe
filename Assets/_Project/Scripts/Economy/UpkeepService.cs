using System.Collections.Generic;
using System.Threading.Tasks;
using SocialUniverse.Core;

namespace SocialUniverse.Economy
{
    // Response shape for the "ApplyUpkeep" Cloud Code function. Public so
    // tests can construct it for a fake IBackendClient.
    public class UpkeepResult
    {
        public int          NewBalance    = -1;
        public List<string> ChargedTiles  = new();
        public List<string> RevertedTiles = new();
    }

    // Applies recurring per-tile upkeep cost for the local player's owned
    // tiles on a planet. Tiles whose owner can't afford upkeep are removed
    // from the registry server-side (revert to Available for everyone).
    public class UpkeepService
    {
        private readonly IBackendClient      _backend;
        private readonly Wallet              _wallet;
        private readonly LandRegistryService _landRegistryService;

        public UpkeepService(IBackendClient backend, Wallet wallet, LandRegistryService landRegistryService)
        {
            _backend             = backend;
            _wallet              = wallet;
            _landRegistryService = landRegistryService;
        }

        public async Task<UpkeepResult> ApplyUpkeepAsync(string planetId)
        {
            var result = await _backend.CallAsync<UpkeepResult>("ApplyUpkeep",
                new Dictionary<string, object> { { "planetId", planetId } });

            if (result == null) return new UpkeepResult();

            if (result.NewBalance >= 0) _wallet.SetCoins(result.NewBalance);

            foreach (var tileId in result.RevertedTiles)
                _landRegistryService.RemoveTile(tileId);

            return result;
        }
    }
}
