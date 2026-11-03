using System.Threading.Tasks;

namespace SocialUniverse.Mining
{
    // Server-tracked idle mining runs (docs/superpowers/plans/2026-10-15-server-tracked-idle-mining.md):
    // the run lives in Cloud Save, so any device the player signs in on can show it and claim it.
    // The claim itself goes through IMineralService.GrantMiningAsync with the run's slot id.
    public interface IIdleMiningService
    {
        // StartIdleMining. Never throws: a network failure comes back as Success = false with
        // Reason "Network error" (or "Empty response"), which the failure toast already explains.
        // replaceSlotId: the slot of a run on this planet that this device can't place, which the
        // server then replaces instead of answering ALREADY_MINING.
        Task<IdleMiningSessionResult> StartAsync(string planetId, string slotId, string mineralId, float durationSeconds,
            string replaceSlotId = null);

        // GetIdleMining: the run in progress, if any (HasSession). A network failure throws, so the
        // caller can tell "no run" from "couldn't ask".
        Task<IdleMiningSessionResult> GetAsync();
    }
}
