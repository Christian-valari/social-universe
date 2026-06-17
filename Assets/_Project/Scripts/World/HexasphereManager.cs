using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using HexasphereGrid;
using SocialUniverse.Core;
using SocialUniverse.Config;

namespace SocialUniverse.World
{
    public class TileSelectedEvent { public TileData Tile; }

    public class BuildItemRequestedEvent { public TileData Tile; public ItemDefinition Item; }

    public class TileSellRequestedEvent { public TileData Tile; }

    public class HexasphereManager : MonoBehaviour
    {
        [SerializeField] private Hexasphere _hexasphere;

        private readonly Dictionary<string, TileData> _tiles = new();
        private readonly Dictionary<string, int> _indexById = new();

        public IReadOnlyDictionary<string, TileData> Tiles => _tiles;

        public Hexasphere PluginHexasphere => _hexasphere;

        public IEnumerable<string> PentagonTileIds =>
            _hexasphere != null && _hexasphere.tiles != null
                ? _hexasphere.tiles.Where(t => t.isPentagon).Select(t => t.index.ToString())
                : Enumerable.Empty<string>();

        public void Generate(int tileCount)
        {
            _tiles.Clear();
            _indexById.Clear();

            if (_hexasphere == null || _hexasphere.tiles == null)
            {
                SULog.Warn("HexasphereManager: Hexasphere component or tiles not ready.", SULog.Channel.World);
                return;
            }

            foreach (var t in _hexasphere.tiles)
            {
                var id = t.index.ToString();
                _tiles[id] = new TileData(id);
                _indexById[id] = t.index;
            }

            SULog.Info($"HexasphereManager: {_tiles.Count} tiles (numDivisions={_hexasphere.numDivisions}).", SULog.Channel.World);
        }

        public TileData GetTile(string tileId) =>
            _tiles.TryGetValue(tileId, out var t) ? t : null;

        public void SelectTile(string tileId)
        {
            if (_tiles.TryGetValue(tileId, out var tile))
                EventBus.Publish(new TileSelectedEvent { Tile = tile });
        }

        public void SetTileColor(string tileId, Color color)
        {
            if (_hexasphere == null) return;
            if (_indexById.TryGetValue(tileId, out var index))
                _hexasphere.SetTileColor(index, color);
        }

        public void SetTileExtrudeAmount(string tileId, float amount)
        {
            if (_hexasphere == null) return;
            if (_indexById.TryGetValue(tileId, out var index))
                _hexasphere.SetTileExtrudeAmount(index, amount);
        }

        // Toggles tile rendering without disabling colliders, so selection raycasts still work.
        public void SetTilesVisible(bool visible)
        {
            if (_hexasphere == null) return;
            _hexasphere.style = visible ? STYLE.ShadedWireframe : STYLE.Invisible;
        }
    }
}
