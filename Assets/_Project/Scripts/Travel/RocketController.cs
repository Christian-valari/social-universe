using System.Threading.Tasks;
using TMPro;
using UnityEngine;

namespace SocialUniverse.Travel
{
    // Plays a brief departure overlay before TravelController hands off to the
    // FSM scene transition (which shows the existing LoadingScreen for the
    // Planet load). The dodge minigame described in the architecture doc is
    // P2/deferred — same "stubbed, logs and closes" treatment as
    // ActiveMiningMinigame; PlayDepartureAsync is the entire travel "game" for
    // now.
    public class RocketController : MonoBehaviour
    {
        [SerializeField] private CanvasGroup _overlayGroup;
        [SerializeField] private RectTransform _rocketIcon;
        [SerializeField] private TMP_Text _statusText;
        [SerializeField] private float _departureDurationSec = 1.5f;
        [SerializeField] private Vector2 _departureOffset = new(0f, 400f);

        private void Awake()
        {
            if (_overlayGroup != null) _overlayGroup.gameObject.SetActive(false);
        }

        public async Task PlayDepartureAsync(string destinationName)
        {
            if (_overlayGroup == null)
            {
                // No overlay wired (e.g. EditMode/headless) — nothing to animate.
                return;
            }

            _overlayGroup.gameObject.SetActive(true);
            _overlayGroup.alpha = 1f;
            if (_statusText != null) _statusText.text = $"Departing for {destinationName}...";

            Vector2 start = _rocketIcon != null ? _rocketIcon.anchoredPosition : Vector2.zero;
            Vector2 end   = start + _departureOffset;

            float t = 0f;
            while (t < _departureDurationSec)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / _departureDurationSec);
                if (_rocketIcon != null) _rocketIcon.anchoredPosition = Vector2.Lerp(start, end, k);
                await Task.Yield();
            }

            if (_rocketIcon != null) _rocketIcon.anchoredPosition = start;
            _overlayGroup.gameObject.SetActive(false);
        }
    }
}
