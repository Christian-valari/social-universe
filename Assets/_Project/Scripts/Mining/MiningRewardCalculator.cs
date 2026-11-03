using UnityEngine;
using SocialUniverse.Config;

namespace SocialUniverse.Mining
{
    public readonly struct MiningReward
    {
        public readonly int   MineralQuantity;
        public readonly int   IdleQuantity;
        public readonly float IdleDurationSeconds;

        public MiningReward(int mineralQuantity, int idleQuantity, float idleDurationSeconds)
        {
            MineralQuantity     = mineralQuantity;
            IdleQuantity        = idleQuantity;
            IdleDurationSeconds = idleDurationSeconds;
        }
    }

    // Single source of truth for the idle duration and the mined mineral quantities for an asteroid.
    // MineralQuantity is the full amount (the asteroid's RemainingYield scaled by the active drone's
    // effective yield multiplier) that a perfect active run pays; IdleQuantity is the share idle
    // mining pays of it.
    public class MiningRewardCalculator
    {
        private readonly EconomyConfig _config;

        public MiningRewardCalculator(EconomyConfig config) => _config = config;

        // droneSpeed is the active drone's effective TravelSpeed (base + Speed upgrades): the idle
        // session shortens in proportion to ReferenceDroneSpeed / droneSpeed, still clamped to the
        // idle min/max. It never changes the quantity. The server doesn't check durations;
        // ValidateMining's per-planet claim budget bounds mining pace.
        public MiningReward Compute(Asteroid asteroid, float effectiveYieldMult, float droneSpeed)
        {
            int remainingYield = asteroid.RemainingYield;
            int quantity       = Mathf.RoundToInt(remainingYield * Mathf.Max(0f, effectiveYieldMult));
            int idleQuantity   = Mathf.RoundToInt(quantity * Mathf.Clamp01(_config.IdleYieldFraction));

            float speedFactor = droneSpeed > 0f ? _config.ReferenceDroneSpeed / droneSpeed : 1f;
            float rawDuration = remainingYield * _config.IdleSecondsPerYieldUnit * speedFactor;
            float duration    = Mathf.Clamp(rawDuration, _config.MinIdleSessionSeconds, _config.MaxIdleSessionSeconds);

            return new MiningReward(quantity, idleQuantity, duration);
        }
    }
}
