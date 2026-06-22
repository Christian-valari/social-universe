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

        [Header("Land Registry")]
        [SerializeField] private float _landRegistryPollIntervalSec = 20f; // how often to refresh other players' tile ownership

        [Header("Build")]
        [SerializeField] private int   _maxBuildLevel           = 4;

        [Header("Yield")]
        [SerializeField] private float _baseYieldPerTilePerHour     = 2f;
        [SerializeField] private float _buildLevelYieldMultiplier   = 0.25f; // +25% per build level
        [SerializeField] private float _visitYieldBonus             = 0.1f;  // +10% per recorded visit (capped)
        [SerializeField] private float _maxYieldAccrualHours        = 24f;
        [SerializeField] private int   _maxVisitCount               = 50;

        [Header("Upkeep & Resale")]
        [SerializeField] private float _upkeepPollIntervalSec   = 60f;  // how often to check whether upkeep is due
        [SerializeField] private float _landResaleRate          = 0.5f; // fraction of base price refunded on sell

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

        [Header("Travel — Fuel")]
        [SerializeField] private float _maxFuel                 = 100f;
        [SerializeField] private float _fuelRechargePerHour     = 10f;  // units/hour, recharges while offline too (server-computed)
        [SerializeField] private int   _fuelRefillCost          = 50;   // coins for an instant full refill

        public string CoinsCurrencyId    => _coinsCurrencyId;
        public string StardustCurrencyId => _stardustCurrencyId;

        public int   StartingCoins         => _startingCoins;
        public int   StartingStardust      => _startingStardust;
        public int   BaseLandPrice         => _baseLandPrice;
        public int   UpkeepPerTilePerDay   => _upkeepPerTilePerDay;
        public float LandRegistryPollIntervalSec => _landRegistryPollIntervalSec;
        public int   MaxBuildLevel              => _maxBuildLevel;
        public float BaseYieldPerTilePerHour    => _baseYieldPerTilePerHour;
        public float BuildLevelYieldMultiplier  => _buildLevelYieldMultiplier;
        public float VisitYieldBonus            => _visitYieldBonus;
        public float MaxYieldAccrualHours       => _maxYieldAccrualHours;
        public int   MaxVisitCount              => _maxVisitCount;
        public float UpkeepPollIntervalSec      => _upkeepPollIntervalSec;
        public float LandResaleRate             => _landResaleRate;
        public float IdleMiningRate        => _idleMiningRate;
        public float MaxOfflineHours       => _maxOfflineHours;
        public float IdleSessionDuration   => _idleSessionDuration;
        public int   IdleSessionClaimTaps  => _idleSessionClaimTaps;
        public float AsteroidRespawnHours  => _asteroidRespawnHours;
        public int   ActiveTapYield        => _activeTapYield;
        public float CritChance            => _critChance;
        public float CritMultiplier        => _critMultiplier;
        public float MaxFuel               => _maxFuel;
        public float FuelRechargePerHour   => _fuelRechargePerHour;
        public int   FuelRefillCost        => _fuelRefillCost;
    }
}
