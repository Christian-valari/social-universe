using UnityEngine;
using SocialUniverse.Config;

namespace SocialUniverse.Mining
{
    public readonly struct MiningReward
    {
        public readonly int   MineralQuantity;
        public readonly float IdleDurationSeconds;
        public readonly int   ActiveTapsRequired;
        public readonly float ActiveSessionDurationSeconds;

        public MiningReward(int mineralQuantity, float idleDurationSeconds, int activeTapsRequired,
            float activeSessionDurationSeconds)
        {
            MineralQuantity              = mineralQuantity;
            IdleDurationSeconds          = idleDurationSeconds;
            ActiveTapsRequired           = activeTapsRequired;
            ActiveSessionDurationSeconds = activeSessionDurationSeconds;
        }
    }

    // Single source of truth for idle duration, active tap count, active countdown, and the
    // mined mineral quantity for an asteroid. Pacing derives from RemainingYield (unchanged
    // from M1); the mined quantity now scales by the active drone's effective yield multiplier.
    public class MiningRewardCalculator
    {
        private readonly EconomyConfig _config;

        public MiningRewardCalculator(EconomyConfig config) => _config = config;

        // droneSpeed is the active drone's effective TravelSpeed (base + Speed upgrades): the idle
        // session shortens in proportion to ReferenceDroneSpeed / droneSpeed, still clamped to the
        // idle min/max. It never changes the quantity or the active minigame. The server doesn't
        // check durations; ValidateMining's per-planet claim budget bounds mining pace.
        public MiningReward Compute(Asteroid asteroid, float effectiveYieldMult, float droneSpeed)
        {
            int remainingYield = asteroid.RemainingYield;
            int quantity       = Mathf.RoundToInt(remainingYield * Mathf.Max(0f, effectiveYieldMult));

            float speedFactor = droneSpeed > 0f ? _config.ReferenceDroneSpeed / droneSpeed : 1f;
            float rawDuration = remainingYield * _config.IdleSecondsPerYieldUnit * speedFactor;
            float duration    = Mathf.Clamp(rawDuration, _config.MinIdleSessionSeconds, _config.MaxIdleSessionSeconds);

            int rawTaps = Mathf.CeilToInt(remainingYield / _config.ActiveYieldPerTap);
            int taps    = Mathf.Clamp(rawTaps, _config.MinActiveTaps, _config.MaxActiveTaps);

            float rawActiveSeconds = taps * _config.ActiveSecondsPerTap;
            float activeSeconds    = Mathf.Clamp(rawActiveSeconds, _config.MinActiveSessionSeconds, _config.MaxActiveSessionSeconds);

            return new MiningReward(quantity, duration, taps, activeSeconds);
        }
    }
}
