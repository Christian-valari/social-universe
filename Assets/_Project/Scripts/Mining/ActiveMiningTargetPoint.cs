using System.Collections.Generic;
using UnityEngine;

namespace SocialUniverse.Mining
{
    // Marker for a single tap target during active mining. Anchored to a random point on the
    // spawned asteroid's surface that currently faces the camera, so it's a genuine 3D point
    // that moves as the asteroid rotates. Only ever placed on the hemisphere facing the viewer
    // at spawn time (no occlusion tracking) — see design spec 2026-07-04 §4.
    public class ActiveMiningTargetPoint : MonoBehaviour
    {
        // Picks a random point on the sphere (center, radius) that lies within the hemisphere
        // facing towardViewer. Pure/static so it's directly unit-testable without a scene.
        public static Vector3 PickFacingPoint(Vector3 center, float radius, Vector3 towardViewer)
        {
            Vector3 viewerDir = towardViewer.normalized;

            for (int attempt = 0; attempt < 64; attempt++)
            {
                Vector3 dir = Random.onUnitSphere;
                if (Vector3.Dot(dir, viewerDir) >= 0f)
                    return center + dir * radius;
            }

            // Fallback so this never loops forever: reflect a random point into the facing
            // hemisphere instead of retrying again.
            Vector3 fallback = Random.onUnitSphere;
            if (Vector3.Dot(fallback, viewerDir) < 0f) fallback = -fallback;
            return center + fallback * radius;
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
        public void PlaceOnAsteroid(Transform asteroidTransform, float radius, Vector3 towardViewer)
        {
            transform.SetParent(asteroidTransform, worldPositionStays: false);
            Vector3 worldPoint = PickFacingPoint(asteroidTransform.position, radius, towardViewer);
            transform.position = worldPoint;
        }
    }
}
