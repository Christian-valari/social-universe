using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using SocialUniverse.Core;
using SocialUniverse.Config;
using SocialUniverse.Economy;
using SocialUniverse.Mining;

namespace SocialUniverse.UI
{
    // Functional (unpolished) Drone Garage: a single unified list of rows — owned drones (active
    // marker + per-stat upgrade buttons) and acquirable drone types (acquire button) — plus an
    // unlock-slot button. One row prefab (DroneRowView) + one parent, so every row shares a layout.
    // Publishes intent events; DroneGarageHandler performs the service calls. Rebuilds on
    // DroneFleetChangedEvent.
    public class DroneGarageView : MonoBehaviour
    {
        // Stats shown per owned drone, in display order.
        private static readonly DroneStat[] UpgradeStats = { DroneStat.Cargo, DroneStat.Yield, DroneStat.Speed };

        [SerializeField] private GameObject   _root;
        [SerializeField] private Transform    _rowParent;        // single unified list parent
        [SerializeField] private DroneRowView _rowPrefab;        // single unified row prefab
        [SerializeField] private Button       _unlockSlotButton;
        [SerializeField] private Text         _unlockSlotLabel;
        [SerializeField] private Button       _closeButton;

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
            // _fleet/_registry/_config are injected at container build; OnEnable can fire earlier at
            // scene load (this lives on an always-active host), so guard against it.
            if (_registry == null || _rowParent == null || _rowPrefab == null) return;
            ClearChildren(_rowParent);

            // Owned drones.
            foreach (var drone in _fleet.Drones)
            {
                var    def      = drone.Definition;
                string droneId  = def.DroneId;
                bool   isActive = def.DroneId == _fleet.ActiveDroneId;
                string title    = $"{def.DisplayName} (T{def.Tier}){(isActive ? "  [ACTIVE]" : "")}\n" +
                                  $"Cargo {drone.EffectiveCargoCap}  Yield {drone.EffectiveYieldMult:0.00}  Speed {drone.EffectiveTravelSpeed:0.0}";

                var row = Instantiate(_rowPrefab, _rowParent);
                row.gameObject.SetActive(true); // template is an inactive prefab/clone source
                row.BindOwned(title, isActive,
                    () => EventBus.Publish(new SetActiveDroneRequestedEvent { DroneId = droneId }),
                    BuildUpgradeVms(drone, droneId));
            }

            // Acquirable drone types (in registry, not yet owned).
            bool slotsAvailable = _fleet.Drones.Count < _fleet.UnlockedSlots;
            foreach (var def in _registry.AllDrones)
            {
                if (_fleet.Get(def.DroneId) != null) continue;
                string droneId = def.DroneId;
                string title   = $"{def.DisplayName} (T{def.Tier}) — {def.UnlockCost}";

                var row = Instantiate(_rowPrefab, _rowParent);
                row.gameObject.SetActive(true); // template is an inactive prefab/clone source
                row.BindAcquirable(title, slotsAvailable,
                    () => EventBus.Publish(new DroneAcquireRequestedEvent { DroneId = droneId }));
            }

            if (_unlockSlotLabel != null)
            {
                int cost = DroneUpgradeMath.SlotUnlockCost(_config.SlotUnlockBaseCost, _config.SlotUnlockCostGrowth, _fleet.UnlockedSlots, _config.StartingFleetSlots);
                _unlockSlotLabel.text = $"Unlock slot — {cost}";
            }
        }

        private List<DroneUpgradeVm> BuildUpgradeVms(DroneRuntime drone, string droneId)
        {
            var list = new List<DroneUpgradeVm>(UpgradeStats.Length);
            foreach (var stat in UpgradeStats)
            {
                var capturedStat = stat;
                int level        = drone.Level(stat);
                var upgradeDef   = _registry.GetUpgrade(stat);
                int cost         = DroneUpgradeMath.NextCost(upgradeDef, level);
                bool maxed       = upgradeDef != null && level >= upgradeDef.MaxLevel;
                string caption   = maxed ? $"{stat} MAX" : $"{stat} {level}→{level + 1} ({cost})";

                list.Add(new DroneUpgradeVm(stat, caption, !maxed && upgradeDef != null,
                    () => EventBus.Publish(new DroneUpgradeRequestedEvent { DroneId = droneId, Stat = capturedStat })));
            }
            return list;
        }

        private static void ClearChildren(Transform parent)
        {
            if (parent == null) return;
            for (int i = parent.childCount - 1; i >= 0; i--) Destroy(parent.GetChild(i).gameObject);
        }
    }
}
