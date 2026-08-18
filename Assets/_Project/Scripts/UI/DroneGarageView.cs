using UnityEngine;
using UnityEngine.UI;
using VContainer;
using SocialUniverse.Core;
using SocialUniverse.Config;
using SocialUniverse.Economy;
using SocialUniverse.Mining;

namespace SocialUniverse.UI
{
    // Functional (unpolished) Drone Garage: owned drones (active marker + per-stat upgrade rows),
    // acquirable drone types, and an unlock-slot button. Publishes intent events; the
    // DroneGarageHandler performs the service calls. Rebuilds on DroneFleetChangedEvent.
    public class DroneGarageView : MonoBehaviour
    {
        [SerializeField] private GameObject _root;
        [SerializeField] private Transform  _ownedParent;      // rows for owned drones
        [SerializeField] private Transform  _acquireParent;    // rows for acquirable drone types
        [SerializeField] private GameObject _ownedRowPrefab;   // Text + N buttons (set-active + 3 upgrade)
        [SerializeField] private GameObject _acquireRowPrefab; // Text + Acquire button
        [SerializeField] private Button     _unlockSlotButton;
        [SerializeField] private Text        _unlockSlotLabel;
        [SerializeField] private Button     _closeButton;

        private DroneFleet       _fleet;
        private DatabaseRegistry _registry;
        private EconomyConfig    _config;

        [Inject]
        public void Construct(DroneFleet fleet, DatabaseRegistry registry, EconomyConfig config)
        {
            _fleet = fleet; _registry = registry; _config = config;
        }

        private void OnEnable()
        {
            EventBus.Subscribe<DroneFleetChangedEvent>(OnFleetChanged);
            if (_unlockSlotButton != null) _unlockSlotButton.onClick.AddListener(OnUnlockSlot);
            if (_closeButton       != null) _closeButton.onClick.AddListener(Close);
            Rebuild();
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<DroneFleetChangedEvent>(OnFleetChanged);
            if (_unlockSlotButton != null) _unlockSlotButton.onClick.RemoveListener(OnUnlockSlot);
            if (_closeButton       != null) _closeButton.onClick.RemoveListener(Close);
        }

        public void Open()  { if (_root != null) _root.SetActive(true); Rebuild(); }
        public void Close() { if (_root != null) _root.SetActive(false); }

        private void OnFleetChanged(DroneFleetChangedEvent _) => Rebuild();
        private void OnUnlockSlot() => EventBus.Publish(new DroneSlotUnlockRequestedEvent());

        private void Rebuild()
        {
            if (_registry == null) return;
            ClearChildren(_ownedParent);
            ClearChildren(_acquireParent);

            // Owned drones
            foreach (var drone in _fleet.Drones)
            {
                var def = drone.Definition;
                var go  = Instantiate(_ownedRowPrefab, _ownedParent);
                var label = go.GetComponentInChildren<Text>();
                bool isActive = def.DroneId == _fleet.ActiveDroneId;
                if (label != null)
                    label.text = $"{def.DisplayName} (T{def.Tier}){(isActive ? "  [ACTIVE]" : "")}\n" +
                                 $"Cargo {drone.EffectiveCargoCap}  Yield {drone.EffectiveYieldMult:0.00}  Speed {drone.EffectiveTravelSpeed:0.0}";

                // Wire buttons by name convention on the prefab. Expected child buttons:
                //   "SetActive", "UpgradeCargo", "UpgradeYield", "UpgradeSpeed".
                Wire(go, "SetActive", () => EventBus.Publish(new SetActiveDroneRequestedEvent { DroneId = def.DroneId }));
                WireUpgrade(go, "UpgradeCargo", def.DroneId, DroneStat.Cargo, drone.Level(DroneStat.Cargo));
                WireUpgrade(go, "UpgradeYield", def.DroneId, DroneStat.Yield, drone.Level(DroneStat.Yield));
                WireUpgrade(go, "UpgradeSpeed", def.DroneId, DroneStat.Speed, drone.Level(DroneStat.Speed));
            }

            // Acquirable drone types (in registry, not yet owned, and slots available)
            bool slotsAvailable = _fleet.Drones.Count < _fleet.UnlockedSlots;
            foreach (var def in _registry.AllDrones)
            {
                if (_fleet.Get(def.DroneId) != null) continue;
                var go  = Instantiate(_acquireRowPrefab, _acquireParent);
                var label = go.GetComponentInChildren<Text>();
                if (label != null) label.text = $"{def.DisplayName} (T{def.Tier}) — {def.UnlockCost}";
                Wire(go, null, () => EventBus.Publish(new DroneAcquireRequestedEvent { DroneId = def.DroneId }));
                var btn = go.GetComponentInChildren<Button>();
                if (btn != null) btn.interactable = slotsAvailable;
            }

            if (_unlockSlotLabel != null)
            {
                int cost = DroneUpgradeMath.SlotUnlockCost(_config.SlotUnlockBaseCost, _config.SlotUnlockCostGrowth, _fleet.UnlockedSlots, _config.StartingFleetSlots);
                _unlockSlotLabel.text = $"Unlock slot — {cost}";
            }
        }

        private void WireUpgrade(GameObject row, string childName, string droneId, DroneStat stat, int level)
        {
            var upgradeDef = _registry.GetUpgrade(stat);
            int cost = DroneUpgradeMath.NextCost(upgradeDef, level);
            bool maxed = upgradeDef != null && level >= upgradeDef.MaxLevel;
            var btn = FindButton(row, childName);
            if (btn == null) return;
            var t = btn.GetComponentInChildren<Text>();
            if (t != null) t.text = maxed ? $"{stat} MAX" : $"{stat} {level}→{level + 1} ({cost})";
            btn.interactable = !maxed && upgradeDef != null;
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => EventBus.Publish(new DroneUpgradeRequestedEvent { DroneId = droneId, Stat = stat }));
        }

        private static void Wire(GameObject row, string childName, UnityEngine.Events.UnityAction action)
        {
            var btn = childName == null ? row.GetComponentInChildren<Button>() : FindButton(row, childName);
            if (btn == null) return;
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(action);
        }

        private static Button FindButton(GameObject row, string childName)
        {
            foreach (var b in row.GetComponentsInChildren<Button>(true))
                if (b.gameObject.name == childName) return b;
            return null;
        }

        private static void ClearChildren(Transform parent)
        {
            if (parent == null) return;
            for (int i = parent.childCount - 1; i >= 0; i--) Destroy(parent.GetChild(i).gameObject);
        }
    }
}
