using NUnit.Framework;
using SocialUniverse.Mining;
using UnityEngine;

namespace SocialUniverse.Tests
{
    public class ActiveMiningTargetPointTests
    {
        [Test]
        public void PickFacingPoint_returns_a_point_on_the_sphere_facing_the_viewer()
        {
            var center = new Vector3(1f, 2f, 3f);
            const float radius = 2f;
            var towardViewer = Vector3.forward;

            for (int i = 0; i < 100; i++)
            {
                Vector3 point  = ActiveMiningTargetPoint.PickFacingPoint(center, radius, towardViewer);
                Vector3 offset = point - center;

                Assert.AreEqual(radius, offset.magnitude, 0.001f, "point must lie exactly on the sphere surface");
                Assert.GreaterOrEqual(Vector3.Dot(offset.normalized, towardViewer), 0f,
                    "point must be on the hemisphere facing the viewer, not the far side");
            }
        }

        [Test]
        public void PlaceOnAsteroid_parents_the_marker_and_positions_it_on_the_asteroid_surface()
        {
            var asteroidGo = new GameObject("Asteroid");
            asteroidGo.transform.position = new Vector3(5f, 0f, 0f);

            var markerGo = new GameObject("Marker");
            var marker   = markerGo.AddComponent<ActiveMiningTargetPoint>();

            marker.PlaceOnAsteroid(asteroidGo.transform, radius: 1.5f, towardViewer: Vector3.back);

            Assert.AreEqual(asteroidGo.transform, marker.transform.parent);
            float distanceFromCenter = Vector3.Distance(marker.transform.position, asteroidGo.transform.position);
            Assert.AreEqual(1.5f, distanceFromCenter, 0.001f);

            Object.DestroyImmediate(markerGo);
            Object.DestroyImmediate(asteroidGo);
        }
    }
}
