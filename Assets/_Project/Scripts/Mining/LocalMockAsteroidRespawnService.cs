using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SocialUniverse.Mining
{
    // Standalone Planet scene (no backend): nothing was ever claimed on a server, so every slot starts
    // full and an emptied asteroid comes back on the next scene load. Production uses
    // AsteroidRespawnService.
    public class LocalMockAsteroidRespawnService : IAsteroidRespawnService
    {
        public Task<IReadOnlyList<AsteroidRespawn>> GetAsync(string planetId) =>
            Task.FromResult<IReadOnlyList<AsteroidRespawn>>(Array.Empty<AsteroidRespawn>());
    }
}
