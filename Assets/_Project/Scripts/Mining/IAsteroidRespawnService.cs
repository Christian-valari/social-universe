using System.Collections.Generic;
using System.Threading.Tasks;

namespace SocialUniverse.Mining
{
    // Which of a planet's asteroid slots the player emptied in the last few hours, on any device: the
    // server's claim log (ValidateMining) is the only record, so every device shows the same field.
    public interface IAsteroidRespawnService
    {
        // GetAsteroidRespawns. A network failure throws, so the caller can tell "nothing emptied"
        // from "couldn't ask".
        Task<IReadOnlyList<AsteroidRespawn>> GetAsync(string planetId);
    }
}
