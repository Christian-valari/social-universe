using UnityEngine;
using HexasphereGrid;

namespace SocialUniverse.World
{
    public class TileSelectionController : MonoBehaviour
    {
        [SerializeField] private HexasphereManager _hexasphere;

        private void Awake()
        {
            var hex = _hexasphere != null ? _hexasphere.PluginHexasphere : null;
            if (hex != null)
                hex.OnTileClick += OnPluginTileClick;
        }

        private void OnDestroy()
        {
            var hex = _hexasphere != null ? _hexasphere.PluginHexasphere : null;
            if (hex != null)
                hex.OnTileClick -= OnPluginTileClick;
        }

        private void OnPluginTileClick(Hexasphere hex, int tileIndex)
        {
            if (hex.style == STYLE.Invisible) return;
            
            Debug.Log($"#{GetType().Name}# Tile Clicked {tileIndex}");
            _hexasphere.SelectTile(tileIndex.ToString());
        }
    }
}
