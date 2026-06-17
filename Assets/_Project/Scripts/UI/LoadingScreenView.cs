using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using SocialUniverse.Core;

namespace SocialUniverse.UI
{
    // Lives in the LoadingScreen scene, loaded additively before the Planet scene.
    // Animates a slider to each progress milestone and shows the live percentage.
    // Enforces a minimum visible duration before unloading itself.
    public class LoadingScreenView : MonoBehaviour
    {
        [SerializeField] private Slider   _slider;
        [SerializeField] private TMP_Text _percentageText;
        [SerializeField] private float    _minDisplaySeconds = 2f;
        // Units per second the slider fills toward its target (0-1 range).
        [SerializeField] private float    _fillSpeed = 0.5f;

        private float _showTime;
        private float _currentProgress;
        private float _targetProgress;

        private void Awake()
        {
            _showTime        = Time.realtimeSinceStartup;
            _currentProgress = 0f;
            _targetProgress  = 0f;

            if (_slider != null)
            {
                _slider.value    = 0f;
            }

            UpdateDisplay(0f);

            EventBus.Subscribe<LoadingStatusEvent>(OnProgressUpdate);
            EventBus.Subscribe<PlanetSceneReadyEvent>(OnSceneReady);
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe<LoadingStatusEvent>(OnProgressUpdate);
            EventBus.Unsubscribe<PlanetSceneReadyEvent>(OnSceneReady);
        }

        private void Update()
        {
            if (Mathf.Approximately(_currentProgress, _targetProgress)) return;

            _currentProgress = Mathf.MoveTowards(
                _currentProgress, _targetProgress, _fillSpeed * Time.unscaledDeltaTime);

            UpdateDisplay(_currentProgress);
        }

        private void OnProgressUpdate(LoadingStatusEvent e) =>
            _targetProgress = Mathf.Clamp01(e.Progress);

        private void OnSceneReady(PlanetSceneReadyEvent _)
        {
            _targetProgress = 1f;
            StartCoroutine(UnloadAfterCompletion());
        }

        private IEnumerator UnloadAfterCompletion()
        {
            // Enforce minimum display time.
            float elapsed   = Time.realtimeSinceStartup - _showTime;
            float remaining = _minDisplaySeconds - elapsed;
            if (remaining > 0f)
                yield return new WaitForSecondsRealtime(remaining);

            // Wait for the fill animation to visually reach 100%.
            yield return new WaitUntil(() => _currentProgress >= 0.999f);

            SceneManager.UnloadSceneAsync(gameObject.scene);
        }

        private void UpdateDisplay(float progress)
        {
            if (_slider != null)
                _slider.value = Mathf.RoundToInt(progress * 100);

            if (_percentageText != null)
                _percentageText.text = $"{Mathf.RoundToInt(progress * 100)}%";
        }
    }
}
