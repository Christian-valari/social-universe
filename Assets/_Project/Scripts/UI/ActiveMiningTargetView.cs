using System;
using UnityEngine;
using UnityEngine.UI;
using SocialUniverse.Mining;

namespace SocialUniverse.UI
{
    // One pooled tap target: a marker and the ring closing in on it. The root's hit area is larger
    // than the marker, so a tap needn't land exactly on it. ActiveMiningMinigameView drives it.
    // A decoy shows a red X in place of the marker, with a red ring, so it differs in shape as well
    // as colour; the next normal target puts the usual look back.
    public class ActiveMiningTargetView : MonoBehaviour
    {
        public const float RingStartScale = 2.2f;

        [SerializeField] private Button        _button;
        [SerializeField] private Image         _marker;
        [SerializeField] private RectTransform _ring;
        [SerializeField] private Sprite        _decoySprite;

        private static readonly Color DecoyColor = new Color(0.95f, 0.2f, 0.2f);

        private Action<ActiveMiningTarget> _onTapped;
        private Image  _ringImage;
        private Sprite _normalSprite;
        private Color  _normalMarkerColor;
        private Color  _normalRingColor;
        private bool   _lookCaptured;

        // Null while this view is back in the pool.
        public ActiveMiningTarget Target { get; private set; }

        private void Awake() => _button.onClick.AddListener(OnClicked);

        public void Show(ActiveMiningTarget target, Action<ActiveMiningTarget> onTapped)
        {
            Target    = target;
            _onTapped = onTapped;
            ApplyLook(target.Kind == ActiveMiningTargetKind.Decoy);
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

        // The normal look is read off the view the first time it shows, before a decoy can change it.
        private void ApplyLook(bool decoy)
        {
            if (!_lookCaptured)
            {
                _ringImage         = _ring.GetComponent<Image>();
                _normalSprite      = _marker.sprite;
                _normalMarkerColor = _marker.color;
                _normalRingColor   = _ringImage != null ? _ringImage.color : Color.white;
                _lookCaptured      = true;
            }

            bool asDecoy = decoy && _decoySprite != null;
            _marker.sprite = asDecoy ? _decoySprite : _normalSprite;
            _marker.color  = asDecoy ? DecoyColor : _normalMarkerColor;
            if (_ringImage != null) _ringImage.color = decoy ? DecoyColor : _normalRingColor;
        }

        private void OnClicked()
        {
            if (Target != null) _onTapped?.Invoke(Target);
        }
    }
}
