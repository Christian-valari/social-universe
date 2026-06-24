using System.Collections;
using UnityEngine;
using Unity.Cinemachine;
using SocialUniverse.Core;

namespace SocialUniverse.Travel
{
    // Fades the Travel scene's ambient camera-drift noise in on load and out
    // just before landing, so the rocket-in-transit feel doesn't pop to full
    // amplitude instantly or cut out abruptly. The noise itself (slow drift +
    // faint engine rumble) is authored on the NoiseSettings asset; this script
    // only ramps CinemachineBasicMultiChannelPerlin.AmplitudeGain between 0 and 1.
    public class RocketDriftCameraController : MonoBehaviour
    {
        [SerializeField] private CinemachineBasicMultiChannelPerlin _noise;
        [SerializeField] private float _fadeInSeconds = 1f;
        [SerializeField] private float _fadeOutSeconds = 0.5f;

        private Coroutine _activeRoutine;

        private void Start()
        {
            if (_noise == null) return;
            _noise.AmplitudeGain = 0f;
            RestartFade(FadeAmplitude(0f, 1f, _fadeInSeconds));
        }

        private void OnEnable()
        {
            EventBus.Subscribe<TravelLandedEvent>(OnLanded);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<TravelLandedEvent>(OnLanded);
        }

        private void OnLanded(TravelLandedEvent e)
        {
            if (_noise == null) return;
            RestartFade(FadeAmplitude(_noise.AmplitudeGain, 0f, _fadeOutSeconds));
        }

        private void RestartFade(IEnumerator routine)
        {
            if (_activeRoutine != null) StopCoroutine(_activeRoutine);
            _activeRoutine = StartCoroutine(routine);
        }

        private IEnumerator FadeAmplitude(float from, float to, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                _noise.AmplitudeGain = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
                yield return null;
            }
            _noise.AmplitudeGain = to;
            _activeRoutine = null;
        }

        private void OnDestroy()
        {
            if (_activeRoutine != null) StopCoroutine(_activeRoutine);
        }
    }
}
