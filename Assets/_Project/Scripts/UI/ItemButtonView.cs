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

        public void Bind(ItemDefinition item)
        {
            if (_label != null) _label.text = $"{item.DisplayName}\n{item.Cost}";
            if (_icon != null)
            {
                _icon.sprite  = item.Icon;
                _icon.enabled = item.Icon != null; // hide the Image when the item has no icon
            }
        }
    }
}
