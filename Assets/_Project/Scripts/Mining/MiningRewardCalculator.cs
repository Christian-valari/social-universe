using UnityEngine;
using SocialUniverse.Config;

namespace SocialUniverse.Mining
{
    public readonly struct MiningReward
    {
        public readonly int   TotalCoins;
        public readonly float IdleDurationSeconds;
        public readonly int   ActiveTapsRequired;
        public readonly float CoinsPerSec;

        public MiningReward(int totalCoins, float idleDurationSeconds, int activeTapsRequired, float coinsPerSec)
        {
            TotalCoins          = totalCoins;
            IdleDurationSeconds = idleDurationSeconds;
            ActiveTapsRequired  = activeTapsRequired;
            CoinsPerSec         = coinsPerSec;
        }
    }

    // Single source of truth for idle-mining duration, active-mining tap count, and total
    // coin payout for a given asteroid — both mining modes derive their pacing from the
    // same RemainingYield so they pay out identical totals (see MiningRewardCalculatorTests).
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

            // Computed per-claim from this asteroid's actual totalCoins/duration (not a fixed
            // per-type constant) so sessionDurationSec * coinsPerSec always equals totalCoins
            // exactly, even when duration was clamped — see EconomyService.GrantMiningRewardAsync.
            float coinsPerSec = duration > 0f ? totalCoins / duration : 0f;

            return new MiningReward(totalCoins, duration, taps, coinsPerSec);
        }
    }
}
