using NUnit.Framework;
using SocialUniverse.Travel;
using UnityEngine;

namespace SocialUniverse.Tests
{
    public class SkyLockOnMathTests
    {
        [Test]
        public void GetSkyDirection_returns_unit_length_vectors()
        {
            for (int i = 0; i < 10; i++)
            {
                var dir = SkyLockOnMath.GetSkyDirection(i, 10);
                Assert.That(dir.magnitude, Is.EqualTo(1f).Within(0.001f));
            }
        }

        [Test]
        public void FindClosest_returns_the_nearest_direction_and_its_angle()
        {
            var directions = new[] { Vector3.forward, Vector3.right, Vector3.up };

            int index = SkyLockOnMath.FindClosest(Vector3.right, directions, out float angle);

            Assert.AreEqual(1, index);
            Assert.That(angle, Is.EqualTo(0f).Within(0.01f));
        }

        [Test]
        public void FindClosest_with_no_directions_returns_negative_one()
        {
            int index = SkyLockOnMath.FindClosest(Vector3.forward, new Vector3[0], out float angle);

            Assert.AreEqual(-1, index);
        }
    }
}
