using UnityEngine;

namespace SocialUniverse.World
{
    // Uses legacy Input API for the M1 prototype. Upgrade to Input System in M2.
    public class PlanetCameraController : MonoBehaviour
    {
        [SerializeField] private Transform _target;
        [SerializeField] private float     _orbitSpeed  = 120f;
        [SerializeField] private float     _zoomSpeed   = 5f;
        [SerializeField] private float     _minDistance = 5f;
        [SerializeField] private float     _maxDistance = 20f;

        private float _distance = 12f;
        private float _yaw;
        private float _pitch    = 20f;

        private void LateUpdate()
        {
            if (Input.GetMouseButton(1))
            {
                _yaw   += Input.GetAxis("Mouse X") * _orbitSpeed * Time.deltaTime;
                _pitch -= Input.GetAxis("Mouse Y") * _orbitSpeed * Time.deltaTime;
                _pitch  = Mathf.Clamp(_pitch, -80f, 80f);
            }

            _distance -= Input.GetAxis("Mouse ScrollWheel") * _zoomSpeed;
            _distance  = Mathf.Clamp(_distance, _minDistance, _maxDistance);

            var center   = _target != null ? _target.position : Vector3.zero;
            var rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            transform.position = center + rotation * new Vector3(0f, 0f, -_distance);
            transform.LookAt(center);
        }
    }
}
