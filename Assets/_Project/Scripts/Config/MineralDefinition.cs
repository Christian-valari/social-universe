using UnityEngine;

namespace SocialUniverse.Config
{
    [CreateAssetMenu(menuName = "SocialUniverse/Config/MineralDefinition", fileName = "NewMineral")]
    public class MineralDefinition : ScriptableObject
    {
        [SerializeField] private string _mineralId;
        [SerializeField] private string _displayName;
        [SerializeField] private int    _tier      = 1;
        [SerializeField] private int    _sellValue = 2;   // MUST MATCH ServerCode/SellMinerals.js SELL_VALUES
        [SerializeField] private Sprite _icon;
        [SerializeField] private Color  _tintColor = Color.white;

        public string MineralId   => _mineralId;
        public string DisplayName => _displayName;
        public int    Tier        => _tier;
        public int    SellValue   => _sellValue;
        public Sprite Icon        => _icon;
        public Color  TintColor   => _tintColor;
    }
}
