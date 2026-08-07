using UnityEngine;

namespace SocialUniverse.Config
{
    public enum ItemRarity { Common, Uncommon, Rare, Epic, Legendary }

    [CreateAssetMenu(menuName = "SocialUniverse/Config/ItemDefinition", fileName = "NewItem")]
    public class ItemDefinition : ScriptableObject
    {
        [SerializeField] private string     _itemId;
        [SerializeField] private string     _displayName;
        [SerializeField] private int        _cost;
        [SerializeField] private ItemRarity _rarity;
        [SerializeField] private float      _yieldBonus; // additive yield/hour when placed
        [SerializeField] private int        _buildLevel; // tile build level this item unlocks
        [SerializeField] private GameObject _prefab;
        [SerializeField] private Sprite     _icon;       // UI icon shown in the build palette

        public string     ItemId      => _itemId;
        public string     DisplayName => _displayName;
        public int        Cost        => _cost;
        public ItemRarity Rarity      => _rarity;
        public float      YieldBonus  => _yieldBonus;
        public int        BuildLevel  => _buildLevel;
        public GameObject Prefab      => _prefab;
        public Sprite     Icon        => _icon;
    }
}
