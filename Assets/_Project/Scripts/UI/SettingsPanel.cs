using UnityEngine;
using UnityEngine.UI;
using VContainer;
using TMPro;
using SocialUniverse.Core;
using SocialUniverse.Safety;

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

        [Inject] private IAudioSettingsService _audio;
        [Inject] private GameStateMachine      _fsm;
        [Inject] private IObjectResolver       _resolver;

        private void Awake()
        {
            _musicSlider.onValueChanged.AddListener(_audio.SetMusicVolume);
            _sfxSlider.onValueChanged.AddListener(_audio.SetSfxVolume);
            _logoutButton.onClick.AddListener(() => _logoutConfirmPanel.SetActive(true));
            _logoutConfirmYes.onClick.AddListener(OnLogoutConfirmed);
            _logoutConfirmNo.onClick.AddListener(() => _logoutConfirmPanel.SetActive(false));
            _closeButton.onClick.AddListener(Close);
            gameObject.SetActive(false);
        }

        public void Open()
        {
            _musicSlider.SetValueWithoutNotify(_audio.MusicVolume01);
            _sfxSlider.SetValueWithoutNotify(_audio.SfxVolume01);
            _logoutConfirmPanel.SetActive(false);
            _versionText.text = $"v{Application.version}";
            gameObject.SetActive(true);
        }

        public void Close() => gameObject.SetActive(false);

        private void OnLogoutConfirmed()
        {
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
