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

        [Header("Build — Hex Board")]
        [SerializeField] private int _hexBoardRadius    = 2;   // radius-2 hexagon = 19 hexatiles
        [SerializeField] private int _freeHexCount      = 5;   // central hexatiles unlocked for free
        [SerializeField] private int _hexatileBasePrice = 200; // coins for the first purchased tile
        [SerializeField] private int _hexatilePriceStep = 100; // added per already-purchased tile

        [Header("Yield")]
        [SerializeField] private float _baseYieldPerTilePerHour     = 2f;
        [SerializeField] private float _buildLevelYieldMultiplier   = 0.25f; // +25% per build level
        [SerializeField] private float _visitYieldBonus             = 0.1f;  // +10% per recorded visit (capped)
        [SerializeField] private float _maxYieldAccrualHours        = 24f;
        [SerializeField] private int   _maxVisitCount               = 50;

        [Header("Upkeep & Resale")]
        [SerializeField] private float _upkeepPollIntervalSec   = 60f;  // how often to check whether upkeep is due
        [SerializeField] private float _landResaleRate          = 0.5f; // fraction of base price refunded on sell

        [Header("Mining — Shared")]
        [SerializeField] private float _asteroidRespawnHours    = 4f;   // claimed asteroid is destroyed and respawns after this many real-world hours
        [SerializeField] private float _asteroidYieldRollMin    = 0.8f; // spawned asteroid yield = BaseYield × a roll in [min, max]
        [SerializeField] private float _asteroidYieldRollMax    = 1.2f; // MUST MATCH ServerCode/ValidateMining.js YIELD_ROLL_MAX

        [Header("Mining — Idle")]
        [SerializeField] private float _idleSecondsPerYieldUnit = 3f;    // idle duration scales with the asteroid's remaining yield
        [SerializeField] private float _minIdleSessionSeconds   = 30f;   // clamp: smallest asteroids still take at least this long
        [SerializeField] private float _maxIdleSessionSeconds   = 1800f; // clamp: largest asteroids cap out at this long (30 min)
        [SerializeField] private float _referenceDroneSpeed     = 5f;    // drone speed at which idle durations are as above; faster drones (base TravelSpeed + Speed upgrades) mine proportionally faster
        [SerializeField] private float _idleYieldFraction       = 0.7f;  // idle mining pays this fraction of what a perfect active run does

        [Header("Mining — Active Difficulty")]
        [SerializeField] private ActiveMiningTierSettings[] _activeTiers; // one row per asteroid tier, Tier 1 first
        [SerializeField] private float _activePerfectShare      = 1f;    // share of a target's payout banked by a Perfect hit
        [SerializeField] private float _activeGoodShare         = 0.8f;  // ... by a Good hit
        [SerializeField] private float _activeLateShare         = 0.6f;  // ... by a Late hit
        [SerializeField] private float _activeSpawnGapSeconds   = 0.25f; // pause between one target resolving and the next spawning
        [SerializeField] private float _activeMissGraceSeconds  = 0.2f;  // stray taps right after a hit are forgiven for this long
        [SerializeField] private float _activeFirstTargetDelaySeconds = 0.6f; // pause between pressing Start and the first target
        [SerializeField] private float _activeSecondTargetStagger = 0.4f; // share of the newest ring that must have run before another target joins it
        [SerializeField] private float _activeVeinRingSeconds   = 1.6f;  // how long a vein stays open
        [SerializeField] private float _activeVeinPerfectBySeconds = 1.1f; // a vein finished at or before this age is Perfect (playtest guess)
        [SerializeField] private float _activeVeinGoodBySeconds = 1.35f; // ... Good; later is Late (playtest guess)
        [SerializeField] private float _activeVeinSpinDegrees   = 5f;    // the asteroid's spin while a vein is live, in degrees per second

        [Header("Drones — M6")]
        [SerializeField] private int   _startingFleetSlots    = 2;
        [SerializeField] private int   _slotUnlockBaseCost    = 500;  // MUST MATCH ServerCode/UnlockDroneSlot.js
        [SerializeField] private float _slotUnlockCostGrowth  = 2f;   // MUST MATCH ServerCode/UnlockDroneSlot.js

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
        public int HexBoardRadius    => _hexBoardRadius;
        public int FreeHexCount      => _freeHexCount;
        public int HexatileBasePrice => _hexatileBasePrice;
        public int HexatilePriceStep => _hexatilePriceStep;
        // Mirror of HexBoardMath.HexCount — Config can't reference Economy (cycle), so inline it.
        public int HexCount     => 3 * _hexBoardRadius * _hexBoardRadius + 3 * _hexBoardRadius + 1;
        public int MaxBuildLevel => HexCount;   // a plot is "maxed" when every hexatile holds a building
        public float BaseYieldPerTilePerHour    => _baseYieldPerTilePerHour;
        public float BuildLevelYieldMultiplier  => _buildLevelYieldMultiplier;
        public float VisitYieldBonus            => _visitYieldBonus;
        public float MaxYieldAccrualHours       => _maxYieldAccrualHours;
        public int   MaxVisitCount              => _maxVisitCount;
        public float UpkeepPollIntervalSec      => _upkeepPollIntervalSec;
        public float LandResaleRate             => _landResaleRate;
        public float AsteroidRespawnHours  => _asteroidRespawnHours;
        public float AsteroidYieldRollMin  => _asteroidYieldRollMin;
        public float AsteroidYieldRollMax  => _asteroidYieldRollMax;

        public float IdleSecondsPerYieldUnit => _idleSecondsPerYieldUnit;
        public float MinIdleSessionSeconds   => _minIdleSessionSeconds;
        public float MaxIdleSessionSeconds   => _maxIdleSessionSeconds;
        public float ReferenceDroneSpeed     => _referenceDroneSpeed;
        public float IdleYieldFraction       => _idleYieldFraction;

        // A tier outside the table takes the nearest row, and TierNumber reports the row used.
        // An empty table plays as Tier 1 defaults.
        public ActiveMiningRunSettings ActiveRunSettings(int tier)
        {
            int rows   = _activeTiers == null ? 0 : _activeTiers.Length;
            int number = Mathf.Clamp(tier, 1, Mathf.Max(1, rows));
            var current  = rows == 0 ? new ActiveMiningTierSettings() : _activeTiers[number - 1];
            var previous = number > 1 ? _activeTiers[number - 2] : null;
            return new ActiveMiningRunSettings(number, current, previous,
                _activePerfectShare, _activeGoodShare, _activeLateShare,
                _activeSpawnGapSeconds, _activeMissGraceSeconds, _activeFirstTargetDelaySeconds, _activeSecondTargetStagger,
                _activeVeinRingSeconds, _activeVeinPerfectBySeconds, _activeVeinGoodBySeconds, _activeVeinSpinDegrees);
        }

        public int   StartingFleetSlots   => _startingFleetSlots;
        public int   SlotUnlockBaseCost    => _slotUnlockBaseCost;
        public float SlotUnlockCostGrowth  => _slotUnlockCostGrowth;

        public float MaxFuel               => _maxFuel;
        public float FuelRechargePerHour   => _fuelRechargePerHour;
        public int   FuelRefillCost        => _fuelRefillCost;
    }
}
