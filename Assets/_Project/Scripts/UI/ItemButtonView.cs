using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SocialUniverse.Config;

namespace SocialUniverse.UI
{
    // Binds an ItemDefinition to a palette button's icon + label. Put this on the ItemButton
    // prefab and wire the icon Image and the label text; LandBuildPaletteView calls Bind per item.
    public class ItemButtonView : MonoBehaviour
    {
        [SerializeField] private Image    _icon;
        [SerializeField] private TMP_Text _label;
        [SerializeField] private TMP_Text _priceText; // dedicated price label (optional)

        public void Bind(ItemDefinition item)
        {
            // With a dedicated price label the name label shows just the name; otherwise it falls
            // back to the combined "name\ncost" so the price stays visible either way.
            if (_label != null) _label.text = _priceText != null ? item.DisplayName : $"{item.DisplayName}\n{item.Cost}";
            if (_priceText != null) _priceText.text = item.Cost.ToString();
            if (_icon != null)
            {
                _icon.sprite  = item.Icon;
                _icon.enabled = item.Icon != null; // hide the Image when the item has no icon
            }
        }
    }
}
