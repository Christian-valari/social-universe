using System;
using UnityEngine;
using UnityEngine.UI;

namespace SocialUniverse.UI
{
    // One row in the Mineral Inventory list: icon + "Name xQty (value/ea)" label + a Sell button.
    // The row owns its child-widget references via the Inspector, so MineralInventoryView never
    // reaches in by component type (GetComponentInChildren). Mirrors the DroneRowView pattern.
    public class MineralRowView : MonoBehaviour
    {
        [SerializeField] private Image  _icon;
        [SerializeField] private Text   _label;
        [SerializeField] private Button _sellButton;

        // Configure this row: icon (hidden when null), the composed label text, and the sell action.
        // `canSell` is false for a mineral the player holds none of — the button stays in place so
        // the row doesn't change shape, but can't be pressed.
        public void Bind(Sprite icon, string label, bool canSell, Action onSell)
        {
            SetIcon(icon);
            if (_label != null) _label.text = label;

            if (_sellButton != null)
            {
                _sellButton.onClick.RemoveAllListeners();
                _sellButton.interactable = canSell;
                if (onSell != null) _sellButton.onClick.AddListener(() => onSell());
            }
        }

        private void SetIcon(Sprite icon)
        {
            if (_icon == null) return;
            _icon.sprite  = icon;
            _icon.enabled = icon != null; // hide the Image when a mineral has no icon assigned yet
        }
    }
}
