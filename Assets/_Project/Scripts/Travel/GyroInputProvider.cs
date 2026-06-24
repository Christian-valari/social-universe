using UnityEngine;
using UnityEngine.InputSystem;

namespace SocialUniverse.Travel
{
    // Wraps the Input System attitude sensor for Sky Discovery. Falls back to
    // mouse/touch drag when no gyroscope is present (desktop, simulator, or a
    // device that doesn't expose one) so the feature still works everywhere.
    // This is the concrete resolution of the M5 "Sky Discovery: AR vs gyro"
    // open decision — gyroscope starfield was chosen over camera AR (simpler,
    // no AR Foundation dependency); see Social_Universe_Architecture.md §1 and
    // PROGRESS.md Open Decisions.
    public class GyroInputProvider : MonoBehaviour
    {
        [SerializeField] private float _dragSensitivity = 0.2f;

        public bool       IsGyroAvailable { get; private set; }
        public Quaternion CurrentAttitude { get; private set; } = Quaternion.identity;

        // Set true while a Cinemachine zoom owns the camera so accumulated drag
        // during the zoom doesn't cause a snap when manual control resumes.
        public bool SuspendDragInput { get; set; }

        private Vector2 _dragEuler; // fallback accumulator (yaw/pitch in degrees)
        private Vector2 _lastPointerPos;
        private bool    _dragging;

        private void OnEnable()
        {
            var sensor = AttitudeSensor.current;
            IsGyroAvailable = sensor != null;
            if (IsGyroAvailable) InputSystem.EnableDevice(sensor);
        }

        private void Update()
        {
            if (IsGyroAvailable)
            {
                var sensor = AttitudeSensor.current;
                if (sensor != null)
                {
                    CurrentAttitude = sensor.attitude.ReadValue();
                    return;
                }
                IsGyroAvailable = false; // sensor disappeared mid-session — degrade gracefully
            }

            if (!SuspendDragInput) UpdateDragFallback();
            CurrentAttitude = Quaternion.Euler(_dragEuler.y, _dragEuler.x, 0f);
        }

        private void UpdateDragFallback()
        {
            var mouse = Mouse.current;
            if (mouse == null) return;

            if (mouse.leftButton.wasPressedThisFrame)
            {
                _dragging       = true;
                _lastPointerPos = mouse.position.ReadValue();
            }
            else if (mouse.leftButton.wasReleasedThisFrame)
            {
                _dragging = false;
            }

            if (!_dragging) return;

            Vector2 pos   = mouse.position.ReadValue();
            Vector2 delta = pos - _lastPointerPos;
            _lastPointerPos = pos;

            _dragEuler.x += delta.x * _dragSensitivity;
            _dragEuler.y = Mathf.Clamp(_dragEuler.y - delta.y * _dragSensitivity, -80f, 80f);
        }
    }
}
