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
    // the service calls.
    //
    // Carousel rule: panels are only added/removed when the SET of cards changes (a drone acquired,
    // a slot unlocked). Every other refresh — selecting a drone, buying an upgrade, a wallet change —
    // re-binds the existing cards in place. SimpleScrollSnap re-centres on its starting panel inside
    // Setup(), and Setup() runs on every Add/Remove, so rebuilding for a state change used to throw
    // the player back to the first drone.
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

        // What the carousel currently holds, index-aligned.
        private readonly List<DroneCardKey> _cards     = new();
        private readonly List<DroneRowView> _cardViews = new();

        // Reused so a refresh doesn't allocate a pair of id lists every time.
        private readonly List<string> _ownedIds = new();
        private readonly List<string> _allIds   = new();

        // The drone the player is looking at — restored by id after a rebuild, since panel indices
        // shift when a card is added or removed.
        private string _centeredDroneId;

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
            if (_scrollSnap        != null) _scrollSnap.OnPanelCentered.AddListener(OnPanelCentered);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<DroneFleetChangedEvent>(OnFleetChanged);
            if (_unlockSlotButton != null) _unlockSlotButton.onClick.RemoveListener(OnUnlockSlot);
            if (_closeButton       != null) _closeButton.onClick.RemoveListener(Close);
            if (_scrollSnap        != null) _scrollSnap.OnPanelCentered.RemoveListener(OnPanelCentered);
        }

        public void Open()
        {
            if (_root != null) _root.SetActive(true);
            // Hidden for now: the fleet-slot purchase flow confuses users pre-launch. Re-enable when
            // slot economy is surfaced. Owned/acquirable drone cards still drive select/unlock.
            if (_unlockSlotButton != null) _unlockSlotButton.gameObject.SetActive(false);

            // Build straight away — the carousel resolves its ScrollRect lazily, so it accepts cards
            // before its own Start() runs, and on a re-open the cards are still there to re-bind.
            // The one case worth waiting for is a viewport the canvas hasn't laid out yet, because
            // SimpleScrollSnap centres its content using the viewport rect.
            if (ViewportReady()) Refresh();
            else if (isActiveAndEnabled) StartCoroutine(RefreshNextFrame());
        }

        public void Close() { if (_root != null) _root.SetActive(false); }

        private IEnumerator RefreshNextFrame()
        {
            yield return null;
            Refresh();
        }

        private bool ViewportReady()
        {
            var viewport = _scrollSnap != null ? _scrollSnap.Viewport : null;
            return viewport != null && viewport.rect.width > 0f && viewport.rect.height > 0f;
        }

        private void OnFleetChanged(DroneFleetChangedEvent _)
        {
            if (_root != null && _root.activeInHierarchy) Refresh();
        }

        // Follow the player along the carousel, and keep the plugin's starting panel in step: any
        // Setup() re-centres on it, including the one SimpleScrollSnap.Start() schedules half a
        // second out.
        private void OnPanelCentered(int centeredPanel, int selectedPanel)
        {
            if (centeredPanel < 0 || centeredPanel >= _cards.Count) return;
            _centeredDroneId = _cards[centeredPanel].DroneId;
            if (_scrollSnap != null) _scrollSnap.StartingPanel = centeredPanel;
        }

        private void OnUnlockSlot() => EventBus.Publish(new DroneSlotUnlockRequestedEvent());

        private void Refresh()
        {
            // _fleet/_registry/_config are injected at container build; guard against an early call.
            // The scroll snap only accepts panels once active (i.e. the panel is open).
            if (_registry == null || _rowPrefab == null || _scrollSnap == null || !_scrollSnap.isActiveAndEnabled) return;

            var plan = DroneCardPlan.Build(OwnedDroneIds(), AllDroneIds());
            if (!DroneCardPlan.SameCards(_cards, plan)) RebuildCards(plan);

            BindCards();
            UpdateSlotLabel();
        }

        // Only when the card set changed: tear the carousel down, re-add it, and put the player back
        // on the drone they were looking at.
        private void RebuildCards(List<DroneCardKey> plan)
        {
            string wasCentered = _centeredDroneId;

            while (_scrollSnap.NumberOfPanels > 0) _scrollSnap.RemoveFromBack();
            _cards.Clear();
            _cardViews.Clear();

            foreach (var key in plan)
            {
                var card = AddCard();
                if (card == null) continue;
                _cards.Add(key);
                _cardViews.Add(card);
            }

            int restore = DroneCardPlan.IndexOf(_cards, wasCentered);
            CenterOn(restore >= 0 ? restore : 0);

            // Our Add() calls have already run Setup(). Drop the one SimpleScrollSnap.Start() queued
            // for +0.5s, which would otherwise re-centre the carousel after we just placed it.
            _scrollSnap.CancelInvoke("Setup");
        }

        private void CenterOn(int index)
        {
            if (_scrollSnap.NumberOfPanels == 0 || _cards.Count == 0) return;

            index = Mathf.Clamp(index, 0, _cards.Count - 1);
            _scrollSnap.StartingPanel = index;
            _scrollSnap.GoToPanel(index);
            _centeredDroneId = _cards[index].DroneId;
        }

        private void BindCards()
        {
            bool slotsAvailable = _fleet.Drones.Count < _fleet.UnlockedSlots;

            for (int i = 0; i < _cards.Count && i < _cardViews.Count; i++)
            {
                var card = _cardViews[i];
                if (card == null) continue;

                string droneId = _cards[i].DroneId;

                if (_cards[i].Owned)
                {
                    var drone = _fleet.Get(droneId);
                    if (drone == null) continue;

                    var def = drone.Definition;
                    card.BindOwned(def.Icon, $"{def.DisplayName} (T{def.Tier})", droneId == _fleet.ActiveDroneId,
                        () => EventBus.Publish(new SetActiveDroneRequestedEvent { DroneId = droneId }),
                        BuildStatVms(drone, droneId));
                }
                else
                {
                    var def = _registry.GetDrone(droneId);
                    if (def == null) continue;

                    bool canBuy = slotsAvailable && _wallet != null && _wallet.CanAfford(def.UnlockCost);
                    card.BindAcquirable(def.Icon, $"{def.DisplayName} (T{def.Tier}) — {def.UnlockCost}", canBuy,
                        () => EventBus.Publish(new DroneAcquireRequestedEvent { DroneId = droneId }),
                        BuildComparison(def), DroneComparison.TierLine(def.Tier), TierDirection(def));
                }
            }
        }

        private void UpdateSlotLabel()
        {
            if (_unlockSlotLabel == null) return;

            int cost = DroneUpgradeMath.SlotUnlockCost(_config.SlotUnlockBaseCost, _config.SlotUnlockCostGrowth, _fleet.UnlockedSlots, _config.StartingFleetSlots);
            _unlockSlotLabel.text = $"{cost}";
        }

        private List<string> OwnedDroneIds()
        {
            _ownedIds.Clear();
            foreach (var drone in _fleet.Drones) _ownedIds.Add(drone.Definition.DroneId);
            return _ownedIds;
        }

        private List<string> AllDroneIds()
        {
            _allIds.Clear();
            foreach (var def in _registry.AllDrones) _allIds.Add(def.DroneId);
            return _allIds;
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
