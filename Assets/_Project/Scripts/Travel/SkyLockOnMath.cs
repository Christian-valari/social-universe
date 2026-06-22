using UnityEngine;

namespace SocialUniverse.Travel
{
    // Pure math for Sky Discovery's "look at a body to lock on" interaction —
    // kept separate from SkyDiscoveryController (MonoBehaviour) so it's
    // unit-testable without a scene.
    public static class SkyLockOnMath
    {
        // Deterministic, evenly-distributed point on a unit sphere for celestial
        // body index `index` of `total` (Fibonacci/golden-angle spiral). Stable
        // across runs so the same planet always renders at the same sky position.
        public static Vector3 GetSkyDirection(int index, int total)
        {
            if (total <= 1) return Vector3.forward;

            float offset = 2f / total;
            float y       = (index * offset - 1f) + offset / 2f;
            float r       = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y));
            float phi     = index * Mathf.PI * (3f - Mathf.Sqrt(5f));

            return new Vector3(Mathf.Cos(phi) * r, y, Mathf.Sin(phi) * r);
        }

        // Returns the index of the direction closest to lookDirection, and the
        // angle to it in degrees. Returns -1 if directions is empty.
        public static int FindClosest(Vector3 lookDirection, Vector3[] directions, out float angleDeg)
        {
            angleDeg = float.MaxValue;
            int best = -1;
            for (int i = 0; i < directions.Length; i++)
            {
                float angle = Vector3.Angle(lookDirection, directions[i]);
                if (angle < angleDeg)
                {
                    angleDeg = angle;
                    best     = i;
                }
            }
            return best;
        }
    }
}
