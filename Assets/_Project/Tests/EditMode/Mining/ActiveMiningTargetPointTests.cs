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

        [Test]
        public void IsFarEnough_rejects_a_point_inside_the_distance()
        {
            var others = new[] { Vector2.zero };

            Assert.IsFalse(ActiveMiningTargetPoint.IsFarEnough(new Vector2(100f, 0f), others, 160f));
            Assert.IsTrue(ActiveMiningTargetPoint.IsFarEnough(new Vector2(200f, 0f), others, 160f));
        }

        [Test]
        public void IsFarEnough_compares_per_axis_so_a_diagonal_point_can_still_be_too_close()
        {
            var others = new[] { Vector2.zero };

            Assert.IsFalse(ActiveMiningTargetPoint.IsFarEnough(new Vector2(120f, 120f), others, 160f), "170 px away in a straight line, but within 160 on both axes");
            Assert.IsTrue(ActiveMiningTargetPoint.IsFarEnough(new Vector2(120f, 160f), others, 160f));
        }

        [Test]
        public void IsFarEnough_accepts_a_point_exactly_at_the_distance()
        {
            Assert.IsTrue(ActiveMiningTargetPoint.IsFarEnough(new Vector2(160f, 0f), new[] { Vector2.zero }, 160f));
        }

        [Test]
        public void IsFarEnough_needs_every_other_point_to_be_far()
        {
            var others = new[] { new Vector2(0f, 0f), new Vector2(500f, 0f) };

            Assert.IsFalse(ActiveMiningTargetPoint.IsFarEnough(new Vector2(450f, 0f), others, 160f));
            Assert.IsTrue(ActiveMiningTargetPoint.IsFarEnough(new Vector2(250f, 0f), others, 160f));
        }

        [Test]
        public void IsFarEnough_accepts_any_point_when_there_are_no_others()
        {
            Assert.IsTrue(ActiveMiningTargetPoint.IsFarEnough(Vector2.zero, new Vector2[0], 160f));
        }

        [Test]
        public void IsOnScreen_accepts_a_point_well_inside_the_margin()
        {
            Assert.IsTrue(ActiveMiningTargetPoint.IsOnScreen(new Vector2(540f, 960f), new Vector2(1080f, 1920f), margin: 70f));
        }

        [TestCase(-10f, 960f,  TestName = "IsOnScreen_rejects_a_point_off_the_left")]
        [TestCase(1090f, 960f, TestName = "IsOnScreen_rejects_a_point_off_the_right")]
        [TestCase(540f, -10f,  TestName = "IsOnScreen_rejects_a_point_off_the_bottom")]
        [TestCase(540f, 1930f, TestName = "IsOnScreen_rejects_a_point_off_the_top")]
        [TestCase(69f, 960f,   TestName = "IsOnScreen_rejects_a_point_inside_the_left_margin")]
        [TestCase(1011f, 960f, TestName = "IsOnScreen_rejects_a_point_inside_the_right_margin")]
        [TestCase(540f, 69f,   TestName = "IsOnScreen_rejects_a_point_inside_the_bottom_margin")]
        [TestCase(540f, 1851f, TestName = "IsOnScreen_rejects_a_point_inside_the_top_margin")]
        public void IsOnScreen_rejects_a_point_past_the_margin(float x, float y)
        {
            Assert.IsFalse(ActiveMiningTargetPoint.IsOnScreen(new Vector2(x, y), new Vector2(1080f, 1920f), margin: 70f));
        }

        [TestCase(70f, 960f,   TestName = "IsOnScreen_accepts_a_point_exactly_on_the_left_margin")]
        [TestCase(1010f, 960f, TestName = "IsOnScreen_accepts_a_point_exactly_on_the_right_margin")]
        [TestCase(540f, 70f,   TestName = "IsOnScreen_accepts_a_point_exactly_on_the_bottom_margin")]
        [TestCase(540f, 1850f, TestName = "IsOnScreen_accepts_a_point_exactly_on_the_top_margin")]
        public void IsOnScreen_accepts_a_point_exactly_on_the_margin(float x, float y)
        {
            Assert.IsTrue(ActiveMiningTargetPoint.IsOnScreen(new Vector2(x, y), new Vector2(1080f, 1920f), margin: 70f));
        }
    }
}
