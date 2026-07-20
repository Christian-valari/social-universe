using System;
using Lean.Touch;
using UnityEngine;

namespace SocialUniverse.World
{
    public class PlanetCameraController : MonoBehaviour
    {
        [SerializeField] private Transform _target;
        [SerializeField] private float     _orbitSpeed  = 0.2f;
        [SerializeField] private float     _minDistance = 5f;
        [SerializeField] private float     _maxDistance = 20f;
        [SerializeField] private Vector3   _offset      = Vector3.zero;

        private float _distance = 12f;
        private float _yaw;
        private float _pitch    = 20f;

        private void Awake()
        {
            _distance  = Mathf.Clamp(5f, _minDistance, _maxDistance);
        }

        private void LateUpdate()
        {
            var fingers = LeanTouch.Fingers;

            if (fingers.Count == 1)
            {
                var delta = LeanGesture.GetScaledDelta(fingers);
                _yaw   += delta.x * _orbitSpeed;
                _pitch -= delta.y * _orbitSpeed;
                _pitch  = Mathf.Clamp(_pitch, -80f, 80f);
            }
            else if (fingers.Count >= 2)
            {
                _distance /= LeanGesture.GetPinchRatio(fingers);
                _distance  = Mathf.Clamp(_distance, _minDistance, _maxDistance);
            }

            var center   = (_target != null ? _target.position : Vector3.zero) + _offset;
            var rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            transform.position = center + rotation * new Vector3(0f, 0f, -_distance);
            transform.LookAt(center);
        }
    }
}
