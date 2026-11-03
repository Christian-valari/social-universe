using System.Threading.Tasks;

namespace SocialUniverse.Mining
{
    // Standalone Planet scene and dev mode (no backend): every start succeeds and there is never a
    // run to restore, so idle runs don't survive a scene reload there. Production uses IdleMiningService.
    public class LocalMockIdleMiningService : IIdleMiningService
    {
        public Task<IdleMiningSessionResult> StartAsync(string planetId, string slotId, string mineralId, float durationSeconds,
            string replaceSlotId = null) =>
            Task.FromResult(new IdleMiningSessionResult
            {
                Success = true, PlanetId = planetId, SlotId = slotId, MineralId = mineralId, DurationSeconds = durationSeconds
            });

        public Task<IdleMiningSessionResult> GetAsync() => Task.FromResult(new IdleMiningSessionResult { Success = true });
    }
}
