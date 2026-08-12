using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SocialUniverse.Config;
using SocialUniverse.Economy;
using SocialUniverse.Progression;

namespace SocialUniverse.UI
{
    // Reusable top-bar view: shows the player's avatar, username, coins and stardust.
    // Pure view (MVP, like CurrencyView) — the hosting scene calls Bind with its resolved
    // services, so the prefab drops into any scene without every scene having to register
    // Wallet/PlayerState in DI. Coins + stardust are delegated to the existing CurrencyView;
    // avatar + username are driven off PlayerState here. The avatar/username Buttons are
    // exposed so a host can wire scene-specific behavior (e.g. the Planet HUD opens the
    // pick-avatar / edit-name modals), or ignore them for a display-only bar.
    public class PlayerTopBarView : MonoBehaviour
    {
        [SerializeField] private CurrencyView _currency;       // drives coins + stardust
        [SerializeField] private Image        _avatarImage;    // the avatar sprite target
        [SerializeField] private TMP_Text     _usernameText;
        [SerializeField] private Button       _avatarButton;   // optional; host wires behavior
        [SerializeField] private Button       _usernameButton; // optional; host wires behavior

        public Button AvatarButton   => _avatarButton;
        public Button UsernameButton => _usernameButton;

        private PlayerState      _playerState;
        private DatabaseRegistry _registry;

        // Wire the bar to live data. Safe to re-call; it unbinds any previous binding first.
        public void Bind(Wallet wallet, PlayerState playerState, DatabaseRegistry registry)
        {
            Unbind();

            _registry    = registry;
            _playerState = playerState;

            if (_currency != null) _currency.Bind(wallet);

            if (_playerState != null)
            {
                _playerState.OnDisplayNameChanged += SetUsername;
                _playerState.OnAvatarChanged      += SetAvatar;
                SetUsername(_playerState.DisplayName);
                SetAvatar(_playerState.AvatarId);
            }
        }

        public void Unbind()
        {
            if (_playerState != null)
            {
                _playerState.OnDisplayNameChanged -= SetUsername;
                _playerState.OnAvatarChanged      -= SetAvatar;
                _playerState = null;
            }
            if (_currency != null) _currency.Unbind();
        }

        private void OnDestroy() => Unbind();

        private void SetUsername(string displayName)
        {
            if (_usernameText != null) _usernameText.text = displayName;
        }

        private void SetAvatar(string avatarId)
        {
            if (_avatarImage == null || _registry == null) return;
            var avatar = _registry.GetAvatar(avatarId);
            if (avatar != null) _avatarImage.sprite = avatar.Sprite;
        }
    }
}
