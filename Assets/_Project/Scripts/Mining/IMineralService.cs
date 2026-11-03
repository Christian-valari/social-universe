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
        // idleSlotId names the server-tracked idle run this claim collects, so the server ends it
        // (null for active mining and for legacy local idle sessions). slotId names the asteroid the
        // claim emptied: the server's claim log keeps it, and GetAsteroidRespawns reports it back to
        // every device until the slot respawns.
        // (Cloud Save hydration is App-layer, not here — see Ruling R4.)
        Task<MiningGrantResult> GrantMiningAsync(string planetId, string mineralId, int qty, string idleSlotId = null,
            string slotId = null);

        // A failed active-mining minigame loses its asteroid: ValidateMining (forfeit) records the slot
        // in the claim log, using one of the planet's claims and granting nothing, so the slot stays
        // empty on every device until it respawns. Reason "FORFEITED" when recorded; never grants.
        Task<MiningGrantResult> ForfeitMiningAsync(string planetId, string mineralId, string slotId);
    }
}
