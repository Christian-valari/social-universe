using System;
using UnityEngine;
using UnityEngine.UI;
using SocialUniverse.Mining;

namespace SocialUniverse.UI
{
    // One pooled tap target: a marker and the ring closing in on it. The root's hit area is larger
    // than the marker, so a tap needn't land exactly on it. ActiveMiningMinigameView drives it.
    public class ActiveMiningTargetView : MonoBehaviour
    {
        private const float RingStartScale = 2.2f;

        [SerializeField] private Button        _button;
        [SerializeField] private Image         _marker;
        [SerializeField] private RectTransform _ring;

        private Action<ActiveMiningTarget> _onTapped;

        // Null while this view is back in the pool.
        public ActiveMiningTarget Target { get; private set; }

        private void Awake() => _button.onClick.AddListener(OnClicked);

        public void Show(ActiveMiningTarget target, Action<ActiveMiningTarget> onTapped)
        {
            Target    = target;
            _onTapped = onTapped;
            SetRingProgress(target.Progress);
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            Target    = null;
            _onTapped = null;
            gameObject.SetActive(false);
        }

        public void SetScreenPosition(Vector3 position) => transform.position = position;

        // 0 = just spawned, 1 = closed onto the marker.
        public void SetRingProgress(float progress)
        {
            float scale = Mathf.Lerp(RingStartScale, 1f, progress);
            _ring.localScale = new Vector3(scale, scale, 1f);
        }

        private void OnClicked()
        {
            if (Target != null) _onTapped?.Invoke(Target);
        }
    }
}
