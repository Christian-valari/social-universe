using UnityEngine;
using UnityEngine.UI;
using VContainer;
using SocialUniverse.Mining;

namespace SocialUniverse.UI
{
    // Overlay shown while an active-mining session is running: renders the current target
    // point, an error counter, and forwards player taps to MiningController. _targetButton
    // must be a UI child rendered above _missAreaButton in the hierarchy so a tap on the
    // point hits the target first and any other tap in the asteroid area falls through to
    // the miss button (standard Unity UI raycast ordering).
    public class ActiveMiningMinigameView : MonoBehaviour
    {
        [SerializeField] private GameObject    _root;
        [SerializeField] private RectTransform _asteroidArea;  // bounds for random point placement
        [SerializeField] private RectTransform _targetPoint;
        [SerializeField] private Button        _targetButton;
        [SerializeField] private Button        _missAreaButton; // full-bleed background behind the target point
        [SerializeField] private Text          _progressText;
        [SerializeField] private Text          _errorText;

        [Inject] private MiningController _mining;

        private void Awake()
        {
            if (_root != null) _root.SetActive(false);
            if (_targetButton   != null) _targetButton.onClick.AddListener(() => OnTapped(hitTarget: true));
            if (_missAreaButton != null) _missAreaButton.onClick.AddListener(() => OnTapped(hitTarget: false));
        }

        private void Start() => _mining.OnActiveSessionChanged += OnSessionChanged;

        private void OnDestroy() => _mining.OnActiveSessionChanged -= OnSessionChanged;

        private void Update()
        {
            var session = _mining.CurrentActiveSession;
            if (session != null) Refresh(session);
        }

        private void OnSessionChanged(ActiveMiningSession session)
        {
            if (session == null)
            {
                if (_root != null) _root.SetActive(false);
                return;
            }

            if (_root != null) _root.SetActive(true);
            Refresh(session);
            PlaceTargetPoint();
        }

        private void Refresh(ActiveMiningSession session)
        {
            if (_progressText != null) _progressText.text = $"{session.SuccessfulTaps}/{session.TapsRequired}";
            if (_errorText    != null) _errorText.text    = $"Misses: {session.ErrorCount}/{session.MaxErrors}";
        }

        private void PlaceTargetPoint()
        {
            if (_targetPoint == null || _asteroidArea == null) return;

            float x = Random.Range(-_asteroidArea.rect.width  * 0.5f, _asteroidArea.rect.width  * 0.5f);
            float y = Random.Range(-_asteroidArea.rect.height * 0.5f, _asteroidArea.rect.height * 0.5f);
            _targetPoint.anchoredPosition = new Vector2(x, y);
        }

        private void OnTapped(bool hitTarget)
        {
            if (_mining.CurrentActiveSession == null) return;

            _mining.RegisterActiveTap(hitTarget);

            if (_mining.CurrentActiveSession != null) // session still in progress -> next point
                PlaceTargetPoint();
        }
    }
}
