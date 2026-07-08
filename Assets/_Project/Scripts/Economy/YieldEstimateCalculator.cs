using UnityEngine;
using SocialUniverse.Config;

namespace SocialUniverse.Economy
{
    public readonly struct YieldEstimate
    {
        public readonly int   AccruedCoins;
        public readonly float RatePerHour;

        public YieldEstimate(int accruedCoins, float ratePerHour)
        {
            AccruedCoins = accruedCoins;
            RatePerHour  = ratePerHour;
        }
    }

    // Client-side mirror of the ClaimYield.js formula, used only to show a live "coins ready
    // to claim" estimate before the player taps Claim. The server call in YieldService is the
    // sole source of actually-granted coins — this never mutates wallet/registry state.
    public class YieldEstimateCalculator
    {
        public YieldEstimate Compute(LandTileEntry entry, EconomyConfig config, long nowUnixMs)
        {
            float elapsedHours = Mathf.Min((nowUnixMs - entry.LastYieldClaimTs) / 3600000f, config.MaxYieldAccrualHours);
            float buildBonus   = entry.BuildLevel * config.BuildLevelYieldMultiplier;
            float visitBonus   = Mathf.Min(entry.VisitCount, config.MaxVisitCount) * config.VisitYieldBonus;
            float rate         = config.BaseYieldPerTilePerHour * (1f + buildBonus + visitBonus);
            int   accrued      = Mathf.FloorToInt(rate * elapsedHours);

            return new YieldEstimate(accrued, rate);
        }
    }
}
