using System.Collections.Generic;

namespace SocialUniverse.Mining
{
    // Public DTO so IBackendClient.CallAsync<AsteroidRespawnsResult> can type the reply (the
    // public-DTO testability pattern, like IdleMiningSessionResult). Shape MUST MATCH
    // ServerCode/GetAsteroidRespawns.js: { success, respawns: [{ slotId, mineralId, secondsLeft }] }.
    public class AsteroidRespawnsResult
    {
        public bool                  Success;
        public List<AsteroidRespawn> Respawns;
    }
}
