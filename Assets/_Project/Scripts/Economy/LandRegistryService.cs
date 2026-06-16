using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SocialUniverse.Core;

namespace SocialUniverse.Economy
{
    // A single tile's entry in the global, cross-player land registry (Cloud Save Custom Data).
    public class LandTileEntry
    {
        public string OwnerId;
        public int    BuildLevel;
        public long   LastYieldClaimTs;
        public long   LastUpkeepTs;
        public int    VisitCount;
    }

    // Response shape for the "GetLandRegistry" Cloud Code function. Public so
    // tests can construct it for a fake IBackendClient.
    public class LandRegistryData
    {
        public Dictionary<string, LandTileEntry> Tiles;
    }

    // Authoritative tile-ownership map for a planet (tileId -> entry), fetched
    // from the global land registry (Cloud Save Custom Data, shared across all
    // players). Unlike LandRegistry (M2's private "my tiles" cache), this covers
    // every owned tile on the planet, not just the local player's, and carries
    // the build/yield/upkeep state used by M3 phases 2-4.
    public class LandRegistryService
    {
        private readonly IBackendClient _backend;
        private readonly Dictionary<string, LandTileEntry> _tiles = new();

        public LandRegistryService(IBackendClient backend) => _backend = backend;

        public IReadOnlyDictionary<string, LandTileEntry> TileOwners => _tiles;

        public async Task RefreshAsync(string planetId)
        {
            var response = await _backend.CallAsync<LandRegistryData>("GetLandRegistry",
                new Dictionary<string, object> { { "planetId", planetId } });

            _tiles.Clear();
            if (response?.Tiles == null) return;

            foreach (var kvp in response.Tiles)
                _tiles[kvp.Key] = kvp.Value;
        }

        public string GetOwner(string tileId) =>
            _tiles.TryGetValue(tileId, out var entry) ? entry.OwnerId : null;

        public LandTileEntry GetEntry(string tileId) =>
            _tiles.TryGetValue(tileId, out var entry) ? entry : null;

        // Optimistic local update — call after a successful purchase so the local
        // view is correct immediately, without waiting for the next poll.
        public void SetOwner(string tileId, string ownerId)
        {
            long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            _tiles[tileId] = new LandTileEntry
            {
                OwnerId          = ownerId,
                BuildLevel       = 0,
                LastYieldClaimTs = now,
                LastUpkeepTs     = now,
                VisitCount       = 0
            };
        }

        // Optimistic local update after a successful build placement.
        public void SetBuildLevel(string tileId, int buildLevel)
        {
            if (_tiles.TryGetValue(tileId, out var entry)) entry.BuildLevel = buildLevel;
        }

        // Optimistic local update after a successful yield claim.
        public void ResetYieldState(string tileId)
        {
            if (!_tiles.TryGetValue(tileId, out var entry)) return;
            entry.LastYieldClaimTs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            entry.VisitCount       = 0;
        }

        // Optimistic local update after a successful sale or an upkeep-driven revert.
        public void RemoveTile(string tileId) => _tiles.Remove(tileId);
    }
}
