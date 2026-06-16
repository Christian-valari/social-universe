using System.Collections.Generic;
using UnityEngine;
using SocialUniverse.Config;

namespace SocialUniverse.World
{
    public class TileExtrusionView : MonoBehaviour
    {
        [SerializeField] private HexasphereManager _hexasphere;
        [SerializeField] private EconomyConfig     _economyConfig;

        public void Refresh(IReadOnlyDictionary<string, TileData> tiles)
        {
            foreach (var tile in tiles.Values)
                Apply(tile);
        }

        public void RefreshTile(TileData tile) => Apply(tile);

        private void Apply(TileData tile)
        {
            float amount = Mathf.Clamp01((float)tile.BuildLevel / _economyConfig.MaxBuildLevel);
            _hexasphere.SetTileExtrudeAmount(tile.TileId, amount);
        }
    }
}
