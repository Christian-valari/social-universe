using System.Collections.Generic;
using UnityEngine;
using SocialUniverse.Core;

namespace SocialUniverse.World
{
    public class TileSelectedEvent { public TileData Tile; }

    // STUB: Replace Generate() internals with Hexasphere Grid System API when asset is installed.
    // The public contract (Tiles dictionary, SelectTile) must remain unchanged.
    public class HexasphereManager : MonoBehaviour
    {
        private readonly Dictionary<string, TileData> _tiles = new();

        public IReadOnlyDictionary<string, TileData> Tiles => _tiles;

        public void Generate(int tileCount)
        {
            _tiles.Clear();

            // TODO: hexasphere.GenerateTiles(tileCount);
            //       foreach (var t in hexasphere.Tiles) _tiles[t.Id] = new TileData(t.Id);
            for (int i = 0; i < tileCount; i++)
            {
                var id = $"tile_{i:D5}";
                _tiles[id] = new TileData(id);
            }

            SULog.Info($"HexasphereManager: {_tiles.Count} tiles (stub)", SULog.Channel.World);
        }

        public TileData GetTile(string tileId) =>
            _tiles.TryGetValue(tileId, out var t) ? t : null;

        public void SelectTile(string tileId)
        {
            if (_tiles.TryGetValue(tileId, out var tile))
                EventBus.Publish(new TileSelectedEvent { Tile = tile });
        }
    }
}
