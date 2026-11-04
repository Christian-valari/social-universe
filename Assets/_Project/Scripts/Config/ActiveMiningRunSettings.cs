namespace SocialUniverse.Config
{
    // Everything an active-mining run reads from EconomyConfig, resolved for one asteroid tier.
    public sealed class ActiveMiningRunSettings
    {
        public ActiveMiningRunSettings(
            int tierNumber,
            ActiveMiningTierSettings tier,
            float perfectShare,
            float goodShare,
            float lateShare,
            float spawnGapSeconds,
            float missGraceSeconds,
            float firstTargetDelaySeconds = 0f,
            float secondTargetStagger = 0.4f,
            float veinRingSeconds = 1.6f,
            float veinPerfectBySeconds = 1.1f,
            float veinGoodBySeconds = 1.35f,
            float veinSpinDegrees = 5f,
            float veinDragTolerance = 90f,
            float veinCompleteProgress = 0.95f,
            float veinMinLength = 300f,
            float veinMaxLength = 550f,
            float veinReachSeconds = 0.5f,
            float veinReferenceLength = 425f)
        {
            VeinMinLength           = veinMinLength;
            VeinMaxLength           = veinMaxLength;
            VeinReachSeconds        = veinReachSeconds;
            VeinReferenceLength     = veinReferenceLength;
            VeinDragTolerance       = veinDragTolerance;
            VeinCompleteProgress    = veinCompleteProgress;
            VeinRingSeconds         = veinRingSeconds;
            VeinPerfectBySeconds    = veinPerfectBySeconds;
            VeinGoodBySeconds       = veinGoodBySeconds;
            VeinSpinDegrees         = veinSpinDegrees;
            SecondTargetStagger     = secondTargetStagger;
            TierNumber              = tierNumber;
            Tier                    = tier;
            PerfectShare            = perfectShare;
            GoodShare               = goodShare;
            LateShare               = lateShare;
            SpawnGapSeconds         = spawnGapSeconds;
            MissGraceSeconds        = missGraceSeconds;
            FirstTargetDelaySeconds = firstTargetDelaySeconds;
        }

        public int                      TierNumber              { get; }
        public ActiveMiningTierSettings Tier                    { get; }
        public float                    PerfectShare            { get; }
        public float                    GoodShare               { get; }
        public float                    LateShare               { get; }
        public float                    SpawnGapSeconds         { get; }
        public float                    MissGraceSeconds        { get; }
        public float                    FirstTargetDelaySeconds { get; }

        // How far round its ring the newest target must be before another can join it (0-1).
        public float                    SecondTargetStagger     { get; }

        // A vein is graded on its own times, and the asteroid eases down to VeinSpinDegrees while one is live.
        // These three are the times of a vein VeinReferenceLength long; VeinRingSecondsFor and the two below
        // give the times for any length.
        public float                    VeinRingSeconds         { get; }
        public float                    VeinPerfectBySeconds    { get; }
        public float                    VeinGoodBySeconds       { get; }
        public float                    VeinSpinDegrees         { get; }

        // A drag fails more than VeinDragTolerance canvas units off the line, and finishes VeinCompleteProgress (0-1) of the way along it.
        public float                    VeinDragTolerance       { get; }
        public float                    VeinCompleteProgress    { get; }

        // The distance between a vein's ends, in canvas units, that placement aims for.
        public float                    VeinMinLength           { get; }
        public float                    VeinMaxLength           { get; }

        // Seeing a vein and reaching its start takes VeinReachSeconds on any vein; only the drag grows with length.
        public float                    VeinReachSeconds        { get; }
        public float                    VeinReferenceLength     { get; }

        public float VeinRingSecondsFor(float length)      => ScaledForLength(VeinRingSeconds, length);
        public float VeinPerfectBySecondsFor(float length) => ScaledForLength(VeinPerfectBySeconds, length);
        public float VeinGoodBySecondsFor(float length)    => ScaledForLength(VeinGoodBySeconds, length);

        // A length outside the band takes the nearest end of it.
        private float ScaledForLength(float referenceSeconds, float length)
        {
            float clamped = UnityEngine.Mathf.Clamp(length, VeinMinLength, VeinMaxLength);
            return VeinReachSeconds + (referenceSeconds - VeinReachSeconds) * clamped / VeinReferenceLength;
        }
    }
}
