using UnityEngine;

namespace SocialUniverse.Config
{
    [CreateAssetMenu(menuName = "SocialUniverse/Config/EconomyConfig", fileName = "EconomyConfig")]
    public class EconomyConfig : ScriptableObject
    {
        [Header("Starting Balances")]
        [SerializeField] private int   _startingCoins     = 500;
        [SerializeField] private int   _startingStardust  = 10;

        [Header("Land")]
        [SerializeField] private int   _baseLandPrice           = 100;
        [SerializeField] private int   _upkeepPerTilePerDay     = 5;

        [Header("Mining — Idle")]
        [SerializeField] private float _idleMiningRate          = 1f;   // units/sec
        [SerializeField] private float _maxOfflineHours         = 8f;

        [Header("Mining — Active")]
        [SerializeField] private int   _activeTapYield          = 5;    // units per tap
        [SerializeField] private float _critChance              = 0.1f;
        [SerializeField] private float _critMultiplier          = 2f;

        public int   StartingCoins         => _startingCoins;
        public int   StartingStardust      => _startingStardust;
        public int   BaseLandPrice         => _baseLandPrice;
        public int   UpkeepPerTilePerDay   => _upkeepPerTilePerDay;
        public float IdleMiningRate        => _idleMiningRate;
        public float MaxOfflineHours       => _maxOfflineHours;
        public int   ActiveTapYield        => _activeTapYield;
        public float CritChance            => _critChance;
        public float CritMultiplier        => _critMultiplier;
    }
}
