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
        public readonly float UnitsPerSec;

        public MiningReward(int mineralQuantity, float idleDurationSeconds, int activeTapsRequired,
            float activeSessionDurationSeconds, float unitsPerSec)
        {
            MineralQuantity              = mineralQuantity;
            IdleDurationSeconds          = idleDurationSeconds;
            ActiveTapsRequired           = activeTapsRequired;
            ActiveSessionDurationSeconds = activeSessionDurationSeconds;
            UnitsPerSec                  = unitsPerSec;
        }
    }

    // Single source of truth for idle duration, active tap count, active countdown, and the
    // mined mineral quantity for an asteroid. Pacing derives from RemainingYield (unchanged
    // from M1); the mined quantity now scales by the active drone's effective yield multiplier.
    public class MiningRewardCalculator
    {
        private readonly EconomyConfig _config;

        public MiningRewardCalculator(EconomyConfig config) => _config = config;

        public MiningReward Compute(Asteroid asteroid, float effectiveYieldMult)
        {
            int remainingYield = asteroid.RemainingYield;
            int quantity       = Mathf.RoundToInt(remainingYield * Mathf.Max(0f, effectiveYieldMult));

            float rawDuration = remainingYield * _config.IdleSecondsPerYieldUnit;
            float duration    = Mathf.Clamp(rawDuration, _config.MinIdleSessionSeconds, _config.MaxIdleSessionSeconds);

            int rawTaps = Mathf.CeilToInt(remainingYield / _config.ActiveYieldPerTap);
            int taps    = Mathf.Clamp(rawTaps, _config.MinActiveTaps, _config.MaxActiveTaps);

            float rawActiveSeconds = taps * _config.ActiveSecondsPerTap;
            float activeSeconds    = Mathf.Clamp(rawActiveSeconds, _config.MinActiveSessionSeconds, _config.MaxActiveSessionSeconds);

            // Per-claim rate so durationSec * unitsPerSec == quantity exactly even when duration
            // was clamped — feeds the server anti-cheat cap in ValidateMining (mineral units).
            float unitsPerSec = duration > 0f ? quantity / duration : 0f;

            return new MiningReward(quantity, duration, taps, activeSeconds, unitsPerSec);
        }
    }
}
