using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using SocialUniverse.Mining;
using SocialUniverse.Core;
using SocialUniverse.Safety;
using SocialUniverse.Config;
using TMPro;

namespace SocialUniverse.UI
{
    // Owns the ActiveMining scene's three-phase flow: a pre-game panel (asteroid info, the rules
    // and a Start button), the in-progress HUD (closing-ring targets, progress, miss counter and
    // what's banked so far, with a grade label and a shake of the rock to answer each tap), and a
    // post-game panel (result + reward preview + Continue).
    // Nothing spawns or ticks until the player presses Start — this also means there's no race
    // between scene-load and the first target (a bug the previous overlay design had).
    // _targetTemplate must be a UI child rendered above _missAreaButton in the hierarchy so a tap
    // on a target hits it first and any other tap falls through to the miss button (standard Unity
    // UI raycast ordering). The template itself stays hidden; its clones sit right after it, which
    // keeps them below the HUD.
    public class ActiveMiningMinigameView : MonoBehaviour
    {
        private const int   MaxHitVfx            = 4;
        private const int   MaxPlacementAttempts = 32;
        private const float GradeFadeSeconds     = 0.5f;
        private const float MissShakeSeconds     = 0.2f;
        private const float FailShakeSeconds     = 0.5f;

        private static readonly Color HitColor  = Color.white;
        private static readonly Color MissColor = new Color(1f, 0.3f, 0.3f);

        [SerializeField] private Camera                    _sceneCamera;
        [SerializeField] private ActiveMiningAsteroidStage  _stage;

        [Header("Pre-game")]
        [SerializeField] private GameObject _preGamePanel;
        [SerializeField] private Text       _mineralTypeText;
        [SerializeField] private Text       _rulesText;
        [SerializeField] private Button     _startButton;

        [Header("In-progress")]
        [SerializeField] private ActiveMiningTargetView _targetTemplate;
        [SerializeField] private Button                 _missAreaButton;
        [SerializeField] private Text                   _progressText;
        [SerializeField] private Text                   _errorText;
        [SerializeField] private Text                   _bankedText;
        [SerializeField] private Text                   _gradeText;

        [Header("Post-game")]
        [SerializeField] private GameObject _resultBanner;
        [SerializeField] private Text       _resultText;
        [SerializeField] private TMP_Text       _rewardText;
        [SerializeField] private Button     _continueButton;

        [Header("VFX")]
        [SerializeField] private GameObject _hitVfxPrefab;

        [Inject] private ActiveMiningSessionRunner _runner;
        [Inject] private ActiveMiningHandoff       _handoff;
        [Inject] private ActiveMiningState         _activeMiningState;
        [Inject] private IAudioManager             _audio;

        // A pooled target view and the anchor that holds its place on the asteroid.
        private sealed class TargetSlot
        {
            public ActiveMiningTargetView  View;
            public ActiveMiningTargetPoint Anchor;
        }

        private readonly List<TargetSlot> _slots  = new List<TargetSlot>();
        private readonly List<GameObject> _hitVfx = new List<GameObject>();

        private int                 _oldestHitVfx;
        private float               _gradeSecondsLeft;
        private ActiveMiningSession _session;
        private Canvas              _canvas;

        private void Awake()
        {
            _canvas = GetComponentInParent<Canvas>();

            if (_missAreaButton != null) _missAreaButton.onClick.AddListener(OnMissAreaTapped);
            if (_startButton    != null) _startButton.onClick.AddListener(OnStartClicked);
            if (_continueButton != null) _continueButton.onClick.AddListener(OnContinueClicked);
        }

        private void Start()
        {
            _targetTemplate.Hide();
            SetInProgressUiActive(false);
            if (_resultBanner != null) _resultBanner.SetActive(false);
            if (_gradeText     != null) _gradeText.gameObject.SetActive(false);

            if (_preGamePanel    != null) _preGamePanel.SetActive(true);
            if (_mineralTypeText != null) _mineralTypeText.text = _handoff.Definition != null ? _handoff.Definition.MineralType : "";
            if (_rulesText       != null) _rulesText.text       = ActiveMiningRulesText.For(_handoff.Settings);
        }

        private void OnDestroy()
        {
            if (_session != null)
            {
                _session.OnStageChanged   -= OnStageChanged;
                _session.OnTargetSpawned  -= OnTargetSpawned;
                _session.OnTargetResolved -= OnTargetResolved;
                _session.OnWrongTap       -= OnWrongTap;
            }

            for (int i = 0; i < _slots.Count; i++)
                if (_slots[i].Anchor != null) Destroy(_slots[i].Anchor.gameObject);

            for (int i = 0; i < _hitVfx.Count; i++)
                if (_hitVfx[i] != null) Destroy(_hitVfx[i]);
        }

        private void Update()
        {
            FadeGradeText();

            if (_session == null || _session.Stage != ActiveMiningStage.InProgress) return;

            for (int i = 0; i < _slots.Count; i++)
            {
                var slot = _slots[i];
                if (slot.View.Target == null) continue;

                ProjectToScreen(slot);
                slot.View.SetRingProgress(slot.View.Target.Progress);
            }
        }

        private void OnStartClicked()
        {
            if (_session != null) return;

            if (_preGamePanel != null) _preGamePanel.SetActive(false);
            SetInProgressUiActive(true);

            // The session's first Tick spawns its first target, so subscribing here misses nothing.
            _session = _runner.BeginRun();
            _session.OnStageChanged   += OnStageChanged;
            _session.OnTargetSpawned  += OnTargetSpawned;
            _session.OnTargetResolved += OnTargetResolved;
            _session.OnWrongTap       += OnWrongTap;
            Refresh();
        }

        private void OnStageChanged(ActiveMiningStage stage)
        {
            if (stage == ActiveMiningStage.InProgress) return;

            SetInProgressUiActive(false);
            ShowResult(stage);
        }

        private void OnTargetSpawned(ActiveMiningTarget target)
        {
            var slot = TakeFreeSlot();

            PlaceAnchor(slot.Anchor, target);
            slot.View.Show(target, OnTargetTapped);
            ProjectToScreen(slot);
        }

        private void OnTargetResolved(ActiveMiningTarget target, ActiveMiningGrade grade)
        {
            for (int i = 0; i < _slots.Count; i++)
            {
                var slot = _slots[i];
                if (slot.View.Target != target) continue;

                if (grade == ActiveMiningGrade.Miss)
                {
                    PlayMissFeedback(slot.View.transform.position);
                }
                else
                {
                    _audio.PlaySfx(SfxId.ActiveMiningTap);
                    PlayHitVfx(slot.Anchor.transform.position);
                    ShowGrade(grade.ToString(), HitColor, slot.View.transform.position);
                }
                slot.View.Hide();
                break;
            }

            Refresh();
        }

        // A wrong tap lands on empty space and leaves its ring live, so the label goes beside that
        // ring rather than over the thing the player has to tap next.
        private void OnWrongTap()
        {
            Refresh();

            for (int i = 0; i < _slots.Count; i++)
            {
                if (_slots[i].View.Target == null) continue;

                PlayMissFeedback(ClearOfRing(_slots[i].View.transform.position));
                break;
            }
        }

        // One target's height toward the middle of the screen, which keeps it on screen.
        private Vector3 ClearOfRing(Vector3 ringPosition)
        {
            Vector2 toMiddle  = _canvas.pixelRect.center - (Vector2)ringPosition;
            float   clearance = ((RectTransform)_targetTemplate.transform).rect.height * _canvas.scaleFactor;
            Vector2 step      = (toMiddle.sqrMagnitude > 0f ? toMiddle.normalized : Vector2.up) * clearance;
            return ringPosition + (Vector3)step;
        }

        private void OnTargetTapped(ActiveMiningTarget target) => _session.TapTarget(target.Id);

        private void OnMissAreaTapped() => _session?.TapOffTarget();

        private void PlayMissFeedback(Vector3 screenPosition)
        {
            _audio.PlaySfx(SfxId.ActiveMiningMiss);
            ShowGrade("Miss", MissColor, screenPosition);
            if (_stage != null) _stage.Shake(MissShakeSeconds);
        }

        private void ShowGrade(string label, Color color, Vector3 screenPosition)
        {
            if (_gradeText == null) return;

            _gradeText.text               = label;
            _gradeText.color              = color;
            _gradeText.transform.position = screenPosition;
            _gradeText.gameObject.SetActive(true);
            _gradeSecondsLeft = GradeFadeSeconds;
        }

        private void FadeGradeText()
        {
            if (_gradeSecondsLeft <= 0f) return;

            _gradeSecondsLeft -= Time.deltaTime;
            if (_gradeSecondsLeft <= 0f)
            {
                _gradeText.gameObject.SetActive(false);
                return;
            }

            Color color = _gradeText.color;
            color.a = _gradeSecondsLeft / GradeFadeSeconds;
            _gradeText.color = color;
        }

        private void Refresh()
        {
            if (_progressText != null) _progressText.text = $"{_session.ResolvedTargets}/{_session.Targets}";
            if (_errorText    != null) _errorText.text    = $"Misses: {_session.ErrorCount}/{_session.MaxErrors}";
            if (_bankedText   != null) _bankedText.text   = $"+{ActiveMiningPayout.Quantity(_handoff.FullQuantity, _session.BankedFraction)} {MineralName()}";
        }

        private void ShowResult(ActiveMiningStage stage)
        {
            bool succeeded = stage == ActiveMiningStage.Success;

            if (succeeded)
            {
                _audio.PlaySfx(SfxId.MiningComplete);
                _audio.PlaySfx(SfxId.AsteroidDestroyed);
            }
            else
            {
                _audio.PlaySfx(SfxId.ActiveMiningFail);
                if (_stage != null) _stage.Shake(FailShakeSeconds);
            }

            if (_resultBanner != null) _resultBanner.SetActive(true);
            if (_resultText   != null)
            {
                _resultText.text = !succeeded ? "Run over"
                    : _session.PerfectCount == _session.Targets ? "Perfect run!"
                    : "Cleared!";
            }

            if (_rewardText != null)
            {
                int mined = _handoff.BankedQuantity;
                _rewardText.text = mined > 0 ? $"+{mined} {MineralName()}" : "No reward";
            }
        }

        private string MineralName() =>
            _handoff.Definition != null && _handoff.Definition.Mineral != null
                ? _handoff.Definition.Mineral.DisplayName
                : "minerals";

        private void OnContinueClicked() => _activeMiningState.Finish();

        // Hiding also clears every live marker: a wrong tap that ends the run leaves its ring live
        // in the session, with no resolve event to hide it.
        private void SetInProgressUiActive(bool active)
        {
            if (_missAreaButton != null) _missAreaButton.gameObject.SetActive(active);
            if (_progressText   != null) _progressText.gameObject.SetActive(active);
            if (_errorText      != null) _errorText.gameObject.SetActive(active);
            if (_bankedText     != null) _bankedText.gameObject.SetActive(active);

            if (active) return;

            for (int i = 0; i < _slots.Count; i++)
                _slots[i].View.Hide();
        }

        private TargetSlot TakeFreeSlot()
        {
            for (int i = 0; i < _slots.Count; i++)
                if (_slots[i].View.Target == null) return _slots[i];

            var view = Instantiate(_targetTemplate, _targetTemplate.transform.parent);
            view.transform.SetSiblingIndex(_targetTemplate.transform.GetSiblingIndex() + 1);

            var slot = new TargetSlot
            {
                View   = view,
                Anchor = new GameObject("ActiveMiningTargetAnchor").AddComponent<ActiveMiningTargetPoint>(),
            };
            _slots.Add(slot);
            return slot;
        }

        // A ring the player can't reach costs a miss they could do nothing about, and the bigger
        // asteroids overflow the screen, so a target's whole tap area has to be on screen both now
        // and when its ring closes.
        private void PlaceAnchor(ActiveMiningTargetPoint anchor, ActiveMiningTarget target)
        {
            if (_stage == null || _stage.StageClone == null || _sceneCamera == null) return;

            var     asteroid     = _stage.StageClone.transform;
            Vector3 towardViewer = _sceneCamera.transform.position - asteroid.position;
            float   margin       = ((RectTransform)_targetTemplate.transform).rect.width / 2f * _canvas.scaleFactor;

            for (int attempt = 0; attempt < MaxPlacementAttempts; attempt++)
            {
                anchor.PlaceOnAsteroid(asteroid, _stage.ColliderRadius, towardViewer);

                Vector3 point = anchor.transform.position;
                if (IsOnScreen(point, margin) && IsOnScreen(_stage.PredictPoint(point, target.RingSeconds), margin))
                    return;
            }

            anchor.transform.position = asteroid.position + towardViewer.normalized * _stage.ColliderRadius;
        }

        private bool IsOnScreen(Vector3 worldPoint, float margin)
        {
            Vector3 screenPoint = _sceneCamera.WorldToScreenPoint(worldPoint);
            return screenPoint.z > 0f && ActiveMiningTargetPoint.IsOnScreen(screenPoint,
                new Vector2(_sceneCamera.pixelWidth, _sceneCamera.pixelHeight), margin);
        }

        private void ProjectToScreen(TargetSlot slot)
        {
            if (_sceneCamera != null)
                slot.View.SetScreenPosition(_sceneCamera.WorldToScreenPoint(slot.Anchor.transform.position));
        }

        // The prefab has playOnAwake, so re-enabling a pooled instance replays it.
        private void PlayHitVfx(Vector3 position)
        {
            if (_hitVfxPrefab == null) return;

            if (_hitVfx.Count < MaxHitVfx)
            {
                _hitVfx.Add(Instantiate(_hitVfxPrefab, position, Quaternion.identity));
                return;
            }

            var vfx = _hitVfx[_oldestHitVfx];
            _oldestHitVfx = (_oldestHitVfx + 1) % MaxHitVfx;

            vfx.SetActive(false);
            vfx.transform.position = position;
            vfx.SetActive(true);
        }
    }
}
