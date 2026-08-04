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
    // Edit-mode UI for a plot: an affordable-item palette plus slot tap-targets that place,
    // remove, or move decorations. Hidden entirely in view mode (visitor). All economy/slot
    // mutations go through LandBuildService (server-authoritative); the handoff's working Slots
    // are updated locally on success so the plot reflects the change immediately.
    public class LandBuildPaletteView : MonoBehaviour
    {
        [SerializeField] private GameObject   _paletteRoot;      // bottom bar; disabled in view mode
        [SerializeField] private GameObject   _slotButtonsRoot;  // parent of the 8 slot tap targets; disabled in view mode
        [SerializeField] private Transform    _itemButtonParent;
        [SerializeField] private Button       _itemButtonPrefab; // a button with a child TMP_Text
        [SerializeField] private Button[]     _slotButtons;      // one per slot; screen-space hit targets
        [SerializeField] private TMP_Text     _statusText;

        [Inject] private LandBuildingHandoff  _handoff;
        [Inject] private LandBuildService     _buildService;
        [Inject] private BuildPaletteService  _palette;
        [Inject] private LandBuildingController _controller;

        private ItemDefinition _selectedItem;
        private int            _localCoins;

        private void Start()
        {
            _localCoins = _handoff.Coins;

            bool canEdit = _handoff.CanEdit;
            _paletteRoot.SetActive(canEdit);
            _slotButtonsRoot.SetActive(canEdit);
            if (!canEdit) return;

            BuildPalette();
            for (int i = 0; i < _slotButtons.Length; i++)
            {
                int index = i;
                _slotButtons[i].onClick.AddListener(() => OnSlotClicked(index));
            }
        }

        private void BuildPalette()
        {
            foreach (Transform c in _itemButtonParent) Destroy(c.gameObject);

            // Build a throwaway TileData describing this owned plot for the palette rule.
            var tile = new TileData(_handoff.TileId)
            {
                State      = TileState.OwnedByPlayer,
                BuildLevel = LandBuildMath.FilledCount(_handoff.Slots),
            };

            foreach (var item in _palette.GetAvailableItems(tile, _localCoins))
            {
                var btn = Instantiate(_itemButtonPrefab, _itemButtonParent);
                var label = btn.GetComponentInChildren<TMP_Text>();
                if (label != null) label.text = $"{item.DisplayName}\n{item.Cost}";
                var captured = item;
                btn.onClick.AddListener(() => _selectedItem = captured);
            }
        }

        private async void OnSlotClicked(int slotIndex)
        {
            var slots = _handoff.Slots;
            if (slots == null || slotIndex < 0 || slotIndex >= slots.Length)
            {
                _statusText.text = "Plot not ready";
                return;
            }

            bool empty = LandBuildMath.IsEmpty(slots, slotIndex);

            if (empty)
            {
                if (_selectedItem == null) { _statusText.text = "Pick an item first"; return; }
                if (_selectedItem.Cost > _localCoins) { _statusText.text = "Not enough coins"; return; }

                var result = await _buildService.PlaceAsync(_handoff.TileId, _handoff.PlanetId, slotIndex, _selectedItem.ItemId, _selectedItem.Cost);
                if (!result.Success) { _statusText.text = $"Place failed: {result.Reason}"; return; }

                slots[slotIndex] = _selectedItem.ItemId;
                if (result.NewBalance >= 0) _localCoins = result.NewBalance;
                _controller.SetSlotVisual(slotIndex, _selectedItem);
                _statusText.text = "";
                BuildPalette(); // affordability may have changed
            }
            else
            {
                // Filled slot tapped → remove it. (Move is available via a long-press/drag in a
                // later pass; v1 exposes remove, then re-place, which is functionally complete.)
                var result = await _buildService.RemoveAsync(_handoff.TileId, _handoff.PlanetId, slotIndex);
                if (!result.Success) { _statusText.text = $"Remove failed: {result.Reason}"; return; }

                slots[slotIndex] = null;
                _controller.SetSlotVisual(slotIndex, null);
                _statusText.text = "";
                BuildPalette();
            }
        }
    }
}
