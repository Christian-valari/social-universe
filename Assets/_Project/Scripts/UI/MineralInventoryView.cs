using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using TMPro;
using SocialUniverse.Core;
using SocialUniverse.Config;
using SocialUniverse.Economy;
using SocialUniverse.Mining;

namespace SocialUniverse.UI
{
    // Mineral inventory panel: one row per mineral in the game — held or not — showing the owned
    // count, plus a Sell-all button carrying the hold's total value. Sales are confirmed in the
    // shared TransactionModal popup. Opened from the HUD. Re-binds on MineralInventoryChangedEvent.
    //
    // The row set is fixed (it comes from the registry), so rows are instantiated once and re-bound
    // afterwards. This panel refreshes on every mined mineral, and rebuilding the list each time
    // churned through a destroy/instantiate cycle for nothing.
    public class MineralInventoryView : MonoBehaviour
    {
        [SerializeField] private GameObject _root;          // panel container, toggled open/closed
        [SerializeField] private Transform     _rowParent;  // vertical layout group (a scroll view's content)
        [SerializeField] private MineralRowView _rowPrefab; // icon + name/qty/value labels + Sell button
        [SerializeField] private Button     _sellAllButton;
        [SerializeField] private Button     _closeButton;
        [SerializeField] private TMP_Text   _totalValueLabel;   // coins for selling everything, on Sell all
        [SerializeField] private TMP_Text   _heldLabel;         // "195 minerals held"
        [SerializeField] private TMP_Text   _balanceLabel;      // the player's coins: the full-screen panel covers the HUD
        [SerializeField] private TransactionModal _saleModal;   // confirm + result popup for sales

        private MineralInventory _inventory;
        private DatabaseRegistry _registry;
        private Wallet           _wallet;

        // One row per mineral, in registry order — index-aligned, built once.
        private readonly List<MineralRowView> _rows       = new();
        private readonly List<string>         _mineralIds = new();

        // What the sale popup is offering, for its success screen.
        private MineralSale _pendingSale;

        [Inject]
        public void Construct(MineralInventory inventory, DatabaseRegistry registry, Wallet wallet)
        {
            _inventory = inventory;
            _registry  = registry;
            BindWallet(wallet);
        }

        // The wallet arrives by injection, which can land after OnEnable, so its subscription lives
        // here (and in OnDestroy) rather than beside the EventBus ones. Same shape as DroneGarageView.
        private void BindWallet(Wallet wallet)
        {
            if (_wallet != null) _wallet.OnCoinsChanged -= OnCoinsChanged;
            _wallet = wallet;
            if (_wallet != null) _wallet.OnCoinsChanged += OnCoinsChanged;
        }

        private void OnDestroy() => BindWallet(null);

        private void OnCoinsChanged(int _) => UpdateHeader();

        private void OnEnable()
        {
            EventBus.Subscribe<MineralInventoryChangedEvent>(OnInventoryChanged);
            EventBus.Subscribe<MineralSaleCompletedEvent>(OnSaleCompleted);
            if (_sellAllButton != null) _sellAllButton.onClick.AddListener(OnSellAll);
            if (_closeButton   != null) _closeButton.onClick.AddListener(Close);
            Refresh();
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<MineralInventoryChangedEvent>(OnInventoryChanged);
            EventBus.Unsubscribe<MineralSaleCompletedEvent>(OnSaleCompleted);
            if (_sellAllButton != null) _sellAllButton.onClick.RemoveListener(OnSellAll);
            if (_closeButton   != null) _closeButton.onClick.RemoveListener(Close);
        }

        public void Open()  { if (_root != null) _root.SetActive(true); Refresh(); }
        public void Close() { if (_root != null) _root.SetActive(false); }

        private void OnInventoryChanged(MineralInventoryChangedEvent _) => Refresh();
        private void OnSellAll() =>
            OfferSale(MineralSale.ForAll(_inventory.TotalOwned(), _inventory.TotalSellValue(_registry)));

        // Sales are confirmed in the popup; its Confirm sends the quantity it showed.
        private void OfferSale(MineralSale sale)
        {
            if (_saleModal == null) return;

            _pendingSale = sale;
            _saleModal.Open(MineralSalePrompt.Confirm(sale), sale.Icon, MineralSalePrompt.Busy, () => EventBus.Publish(
                sale.IsAll ? new SellMineralsRequestedEvent { All = true }
                           : new SellMineralsRequestedEvent { MineralId = sale.MineralId, Qty = sale.Qty }));
        }

        // The popup is busy only between its Confirm and this result, so any other result is ignored.
        private void OnSaleCompleted(MineralSaleCompletedEvent e)
        {
            if (_saleModal == null || !_saleModal.IsBusy) return;

            if (e.Success) _saleModal.ShowSuccess(MineralSalePrompt.Success(_pendingSale));
            else           _saleModal.ShowFailure(e.Message);
        }

        private void Refresh()
        {
            // _inventory/_registry are injected at container build; OnEnable can fire earlier at
            // scene load (this component lives on an always-active host), so guard against it.
            if (_rowParent == null || _rowPrefab == null || _inventory == null || _registry == null) return;

            EnsureRows();
            BindRows();
            UpdateHeader();
        }

        // Every mineral gets a row, whether or not the player holds any, so the list reads as a
        // collection and never reshuffles as minerals are mined or sold.
        private void EnsureRows()
        {
            if (_rows.Count > 0) return;

            for (int i = _rowParent.childCount - 1; i >= 0; i--)
                Destroy(_rowParent.GetChild(i).gameObject); // clear any design-time placeholder rows

            foreach (var def in _registry.AllMinerals)
            {
                if (def == null) continue;

                var row = Instantiate(_rowPrefab, _rowParent);
                row.gameObject.SetActive(true); // the prefab root is inactive; activate the clone
                _rows.Add(row);
                _mineralIds.Add(def.MineralId);
            }
        }

        private void BindRows()
        {
            for (int i = 0; i < _rows.Count && i < _mineralIds.Count; i++)
            {
                var row = _rows[i];
                if (row == null) continue;

                string id   = _mineralIds[i];
                var    def  = _registry.GetMineral(id);
                int    qty  = _inventory.Get(id);
                string name = def?.DisplayName ?? id;
                int    unit = def != null ? def.SellValue : 0;

                // Rows outlive a single bind, so the sale is described when the button is pressed
                // rather than captured here.
                row.Bind(def?.Icon, name, def != null ? def.Tier : 1, qty, unit,
                    () => OfferSale(MineralSale.ForMineral(id, name, def?.Icon, _inventory.Get(id), unit)));
            }
        }

        private void UpdateHeader()
        {
            if (_balanceLabel != null) _balanceLabel.text = $"{(_wallet != null ? _wallet.Coins : 0)}";
            if (_inventory == null || _registry == null) return;

            int owned = _inventory.TotalOwned();

            if (_heldLabel != null)       _heldLabel.text       = $"{owned} mineral{(owned == 1 ? "" : "s")} held";
            if (_totalValueLabel != null) _totalValueLabel.text = $"{_inventory.TotalSellValue(_registry)}";

            if (_sellAllButton != null) _sellAllButton.interactable = owned > 0;
        }
    }
}
