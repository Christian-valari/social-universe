using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using TMPro;
using SocialUniverse.Core;
using SocialUniverse.Config;
using SocialUniverse.Social;
using SocialUniverse.World;

namespace SocialUniverse.UI
{
    // Read-only tile info for OwnedByPlayer/OwnedByOther/Landmark tiles, with a
    // Sell action shown only for tiles the player owns. Opened by HUDController
    // when a TileSelectedEvent arrives for a non-Available tile.
    public class TileInfoModal : MonoBehaviour
    {
        [SerializeField] private TMP_Text _titleText;
        [SerializeField] private Image    _avatarImage;
        [SerializeField] private TMP_Text _ownerInfoText;
        [SerializeField] private TMP_Text _tileStatsText;
        [SerializeField] private Button   _sellButton;
        [SerializeField] private Button   _closeButton;
        [SerializeField] private TMP_Text _statusText;

        [Inject] private ProfileService   _profileService;
        [Inject] private DatabaseRegistry _registry;

        private TileData _currentTile;

        private void Awake()
        {
            _sellButton.onClick.AddListener(OnSellClicked);
            _closeButton.onClick.AddListener(Close);
            gameObject.SetActive(false);
        }

        private void OnEnable()  => EventBus.Subscribe<TileSaleCompletedEvent>(OnTileSaleCompleted);
        private void OnDisable() => EventBus.Unsubscribe<TileSaleCompletedEvent>(OnTileSaleCompleted);

        public async void Open(TileData tile)
        {
            SetBusy(false);
            _currentTile = tile;
            _statusText.text    = "";
            _tileStatsText.text = $"Build level {tile.BuildLevel} · Yield {tile.YieldRate:0.0}/hr";
            _sellButton.gameObject.SetActive(tile.State == TileState.OwnedByPlayer);
            _ownerInfoText.gameObject.SetActive(false);
            _avatarImage.gameObject.SetActive(false);

            gameObject.SetActive(true);

            switch (tile.State)
            {
                case TileState.OwnedByPlayer:
                    _titleText.text = "Your Tile";
                    break;
                case TileState.OwnedByOther:
                    await LoadOwnerProfileAsync(tile);
                    break;
                default:
                    _titleText.text = "Landmark";
                    break;
            }
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
                _ownerInfoText.text = $"Level {profile.Level} · {profile.TilesOwned} tiles owned"
                    + (profile.Badges is { Length: > 0 } ? $"\n{string.Join(", ", profile.Badges)}" : "");

                var avatar = _registry.GetAvatar(profile.AvatarId);
                if (avatar != null)
                {
                    _avatarImage.gameObject.SetActive(true);
                    _avatarImage.sprite = avatar.Sprite;
                }
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
            _currentTile = null;
            gameObject.SetActive(false);
        }

        private void OnSellClicked()
        {
            if (_currentTile == null) return;
            SetBusy(true);
            _statusText.text = "Selling…";
            EventBus.Publish(new TileSellRequestedEvent { Tile = _currentTile });
        }

        private void OnTileSaleCompleted(TileSaleCompletedEvent e)
        {
            if (e.Tile != _currentTile) return;

            SetBusy(false);
            if (e.Success) Close();
            else _statusText.text = $"Sell failed: {e.FailureReason}";
        }

        private void SetBusy(bool busy)
        {
            _sellButton.interactable  = !busy;
            _closeButton.interactable = !busy;
        }
    }
}
