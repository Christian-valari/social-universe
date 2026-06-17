using System;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using TMPro;
using SocialUniverse.Core;
using SocialUniverse.Social;
using SocialUniverse.Progression;
using SocialUniverse.Config;

namespace SocialUniverse.UI
{
    // Pop-up modal that lets the player change their in-game display name.
    // Validates locally, then commits through ProfileService (server-authoritative).
    // After a successful update both PlayerState and IAuthService are notified so
    // the HUD and any future reconnect both reflect the new name.
    public class DisplayNameModal : MonoBehaviour
    {
        [SerializeField] private TMP_InputField _nameInput;
        [SerializeField] private Button         _confirmButton;
        [SerializeField] private Button         _cancelButton;
        [SerializeField] private TMP_Text       _statusText;

        [Inject] private IAuthService   _auth;
        [Inject] private PlayerState    _playerState;
        [Inject] private ProfileService _profiles;
        [Inject] private SocialConfig   _config;

        private void Awake()
        {
            _confirmButton.onClick.AddListener(OnConfirmClicked);
            _cancelButton.onClick.AddListener(Close);
            gameObject.SetActive(false);
        }

        public void Open()
        {
            _nameInput.text  = _playerState.DisplayName;
            _statusText.text = "";
            gameObject.SetActive(true);
        }

        public void Close() => gameObject.SetActive(false);

        private async void OnConfirmClicked()
        {
            string name = _nameInput.text.Trim();
            int maxLen  = _config != null ? _config.MaxDisplayNameLength : 20;

            if (string.IsNullOrEmpty(name) || name.Length < 2)
            {
                _statusText.text = "Name must be at least 2 characters";
                return;
            }
            if (name.Length > maxLen)
            {
                _statusText.text = $"Name must be {maxLen} characters or fewer";
                return;
            }

            SetBusy(true);
            _statusText.text = "Saving…";

            try
            {
                var result = await _profiles.UpdateDisplayNameAsync(name);

                // null result means mock backend — treat as success with local name
                if (result == null || result.Success)
                {
                    string committed = result?.DisplayName ?? name;
                    _playerState.SetDisplayName(committed);
                    await _auth.UpdateDisplayNameAsync(committed);
                    Close();
                }
                else
                {
                    _statusText.text = result.Reason switch
                    {
                        "NAME_TOO_LONG" => $"Name is too long (max {maxLen} chars)",
                        "NAME_REJECTED" => "That name isn't allowed",
                        "NAME_EMPTY"    => "Name cannot be empty",
                        _               => "Could not update — please try again"
                    };
                }
            }
            catch (Exception ex)
            {
                _statusText.text = "Error updating name";
                SULog.Warn($"DisplayNameModal: update failed ({ex.Message})", SULog.Channel.Net);
            }
            finally
            {
                SetBusy(false);
            }
        }

        private void SetBusy(bool busy)
        {
            _confirmButton.interactable = !busy;
            _cancelButton.interactable  = !busy;
        }
    }
}
