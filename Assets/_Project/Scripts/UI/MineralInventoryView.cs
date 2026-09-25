using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using SocialUniverse.Core;
using SocialUniverse.Config;
using SocialUniverse.Mining;

namespace SocialUniverse.UI
{
    // Mineral inventory panel: one row per mineral in the game — held or not — showing the owned
    // count, plus a Sell-all button. Opened from the HUD. Re-binds on MineralInventoryChangedEvent.
    //
    // The row set is fixed (it comes from the registry), so rows are instantiated once and re-bound
    // afterwards. This panel refreshes on every mined mineral, and rebuilding the list each time
    // churned through a destroy/instantiate cycle for nothing.
    public class MineralInventoryView : MonoBehaviour
    {
        [SerializeField] private GameObject _root;          // panel container, toggled open/closed
        [SerializeField] private Transform     _rowParent;  // vertical layout group
        [SerializeField] private MineralRowView _rowPrefab; // icon + name/qty/value label + Sell button
        [SerializeField] private Button     _sellAllButton;
        [SerializeField] private Button     _closeButton;
        [SerializeField] private Text        _totalValueLabel;

        private MineralInventory _inventory;
        private DatabaseRegistry _registry;

        // One row per mineral, in registry order — index-aligned, built once.
        private readonly List<MineralRowView> _rows       = new();
        private readonly List<string>         _mineralIds = new();

        [Inject]
        public void Construct(MineralInventory inventory, DatabaseRegistry registry)
        {
            _inventory = inventory;
            _registry  = registry;
        }

        private void OnEnable()
        {
            EventBus.Subscribe<MineralInventoryChangedEvent>(OnInventoryChanged);
            if (_sellAllButton != null) _sellAllButton.onClick.AddListener(OnSellAll);
            if (_closeButton   != null) _closeButton.onClick.AddListener(Close);
            Refresh();
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<MineralInventoryChangedEvent>(OnInventoryChanged);
            if (_sellAllButton != null) _sellAllButton.onClick.RemoveListener(OnSellAll);
            if (_closeButton   != null) _closeButton.onClick.RemoveListener(Close);
        }

        public void Open()  { if (_root != null) _root.SetActive(true); Refresh(); }
        public void Close() { if (_root != null) _root.SetActive(false); }

        private void OnInventoryChanged(MineralInventoryChangedEvent _) => Refresh();
        private void OnSellAll() => EventBus.Publish(new SellMineralsRequestedEvent { All = true });

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

                string id  = _mineralIds[i];
                var    def = _registry.GetMineral(id);
                int    qty = _inventory.Get(id);

                string label = $"{def?.DisplayName ?? id}  x{qty}  ({(def != null ? def.SellValue : 0)}/ea)";

                // Rows outlive a single bind now, so the sell amount is read when the button is
                // pressed rather than captured here.
                row.Bind(def?.Icon, label, qty > 0,
                    () => EventBus.Publish(new SellMineralsRequestedEvent { MineralId = id, Qty = _inventory.Get(id) }));
            }
        }

        private void UpdateHeader()
        {
            int owned = _inventory.TotalOwned();

            if (_totalValueLabel != null)
                _totalValueLabel.text = $"{owned} mineral{(owned == 1 ? "" : "s")}  ·  Total: {_inventory.TotalSellValue(_registry)}";

            if (_sellAllButton != null) _sellAllButton.interactable = owned > 0;
        }
    }
}
