using System.Collections.Generic;
using UnityEngine;

namespace SocialUniverse.Mining
{
    // Marker for a single tap target during active mining. Anchored to a random point on the
    // spawned asteroid's surface that currently faces the camera, so it's a genuine 3D point
    // that moves as the asteroid rotates. Placed within the cap of the facing hemisphere that
    // the spin can't carry past the edge before the ring closes (no occlusion tracking) — see
    // design spec 2026-07-04 §4.
    public class ActiveMiningTargetPoint : MonoBehaviour
    {
        const float MinPlacementAngle = 15f;
        const float MaxPlacementAngleCap = 85f;

        // How far from the viewer axis a target may be placed so that the spin can't carry it past
        // the asteroid's edge before its ring closes: the cap less the turn at the fastest spin.
        public static float MaxPlacementAngle(float maxSpinDegrees, float ringSeconds) =>
            Mathf.Clamp(MaxPlacementAngleCap - maxSpinDegrees * ringSeconds, MinPlacementAngle, MaxPlacementAngleCap);

        // Picks a random point on the sphere (center, radius) within maxAngleDegrees of the
        // direction towardViewer, uniformly over the cap. 90 is the whole facing hemisphere.
        // Pure/static so it's directly unit-testable without a scene.
        public static Vector3 PickFacingPoint(Vector3 center, float radius, Vector3 towardViewer, float maxAngleDegrees = 90f)
        {
            Vector3 viewerDir = towardViewer.normalized;
            float   cosMax    = maxAngleDegrees >= 90f ? 0f : Mathf.Cos(Mathf.Max(maxAngleDegrees, 0f) * Mathf.Deg2Rad);

            float cos = Random.Range(cosMax, 1f);
            float sin = Mathf.Sqrt(Mathf.Max(0f, 1f - cos * cos));

            Vector3 side = Vector3.Cross(viewerDir, Mathf.Abs(viewerDir.y) < 0.99f ? Vector3.up : Vector3.right).normalized;
            Vector3 up   = Vector3.Cross(viewerDir, side);
            float   spin = Random.Range(0f, Mathf.PI * 2f);

            Vector3 dir = viewerDir * cos + (side * Mathf.Cos(spin) + up * Mathf.Sin(spin)) * sin;
            return center + dir * radius;
        }

        // Whether a screen point is at least margin pixels inside every edge of the screen.
        public static bool IsOnScreen(Vector2 screenPoint, Vector2 screenSize, float margin)
        {
            return screenPoint.x >= margin && screenPoint.x <= screenSize.x - margin
                && screenPoint.y >= margin && screenPoint.y <= screenSize.y - margin;
        }

        // How far apart two screen points are by the larger of their two axis gaps. Two square tap
        // areas of side s can't overlap when this is at least s.
        public static float AxisGap(Vector2 a, Vector2 b) => Mathf.Max(Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y));

        // Whether a screen point is at least minDistance pixels from every one of the others along
        // at least one axis, so square areas centred on them don't overlap.
        public static bool IsFarEnough(Vector2 candidate, IReadOnlyList<Vector2> others, float minDistance)
        {
            for (int i = 0; i < others.Count; i++)
                if (AxisGap(candidate, others[i]) < minDistance) return false;
            return true;
        }

        // Parents this marker to the asteroid and positions it at a random point on its surface
        // facing towardViewer, so subsequent asteroid rotation carries the marker along with it.
        public void PlaceOnAsteroid(Transform asteroidTransform, float radius, Vector3 towardViewer, float maxAngleDegrees = 90f)
        {
            transform.SetParent(asteroidTransform, worldPositionStays: false);
            Vector3 worldPoint = PickFacingPoint(asteroidTransform.position, radius, towardViewer, maxAngleDegrees);
            transform.position = worldPoint;
        }
    }
}
