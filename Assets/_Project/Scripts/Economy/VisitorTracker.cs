using System.Collections.Generic;
using System.Threading.Tasks;
using SocialUniverse.Core;

namespace SocialUniverse.Economy
{
    // Response shape for the "RecordVisit" Cloud Code function. Public so tests
    // can construct it for a fake IBackendClient.
    public class RecordVisitResult
    {
        public bool Success;
        public int  VisitCount;
    }

    // Records that the local player visited another player's tile, feeding
    // into that tile's ClaimYield visit bonus. M3 stand-in for real
    // presence-based visit detection — see VisitorTrackingController and the
    // M4 dependency caveat in PROGRESS.md.
    public class VisitorTracker
    {
        private readonly IBackendClient _backend;

        public VisitorTracker(IBackendClient backend) => _backend = backend;

        public Task<RecordVisitResult> RecordVisitAsync(string tileId, string planetId) =>
            _backend.CallAsync<RecordVisitResult>("RecordVisit",
                new Dictionary<string, object> { { "tileId", tileId }, { "planetId", planetId } });
    }
}
