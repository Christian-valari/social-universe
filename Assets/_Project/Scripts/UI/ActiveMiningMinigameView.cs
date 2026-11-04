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
    // keeps them below the HUD. A vein goes to _veinView rather than to a target clone, and has the
    // screen to itself until it resolves.
    public class ActiveMiningMinigameView : MonoBehaviour
    {
        private const int   MaxHitVfx            = 4;
        private const int   MaxPlacementAttempts = 32;
        private const float MinMarkerSeparation  = 160f; // canvas pixels between live markers' centres along an axis, so their tap areas can't overlap
        private const float GradeFadeSeconds     = 0.5f;
        private const float MissShakeSeconds     = 0.2f;
        private const float FailShakeSeconds     = 0.5f;

        // The rocks are irregular and fill less than their bounds suggest, so a marker goes this share
        // of the way out to the stage's SurfaceRadius. On the shipped asteroids at 1080x1920 and 1080x2340
        // the full radius left 73-100% of marker centres over rock and this leaves every one over it.
        public const float PlacementInset = 0.75f;

        private static readonly Color PerfectColor = new Color(1f, 0.84f, 0.3f);
        private static readonly Color GoodColor    = new Color(0.42f, 0.83f, 0.46f);
        private static readonly Color LateColor    = Color.white;
        private static readonly Color MissColor    = new Color(1f, 0.3f, 0.3f);

        [SerializeField] private Camera                    _sceneCamera;
        [SerializeField] private ActiveMiningAsteroidStage  _stage;

        [Header("Pre-game")]
        [SerializeField] private GameObject _preGamePanel;
        [SerializeField] private Text       _mineralTypeText;
        [SerializeField] private Text       _rulesText;
        [SerializeField] private Button     _startButton;

        [Header("In-progress")]
        [SerializeField] private ActiveMiningTargetView _targetTemplate;
        [SerializeField] private ActiveMiningVeinView   _veinView;
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

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // The rock the dev tier picker shows for each tier, in tier order. Dev mode only reaches the
        // first tier's rocks, which leave little room for what the later tiers add.
        [Header("Dev")]
        [SerializeField] private AsteroidDefinition[] _devAsteroidsByTier;
#endif

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
        private ActiveMiningRunSettings  _settings;
        private ActiveMiningTierSettings _tier = new ActiveMiningTierSettings();
        private Canvas              _canvas;
        private ActiveMiningTargetPoint _veinStart; // made by the first vein, then reused
        private ActiveMiningTargetPoint _veinEnd;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private ActiveMiningDevTierPicker _tierPicker;
#endif

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

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            ShowTierPicker();
#endif
        }

        private void OnDestroy()
        {
            if (_session != null)
            {
                _session.OnStageChanged   -= OnStageChanged;
                _session.OnTargetSpawned  -= OnTargetSpawned;
                _session.OnTargetResolved -= OnTargetResolved;
                _session.OnWrongTap       -= OnWrongTap;
                _session.OnDecoyExpired   -= OnDecoyExpired;
            }

            for (int i = 0; i < _slots.Count; i++)
                if (_slots[i].Anchor != null) Destroy(_slots[i].Anchor.gameObject);

            if (_veinStart != null) Destroy(_veinStart.gameObject);
            if (_veinEnd   != null) Destroy(_veinEnd.gameObject);

            for (int i = 0; i < _hitVfx.Count; i++)
                if (_hitVfx[i] != null) Destroy(_hitVfx[i]);
        }

        // A phone can kill a paused app without another callback, so the run is saved on the way out.
        private void OnApplicationPause(bool paused)
        {
            if (paused) _runner.SaveProgress();
        }

        private void OnApplicationQuit() => _runner.SaveProgress();

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

            if (_veinView != null && _veinView.Target != null) ProjectVein();
        }

        private void OnStartClicked()
        {
            if (_session != null) return;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // A tap on the picker also lands on this full-screen button, and isn't a request to start.
            if (_tierPicker != null && _tierPicker.IsPressed) return;
            HideTierPicker();
#endif

            if (_preGamePanel != null) _preGamePanel.SetActive(false);
            SetInProgressUiActive(true);

            // Read now, since the dev tier picker can change the settings before Start. The spin is
            // set before the first target is placed, which predicts where the target will be.
            _settings = _handoff.Settings;
            _tier     = _settings?.Tier ?? new ActiveMiningTierSettings();
            if (_stage != null) _stage.SetSpinRange(_tier.MinSpinDegrees, _tier.MaxSpinDegrees);

            // Nothing spawns before the session's first Tick, so subscribing here misses nothing.
            _session = _runner.BeginRun();
            _session.OnStageChanged   += OnStageChanged;
            _session.OnTargetSpawned  += OnTargetSpawned;
            _session.OnTargetResolved += OnTargetResolved;
            _session.OnWrongTap       += OnWrongTap;
            _session.OnDecoyExpired   += OnDecoyExpired;
            Refresh();
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // Offered only when a run began through Planet, which is what supplies the tier lookup. A
        // pick changes how the minigame plays for this run, and shows a real rock of that tier; what the
        // run pays and the mineral it names stay as they were.
        private void ShowTierPicker()
        {
            if (_handoff.DevSettingsForTier == null) return;

            _tierPicker = gameObject.AddComponent<ActiveMiningDevTierPicker>();
            _tierPicker.SelectedTier = _handoff.Settings?.TierNumber ?? 1;
            _tierPicker.OnTierPicked += OnTierPicked;
        }

        private void OnTierPicked(int tier)
        {
            var settings = _handoff.DevSettingsForTier(tier);
            _handoff.UseSettings(settings);
            if (_rulesText != null) _rulesText.text = ActiveMiningRulesText.For(settings);

            int index = tier - 1;
            if (_session == null && _stage != null && _devAsteroidsByTier != null
                && index >= 0 && index < _devAsteroidsByTier.Length && _devAsteroidsByTier[index] != null)
                _stage.SpawnClone(_devAsteroidsByTier[index]);
        }

        private void HideTierPicker()
        {
            if (_tierPicker == null) return;

            Destroy(_tierPicker);
            _tierPicker = null;
        }
#endif

        private void OnStageChanged(ActiveMiningStage stage)
        {
            if (stage == ActiveMiningStage.InProgress) return;

            SetInProgressUiActive(false);
            ShowResult(stage);
        }

        private void OnTargetSpawned(ActiveMiningTarget target)
        {
            if (target.Kind == ActiveMiningTargetKind.Vein)
            {
                ShowVein(target);
                return;
            }

            var slot    = TakeFreeSlot();
            bool decoy  = target.Kind == ActiveMiningTargetKind.Decoy;

            // A decoy with no clear spot isn't shown; a real target always is.
            if (!PlaceAnchor(slot.Anchor, target))
            {
                _session.DismissDecoy(target.Id);
                return;
            }

            slot.View.Show(target, OnTargetTapped);
            ProjectToScreen(slot);
            OrderInCanvas(slot.View, decoy);

            if (!decoy && _sceneCamera != null)
                DismissDecoysNear(_sceneCamera.WorldToScreenPoint(slot.Anchor.transform.position));
        }

        // A real target takes the spot a live decoy's tap area would share, so the player's tap on it
        // can't land on a decoy.
        private void DismissDecoysNear(Vector2 screenPoint)
        {
            float separation = MinMarkerSeparation * _canvas.scaleFactor;

            for (int i = 0; i < _slots.Count; i++)
            {
                var other = _slots[i].View.Target;
                if (other == null || other.Kind != ActiveMiningTargetKind.Decoy) continue;

                Vector2 at = _sceneCamera.WorldToScreenPoint(_slots[i].Anchor.transform.position);
                if (ActiveMiningTargetPoint.AxisGap(screenPoint, at) < separation)
                    _session.DismissDecoy(other.Id);
            }
        }

        // Decoys sit below the real targets in the canvas, so where the spin brings a pair together
        // the real target takes the tap.
        private void OrderInCanvas(ActiveMiningTargetView view, bool decoy)
        {
            int lowest = int.MaxValue, highest = int.MinValue;
            for (int i = 0; i < _slots.Count; i++)
            {
                int index = _slots[i].View.transform.GetSiblingIndex();
                lowest  = Mathf.Min(lowest, index);
                highest = Mathf.Max(highest, index);
            }
            view.transform.SetSiblingIndex(decoy ? lowest : highest);
        }

        // The spin eases down first, because placing the ends predicts where it will carry them.
        private void ShowVein(ActiveMiningTarget vein)
        {
            if (_veinView == null || _settings == null) return;

            if (_stage != null) _stage.EaseSpinTo(_settings.VeinSpinDegrees);

            if (_veinStart == null)
            {
                _veinStart = new GameObject("ActiveMiningVeinStart").AddComponent<ActiveMiningTargetPoint>();
                _veinEnd   = new GameObject("ActiveMiningVeinEnd").AddComponent<ActiveMiningTargetPoint>();
            }
            if (PlaceVein())
            {
                // The ring hasn't started aging yet, so the session still takes the vein's length.
                float length = Vector2.Distance(_sceneCamera.WorldToScreenPoint(_veinStart.transform.position),
                    _sceneCamera.WorldToScreenPoint(_veinEnd.transform.position)) / _canvas.scaleFactor;
                _session.SetVeinLength(vein.Id, length);
            }

            _veinView.Show(vein, TapRadius, _settings.VeinDragTolerance, _settings.VeinCompleteProgress, OnVeinCompleted, OnVeinFailed);
            ProjectVein();
        }

        private void OnVeinCompleted(ActiveMiningTarget vein) => _session.CompleteVein(vein.Id);

        private void OnVeinFailed(ActiveMiningTarget vein) => _session.FailVein(vein.Id);

        // A finished vein answers at its end marker, one marker's height clear of it so the fingertip
        // doesn't hide the label. A failed one has no such place, so the label goes back to where it began.
        private void OnVeinResolved(ActiveMiningGrade grade)
        {
            Vector2 start = _veinView != null ? _veinView.StartPosition : default;
            Vector2 end   = _veinView != null ? _veinView.EndPosition   : default;

            if (grade == ActiveMiningGrade.Miss)
                PlayMissFeedback(start);
            else
                PlayHitFeedback(grade, _veinEnd != null ? _veinEnd.transform.position : Vector3.zero, ClearOfRing(end));

            HideVein();
        }

        private void HideVein()
        {
            if (_veinView != null) _veinView.Hide();
            if (_stage != null) _stage.RestoreSpin();
        }

        // A tapped decoy arrives here as a Miss, so it gets the miss feedback and no hit effect.
        private void OnTargetResolved(ActiveMiningTarget target, ActiveMiningGrade grade)
        {
            if (target.Kind == ActiveMiningTargetKind.Vein) OnVeinResolved(grade);

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
                    PlayHitFeedback(grade, slot.Anchor.transform.position, slot.View.transform.position);
                }
                slot.View.Hide();
                break;
            }

            Refresh();
        }

        // An ignored decoy just goes away: nothing was earned or lost.
        private void OnDecoyExpired(ActiveMiningTarget decoy)
        {
            for (int i = 0; i < _slots.Count; i++)
            {
                if (_slots[i].View.Target != decoy) continue;

                _slots[i].View.Hide();
                break;
            }
        }

        // A wrong tap lands on empty space and leaves its ring live, so the label goes beside the oldest
        // live ring rather than over the thing the player has to tap next.
        private void OnWrongTap()
        {
            Refresh();

            TargetSlot oldest = null;
            for (int i = 0; i < _slots.Count; i++)
            {
                var shown = _slots[i].View.Target;
                if (shown == null || shown.Kind == ActiveMiningTargetKind.Decoy) continue;

                if (oldest == null || shown.Id < oldest.View.Target.Id) oldest = _slots[i];
            }

            if (oldest != null) PlayMissFeedback(ClearOfRing(oldest.View.transform.position));
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

        private void PlayHitFeedback(ActiveMiningGrade grade, Vector3 worldPoint, Vector3 screenPosition)
        {
            _audio.PlaySfx(SfxId.ActiveMiningTap);
            PlayHitVfx(worldPoint);
            ShowGrade(grade.ToString(), ColorOf(grade), screenPosition);
        }

        // The miss that fails the run is answered by the fail sound alone, which ShowResult plays.
        private void PlayMissFeedback(Vector3 screenPosition)
        {
            if (_session.ErrorCount < _session.MaxErrors) _audio.PlaySfx(SfxId.ActiveMiningMiss);
            ShowGrade("Miss", MissColor, screenPosition);
            if (_stage != null) _stage.Shake(MissShakeSeconds);
        }

        private static Color ColorOf(ActiveMiningGrade grade) => grade switch
        {
            ActiveMiningGrade.Perfect => PerfectColor,
            ActiveMiningGrade.Good    => GoodColor,
            _                         => LateColor,
        };

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

            HideVein();
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
        // and when its ring closes. Points sit inside the rock's bounds, so the marker projects over
        // the rock rather than over the empty sky around it. A marker also keeps clear of the ones
        // already live, so a decoy can't sit on a target. Where nothing is both on screen and clear,
        // a real target takes the on-screen point farthest from the others, and a decoy isn't placed:
        // this returns false for it.
        private bool PlaceAnchor(ActiveMiningTargetPoint anchor, ActiveMiningTarget target)
        {
            if (_stage == null || _stage.StageClone == null || _sceneCamera == null) return true;

            var     asteroid     = _stage.StageClone.transform;
            Vector3 towardViewer = _sceneCamera.transform.position - asteroid.position;
            float   margin       = TapRadius * _canvas.scaleFactor;
            float   radius       = _stage.SurfaceRadius * PlacementInset;
            float   separation   = MinMarkerSeparation * _canvas.scaleFactor;
            float   maxAngle     = ActiveMiningTargetPoint.MaxPlacementAngle(_tier.MaxSpinDegrees, target.RingSeconds);

            var     others       = LiveMarkerScreenPositions();
            Vector3 farthest     = default;
            float   farthestGap  = -1f;

            for (int attempt = 0; attempt < MaxPlacementAttempts; attempt++)
            {
                anchor.PlaceOnAsteroid(asteroid, radius, towardViewer, maxAngle);

                Vector3 point = anchor.transform.position;
                if (!IsOnScreen(point, margin) || !IsOnScreen(_stage.PredictPoint(point, target.RingSeconds), margin))
                    continue;

                Vector2 screen = _sceneCamera.WorldToScreenPoint(point);
                if (ActiveMiningTargetPoint.IsFarEnough(screen, others, separation)) return true;

                float gap = NearestDistance(screen, others);
                if (gap > farthestGap)
                {
                    farthestGap = gap;
                    farthest    = point;
                }
            }

            if (target.Kind == ActiveMiningTargetKind.Decoy) return false;

            anchor.transform.position = farthestGap >= 0f
                ? farthest
                : asteroid.position + towardViewer.normalized * radius;
            return true;
        }

        // Both ends are held to what a target is held to: on screen now and when the ring closes, over
        // the rock. The first pair whose length is inside the config's band is used, or else the pair
        // that came nearest the band. A rock too big to leave any pair on screen gets a level line of
        // the band's shortest length through the point facing the viewer, so there is still something
        // the player can drag. The ring they are held to is the longest a vein's can be, so a long vein
        // can't leave the screen before its ring closes. Returns false if there was nowhere to place them.
        private bool PlaceVein()
        {
            if (_settings == null || _stage == null || _stage.StageClone == null || _sceneCamera == null) return false;

            var     asteroid     = _stage.StageClone.transform;
            Vector3 towardViewer = _sceneCamera.transform.position - asteroid.position;
            float   margin       = TapRadius * _canvas.scaleFactor;
            float   radius       = _stage.SurfaceRadius * PlacementInset;
            float   minLength    = _settings.VeinMinLength * _canvas.scaleFactor;
            float   maxLength    = _settings.VeinMaxLength * _canvas.scaleFactor;
            float   ringSeconds  = _settings.VeinRingSecondsFor(_settings.VeinMaxLength);
            float   maxAngle     = ActiveMiningTargetPoint.MaxPlacementAngle(_settings.VeinSpinDegrees, ringSeconds);

            Vector3 nearestStart = default, nearestEnd = default;
            float   nearestMiss  = float.MaxValue;

            for (int attempt = 0; attempt < MaxPlacementAttempts; attempt++)
            {
                _veinStart.PlaceOnAsteroid(asteroid, radius, towardViewer, maxAngle);
                _veinEnd.PlaceOnAsteroid(asteroid, radius, towardViewer, maxAngle);

                Vector3 start = _veinStart.transform.position, end = _veinEnd.transform.position;
                if (!StaysOnScreen(start, ringSeconds, margin) || !StaysOnScreen(end, ringSeconds, margin)) continue;

                float length = Vector2.Distance(_sceneCamera.WorldToScreenPoint(start), _sceneCamera.WorldToScreenPoint(end));
                float miss = Mathf.Max(minLength - length, length - maxLength);
                if (miss <= 0f) return true;

                if (miss < nearestMiss)
                {
                    nearestMiss  = miss;
                    nearestStart = start;
                    nearestEnd   = end;
                }
            }

            if (nearestMiss < float.MaxValue)
            {
                _veinStart.transform.position = nearestStart;
                _veinEnd.transform.position   = nearestEnd;
                return true;
            }

            Vector3 facing = _sceneCamera.WorldToScreenPoint(asteroid.position + towardViewer.normalized * radius);
            Vector3 half   = Vector3.right * (minLength / 2f);
            _veinStart.transform.position = _sceneCamera.ScreenToWorldPoint(facing - half);
            _veinEnd.transform.position   = _sceneCamera.ScreenToWorldPoint(facing + half);
            return true;
        }

        private bool StaysOnScreen(Vector3 worldPoint, float seconds, float margin) =>
            IsOnScreen(worldPoint, margin) && IsOnScreen(_stage.PredictPoint(worldPoint, seconds), margin);

        private void ProjectVein()
        {
            if (_veinView == null || _sceneCamera == null) return;

            _veinView.UpdateLine(_sceneCamera.WorldToScreenPoint(_veinStart.transform.position),
                _sceneCamera.WorldToScreenPoint(_veinEnd.transform.position));
        }

        // Half a target's hit area, in canvas units.
        private float TapRadius => ((RectTransform)_targetTemplate.transform).rect.width / 2f;

        private List<Vector2> LiveMarkerScreenPositions()
        {
            var positions = new List<Vector2>();
            for (int i = 0; i < _slots.Count; i++)
                if (_slots[i].View.Target != null)
                    positions.Add(_sceneCamera.WorldToScreenPoint(_slots[i].Anchor.transform.position));
            return positions;
        }

        private static float NearestDistance(Vector2 point, List<Vector2> others)
        {
            float nearest = float.MaxValue;
            for (int i = 0; i < others.Count; i++)
                nearest = Mathf.Min(nearest, ActiveMiningTargetPoint.AxisGap(point, others[i]));
            return nearest;
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
