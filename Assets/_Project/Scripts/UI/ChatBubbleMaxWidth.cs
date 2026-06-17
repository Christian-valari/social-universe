using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

namespace SocialUniverse.UI
{
    // Sizes a chat bubble background horizontally to fit its TMP text content,
    // capped at maxWidth. Pair with ContentSizeFitter (V: Preferred) on the same
    // object so height still auto-adjusts after word-wrap kicks in.
    [ExecuteAlways]
    [RequireComponent(typeof(RectTransform))]
    public class ChatBubbleMaxWidth : UIBehaviour, ILayoutSelfController
    {
        [SerializeField] public float maxWidth = 450f;
        [SerializeField] private TextMeshProUGUI _text;

        private RectTransform _rt;

        protected override void Awake()
        {
            base.Awake();
            _rt = GetComponent<RectTransform>();
        }

        public void SetLayoutHorizontal()
        {
            if (_text == null || _rt == null) return;

            // Measure the text at infinite width to get its natural single-line preferred width.
            float preferred = _text.GetPreferredValues(float.MaxValue, float.MaxValue).x;

            // Account for any padding the VerticalLayoutGroup adds around the text.
            var vlg = GetComponent<VerticalLayoutGroup>();
            float hPadding = vlg != null ? vlg.padding.left + vlg.padding.right : 0f;

            float bubbleWidth = Mathf.Min(preferred + hPadding, maxWidth);
            float textWidth   = bubbleWidth - hPadding;

            // Set bubble width first.
            _rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, bubbleWidth);

            // Set text width directly — VerticalLayoutGroup runs before this component so
            // it would otherwise use a stale width, causing TMP to wrap at the wrong boundary.
            _text.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, textWidth);
        }

        public void SetLayoutVertical() { }

        protected override void OnEnable()
        {
            base.OnEnable();
            if (_rt != null) LayoutRebuilder.MarkLayoutForRebuild(_rt);
        }

        protected override void OnRectTransformDimensionsChange()
        {
            base.OnRectTransformDimensionsChange();
            if (isActiveAndEnabled && _rt != null) LayoutRebuilder.MarkLayoutForRebuild(_rt);
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            if (_rt == null) _rt = GetComponent<RectTransform>();
            if (isActiveAndEnabled && _rt != null) LayoutRebuilder.MarkLayoutForRebuild(_rt);
        }
#endif
    }
}
