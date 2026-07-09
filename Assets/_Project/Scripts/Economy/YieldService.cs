using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SocialUniverse.Core;

namespace SocialUniverse.Economy
{
    // Response shape for the "ClaimYield" Cloud Code function. Public so tests
    // can construct it for a fake IBackendClient.
    public class YieldClaimResult
    {
        public bool   Success;
        public string Reason;
        public int    Granted;
        public int    NewBalance = -1;
    }

    // Claims accrued visitor-driven yield for an owned tile.
    public class YieldService
    {
        private readonly IBackendClient      _backend;
        private readonly Wallet              _wallet;
        private readonly LandRegistryService _landRegistryService;

        public YieldService(IBackendClient backend, Wallet wallet, LandRegistryService landRegistryService)
        {
            _backend             = backend;
            _wallet              = wallet;
            _landRegistryService = landRegistryService;
        }

        public async Task<YieldClaimResult> ClaimYieldAsync(string tileId, string planetId)
        {
            YieldClaimResult result;
            try
            {
                result = await _backend.CallAsync<YieldClaimResult>("ClaimYield",
                    new Dictionary<string, object> { { "tileId", tileId }, { "planetId", planetId } });
            }
            catch (Exception ex)
            {
                SULog.Error($"YieldService: server call failed — {ex.Message}", SULog.Channel.Economy);
                return new YieldClaimResult { Success = false, Reason = "Network error" };
            }

            if (result is { Success: true })
            {
                if (result.NewBalance >= 0) _wallet.SetCoins(result.NewBalance);
                _landRegistryService.ResetYieldState(tileId);
            }

            return result;
        }
    }
}
