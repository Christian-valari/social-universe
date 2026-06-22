using UnityEngine;

namespace SocialUniverse.Travel
{
    // Owns the SolarSystem (Hub) scene ambience — a slow idle rotation on the
    // orbit pivot. SkyDiscoveryController is the sole interactive view (the
    // StarMap list was removed — Sky Discovery is always active, no toggle).
    public class SolarSystemController : MonoBehaviour
    {
        [SerializeField] private Transform _orbitPivot;
        [SerializeField] private float     _idleRotationDegPerSec = 2f;

        private void Update()
        {
            if (_orbitPivot != null)
                _orbitPivot.Rotate(Vector3.up, _idleRotationDegPerSec * Time.deltaTime, Space.World);
        }
    }
}
