using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using TMPro;
using SocialUniverse.Core;
using SocialUniverse.Config;
using SocialUniverse.Social;
using SocialUniverse.Economy;
using SocialUniverse.World;
using SocialUniverse.Safety;

namespace SocialUniverse.UI
{
    // Read-only tile info for OwnedByPlayer/OwnedByOther/Landmark tiles, with a Sell action and
    // a yield Claim action (both shown only for tiles the player owns). Opened by HUDController
    // when a TileSelectedEvent arrives for a non-Available tile.
    public class TileInfoModal : MonoBehaviour
    {
        [SerializeField] private TMP_Text _titleText;
        [SerializeField] private Image    _avatarImage;
        [SerializeField] private TMP_Text _ownerInfoText;
        [SerializeField] private TMP_Text _tileStatsText;
        [SerializeField] private TMP_Text _yieldText;
        [SerializeField] private Button   _sellButton;
        [SerializeField] private Button   _claimButton;
        [SerializeField] private Button   _closeButton;
        [SerializeField] private TMP_Text _statusText;
        [SerializeField] private TMP_Text _readyToClaimText;

        [Inject] private ProfileService          _profileService;
        [Inject] private DatabaseRegistry        _registry;
        [Inject] private LandRegistryService     _landRegistryService;
        [Inject] private EconomyConfig           _economyConfig;
        [Inject] private YieldEstimateCalculator _yieldEstimateCalculator;
        [Inject] private IAudioManager           _audio;

        private TileData _currentTile;

        private void Awake()
        {
            _sellButton.onClick.AddListener(OnSellClicked);
            _claimButton.onClick.AddListener(OnClaimClicked);
            _closeButton.onClick.AddListener(Close);
            gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            EventBus.Subscribe<TileSaleCompletedEvent>(OnTileSaleCompleted);
            EventBus.Subscribe<TileYieldClaimCompletedEvent>(OnTileYieldClaimCompleted);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<TileSaleCompletedEvent>(OnTileSaleCompleted);
            EventBus.Unsubscribe<TileYieldClaimCompletedEvent>(OnTileYieldClaimCompleted);
        }

        public async void Open(TileData tile)
        {
            SetBusy(false);
            CancelInvoke(nameof(RefreshYieldEstimate));
            _currentTile = tile;
            _statusText.text    = "";
            _readyToClaimText.text = "";
            _tileStatsText.text = $"{tile.BuildLevel}";

            bool ownedByPlayer = tile.State == TileState.OwnedByPlayer;
            _sellButton.gameObject.SetActive(ownedByPlayer);
            _claimButton.gameObject.SetActive(ownedByPlayer);
            _ownerInfoText.gameObject.SetActive(false);
            _avatarImage.gameObject.SetActive(false);

            _audio.PlaySfx(SfxId.OpenPanel);
            gameObject.SetActive(true);

            if (ownedByPlayer)
            {
                RefreshYieldEstimate();
                InvokeRepeating(nameof(RefreshYieldEstimate), 1f, 1f);
            }
            else
            {
                RefreshYieldEstimateOtherPlayers();
            }

            switch (tile.State)
            {
                case TileState.OwnedByPlayer:
                case TileState.OwnedByOther:
                    await LoadOwnerProfileAsync(tile);
                    break;
                default:
                    _titleText.text = "Landmark";
                    break;
            }
        }

        private void RefreshYieldEstimate()
        {
            if (_currentTile == null) return;

            var entry = _landRegistryService.GetEntry(_currentTile.TileId);
            if (entry == null) return;

            var estimate = _yieldEstimateCalculator.Compute(entry, _economyConfig, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            _yieldText.text = $"{estimate.RatePerHour:0.0}/hr";
            _readyToClaimText.text = $"{estimate.AccruedCoins} coins ready";
        }

        private void RefreshYieldEstimateOtherPlayers()
        {
            if (_currentTile == null) return;

            var entry = _landRegistryService.GetEntry(_currentTile.TileId);
            if (entry == null) return;

            var estimate = _yieldEstimateCalculator.Compute(entry, _economyConfig, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            _yieldText.text = $"{estimate.RatePerHour:0.0}/hr";
        }

        private async Task LoadOwnerProfileAsync(TileData tile)
        {
            _titleText.text = "Loading…";

            if (string.IsNullOrEmpty(tile.OwnerId))
            {
                _titleText.text = "Owned by another player";
                return;
            }

            try
            {
                var profile = await _profileService.GetProfileAsync(tile.OwnerId);
                if (_currentTile != tile) return;

                _titleText.text = $"{profile.DisplayName}'s Tile";
                _ownerInfoText.gameObject.SetActive(true);
                _ownerInfoText.text = $"{profile.TilesOwned}";

                // var avatar = _registry.GetAvatar(profile.AvatarId);
                // if (avatar != null)
                // {
                //     _avatarImage.gameObject.SetActive(true);
                //     _avatarImage.sprite = avatar.Sprite;
                // }
            }
            catch (Exception ex)
            {
                if (_currentTile != tile) return;
                _titleText.text  = "Owned by another player";
                _statusText.text = "Couldn't load profile";
                SULog.Warn($"TileInfoModal: profile fetch failed for {tile.OwnerId} — {ex.Message}", SULog.Channel.Social);
            }
        }

        public void Close()
        {
            _audio.PlaySfx(SfxId.Cancel);
            _currentTile = null;
            CancelInvoke(nameof(RefreshYieldEstimate));
            gameObject.SetActive(false);
        }

        private void OnSellClicked()
        {
            if (_currentTile == null) return;
            SetBusy(true);
            _statusText.text = "Selling…";
            EventBus.Publish(new TileSellRequestedEvent { Tile = _currentTile });
        }

        private void OnClaimClicked()
        {
            if (_currentTile == null) return;
            SetBusy(true);
            _statusText.text = "Claiming…";
            EventBus.Publish(new TileYieldClaimRequestedEvent { Tile = _currentTile });
        }

        private void OnTileSaleCompleted(TileSaleCompletedEvent e)
        {
            if (e.Tile != _currentTile) return;

            SetBusy(false);
            if (e.Success) Close();
            else _statusText.text = $"Sell failed: {e.FailureReason}";
        }

        private void OnTileYieldClaimCompleted(TileYieldClaimCompletedEvent e)
        {
            if (e.Tile != _currentTile) return;

            SetBusy(false);
            if (e.Success)
            {
                _audio.PlaySfx(SfxId.CoinsReward);
                _statusText.text = $"+{e.Granted} coins!";
                RefreshYieldEstimate();
            }
            else
            {
                _statusText.text = $"Claim failed: {e.FailureReason}";
            }
        }

        private void SetBusy(bool busy)
        {
            _sellButton.interactable  = !busy;
            _claimButton.interactable = !busy;
            _closeButton.interactable = !busy;
        }
    }
}
