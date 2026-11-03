using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SocialUniverse.Core;

namespace SocialUniverse.Mining
{
    // The client side of ServerCode/StartIdleMining.js and GetIdleMining.js.
    public class IdleMiningService : IIdleMiningService
    {
        private readonly IBackendClient _backend;

        public IdleMiningService(IBackendClient backend) => _backend = backend;

        public async Task<IdleMiningSessionResult> StartAsync(string planetId, string slotId, string mineralId, float durationSeconds,
            string replaceSlotId = null)
        {
            var args = new Dictionary<string, object>
            {
                { "planetId",    planetId },
                { "slotId",      slotId },
                { "mineralId",   mineralId },
                { "durationSec", durationSeconds }
            };
            if (!string.IsNullOrEmpty(replaceSlotId)) args["replaceSlotId"] = replaceSlotId;

            try
            {
                return await _backend.CallAsync<IdleMiningSessionResult>("StartIdleMining", args)
                    ?? new IdleMiningSessionResult { Reason = "Empty response" };
            }
            catch (Exception ex)
            {
                SULog.Warn($"IdleMiningService.Start failed — {ex.Message}", SULog.Channel.Mining);
                return new IdleMiningSessionResult { Reason = "Network error" };
            }
        }

        public async Task<IdleMiningSessionResult> GetAsync() =>
            await _backend.CallAsync<IdleMiningSessionResult>("GetIdleMining") ?? new IdleMiningSessionResult { Success = true };
    }
}
