using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace SocialUniverse.UI
{
    // One row in the Mineral Inventory list: icon + tier badge, name and price each, the count held,
    // and a Sell button showing what selling every unit pays. The row owns its child-widget
    // references via the Inspector, so MineralInventoryView never reaches in by component type
    // (GetComponentInChildren). Mirrors the DroneRowView pattern.
    public class MineralRowView : MonoBehaviour
    {
        [SerializeField] private Image       _icon;
        [SerializeField] private TMP_Text    _nameLabel;      // "Nickel"
        [SerializeField] private TMP_Text    _unitValueLabel; // "8 each"
        [SerializeField] private TMP_Text    _qtyLabel;       // "×42"
        [SerializeField] private Image       _tierBadge;      // hexagon, tinted per tier (TierColors)
        [SerializeField] private TMP_Text    _tierLabel;      // "T2"
        [SerializeField] private Button      _sellButton;
        [SerializeField] private TMP_Text    _sellValueLabel; // coins for selling every unit held
        [SerializeField] private CanvasGroup _group;          // dims the whole row when none are held
        [SerializeField] private float       _emptyAlpha = 0.45f;

        // Configure this row for `qty` units of a mineral worth `unitValue` coins each. A mineral the
        // player holds none of keeps its row and button in place — the list doesn't change shape —
        // but the row dims and Sell can't be pressed.
        public void Bind(Sprite icon, string displayName, int tier, int qty, int unitValue, Action onSell)
        {
            SetIcon(icon);
            bool held = qty > 0;

            if (_nameLabel != null)      _nameLabel.text      = displayName;
            if (_unitValueLabel != null) _unitValueLabel.text = $"{unitValue} each";
            if (_qtyLabel != null)       _qtyLabel.text       = $"×{qty}";
            if (_tierLabel != null)      _tierLabel.text      = $"T{tier}";
            if (_tierBadge != null)      _tierBadge.color     = TierColors.For(tier);
            if (_sellValueLabel != null) _sellValueLabel.text = $"{qty * unitValue}";
            if (_group != null)          _group.alpha         = held ? 1f : _emptyAlpha;

            if (_sellButton != null)
            {
                _sellButton.onClick.RemoveAllListeners();
                _sellButton.interactable = held;
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
