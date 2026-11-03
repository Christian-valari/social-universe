namespace SocialUniverse.Config
{
    // Everything an active-mining run reads from EconomyConfig, resolved for one asteroid tier.
    // PreviousTier is the row below Tier (null at Tier 1).
    public sealed class ActiveMiningRunSettings
    {
        public ActiveMiningRunSettings(
            int tierNumber,
            ActiveMiningTierSettings tier,
            ActiveMiningTierSettings previousTier,
            float perfectShare,
            float goodShare,
            float lateShare,
            float spawnGapSeconds,
            float missGraceSeconds,
            float firstTargetDelaySeconds = 0f)
        {
            TierNumber              = tierNumber;
            Tier                    = tier;
            PreviousTier            = previousTier;
            PerfectShare            = perfectShare;
            GoodShare               = goodShare;
            LateShare               = lateShare;
            SpawnGapSeconds         = spawnGapSeconds;
            MissGraceSeconds        = missGraceSeconds;
            FirstTargetDelaySeconds = firstTargetDelaySeconds;
        }

        public int                      TierNumber              { get; }
        public ActiveMiningTierSettings Tier                    { get; }
        public ActiveMiningTierSettings PreviousTier            { get; }
        public float                    PerfectShare            { get; }
        public float                    GoodShare               { get; }
        public float                    LateShare               { get; }
        public float                    SpawnGapSeconds         { get; }
        public float                    MissGraceSeconds        { get; }
        public float                    FirstTargetDelaySeconds { get; }
    }
}
