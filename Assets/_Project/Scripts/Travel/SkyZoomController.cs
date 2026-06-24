using System.Collections;
using UnityEngine;
using Unity.Cinemachine;
using VContainer;
using SocialUniverse.Core;

namespace SocialUniverse.Travel
{
    // Drives the Cinemachine zoom-in/out for the Sky Discovery "Launch" button.
    // Two CinemachineCameras share one CinemachineBrain on the Hub's Main Camera:
    // _domeVcam mirrors the manual gyro/drag-driven dome view (SkyDiscoveryController
    // writes rotation onto its transform) and _closeUpVcam frames the locked planet's
    // spawned model, raised to a higher priority on zoom-in so the brain blends to it.
    // While the close-up is live, SkyDiscoveryController's per-frame rotation/dwell
    // loop and GyroInputProvider's drag accumulation are both suspended so they don't
    // fight the blend or leave a stale drag delta for when control returns.
    public class SkyZoomController : MonoBehaviour
    {
        [SerializeField] private CinemachineBrain  _brain;
        [SerializeField] private CinemachineCamera _domeVcam;
        [SerializeField] private CinemachineCamera _closeUpVcam;
        [SerializeField] private int _restPriority     = 10;
        [SerializeField] private int _zoomedInPriority  = 20;

        [Inject] private SkyDiscoveryController _skyDiscovery;
        [Inject] private GyroInputProvider      _gyro;

        private Coroutine _activeRoutine;

        private void Awake()
        {
            if (_domeVcam != null)    _domeVcam.Priority.Value    = _restPriority;
            if (_closeUpVcam != null) _closeUpVcam.Priority.Value = _restPriority;
        }

        private void OnEnable()
        {
            EventBus.Subscribe<TravelPreviewRequestedEvent>(OnPreviewRequested);
            EventBus.Subscribe<TravelPreviewClosedEvent>(OnPreviewClosed);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<TravelPreviewRequestedEvent>(OnPreviewRequested);
            EventBus.Unsubscribe<TravelPreviewClosedEvent>(OnPreviewClosed);
        }

        private void OnPreviewRequested(TravelPreviewRequestedEvent e)
        {
            var target = _skyDiscovery.LockedPlanetTransform;
            if (target == null || _closeUpVcam == null) return;

            _closeUpVcam.Target.TrackingTarget    = target;
            _closeUpVcam.Target.LookAtTarget      = target;
            _closeUpVcam.Target.CustomLookAtTarget = true;

            _skyDiscovery.IsCameraSuspended = true;
            _gyro.SuspendDragInput          = true;

            _closeUpVcam.Priority.Value = _zoomedInPriority;

            RestartBlendWait();
        }

        private void OnPreviewClosed(TravelPreviewClosedEvent e)
        {
            if (_closeUpVcam == null) return;

            _closeUpVcam.Priority.Value = _restPriority - 5;

            RestartBlendWait();
        }

        private void RestartBlendWait()
        {
            if (_activeRoutine != null) StopCoroutine(_activeRoutine);
            _activeRoutine = StartCoroutine(WaitForBlendThenRelease());
        }

        private IEnumerator WaitForBlendThenRelease()
        {
            // Wait a frame so the brain has registered the priority change and started blending.
            yield return null;
            while (_brain != null && _brain.IsBlending)
                yield return null;

            // Only release manual control once the dome vcam is fully live again
            // (i.e. a zoom-OUT blend just finished); zoom-IN leaves control suspended.
            if (_closeUpVcam != null && _closeUpVcam.Priority.Value == _restPriority - 5)
            {
                _skyDiscovery.IsCameraSuspended = false;
                _gyro.SuspendDragInput          = false;
            }
            _activeRoutine = null;
        }

        private void OnDestroy()
        {
            if (_activeRoutine != null) StopCoroutine(_activeRoutine);
        }
    }
}
