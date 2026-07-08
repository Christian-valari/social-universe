using UnityEngine;
using UnityEngine.UI;
using VContainer;
using TMPro;
using SocialUniverse.Core;
using SocialUniverse.Config;
using SocialUniverse.Economy;
using SocialUniverse.World;

namespace SocialUniverse.UI
{
    // Confirmation modal for purchasing an Available tile. Opened by HUDController
    // when a TileSelectedEvent arrives for an Available tile; this is what replaced
    // the old auto-buy-on-select behavior in TilePurchaseHandler.
    public class LandPurchaseModal : MonoBehaviour
    {
        [SerializeField] private TMP_Text _priceText;
        [SerializeField] private TMP_Text _balanceText;
        [SerializeField] private TMP_Text _statusText;
        [SerializeField] private Button   _confirmButton;
        [SerializeField] private Button   _cancelButton;

        [Inject] private Wallet           _wallet;
        [Inject] private PlanetDefinition _planet;
        [Inject] private EconomyConfig    _economyConfig;

        private TileData _currentTile;

        private void Awake()
        {
            _confirmButton.onClick.AddListener(OnConfirmClicked);
            _cancelButton.onClick.AddListener(Close);
            gameObject.SetActive(false);
        }

        private void OnEnable()  => EventBus.Subscribe<TilePurchaseCompletedEvent>(OnTilePurchaseCompleted);
        private void OnDisable() => EventBus.Unsubscribe<TilePurchaseCompletedEvent>(OnTilePurchaseCompleted);

        public void Open(TileData tile)
        {
            SetBusy(false);
            _currentTile = tile;

            int  price     = Mathf.RoundToInt(_economyConfig.BaseLandPrice * _planet.LandPriceMultiplier);
            bool canAfford = _wallet.CanAfford(price);

            _priceText.text   = $"{price} coins";
            _balanceText.text = $"Balance: {_wallet.Coins} coins";
            _statusText.text  = canAfford ? "" : "Not enough coins";
            _confirmButton.interactable = canAfford;

            gameObject.SetActive(true);
        }

        public void Close()
        {
            _currentTile = null;
            gameObject.SetActive(false);
        }

        private void OnConfirmClicked()
        {
            if (_currentTile == null) return;
            SetBusy(true);
            _statusText.text = "Purchasing…";
            EventBus.Publish(new TilePurchaseConfirmedEvent { Tile = _currentTile });
        }

        private void OnTilePurchaseCompleted(TilePurchaseCompletedEvent e)
        {
            if (e.Tile != _currentTile) return;

            SetBusy(false);
            if (e.Success)
            {
                _statusText.text = "Purchased!";
                Close();
            }
            else
            {
                _statusText.text = e.FailureReason;
            }
        }

        private void SetBusy(bool busy)
        {
            _confirmButton.interactable = !busy;
            _cancelButton.interactable  = !busy;
        }
    }
}
