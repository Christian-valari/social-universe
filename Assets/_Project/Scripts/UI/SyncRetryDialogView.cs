using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SocialUniverse.UI
{
    // Shown when the player's server data didn't load for a scene (App.PlayerDataPull): the game waits
    // behind it instead of opening with defaults, and Retry loads again. The layout is the
    // SyncRetryDialog prefab (Prefabs/UI/Resources — edit it there), with its own overlay canvas —
    // above the loading screen and ToastView (5000), below a replaced session's QuitDialogView (6000) —
    // so no scene wiring is needed, and it survives scene loads (hidden between uses).
    public class SyncRetryDialogView : MonoBehaviour
    {
        private const string PrefabResource = "SyncRetryDialog";

        [SerializeField] private TMP_Text      _message;
        [SerializeField] private Button        _retryButton;
        [SerializeField] private RectTransform _window;   // the part that pops in (PopupTransition)

        private static SyncRetryDialogView _instance;

        private EventSystem _ownEventSystem;
        private TaskCompletionSource<bool> _retry;

        // Completes when the player taps Retry. A second call while the dialog is up shares the wait.
        public static Task ShowAsync(string message)
        {
            if (_instance == null)
            {
                _instance = Instantiate(Resources.Load<SyncRetryDialogView>(PrefabResource));
                _instance.name = PrefabResource;
                // Wired here rather than in Awake, which doesn't run in EditMode tests.
                _instance._retryButton.onClick.AddListener(_instance.OnRetry);
                if (Application.isPlaying) DontDestroyOnLoad(_instance.gameObject);
            }
            return _instance.Show(message);
        }

        private Task Show(string message)
        {
            _message.text = message;
            PopupTransition.On(gameObject, _window).Show();
            UseOwnEventSystemOnlyIfNoSceneHasOne();
            _retry ??= new TaskCompletionSource<bool>();
            return _retry.Task;
        }

        // Hidden at once rather than faded: the reload starts now, and the dialog may be needed again.
        private void OnRetry()
        {
            gameObject.SetActive(false);
            var retry = _retry;
            _retry = null;
            retry?.TrySetResult(true);
        }

        // At boot, and between the Auth scene unloading and the next scene loading, no loaded scene
        // has an EventSystem, and without one Retry can't be tapped. The dialog then brings its own,
        // switched off whenever a scene has one (two active EventSystems make Unity ignore one).
        private void UseOwnEventSystemOnlyIfNoSceneHasOne()
        {
            bool sceneHasOne = false;
            foreach (var es in FindObjectsByType<EventSystem>(FindObjectsSortMode.None))
                if (es != _ownEventSystem && es.enabled) sceneHasOne = true;

            if (sceneHasOne)
            {
                if (_ownEventSystem != null) _ownEventSystem.gameObject.SetActive(false);
                return;
            }

            if (_ownEventSystem == null)
            {
                // Active Input Handling is "Both", so the legacy module reads mouse and touch.
                var go = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
                go.transform.SetParent(transform, false);
                _ownEventSystem = go.GetComponent<EventSystem>();
            }
            _ownEventSystem.gameObject.SetActive(true);
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }
    }
}
