using NUnit.Framework;
using SocialUniverse.Travel;
using UnityEngine;

namespace SocialUniverse.Tests
{
    // Device attitudes below are raw sensor-frame quaternions: right-handed,
    // Earth-fixed (X east, Y north, Z up), rotating the device frame (X right,
    // Y screen-top, Z out of the screen) into it. Quaternion.AngleAxis is only
    // used as a component-level constructor here — the resulting (x, y, z, w)
    // values match the right-handed sensor convention.
    public class GyroAttitudeMathTests
    {
        private const float Tolerance = 1e-3f;

        // Phone flat on its back, screen up, top pointing north.
        private static readonly Quaternion FlatOnBack = Quaternion.identity;

        // Phone upright, screen facing the player, top pointing up
        // (rotated 90° about the east axis from flat).
        private static readonly Quaternion Upright =
            Quaternion.AngleAxis(90f, new Vector3(1f, 0f, 0f));

        private static void AssertDirection(Vector3 actual, Vector3 expected)
        {
            Assert.That((actual - expected).magnitude, Is.LessThan(Tolerance),
                $"expected {expected} but got {actual}");
        }

        [Test]
        public void Flat_on_back_looks_straight_down()
        {
            var camera = GyroAttitudeMath.DeviceToUnityCamera(FlatOnBack);

            AssertDirection(camera * Vector3.forward, Vector3.down);
        }

        [Test]
        public void Upright_phone_looks_at_the_horizon_with_no_roll()
        {
            var camera = GyroAttitudeMath.DeviceToUnityCamera(Upright);

            AssertDirection(camera * Vector3.forward, Vector3.forward);
            AssertDirection(camera * Vector3.up, Vector3.up);
        }

        [Test]
        public void Turning_the_phone_left_turns_the_view_left()
        {
            // 90° counterclockwise (seen from above) about the Earth up axis.
            var device = Quaternion.AngleAxis(90f, new Vector3(0f, 0f, 1f)) * Upright;

            var camera = GyroAttitudeMath.DeviceToUnityCamera(device);

            AssertDirection(camera * Vector3.forward, Vector3.left);
        }

        [Test]
        public void Turning_the_phone_right_turns_the_view_right()
        {
            var device = Quaternion.AngleAxis(-90f, new Vector3(0f, 0f, 1f)) * Upright;

            var camera = GyroAttitudeMath.DeviceToUnityCamera(device);

            AssertDirection(camera * Vector3.forward, Vector3.right);
        }

        [Test]
        public void Tilting_the_phone_back_past_upright_looks_up_at_the_sky()
        {
            // 45° beyond upright about the east axis — player aiming at the sky.
            var device = Quaternion.AngleAxis(135f, new Vector3(1f, 0f, 0f));

            var camera = GyroAttitudeMath.DeviceToUnityCamera(device);

            AssertDirection(camera * Vector3.forward,
                new Vector3(0f, Mathf.Sin(45f * Mathf.Deg2Rad), Mathf.Cos(45f * Mathf.Deg2Rad)));
        }
    }
}
