using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using TMPro;
using SocialUniverse.Core;
using SocialUniverse.Social;
using SocialUniverse.Progression;
using SocialUniverse.Config;
using SocialUniverse.Safety;

namespace SocialUniverse.UI
{
    // Grid picker for the player's profile avatar. Mirrors DisplayNameModal's
    // injected-services / Open() / Close() shape. Unlike DisplayNameModal
    // there's no separate confirm step — tapping an avatar commits
    // immediately, the same way tapping a hex tile does.
    public class AvatarSelectionModal : MonoBehaviour
    {
        [SerializeField] private Transform _gridContainer;
        [SerializeField] private Image _avatarPreview;
        [SerializeField] private Button    _avatarButtonPrefab;  // Button + Image, one avatar tile; starts inactive
        [SerializeField] private TMP_Text  _statusText;

        [Inject] private PlayerState      _playerState;
        [Inject] private ProfileService   _profiles;
        [Inject] private DatabaseRegistry _registry;
        [Inject] private IAudioManager    _audio;

        private string _selectedAvatarId;

        private readonly List<(Button Button, string AvatarId)> _entries = new();
        private bool _built;

        private void Awake()
        {
            gameObject.SetActive(false);
        }

        public void Open()
        {
            if (!_built) BuildGrid();
            RefreshHighlight();
            _statusText.text = "";
            _selectedAvatarId = _playerState.AvatarId;
            var currentAvatar = _registry.AllAvatars.ToList().Find(x => x.AvatarId == _selectedAvatarId);
            // currentAvatar is null when AvatarId is empty or references an id not in
            // the catalog (e.g. a first-login account whose avatar hasn't been assigned
            // yet) — AvatarDefinition is a reference type, so Find returns null on a miss.
            _avatarPreview.sprite = currentAvatar != null ? currentAvatar.Sprite : null;
            _audio.PlaySfx(SfxId.OpenPanel);
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

                button.onClick.AddListener(() => OnAvatarClicked(avatarId, avatar.Sprite));
                _entries.Add((button, avatarId));
            }
            _built = true;
        }

        private void OnAvatarClicked(string avatarId, Sprite avatarIcon)
        {
            if (avatarId == _playerState.AvatarId) return;

            _selectedAvatarId = avatarId;
            _avatarPreview.sprite = avatarIcon;
        }

        public async void UpdateAvatar()
        {
            if (_selectedAvatarId == _playerState.AvatarId) return;
            
            SetBusy(true);
            _statusText.text = "Saving…";

            try
            {
                var result = await _profiles.UpdateAvatarAsync(_selectedAvatarId);

                // null result means mock backend — treat as success with local id
                if (result == null || result.Success)
                {
                    _playerState.SetAvatarId(_selectedAvatarId);
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
