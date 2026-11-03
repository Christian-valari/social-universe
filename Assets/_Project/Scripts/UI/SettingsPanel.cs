using UnityEngine;
using UnityEngine.UI;
using VContainer;
using TMPro;
using SocialUniverse.Core;
using SocialUniverse.Safety;
using SocialUniverse.Config;

namespace SocialUniverse.UI
{
    // Settings modal: music/SFX volume, logout (with inline Yes/No confirm),
    // app version, close. Same show/hide modal shape as DisplayNameModal.
    public class SettingsPanel : MonoBehaviour
    {
        [SerializeField] private Slider _musicSlider;
        [SerializeField] private Slider _sfxSlider;
        [SerializeField] private Button _logoutButton;
        [SerializeField] private GameObject _logoutConfirmPanel;
        [SerializeField] private Button _logoutConfirmYes;
        [SerializeField] private Button _logoutConfirmNo;
        [SerializeField] private Button _closeButton;
        [SerializeField] private TMP_Text _versionText;
        [SerializeField] private RectTransform _window;   // the panel that pops in and out (PopupTransition)
        [SerializeField] private RectTransform _logoutConfirmWindow;   // the logout confirm's own panel

        [Inject] private IAudioSettingsService _audioSettings;
        [Inject] private GameStateMachine      _fsm;
        [Inject] private IObjectResolver       _resolver;
        [Inject] private IAudioManager         _audio;

        private PopupTransition _transition;
        private PopupTransition Transition => _transition != null ? _transition : _transition = PopupTransition.On(gameObject, _window);

        private PopupTransition _logoutTransition;
        private PopupTransition LogoutTransition => _logoutTransition != null ? _logoutTransition : _logoutTransition = PopupTransition.On(_logoutConfirmPanel, _logoutConfirmWindow);

        private void Awake()
        {
            _logoutButton.onClick.AddListener(() => LogoutTransition.Show());
            _logoutConfirmYes.onClick.AddListener(OnLogoutConfirmed);
            _logoutConfirmNo.onClick.AddListener(() => { _audio.PlaySfx(SfxId.Cancel); LogoutTransition.Hide(); });
            _closeButton.onClick.AddListener(Close);
            gameObject.SetActive(false);
        }

        private void Start()
        {
            _musicSlider.onValueChanged.AddListener(_audioSettings.SetMusicVolume);
            _sfxSlider.onValueChanged.AddListener(_audioSettings.SetSfxVolume);
        }

        public void Open()
        {
            _musicSlider.SetValueWithoutNotify(_audioSettings.MusicVolume01);
            _sfxSlider.SetValueWithoutNotify(_audioSettings.SfxVolume01);
            _logoutConfirmPanel.SetActive(false);
            _versionText.text = $"v{Application.version}";
            _audio.PlaySfx(SfxId.OpenPanel);
            Transition.Show();
        }

        public void Close()
        {
            _audio.PlaySfx(SfxId.Cancel);
            Transition.Hide();
        }

        private void OnLogoutConfirmed()
        {
            _audio.PlaySfx(SfxId.Confirm);
            SetInteractable(false);
            _fsm.TransitionTo(_resolver.Resolve<LogoutState>());
        }

        private void SetInteractable(bool interactable)
        {
            _logoutButton.interactable       = interactable;
            _logoutConfirmYes.interactable   = interactable;
            _logoutConfirmNo.interactable    = interactable;
            _closeButton.interactable        = interactable;
        }
    }
}
