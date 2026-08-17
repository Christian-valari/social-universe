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

        [Header("Mining — Idle")]
        [SerializeField] private float _idleSecondsPerYieldUnit = 3f;    // idle duration scales with the asteroid's remaining yield
        [SerializeField] private float _minIdleSessionSeconds   = 30f;   // clamp: smallest asteroids still take at least this long
        [SerializeField] private float _maxIdleSessionSeconds   = 1800f; // clamp: largest asteroids cap out at this long (30 min)

        [Header("Mining — Active")]
        [SerializeField] private float _activeYieldPerTap       = 8f;    // how much RemainingYield one successful tap represents
        [SerializeField] private int   _minActiveTaps           = 5;     // clamp: smallest asteroids still take at least this many taps
        [SerializeField] private int   _maxActiveTaps            = 20;    // clamp: largest asteroids cap out at this many taps
        [SerializeField] private float _activeSecondsPerTap     = 3f;    // seconds contributed per required tap toward the overall session countdown
        [SerializeField] private float _minActiveSessionSeconds = 12f;   // clamp: smallest asteroids still get at least this long
        [SerializeField] private float _maxActiveSessionSeconds = 60f;   // clamp: largest asteroids cap out at this long
        [SerializeField] private int   _activeMaxErrors         = 3;     // wrong taps before the asteroid is lost

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

        public float IdleSecondsPerYieldUnit => _idleSecondsPerYieldUnit;
        public float MinIdleSessionSeconds   => _minIdleSessionSeconds;
        public float MaxIdleSessionSeconds   => _maxIdleSessionSeconds;

        public float ActiveYieldPerTap        => _activeYieldPerTap;
        public int   MinActiveTaps            => _minActiveTaps;
        public int   MaxActiveTaps            => _maxActiveTaps;
        public float ActiveSecondsPerTap      => _activeSecondsPerTap;
        public float MinActiveSessionSeconds  => _minActiveSessionSeconds;
        public float MaxActiveSessionSeconds  => _maxActiveSessionSeconds;
        public int   ActiveMaxErrors          => _activeMaxErrors;

        public int   StartingFleetSlots   => _startingFleetSlots;
        public int   SlotUnlockBaseCost    => _slotUnlockBaseCost;
        public float SlotUnlockCostGrowth  => _slotUnlockCostGrowth;

        public float MaxFuel               => _maxFuel;
        public float FuelRechargePerHour   => _fuelRechargePerHour;
        public int   FuelRefillCost        => _fuelRefillCost;
    }
}
