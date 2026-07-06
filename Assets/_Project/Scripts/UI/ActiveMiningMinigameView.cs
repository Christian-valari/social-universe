using UnityEngine;
using UnityEngine.UI;
using VContainer;
using SocialUniverse.Mining;
using SocialUniverse.Core;
using TMPro;

namespace SocialUniverse.UI
{
    // Owns the ActiveMining scene's three-phase flow: a pre-game panel (asteroid info + Start
    // button), the in-progress HUD (target point/timer/progress/miss counter, unchanged from the
    // previous scene-based redesign), and a post-game panel (result + reward preview + Continue).
    // Nothing spawns or ticks until the player presses Start — this also means there's no race
    // between scene-load and the first target point (a bug the previous overlay design had).
    // _targetButton must be a UI child rendered above _missAreaButton in the hierarchy so a tap
    // on the point hits the target first and any other tap in the asteroid area falls through to
    // the miss button (standard Unity UI raycast ordering).
    public class ActiveMiningMinigameView : MonoBehaviour
    {
        [SerializeField] private Camera                    _sceneCamera;
        [SerializeField] private ActiveMiningAsteroidStage  _stage;

        [Header("Pre-game")]
        [SerializeField] private GameObject _preGamePanel;
        [SerializeField] private Text       _mineralTypeText;
        [SerializeField] private Button     _startButton;

        [Header("In-progress")]
        [SerializeField] private RectTransform _targetPoint;
        [SerializeField] private Button        _targetButton;
        [SerializeField] private Button        _missAreaButton;
        [SerializeField] private Text          _progressText;
        [SerializeField] private Text          _errorText;
        [SerializeField] private Text          _timeText;

        [Header("Post-game")]
        [SerializeField] private GameObject _resultBanner;
        [SerializeField] private Text       _resultText;
        [SerializeField] private TMP_Text       _rewardText;
        [SerializeField] private Button     _continueButton;

        [Inject] private ActiveMiningSessionRunner _runner;
        [Inject] private ActiveMiningHandoff       _handoff;
        [Inject] private ActiveMiningState         _activeMiningState;

        private ActiveMiningTargetPoint _currentTargetAnchor;
        private bool                    _started;

        private void Awake()
        {
            if (_targetButton   != null) _targetButton.onClick.AddListener(() => OnTapped(hitTarget: true));
            if (_missAreaButton != null) _missAreaButton.onClick.AddListener(() => OnTapped(hitTarget: false));
            if (_startButton    != null) _startButton.onClick.AddListener(OnStartClicked);
            if (_continueButton != null) _continueButton.onClick.AddListener(OnContinueClicked);
        }

        private void Start()
        {
            SetInProgressUiActive(false);
            if (_resultBanner != null) _resultBanner.SetActive(false);

            if (_preGamePanel    != null) _preGamePanel.SetActive(true);
            if (_mineralTypeText != null) _mineralTypeText.text = _handoff.Definition != null ? _handoff.Definition.MineralType : "";
        }

        private void OnDestroy()
        {
            if (_started && _runner.Session != null) _runner.Session.OnStageChanged -= OnStageChanged;
            if (_currentTargetAnchor != null) Destroy(_currentTargetAnchor.gameObject);
        }

        private void Update()
        {
            if (!_started) return;

            var session = _runner.Session;
            if (session == null || session.Stage != ActiveMiningStage.InProgress) return;

            Refresh(session);
            ProjectTargetPointToScreen();
        }

        private void OnStartClicked()
        {
            if (_started || _runner.Session == null) return;
            _started = true;

            if (_preGamePanel != null) _preGamePanel.SetActive(false);
            SetInProgressUiActive(true);

            _runner.Session.OnStageChanged += OnStageChanged;
            _runner.BeginTicking();
            Refresh(_runner.Session);
            SpawnNextTargetPoint();
        }

        private void OnStageChanged(ActiveMiningStage stage)
        {
            if (stage == ActiveMiningStage.InProgress) return;

            SetInProgressUiActive(false);
            ShowResult(stage);
        }

        private void Refresh(ActiveMiningSession session)
        {
            if (_progressText != null) _progressText.text = $"{session.SuccessfulTaps}/{session.TapsRequired}";
            if (_errorText    != null) _errorText.text    = $"Misses: {session.ErrorCount}/{session.MaxErrors}";
            if (_timeText     != null) _timeText.text     = $"{Mathf.CeilToInt(session.TimeRemainingSeconds)}s";
        }

        private void ShowResult(ActiveMiningStage stage)
        {
            bool succeeded = stage == ActiveMiningStage.Success;

            if (_resultBanner != null) _resultBanner.SetActive(true);
            if (_resultText   != null) _resultText.text = succeeded ? "Success!" : "Failed";

            if (_rewardText != null)
            {
                if (succeeded && _handoff.Definition != null)
                {
                    int mined = _handoff.RemainingYieldAtStart;
                    int coins = mined * _handoff.Definition.CoinsPerUnit;
                    _rewardText.text = $"+{coins} coins";
                    //_rewardText.text = $"+{mined} {_handoff.Definition.MineralType} -> {coins} coins";
                }
                else
                {
                    _rewardText.text = "No reward";
                }
            }
        }

        private void OnContinueClicked() => _activeMiningState.Finish();

        private void SetInProgressUiActive(bool active)
        {
            if (_targetPoint    != null) _targetPoint.gameObject.SetActive(active);
            if (_missAreaButton != null) _missAreaButton.gameObject.SetActive(active);
            if (_progressText   != null) _progressText.gameObject.SetActive(active);
            if (_errorText      != null) _errorText.gameObject.SetActive(active);
            if (_timeText       != null) _timeText.gameObject.SetActive(active);
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
            if (!_started || _runner.Session.Stage != ActiveMiningStage.InProgress) return;

            if (hitTarget) _runner.Session.RegisterHit();
            else           _runner.Session.RegisterMiss();

            if (_runner.Session.Stage == ActiveMiningStage.InProgress)
                SpawnNextTargetPoint();
        }
    }
}
