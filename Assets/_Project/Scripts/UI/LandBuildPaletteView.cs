using UnityEngine;
using UnityEngine.UI;
using VContainer;
using TMPro;
using SocialUniverse.Config;
using SocialUniverse.Core;
using SocialUniverse.Economy;
using SocialUniverse.World;

namespace SocialUniverse.UI
{
    // Owner edit flow for the hex board: a drag-source palette of affordable buildings plus
    // tap-to-purchase (locked hexatile) and tap-to-remove (placed building) via popups. Builds
    // the board for everyone (view + edit); hides the palette + skips interaction wiring in view
    // mode. All economy/slot mutations go through LandBuildService (server-authoritative); the
    // handoff's working Slots/Unlocked are updated locally on success so the board reflects the
    // change immediately.
    public class LandBuildPaletteView : MonoBehaviour
    {
        [SerializeField] private GameObject   _paletteRoot;     // bottom bar; disabled in view mode
        [SerializeField] private Transform    _itemButtonParent;
        [SerializeField] private Button       _itemButtonPrefab; // a button with a child TMP_Text
        [SerializeField] private TMP_Text     _statusText;
        [SerializeField] private HexBuildPopup _purchasePopup;
        [SerializeField] private HexBuildPopup _removePopup;
        [SerializeField] private Camera       _camera;          // for palette drag -> board raycast
        [SerializeField] private Material     _dragGhostMaterial; // transparent preview material (optional)

        [Inject] private LandBuildingHandoff     _handoff;
        [Inject] private LandBuildService        _buildService;
        [Inject] private BuildPaletteService     _palette;
        [Inject] private PlotHexBoard            _board;
        [Inject] private PlotBoardInputController _input;
        [Inject] private DatabaseRegistry        _registry;
        [Inject] private EconomyConfig           _config;

        private int      _localCoins;
        private bool[]   _unlocked;
        private string[] _slots;

        private void Start()
        {
            _localCoins = _handoff.Coins;
            _unlocked   = _handoff.Unlocked;
            _slots      = _handoff.Slots;

            _board.Build(_unlocked, _slots);

            bool canEdit = _handoff.CanEdit;
            _paletteRoot.SetActive(canEdit);
            if (!canEdit) return;

            _input.CellTapped      += OnCellTapped;
            _input.BuildingDragged += OnBuildingDragged;
            BuildPalette();
        }

        private void OnDestroy()
        {
            if (_input == null) return;
            _input.CellTapped      -= OnCellTapped;
            _input.BuildingDragged -= OnBuildingDragged;
        }

        private void BuildPalette()
        {
            foreach (Transform c in _itemButtonParent) Destroy(c.gameObject);

            var tile = new TileData(_handoff.TileId)
            {
                State      = TileState.OwnedByPlayer,
                BuildLevel = LandBuildMath.FilledCount(_slots),
            };

            foreach (var item in _palette.GetAvailableItems(tile, _localCoins))
            {
                var btn      = Instantiate(_itemButtonPrefab, _itemButtonParent);
                var captured = item;

                var view = btn.GetComponent<ItemButtonView>();
                if (view != null)
                {
                    view.Bind(item);
                }
                else
                {
                    var label = btn.GetComponentInChildren<TMP_Text>();
                    if (label != null) label.text = $"{item.DisplayName}\n{item.Cost}";
                }

                var drag = btn.gameObject.AddComponent<PaletteItemDragHandler>();
                drag.Init(_camera, captured.Prefab, _dragGhostMaterial, hex => PlaceFromPalette(captured, hex));
            }
        }

        private void OnCellTapped(int hexIndex)
        {
            bool unlocked = _unlocked[hexIndex];
            bool occupied = unlocked && !string.IsNullOrEmpty(_slots[hexIndex]);

            if (!unlocked)
            {
                if (!HexBoardMath.IsAdjacentToUnlocked(hexIndex, _unlocked, _config.HexBoardRadius))
                { SetStatus("Expand from your unlocked tiles"); return; }

                int price = HexBoardMath.HexatilePrice(CountUnlocked(), _config.FreeHexCount, _config.HexatileBasePrice, _config.HexatilePriceStep);
                if (price > _localCoins) { SetStatus("Not enough coins"); return; }

                _purchasePopup.Show($"Unlock this hexatile for {price} coins?", () => Purchase(hexIndex));
            }
            else if (occupied)
            {
                _removePopup.Show("Remove this building?", () => Remove(hexIndex));
            }
        }

        private async void Purchase(int hexIndex)
        {
            var r = await _buildService.PurchaseHexatileAsync(_handoff.TileId, _handoff.RegistryPlanetId, hexIndex);
            if (!r.Success) { SetStatus($"Unlock failed: {r.Reason}"); return; }

            _unlocked[hexIndex] = true;
            if (r.NewBalance >= 0) _localCoins = r.NewBalance;
            _board.SetCell(hexIndex, true, null);
            SetStatus("");
            BuildPalette();
        }

        private async void Remove(int hexIndex)
        {
            var r = await _buildService.RemoveAsync(_handoff.TileId, _handoff.RegistryPlanetId, hexIndex);
            if (!r.Success) { SetStatus($"Remove failed: {r.Reason}"); return; }

            _slots[hexIndex] = null;
            _board.SetCell(hexIndex, true, null);
            SetStatus("");
            BuildPalette();
        }

        private async void OnBuildingDragged(int fromHex, int toHex)
        {
            if (string.IsNullOrEmpty(_slots[fromHex])) return;
            if (!_unlocked[toHex] || !string.IsNullOrEmpty(_slots[toHex])) { SetStatus("Can't move there"); return; }

            var r = await _buildService.MoveAsync(_handoff.TileId, _handoff.RegistryPlanetId, fromHex, toHex);
            if (!r.Success) { SetStatus($"Move failed: {r.Reason}"); return; }

            _slots[toHex] = _slots[fromHex];
            _slots[fromHex] = null;
            _board.SetCell(fromHex, true, null);
            _board.SetCell(toHex, true, _slots[toHex]);
            SetStatus("");
        }

        // Called by a palette item's drag-end (PaletteItemDragHandler) with the target hex (or -1).
        private async void PlaceFromPalette(ItemDefinition item, int hexIndex)
        {
            if (hexIndex < 0) return;
            if (!_unlocked[hexIndex] || !string.IsNullOrEmpty(_slots[hexIndex])) { SetStatus("Pick an unlocked empty tile"); return; }
            if (item.Cost > _localCoins) { SetStatus("Not enough coins"); return; }

            var r = await _buildService.PlaceAsync(_handoff.TileId, _handoff.RegistryPlanetId, hexIndex, item.ItemId, item.Cost);
            if (!r.Success) { SetStatus($"Place failed: {r.Reason}"); return; }

            _slots[hexIndex] = item.ItemId;
            if (r.NewBalance >= 0) _localCoins = r.NewBalance;
            _board.SetCell(hexIndex, true, item.ItemId);
            SetStatus("");
            BuildPalette();
        }

        private int CountUnlocked()
        {
            int n = 0;
            foreach (var b in _unlocked) if (b) n++;
            return n;
        }

        private void SetStatus(string text)
        {
            if (_statusText != null) _statusText.text = text;
        }
    }
}
