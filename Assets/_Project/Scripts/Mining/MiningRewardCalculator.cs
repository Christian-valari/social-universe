using UnityEngine;
using SocialUniverse.Config;

namespace SocialUniverse.Mining
{
    public readonly struct MiningReward
    {
        public readonly int   TotalCoins;
        public readonly float IdleDurationSeconds;
        public readonly int   ActiveTapsRequired;
        public readonly float ActiveSessionDurationSeconds;
        public readonly float CoinsPerSec;

        public MiningReward(int totalCoins, float idleDurationSeconds, int activeTapsRequired,
            float activeSessionDurationSeconds, float coinsPerSec)
        {
            TotalCoins                   = totalCoins;
            IdleDurationSeconds          = idleDurationSeconds;
            ActiveTapsRequired           = activeTapsRequired;
            ActiveSessionDurationSeconds = activeSessionDurationSeconds;
            CoinsPerSec                  = coinsPerSec;
        }
    }

    // Single source of truth for idle-mining duration, active-mining tap count, active-mining
    // session countdown, and total coin payout for a given asteroid — all three pacing values
    // derive from the same RemainingYield so both mining modes pay out identical totals (see
    // MiningRewardCalculatorTests) and the active-mining countdown scales with the asteroid's
    // effective size without needing a separate "size" field anywhere.
    public class MiningRewardCalculator
    {
        private readonly EconomyConfig _config;

        public MiningRewardCalculator(EconomyConfig config) => _config = config;

        public MiningReward Compute(Asteroid asteroid)
        {
            int remainingYield = asteroid.RemainingYield;
            int totalCoins     = remainingYield * asteroid.Definition.CoinsPerUnit;

            float rawDuration = remainingYield * _config.IdleSecondsPerYieldUnit;
            float duration    = Mathf.Clamp(rawDuration, _config.MinIdleSessionSeconds, _config.MaxIdleSessionSeconds);

            int rawTaps = Mathf.CeilToInt(remainingYield / _config.ActiveYieldPerTap);
            int taps    = Mathf.Clamp(rawTaps, _config.MinActiveTaps, _config.MaxActiveTaps);

            float rawActiveSeconds = taps * _config.ActiveSecondsPerTap;
            float activeSeconds    = Mathf.Clamp(rawActiveSeconds, _config.MinActiveSessionSeconds, _config.MaxActiveSessionSeconds);

            // Computed per-claim from this asteroid's actual totalCoins/duration (not a fixed
            // per-type constant) so sessionDurationSec * coinsPerSec always equals totalCoins
            // exactly, even when duration was clamped — see EconomyService.GrantMiningRewardAsync.
            float coinsPerSec = duration > 0f ? totalCoins / duration : 0f;

            return new MiningReward(totalCoins, duration, taps, activeSeconds, coinsPerSec);
        }
    }
}
