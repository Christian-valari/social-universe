using UnityEngine;
using UnityEngine.UI;
using SocialUniverse.Core;

namespace SocialUniverse.UI
{
    // Minimal failure toast (Known Issue #17): shows ServerActionFailedEvent messages for a few
    // seconds at the bottom of the screen. Builds its own overlay canvas, sorted above every
    // panel (Drone Garage, Mineral Inventory), so no scene or prefab wiring is needed — call
    // EnsureExists() from the scene's HUD. Lives in the active scene and unloads with it.
    public class ToastView : MonoBehaviour
    {
        private const float VisibleSeconds = 2.5f;
        private const float FadeSeconds    = 0.3f;

        private static ToastView _instance;

        private CanvasGroup _group;
        private Text        _label;
        private float       _hideAt;

        public static void EnsureExists()
        {
            if (_instance != null) return;
            _instance = new GameObject("ToastView").AddComponent<ToastView>();
        }

        private void Awake()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5000;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight  = 0.5f;

            _group = gameObject.AddComponent<CanvasGroup>();
            _group.alpha          = 0f;
            _group.blocksRaycasts = false;
            _group.interactable   = false;

            var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(transform, false);
            var panelRect = (RectTransform)panel.transform;
            panelRect.anchorMin        = new Vector2(0.5f, 0f);
            panelRect.anchorMax        = new Vector2(0.5f, 0f);
            panelRect.pivot            = new Vector2(0.5f, 0f);
            panelRect.anchoredPosition = new Vector2(0f, 260f);
            panelRect.sizeDelta        = new Vector2(900f, 130f);
            var bg = panel.GetComponent<Image>();
            bg.color         = new Color(0.08f, 0.08f, 0.12f, 0.92f);
            bg.raycastTarget = false;

            var text = new GameObject("Message", typeof(RectTransform), typeof(Text));
            text.transform.SetParent(panel.transform, false);
            var textRect = (RectTransform)text.transform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(32f, 12f);
            textRect.offsetMax = new Vector2(-32f, -12f);
            _label = text.GetComponent<Text>();
            _label.font          = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _label.fontSize      = 44;
            _label.alignment     = TextAnchor.MiddleCenter;
            _label.color         = Color.white;
            _label.raycastTarget = false;
        }

        private void OnEnable()  => EventBus.Subscribe<ServerActionFailedEvent>(OnFailed);
        private void OnDisable() => EventBus.Unsubscribe<ServerActionFailedEvent>(OnFailed);

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        private void OnFailed(ServerActionFailedEvent e)
        {
            _label.text = e.Message;
            _hideAt     = Time.unscaledTime + VisibleSeconds;
            _group.alpha = 1f;
        }

        private void Update()
        {
            if (_group.alpha <= 0f) return;
            float remaining = _hideAt - Time.unscaledTime;
            _group.alpha = remaining >= 0f ? 1f : Mathf.Clamp01(1f + remaining / FadeSeconds);
        }
    }
}
