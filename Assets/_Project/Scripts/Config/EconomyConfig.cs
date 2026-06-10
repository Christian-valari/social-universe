using UnityEngine;

namespace SocialUniverse.Config
{
    [CreateAssetMenu(menuName = "SocialUniverse/Config/EconomyConfig", fileName = "EconomyConfig")]
    public class EconomyConfig : ScriptableObject
    {
        [Header("UGS Currency IDs")]
        [SerializeField] private string _coinsCurrencyId    = "COINS";
        [SerializeField] private string _stardustCurrencyId = "STARDUST";

        [Header("Starting Balances")]
        [SerializeField] private int   _startingCoins     = 500;
        [SerializeField] private int   _startingStardust  = 10;

        [Header("Land")]
        [SerializeField] private int   _baseLandPrice           = 100;
        [SerializeField] private int   _upkeepPerTilePerDay     = 5;

        [Header("Mining — Idle")]
        [SerializeField] private float _idleMiningRate          = 1f;   // units/sec
        [SerializeField] private float _maxOfflineHours         = 8f;

        [Header("Mining — Idle Session (click-to-mine)")]
        [SerializeField] private float _idleSessionDuration     = 30f;  // seconds spent mining before claim
        [SerializeField] private int   _idleSessionClaimTaps    = 5;    // taps required to claim the haul
        [SerializeField] private float _asteroidRespawnHours    = 4f;   // claimed asteroid is destroyed and respawns after this many real-world hours

        [Header("Mining — Active")]
        [SerializeField] private int   _activeTapYield          = 5;    // units per tap
        [SerializeField] private float _critChance              = 0.1f;
        [SerializeField] private float _critMultiplier          = 2f;

        public string CoinsCurrencyId    => _coinsCurrencyId;
        public string StardustCurrencyId => _stardustCurrencyId;

        public int   StartingCoins         => _startingCoins;
        public int   StartingStardust      => _startingStardust;
        public int   BaseLandPrice         => _baseLandPrice;
        public int   UpkeepPerTilePerDay   => _upkeepPerTilePerDay;
        public float IdleMiningRate        => _idleMiningRate;
        public float MaxOfflineHours       => _maxOfflineHours;
        public float IdleSessionDuration   => _idleSessionDuration;
        public int   IdleSessionClaimTaps  => _idleSessionClaimTaps;
        public float AsteroidRespawnHours  => _asteroidRespawnHours;
        public int   ActiveTapYield        => _activeTapYield;
        public float CritChance            => _critChance;
        public float CritMultiplier        => _critMultiplier;
    }
}
