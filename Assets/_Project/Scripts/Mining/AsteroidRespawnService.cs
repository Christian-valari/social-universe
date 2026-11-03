using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SocialUniverse.Core;

namespace SocialUniverse.Mining
{
    // The client side of ServerCode/GetAsteroidRespawns.js.
    public class AsteroidRespawnService : IAsteroidRespawnService
    {
        private readonly IBackendClient _backend;

        public AsteroidRespawnService(IBackendClient backend) => _backend = backend;

        public async Task<IReadOnlyList<AsteroidRespawn>> GetAsync(string planetId)
        {
            var res = await _backend.CallAsync<AsteroidRespawnsResult>("GetAsteroidRespawns",
                new Dictionary<string, object> { { "planetId", planetId } });
            return (IReadOnlyList<AsteroidRespawn>)res?.Respawns ?? Array.Empty<AsteroidRespawn>();
        }
    }
}
