using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using DanielLochner.Assets.SimpleScrollSnap;
using SocialUniverse.Core;
using SocialUniverse.Config;
using SocialUniverse.Economy;
using SocialUniverse.Mining;
using TMPro;

namespace SocialUniverse.UI
{
    // Functional Drone Garage: a Simple Scroll-Snap carousel of drone cards — owned drones (icon +
    // per-stat upgrade meters) and acquirable drone types (icon + acquire) — plus an unlock-slot
    // button. One card prefab (DroneRowView). Publishes intent events; DroneGarageHandler performs
    // the service calls. Rebuilds on DroneFleetChangedEvent while open.
    public class DroneGarageView : MonoBehaviour
    {
        // Stats shown per owned drone, in display order.
        private static readonly DroneStat[] UpgradeStats = { DroneStat.Cargo, DroneStat.Yield, DroneStat.Speed };

        [SerializeField] private GameObject      _root;
        [SerializeField] private SimpleScrollSnap _scrollSnap;      // carousel that hosts the cards
        [SerializeField] private DroneRowView    _rowPrefab;        // card prefab (AddToBack clones it)
        [SerializeField] private Button          _unlockSlotButton;
        [SerializeField] private TMP_Text        _unlockSlotLabel;
        [SerializeField] private Button          _closeButton;

        private DroneFleet       _fleet;
        private DatabaseRegistry _registry;
        private EconomyConfig    _config;
        private Wallet           _wallet;

        [Inject]
        public void Construct(DroneFleet fleet, DatabaseRegistry registry, EconomyConfig config, Wallet wallet)
        {
            _fleet = fleet; _registry = registry; _config = config; _wallet = wallet;
        }

        private void OnEnable()
        {
            EventBus.Subscribe<DroneFleetChangedEvent>(OnFleetChanged);
            if (_unlockSlotButton != null) _unlockSlotButton.onClick.AddListener(OnUnlockSlot);
            if (_closeButton       != null) _closeButton.onClick.AddListener(Close);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<DroneFleetChangedEvent>(OnFleetChanged);
            if (_unlockSlotButton != null) _unlockSlotButton.onClick.RemoveListener(OnUnlockSlot);
            if (_closeButton       != null) _closeButton.onClick.RemoveListener(Close);
        }

        public void Open()
        {
            if (_root != null) _root.SetActive(true);
            // Hidden for now: the fleet-slot purchase flow confuses users pre-launch. Re-enable when
            // slot economy is surfaced. Owned/acquirable drone cards still drive select/unlock.
            if (_unlockSlotButton != null) _unlockSlotButton.gameObject.SetActive(false);
            // Defer one frame so SimpleScrollSnap.Start() runs before we add panels to it.
            if (isActiveAndEnabled) StartCoroutine(RebuildNextFrame());
        }

        public void Close() { if (_root != null) _root.SetActive(false); }

        private IEnumerator RebuildNextFrame()
        {
            yield return null;
            Rebuild();
        }

        private void OnFleetChanged(DroneFleetChangedEvent _)
        {
            if (_root != null && _root.activeInHierarchy) Rebuild();
        }

        private void OnUnlockSlot() => EventBus.Publish(new DroneSlotUnlockRequestedEvent());

        private void Rebuild()
        {
            // _fleet/_registry/_config are injected at container build; guard against an early call.
            // The scroll snap only accepts panels once active + started (i.e. the panel is open).
            if (_registry == null || _rowPrefab == null || _scrollSnap == null || !_scrollSnap.isActiveAndEnabled) return;

            while (_scrollSnap.NumberOfPanels > 0) _scrollSnap.RemoveFromBack();

            foreach (var drone in _fleet.Drones)
            {
                var    def      = drone.Definition;
                string droneId  = def.DroneId;
                bool   isActive = def.DroneId == _fleet.ActiveDroneId;
                string title    = $"{def.DisplayName} (T{def.Tier})";

                var card = AddCard();
                if (card == null) continue;
                card.BindOwned(def.Icon, title, isActive,
                    () => EventBus.Publish(new SetActiveDroneRequestedEvent { DroneId = droneId }),
                    BuildStatVms(drone, droneId));
            }

            bool slotsAvailable = _fleet.Drones.Count < _fleet.UnlockedSlots;
            foreach (var def in _registry.AllDrones)
            {
                if (_fleet.Get(def.DroneId) != null) continue;
                string droneId = def.DroneId;
                bool   canBuy  = slotsAvailable && _wallet != null && _wallet.CanAfford(def.UnlockCost);
                string title   = $"{def.DisplayName} (T{def.Tier}) — {def.UnlockCost}";

                var card = AddCard();
                if (card == null) continue;
                card.BindAcquirable(def.Icon, title, canBuy,
                    () => EventBus.Publish(new DroneAcquireRequestedEvent { DroneId = droneId }),
                    BuildComparison(def), DroneComparison.TierLine(def.Tier), TierDirection(def));
            }

            if (_unlockSlotLabel != null)
            {
                int cost = DroneUpgradeMath.SlotUnlockCost(_config.SlotUnlockBaseCost, _config.SlotUnlockCostGrowth, _fleet.UnlockedSlots, _config.StartingFleetSlots);
                _unlockSlotLabel.text = $"{cost}";
            }
        }

        // AddToBack instantiates the prefab into the snap's Content and returns nothing, so the new
        // card is the last Content child. Grab its DroneRowView to bind.
        private DroneRowView AddCard()
        {
            _scrollSnap.AddToBack(_rowPrefab.gameObject);
            var content = _scrollSnap.GetComponent<ScrollRect>()?.content;
            if (content == null || content.childCount == 0) return null;
            return content.GetChild(content.childCount - 1).GetComponent<DroneRowView>();
        }

        // "Why buy this?" — how a candidate drone's base stats compare to the active drone. When there
        // is no active drone (empty fleet), fall back to the candidate's own stats (neutral, no arrows).
        private List<DroneStatDeltaVm> BuildComparison(DroneDefinition def)
        {
            var active = _fleet.Active;
            float fromCargo = active != null ? active.EffectiveCargoCap    : def.CargoCap;
            float fromYield = active != null ? active.EffectiveYieldMult   : def.YieldMultiplier;
            float fromSpeed = active != null ? active.EffectiveTravelSpeed : def.TravelSpeed;

            return new List<DroneStatDeltaVm>(3)
            {
                DroneComparison.IntStat (DroneStat.Cargo.ToString(), fromCargo, def.CargoCap),
                DroneComparison.MultStat(DroneStat.Yield.ToString(), fromYield, def.YieldMultiplier),
                DroneComparison.IntStat (DroneStat.Speed.ToString(), fromSpeed, def.TravelSpeed),
            };
        }

        private DeltaDirection TierDirection(DroneDefinition def)
        {
            var active = _fleet.Active;
            return active != null ? DroneComparison.DirectionOf(active.Definition.Tier, def.Tier) : DeltaDirection.Same;
        }

        private List<DroneStatVm> BuildStatVms(DroneRuntime drone, string droneId)
        {
            var list = new List<DroneStatVm>(UpgradeStats.Length);
            foreach (var stat in UpgradeStats)
            {
                var capturedStat = stat;
                int level        = drone.Level(stat);
                var upgradeDef   = _registry.GetUpgrade(stat);
                int maxLevel     = upgradeDef != null ? upgradeDef.MaxLevel : 0;
                bool maxed       = upgradeDef != null && level >= upgradeDef.MaxLevel;
                int cost         = DroneUpgradeMath.NextCost(upgradeDef, level);
                bool canAfford   = !maxed && upgradeDef != null && _wallet != null && _wallet.CanAfford(cost);

                list.Add(new DroneStatVm(stat, level, maxLevel, cost, maxed, canAfford,
                    () => EventBus.Publish(new DroneUpgradeRequestedEvent { DroneId = droneId, Stat = capturedStat })));
            }
            return list;
        }
    }
}
