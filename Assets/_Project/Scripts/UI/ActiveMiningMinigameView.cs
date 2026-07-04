using UnityEngine;
using UnityEngine.UI;
using VContainer;
using SocialUniverse.Mining;

namespace SocialUniverse.UI
{
    // Overlay UI for the active-mining minigame scene: renders the current target point (a real
    // 3D anchor on the spawned asteroid clone, projected to screen space every frame so it moves
    // as the asteroid rotates), the countdown/progress/error counters, and forwards player taps
    // to MiningController. Unlike the old Planet-scene overlay, this view's GameObject doesn't
    // need to hide/show itself — the whole ActiveMining scene only exists while a session is
    // running, so scene load/unload (see ActiveMiningSceneController) is the visibility switch.
    // _targetButton must be a UI child rendered above _missAreaButton in the hierarchy so a tap
    // on the point hits the target first and any other tap in the asteroid area falls through to
    // the miss button (standard Unity UI raycast ordering).
    public class ActiveMiningMinigameView : MonoBehaviour
    {
        [SerializeField] private Camera                    _sceneCamera;
        [SerializeField] private ActiveMiningAsteroidStage  _stage;
        [SerializeField] private RectTransform              _targetPoint;
        [SerializeField] private Button                     _targetButton;
        [SerializeField] private Button                     _missAreaButton;
        [SerializeField] private Text                       _progressText;
        [SerializeField] private Text                       _errorText;
        [SerializeField] private Text                       _timeText;
        [SerializeField] private GameObject                 _resultBanner;
        [SerializeField] private Text                       _resultText;

        [Inject] private MiningController _mining;

        private ActiveMiningTargetPoint _currentTargetAnchor;

        private void Awake()
        {
            if (_resultBanner != null) _resultBanner.SetActive(false);
            if (_targetButton   != null) _targetButton.onClick.AddListener(() => OnTapped(hitTarget: true));
            if (_missAreaButton != null) _missAreaButton.onClick.AddListener(() => OnTapped(hitTarget: false));
        }

        private void Start() => _mining.OnActiveSessionChanged += OnSessionChanged;

        private void OnDestroy()
        {
            _mining.OnActiveSessionChanged -= OnSessionChanged;
            if (_currentTargetAnchor != null) Destroy(_currentTargetAnchor.gameObject);
        }

        private void Update()
        {
            var session = _mining.CurrentActiveSession;
            if (session == null) return;

            Refresh(session);
            ProjectTargetPointToScreen();
        }

        private void OnSessionChanged(ActiveMiningSession session)
        {
            if (session == null) return;

            if (session.Stage != ActiveMiningStage.InProgress)
            {
                ShowResult(session.Stage);
                return;
            }

            if (_resultBanner != null) _resultBanner.SetActive(false);
            Refresh(session);
            SpawnNextTargetPoint();
        }

        private void Refresh(ActiveMiningSession session)
        {
            if (_progressText != null) _progressText.text = $"{session.SuccessfulTaps}/{session.TapsRequired}";
            if (_errorText    != null) _errorText.text    = $"Misses: {session.ErrorCount}/{session.MaxErrors}";
            if (_timeText     != null) _timeText.text     = $"{Mathf.CeilToInt(session.TimeRemainingSeconds)}s";
        }

        private void ShowResult(ActiveMiningStage stage)
        {
            if (_resultBanner != null) _resultBanner.SetActive(true);
            if (_resultText   != null) _resultText.text = stage == ActiveMiningStage.Success ? "Success!" : "Failed";
        }

        private void SpawnNextTargetPoint()
        {
            if (_stage == null || _stage.StageClone == null) return;

            if (_currentTargetAnchor != null) Destroy(_currentTargetAnchor.gameObject);

            var anchorGo = new GameObject("ActiveMiningTargetAnchor");
            _currentTargetAnchor = anchorGo.AddComponent<ActiveMiningTargetPoint>();

            Vector3 towardViewer = _sceneCamera != null
                ? _sceneCamera.transform.position - _stage.StageClone.transform.position
                : Vector3.back;

            _currentTargetAnchor.PlaceOnAsteroid(_stage.StageClone.transform, _stage.ColliderRadius, towardViewer);
        }

        private void ProjectTargetPointToScreen()
        {
            if (_targetPoint == null || _currentTargetAnchor == null || _sceneCamera == null) return;

            _targetPoint.position = _sceneCamera.WorldToScreenPoint(_currentTargetAnchor.transform.position);
        }

        private void OnTapped(bool hitTarget)
        {
            if (_mining.CurrentActiveSession == null) return;

            _mining.RegisterActiveTap(hitTarget);

            if (_mining.CurrentActiveSession != null && _mining.CurrentActiveSession.Stage == ActiveMiningStage.InProgress)
                SpawnNextTargetPoint();
        }
    }
}
