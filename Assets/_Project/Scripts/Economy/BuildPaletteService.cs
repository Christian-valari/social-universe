using System.Collections.Generic;
using System.Linq;
using SocialUniverse.Config;
using SocialUniverse.World;

namespace SocialUniverse.Economy
{
    // Returns the buildable items a player can place on a given tile in the slot model:
    // any item the player can afford may go in any empty slot of a tile they own.
    // A tile is "full" when BuildLevel (== filled slot count) reaches MaxBuildLevel.
    // (Rarity / unlock-level gating is intentionally deferred — see the design spec.)
    public class BuildPaletteService
    {
        private readonly DatabaseRegistry _registry;
        private readonly EconomyConfig    _config;

        public BuildPaletteService(DatabaseRegistry registry, EconomyConfig config)
        {
            _registry = registry;
            _config   = config;
        }

        public IEnumerable<ItemDefinition> GetAvailableItems(TileData tile, int availableCoins)
        {
            if (tile.State != TileState.OwnedByPlayer) return Enumerable.Empty<ItemDefinition>();
            if (tile.BuildLevel >= _config.MaxBuildLevel) return Enumerable.Empty<ItemDefinition>();

            return _registry.AllItems.Where(i => i.Cost <= availableCoins);
        }
    }
}
