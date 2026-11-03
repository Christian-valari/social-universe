using UnityEngine;

namespace SocialUniverse.Mining
{
    // One ring in an active-mining run. The session ages it; views read Progress
    // (0 = just spawned, 1 = closed) to draw it.
    public sealed class ActiveMiningTarget
    {
        public ActiveMiningTarget(int id, ActiveMiningTargetKind kind, float ringSeconds)
        {
            Id          = id;
            Kind        = kind;
            RingSeconds = ringSeconds;
        }

        public int                    Id             { get; }
        public ActiveMiningTargetKind Kind           { get; }
        public float                  RingSeconds    { get; }
        public float                  ElapsedSeconds { get; internal set; }

        public float Progress => RingSeconds > 0f ? Mathf.Clamp01(ElapsedSeconds / RingSeconds) : 1f;
    }
}
