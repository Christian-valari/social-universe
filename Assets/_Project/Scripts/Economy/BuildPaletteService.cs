using System.Collections.Generic;
using System.Linq;
using SocialUniverse.Config;
using SocialUniverse.World;

namespace SocialUniverse.Economy
{
    // Returns the buildable items a player can place next on a given tile.
    // Progression is linear: a tile at BuildLevel N can only place items whose
    // ItemDefinition.BuildLevel == N + 1.
    public class BuildPaletteService
    {
        private readonly DatabaseRegistry _registry;
        private readonly EconomyConfig    _config;

        public BuildPaletteService(DatabaseRegistry registry, EconomyConfig config)
        {
            _registry = registry;
            _config   = config;
        }

        public IEnumerable<ItemDefinition> GetAvailableItems(TileData tile)
        {
            if (tile.State != TileState.OwnedByPlayer) return Enumerable.Empty<ItemDefinition>();
            if (tile.BuildLevel >= _config.MaxBuildLevel) return Enumerable.Empty<ItemDefinition>();

            int nextLevel = tile.BuildLevel + 1;
            return _registry.AllItems.Where(i => i.BuildLevel == nextLevel);
        }
    }
}
