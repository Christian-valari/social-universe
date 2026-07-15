using UnityEngine;

namespace SocialUniverse.Travel
{
    // Pure math for converting the Input System attitude sensor's quaternion
    // into a Unity camera rotation — kept separate from GyroInputProvider
    // (MonoBehaviour) so it's unit-testable without a device, mirroring
    // SkyLockOnMath.
    //
    // The sensor reports device orientation in a right-handed, Z-up Earth
    // frame; Unity is left-handed and Y-up. Negating z/w flips handedness and
    // the 90° X pre-rotation remaps Z-up to Y-up, so holding the phone upright
    // looks at the horizon and tilting it back looks up at the sky. Applying
    // the raw quaternion instead mirrors every rotation (turning the phone
    // left panned the sky right).
    public static class GyroAttitudeMath
    {
        public static Quaternion DeviceToUnityCamera(Quaternion deviceAttitude)
        {
            var leftHanded = new Quaternion(deviceAttitude.x, deviceAttitude.y, -deviceAttitude.z, -deviceAttitude.w);
            return Quaternion.Euler(90f, 0f, 0f) * leftHanded;
        }
    }
}
