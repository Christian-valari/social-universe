using System.Collections.Generic;
using UnityEngine;

namespace SocialUniverse.World
{
    public class TileColorizer : MonoBehaviour
    {
        [SerializeField] private HexasphereManager _hexasphere;

        [SerializeField] private Color _availableColor = new Color(0.4f, 0.4f, 0.4f);
        [SerializeField] private Color _ownedColor     = new Color(0.2f, 0.8f, 0.2f);
        [SerializeField] private Color _othersColor    = new Color(0.2f, 0.4f, 0.9f);
        [SerializeField] private Color _landmarkColor  = new Color(1.0f, 0.8f, 0.1f);

        public void Refresh(IReadOnlyDictionary<string, TileData> tiles)
        {
            foreach (var tile in tiles.Values)
                Apply(tile);
        }

        public void RefreshTile(TileData tile) => Apply(tile);

        private void Apply(TileData tile)
        {
            var color = tile.State switch
            {
                TileState.Landmark      => _landmarkColor,
                TileState.OwnedByPlayer => _ownedColor,
                TileState.OwnedByOther  => _othersColor,
                _                       => _availableColor
            };

            _hexasphere.SetTileColor(tile.TileId, color);
            Debug.Log($"#{GetType().Name}# Change Color -> {tile.TileId}");
        }
    }
}
