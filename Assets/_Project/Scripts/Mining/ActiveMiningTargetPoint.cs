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
