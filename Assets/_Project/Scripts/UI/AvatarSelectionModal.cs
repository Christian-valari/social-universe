using System;
using System.Collections.Generic;
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
    // Grid picker for the player's profile avatar. Mirrors DisplayNameModal's
    // injected-services / Open() / Close() shape. Unlike DisplayNameModal
    // there's no separate confirm step — tapping an avatar commits
    // immediately, the same way tapping a hex tile does.
    public class AvatarSelectionModal : MonoBehaviour
    {
        [SerializeField] private Transform _gridContainer;
        [SerializeField] private Button    _avatarButtonPrefab;  // Button + Image, one avatar tile; starts inactive
        [SerializeField] private Button    _cancelButton;
        [SerializeField] private TMP_Text  _statusText;

        [Inject] private PlayerState      _playerState;
        [Inject] private ProfileService   _profiles;
        [Inject] private DatabaseRegistry _registry;

        private readonly List<(Button Button, string AvatarId)> _entries = new();
        private bool _built;

        private void Awake()
        {
            _cancelButton.onClick.AddListener(Close);
            gameObject.SetActive(false);
        }

        public void Open()
        {
            if (!_built) BuildGrid();
            RefreshHighlight();
            _statusText.text = "";
            gameObject.SetActive(true);
        }

        public void Close() => gameObject.SetActive(false);

        private void BuildGrid()
        {
            foreach (var avatar in _registry.AllAvatars)
            {
                string avatarId = avatar.AvatarId;
                var button = Instantiate(_avatarButtonPrefab, _gridContainer);
                button.gameObject.SetActive(true);

                var image = button.GetComponent<Image>();
                if (image != null) image.sprite = avatar.Sprite;

                button.onClick.AddListener(() => OnAvatarClicked(avatarId));
                _entries.Add((button, avatarId));
            }
            _built = true;
        }

        private async void OnAvatarClicked(string avatarId)
        {
            if (avatarId == _playerState.AvatarId) return;

            SetBusy(true);
            _statusText.text = "Saving…";

            try
            {
                var result = await _profiles.UpdateAvatarAsync(avatarId);

                // null result means mock backend — treat as success with local id
                if (result == null || result.Success)
                {
                    _playerState.SetAvatarId(avatarId);
                    Close();
                }
                else
                {
                    _statusText.text = result.Reason == "AVATAR_INVALID"
                        ? "That avatar isn't available"
                        : "Could not update — please try again";
                }
            }
            catch (Exception ex)
            {
                _statusText.text = "Error updating avatar";
                SULog.Warn($"AvatarSelectionModal: update failed ({ex.Message})", SULog.Channel.Net);
            }
            finally
            {
                SetBusy(false);
            }
        }

        private void RefreshHighlight()
        {
            foreach (var entry in _entries)
                entry.Button.interactable = entry.AvatarId != _playerState.AvatarId;
        }

        private void SetBusy(bool busy)
        {
            _cancelButton.interactable = !busy;
            if (busy)
            {
                foreach (var entry in _entries) entry.Button.interactable = false;
            }
            else
            {
                RefreshHighlight();
            }
        }
    }
}
