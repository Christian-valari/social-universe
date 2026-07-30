using System;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using SocialUniverse.Config;
using SocialUniverse.Core;
using SocialUniverse.Economy;
using SocialUniverse.Mining;
using SocialUniverse.Net;
using SocialUniverse.World;
using SocialUniverse.Progression;
using TMPro;
using UnityEngine.Serialization;

namespace SocialUniverse.UI
{
    // Planet-scene HUD: surfaces currency and the player's current run state at a glance.
    public class HUDController : MonoBehaviour
    {
        [SerializeField] private CurrencyView _currency;
        [SerializeField] private TMP_Text _levelText;
        [SerializeField] private Slider _fuelSlider;
        [SerializeField] private Text _miningStatusText;
        [SerializeField] private Text _landStatusText;
        [SerializeField] private TMP_Text _usernameText;
        [SerializeField] private Button _usernameButton;
        [SerializeField] private DisplayNameModal _displayNameModal;
        [SerializeField] private Image _avatarImage;
        [SerializeField] private Button _avatarButton;
        [SerializeField] private AvatarSelectionModal _avatarSelectionModal;
        [SerializeField] private EmailVerificationModal _emailVerificationModal;
        [SerializeField] private Button _verifyEmailButton;
        [SerializeField] private TMP_Text _asteroidRefreshText;
        [SerializeField] private Button _chatButton;
        [SerializeField] private SocialDebugPanel _socialPanel;
        [SerializeField] private Toggle _tileViewToggle;
        [SerializeField] private TileColorizer _tileColorizer;
        [SerializeField] private TMP_Text _explorersText;
        [SerializeField] private Button _launchButton;
        [SerializeField] private TMP_Text _planetNameText;
        [SerializeField] private LandPurchaseModal _landPurchaseModal;
        [SerializeField] private TileInfoModal     _tileInfoModal;
        [SerializeField] private Button             _settingsButton;
        [SerializeField] private SettingsPanel      _settingsPanel;
        [SerializeField] private Button             _fuelButton;
        [SerializeField] private FuelPanel          _fuelPanel;


        [Inject] private Wallet _wallet;
        [Inject] private PlayerState _playerState;
        [Inject] private MiningController _mining;
        [Inject] private HexasphereManager _hexasphere;
        [Inject] private AsteroidSpawner _asteroidSpawner;
        [Inject] private IPresenceService _presence;
        [Inject] private PlanetDefinition _planet;
        [Inject] private DatabaseRegistry _registry;

        private void Start()
        {
            _currency.Bind(_wallet);
            _chatButton.onClick.AddListener(_socialPanel.Open);
            _usernameButton?.onClick.AddListener(OnUsernameClicked);
            _avatarButton?.onClick.AddListener(() =>
            {
                _avatarSelectionModal?.Open();
                _displayNameModal?.Open();
            });
            _launchButton?.onClick.AddListener(() => EventBus.Publish(new LaunchRequestedEvent()));
            _settingsButton?.onClick.AddListener(() => _settingsPanel?.Open());
            _fuelButton?.onClick.AddListener(() => _fuelPanel?.Open());

            if (_verifyEmailButton != null) _verifyEmailButton.onClick.AddListener(() => _emailVerificationModal?.Open());
            EventBus.Subscribe<ShowEmailVerificationPromptEvent>(OnShowEmailVerificationPrompt);
            EventBus.Subscribe<ShowProfileOnboardingEvent>(OnShowProfileOnboarding);
            EventBus.Subscribe<TileSelectedEvent>(OnTileSelectedForModal);

            // Tiles hidden by default; toggled by the view-land-tile toggle.
            _hexasphere.SetTilesVisible(false);
            if (_tileViewToggle != null)
            {
                _tileViewToggle.SetIsOnWithoutNotify(false);
                _tileViewToggle.onValueChanged.AddListener(OnTileViewToggled);
            }

            _playerState.OnLevelChanged       += SetLevel;
            _playerState.OnFuelChanged        += SetFuel;
            _playerState.OnDisplayNameChanged += SetUsername;
            _playerState.OnAvatarChanged      += SetAvatar;
            _presence.PresenceChanged         += RefreshExplorerCount;

            if (_planetNameText != null) _planetNameText.text = _planet.DisplayName;

            SetLevel(_playerState.Level);
            SetFuel(_playerState.Fuel);
            SetUsername(_playerState.DisplayName);
            SetAvatar(_playerState.AvatarId);
            RefreshMiningStatus();
            RefreshLandStatus();
            RefreshAsteroidRefresh();
            RefreshExplorerCount();
        }

        private void OnDestroy()
        {
            _playerState.OnLevelChanged       -= SetLevel;
            _playerState.OnFuelChanged        -= SetFuel;
            _playerState.OnDisplayNameChanged -= SetUsername;
            _playerState.OnAvatarChanged      -= SetAvatar;
            _presence.PresenceChanged         -= RefreshExplorerCount;
            EventBus.Unsubscribe<ShowEmailVerificationPromptEvent>(OnShowEmailVerificationPrompt);
            EventBus.Unsubscribe<ShowProfileOnboardingEvent>(OnShowProfileOnboarding);
            EventBus.Unsubscribe<TileSelectedEvent>(OnTileSelectedForModal);
        }

        private void OnShowEmailVerificationPrompt(ShowEmailVerificationPromptEvent _)
        {
            _emailVerificationModal?.Open();
        }

        private void OnShowProfileOnboarding(ShowProfileOnboardingEvent _)
        {
            // Mirrors the HUD avatar-button flow (opens both modals together), but in
            // mandatory onboarding mode: Cancel hidden, a valid name required.
            _avatarSelectionModal?.OpenForOnboarding();
            _displayNameModal?.OpenForOnboarding();
        }

        private void OnTileSelectedForModal(TileSelectedEvent e)
        {
            var tile = e.Tile;
            if (tile.State == TileState.Available)
                _landPurchaseModal?.Open(tile);
            else
                _tileInfoModal?.Open(tile);
        }

        private void Update()
        {
            // Cargo amount and owned-tile count have no change events — cheap to poll each frame.
            RefreshMiningStatus();
            RefreshLandStatus();
            RefreshAsteroidRefresh();
        }

        private void SetUsername(string name)
        {
            if (_usernameText != null) _usernameText.text = name;
        }

        private void OnUsernameClicked()
        {
            _displayNameModal?.Open();
        }

        private void OnTileViewToggled(bool visible)
        {
            _hexasphere.SetTilesVisible(visible);

            // Toggling the Hexasphere style regenerates its mesh and drops custom tile
            // materials, so owned/landmark/etc. colors must be reapplied each time tiles show.
            if (visible) _tileColorizer?.Refresh(_hexasphere.Tiles);
        }

        private void SetAvatar(string avatarId)
        {
            if (_avatarImage == null) return;
            var avatar = _registry.GetAvatar(avatarId);
            if (avatar != null) _avatarImage.sprite = avatar.Sprite;
        }

        private void RefreshAsteroidRefresh()
        {
            if (_asteroidRefreshText == null) return;

            var next = _asteroidSpawner.NextRespawnUtc;
            if (next == null)
            {
                _asteroidRefreshText.text = "Asteroids: Ready";
                return;
            }

            var remaining = next.Value - DateTime.UtcNow;
            if (remaining <= TimeSpan.Zero)
            {
                _asteroidRefreshText.text = "Asteroids: Ready";
                return;
            }

            _asteroidRefreshText.text = remaining.TotalHours >= 1
                ? $"Next asteroid: {(int)remaining.TotalHours}h {remaining.Minutes}m"
                : $"Next asteroid: {remaining.Minutes}m {remaining.Seconds}s";
        }

        private void SetLevel(int level) => _levelText.text = $"{level}";

        private void SetFuel(float fuel) =>
            _fuelSlider.value = Mathf.CeilToInt(fuel);

        private void RefreshMiningStatus()
        {
            if (_miningStatusText == null) return;
            var session = _mining.CurrentIdleSession;
            if (session != null)
            {
                _miningStatusText.text = session.Stage switch
                {
                    IdleMiningStage.Traveling    => $"Heading to {session.Asteroid.Definition.MineralType} asteroid...",
                    IdleMiningStage.Mining       => $"Mining {session.Asteroid.Definition.MineralType}: {Mathf.RoundToInt(session.MiningProgress01 * 100f)}%",
                    IdleMiningStage.ReadyToClaim => "Tap the asteroid to claim!",
                    _                            => "Mining: —"
                };
                return;
            }

            _miningStatusText.text = "Mining: —";
        }

        private void RefreshLandStatus()
        {
            if (_landStatusText == null) return;
            int owned = 0;
            foreach (var kv in _hexasphere.Tiles)
                if (kv.Value.State == TileState.OwnedByPlayer) owned++;

            _landStatusText.text = $"Tiles owned: {owned}";
        }

        private void RefreshExplorerCount()
        {
            if (_explorersText == null) return;
            _explorersText.text = $"{_presence.Players.Count} explorers here";
        }
    }
}
