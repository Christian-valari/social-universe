using UnityEngine;

namespace SocialUniverse.Config
{
    [CreateAssetMenu(menuName = "SocialUniverse/Config/UpgradeDefinition", fileName = "NewUpgrade")]
    public class UpgradeDefinition : ScriptableObject
    {
        [SerializeField] private DroneStat _stat;
        [SerializeField] private int       _maxLevel      = 10;
        [SerializeField] private int       _baseCost      = 50;   // MUST MATCH ServerCode/UpgradeDrone.js cost formula
        [SerializeField] private float     _costGrowth    = 1.5f; // MUST MATCH ServerCode/UpgradeDrone.js
        [SerializeField] private float     _deltaPerLevel = 10f;

        public DroneStat Stat          => _stat;
        public int       MaxLevel      => _maxLevel;
        public int       BaseCost      => _baseCost;
        public float     CostGrowth    => _costGrowth;
        public float     DeltaPerLevel => _deltaPerLevel;
    }
}
