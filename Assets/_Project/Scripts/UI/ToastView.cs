using TMPro;
using UnityEngine;
using SocialUniverse.Core;

namespace SocialUniverse.UI
{
    // Minimal failure toast (Known Issue #17): shows ServerActionFailedEvent messages — and any
    // notice passed to Show() — for a few seconds at the bottom of the screen. The layout is the Toast
    // prefab (Prefabs/UI/Resources — edit it there), with its own overlay canvas sorted above every
    // panel (Drone Garage, Mineral Inventory), so no scene wiring is needed — call EnsureExists() from
    // the scene's HUD. Lives in the active scene and unloads with it.
    public class ToastView : MonoBehaviour
    {
        private const string PrefabResource = "Toast";
        private const float  VisibleSeconds = 2.5f;
        private const float  FadeSeconds    = 0.3f;

        [SerializeField] private CanvasGroup _group;   // starts at alpha 0, never blocks taps
        [SerializeField] private TMP_Text    _label;

        private static ToastView _instance;

        private float _hideAt;

        public static void EnsureExists()
        {
            if (_instance != null) return;
            _instance = Instantiate(Resources.Load<ToastView>(PrefabResource));
            _instance.name = PrefabResource;
        }

        private void OnEnable()  => EventBus.Subscribe<ServerActionFailedEvent>(OnFailed);
        private void OnDisable() => EventBus.Unsubscribe<ServerActionFailedEvent>(OnFailed);

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        // For player-facing notices that aren't server failures (e.g. "your drone is busy").
        public static void Show(string message)
        {
            EnsureExists();
            _instance.Display(message);
        }

        private void OnFailed(ServerActionFailedEvent e) => Display(e.Message);

        private void Display(string message)
        {
            _label.text = message;
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
