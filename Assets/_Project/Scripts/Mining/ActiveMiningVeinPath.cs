using UnityEngine;

namespace SocialUniverse.Mining
{
    // Where a finger is relative to a vein's line, in whatever 2D units the caller works in.
    // Pure/static so it's directly unit-testable without a scene.
    public static class ActiveMiningVeinPath
    {
        public static float DistanceToSegment(Vector2 point, Vector2 start, Vector2 end) =>
            Vector2.Distance(point, start + (end - start) * ProgressAlong(point, start, end));

        // How far along the line the point's nearest spot is, from 0 at the start to 1 at the end. A
        // line with no length is finished wherever the point is.
        public static float ProgressAlong(Vector2 point, Vector2 start, Vector2 end)
        {
            Vector2 line = end - start;
            float   sqrLength = line.sqrMagnitude;
            return sqrLength > 0f ? Mathf.Clamp01(Vector2.Dot(point - start, line) / sqrLength) : 1f;
        }
    }
}
