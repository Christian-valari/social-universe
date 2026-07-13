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

        [Inject] private IAudioSettingsService _audioSettings;
        [Inject] private GameStateMachine      _fsm;
        [Inject] private IObjectResolver       _resolver;
        [Inject] private IAudioManager         _audio;

        private void Awake()
        {
            _logoutButton.onClick.AddListener(() => _logoutConfirmPanel.SetActive(true));
            _logoutConfirmYes.onClick.AddListener(OnLogoutConfirmed);
            _logoutConfirmNo.onClick.AddListener(() => { _audio.PlaySfx(SfxId.Cancel); _logoutConfirmPanel.SetActive(false); });
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
            gameObject.SetActive(true);
        }

        public void Close()
        {
            _audio.PlaySfx(SfxId.Cancel);
            gameObject.SetActive(false);
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
