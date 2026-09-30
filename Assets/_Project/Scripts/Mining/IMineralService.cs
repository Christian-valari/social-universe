using System.Collections.Generic;
using System.Threading.Tasks;

namespace SocialUniverse.Mining
{
    // Public top-level DTO so IBackendClient.CallAsync<SellResult> can type the response
    // (the public-DTO testability pattern). Shape MUST MATCH ServerCode/SellMinerals.js.
    public class SellResult
    {
        public bool                    Success;
        public string                  Reason;
        public int                     NewBalance = -1;
        public Dictionary<string, int> RemainingInventory;
    }

    public interface IMineralService
    {
        Task<SellResult> SellAsync(string mineralId, int qty);
        Task<SellResult> SellAllAsync();

        // Mining payout: round-trips ValidateMining, which checks planet, mineral, drone tier and
        // the per-planet claim budget, and clamps qty. Applies Granted to MineralInventory.
        // Granted == 0 with a Reason means the server rejected the claim.
        // (Cloud Save hydration is App-layer, not here — see Ruling R4.)
        Task<MiningGrantResult> GrantMiningAsync(string planetId, string mineralId, int qty);
    }
}
