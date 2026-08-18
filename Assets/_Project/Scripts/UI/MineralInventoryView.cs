using UnityEngine;
using UnityEngine.UI;
using VContainer;
using SocialUniverse.Core;
using SocialUniverse.Config;
using SocialUniverse.Mining;

namespace SocialUniverse.UI
{
    // Functional (unpolished) mineral inventory panel: one row per held mineral + a Sell-all
    // button. Opened from the HUD. Rebuilds on MineralInventoryChangedEvent.
    public class MineralInventoryView : MonoBehaviour
    {
        [SerializeField] private GameObject _root;          // panel container, toggled open/closed
        [SerializeField] private Transform  _rowParent;     // vertical layout group
        [SerializeField] private GameObject _rowPrefab;     // has a Text (name/qty/value) + a Sell button
        [SerializeField] private Button     _sellAllButton;
        [SerializeField] private Button     _closeButton;
        [SerializeField] private Text        _totalValueLabel;

        private MineralInventory _inventory;
        private DatabaseRegistry _registry;

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
            Rebuild();
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<MineralInventoryChangedEvent>(OnInventoryChanged);
            if (_sellAllButton != null) _sellAllButton.onClick.RemoveListener(OnSellAll);
            if (_closeButton   != null) _closeButton.onClick.RemoveListener(Close);
        }

        public void Open()  { if (_root != null) _root.SetActive(true); Rebuild(); }
        public void Close() { if (_root != null) _root.SetActive(false); }

        private void OnInventoryChanged(MineralInventoryChangedEvent _) => Rebuild();
        private void OnSellAll() => EventBus.Publish(new SellMineralsRequestedEvent { All = true });

        private void Rebuild()
        {
            // _inventory/_registry are injected at container build; OnEnable can fire earlier at
            // scene load (this component lives on an always-active host), so guard against it.
            if (_rowParent == null || _rowPrefab == null || _inventory == null || _registry == null) return;
            for (int i = _rowParent.childCount - 1; i >= 0; i--)
                Destroy(_rowParent.GetChild(i).gameObject);

            foreach (var kv in _inventory.All)
            {
                var def = _registry.GetMineral(kv.Key);
                var go  = Instantiate(_rowPrefab, _rowParent);
                var label = go.GetComponentInChildren<Text>();
                if (label != null)
                    label.text = $"{def?.DisplayName ?? kv.Key}  x{kv.Value}  ({(def != null ? def.SellValue : 0)}/ea)";
                var sell = go.GetComponentInChildren<Button>();
                if (sell != null)
                {
                    string id = kv.Key; int qty = kv.Value;
                    sell.onClick.AddListener(() => EventBus.Publish(new SellMineralsRequestedEvent { MineralId = id, Qty = qty }));
                }
            }
            if (_totalValueLabel != null)
                _totalValueLabel.text = $"Total: {_inventory.TotalSellValue(_registry)}";
        }
    }
}
