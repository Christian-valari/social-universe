using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SocialUniverse.UI
{
    // A message with a single Quit button that closes the app; a backdrop over the whole screen
    // swallows every other tap. Shown by ActiveSessionController when this account signs in on
    // another device. The layout is the QuitDialog prefab (Prefabs/UI/Resources — edit it there),
    // with its own overlay canvas above ToastView; it may be saved active or not. Show() instantiates
    // it on first use, keeps it across scene changes, and opens it with PopupTransition's pop & fade;
    // the EventSystem of whichever scene is loaded handles the button.
    public class QuitDialogView : MonoBehaviour
    {
        private const string PrefabResource = "QuitDialog";

        [SerializeField] private TMP_Text      _message;
        [SerializeField] private Button        _quitButton;
        [SerializeField] private RectTransform _window;   // the part that pops in (PopupTransition)

        private static QuitDialogView _instance;

        public static void Show(string message)
        {
            if (_instance == null)
            {
                _instance = Instantiate(Resources.Load<QuitDialogView>(PrefabResource));
                if (Application.isPlaying) DontDestroyOnLoad(_instance.gameObject);
            }
            _instance._message.text = message;
            PopupTransition.On(_instance.gameObject, _instance._window).Show();
        }

        private void Awake() => _quitButton.onClick.AddListener(Quit);

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        private static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false; // Application.Quit is ignored in the Editor
#else
            Application.Quit();
#endif
        }
    }
}
