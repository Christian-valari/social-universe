using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
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
    // per-stat upgrade meters) and acquirable drone types (icon + acquire) — plus a buy button that
    // follows the centered card (DroneBuyButton) and a header with the balance and fleet size. One
    // card prefab (DroneRowView). Publishes intent events; DroneGarageHandler performs the service calls.
    //
    // Carousel rule: panels are only added/removed when the SET of cards changes (a drone acquired,
    // a slot unlocked). Every other refresh — selecting a drone, buying an upgrade, a wallet change —
    // re-binds the existing cards in place. SimpleScrollSnap re-centres on its starting panel inside
    // Setup(), and Setup() runs on every Add/Remove, so rebuilding for a state change used to throw
    // the player back to the first drone.
    public class DroneGarageView : MonoBehaviour
    {
        // Upgrade tracks offered per drone, in display order. Cargo is left out on purpose (Known
        // Issue #15): nothing in the mining loop caps a claim by cargo, so buying it did nothing.
        // Meters for stats not listed here are hidden on the card (DroneRowView).
        public static readonly IReadOnlyList<DroneStat> OfferedUpgradeStats = new[] { DroneStat.Yield, DroneStat.Speed };

        [SerializeField] private GameObject      _root;
        [SerializeField] private SimpleScrollSnap _scrollSnap;      // carousel that hosts the cards
        [SerializeField] private DroneRowView    _rowPrefab;        // card prefab (AddToBack clones it)
        [FormerlySerializedAs("_unlockSlotButton")]
        [SerializeField] private Button          _buyButton;        // buys the centered drone, or the next slot
        [FormerlySerializedAs("_unlockSlotLabel")]
        [SerializeField] private TMP_Text        _buyLabel;         // the price
        [SerializeField] private TMP_Text        _buyCaption;       // "Buy" / "Unlock slot", before the coin and price
        [SerializeField] private TMP_Text        _balanceLabel;     // the player's coins: the full-screen garage covers the HUD
        [SerializeField] private TMP_Text        _fleetLabel;       // "Fleet 2/3"
        [SerializeField] private Button          _closeButton;
        [SerializeField] private TransactionModal _purchaseModal;   // confirm + result popup for drone/slot purchases

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

        // What the purchase popup is offering, for its success screen.
        private DronePurchase _pendingPurchase;

        [Inject]
        public void Construct(DroneFleet fleet, DatabaseRegistry registry, EconomyConfig config, Wallet wallet)
        {
            _fleet = fleet; _registry = registry; _config = config;
            BindWallet(wallet);
        }

        // The wallet arrives by injection, which can land after OnEnable, so its subscription lives
        // here (and in OnDestroy) rather than beside the EventBus one. Same shape as CurrencyView.Bind.
        private void BindWallet(Wallet wallet)
        {
            if (_wallet != null) _wallet.OnCoinsChanged -= OnCoinsChanged;
            _wallet = wallet;
            if (_wallet != null) _wallet.OnCoinsChanged += OnCoinsChanged;
        }

        private void OnDestroy() => BindWallet(null);

        private void OnEnable()
        {
            EventBus.Subscribe<DroneFleetChangedEvent>(OnFleetChanged);
            EventBus.Subscribe<DronePurchaseCompletedEvent>(OnPurchaseCompleted);
            if (_buyButton         != null) _buyButton.onClick.AddListener(OnBuy);
            if (_closeButton       != null) _closeButton.onClick.AddListener(Close);
            if (_scrollSnap        != null) _scrollSnap.OnPanelCentered.AddListener(OnPanelCentered);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<DroneFleetChangedEvent>(OnFleetChanged);
            EventBus.Unsubscribe<DronePurchaseCompletedEvent>(OnPurchaseCompleted);
            if (_buyButton         != null) _buyButton.onClick.RemoveListener(OnBuy);
            if (_closeButton       != null) _closeButton.onClick.RemoveListener(Close);
            if (_scrollSnap        != null) _scrollSnap.OnPanelCentered.RemoveListener(OnPanelCentered);
        }

        public void Open()
        {
            if (_root != null) _root.SetActive(true);
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

        private void OnFleetChanged(DroneFleetChangedEvent _) => RefreshIfOpen();

        // Re-price every button. DroneService applies a server reply's fleet before its balance, so
        // the fleet-changed refresh still sees the old coins; this one corrects it.
        private void OnCoinsChanged(int _) => RefreshIfOpen();

        private void RefreshIfOpen()
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
            UpdateArrows(centeredPanel);
            UpdateBuyButton();
        }

        // An arrow with nowhere to go reads as a broken button: none before the first card, none
        // after the last.
        private void UpdateArrows(int centeredPanel)
        {
            if (_scrollSnap == null) return;
            if (_scrollSnap.PreviousButton != null) _scrollSnap.PreviousButton.gameObject.SetActive(centeredPanel > 0);
            if (_scrollSnap.NextButton     != null) _scrollSnap.NextButton.gameObject.SetActive(centeredPanel < _cards.Count - 1);
        }

        // Re-derived at click time so it acts on the card centered now, not when the label was drawn.
        // Drone and slot purchases are confirmed in the popup; its Confirm sends the request.
        private void OnBuy()
        {
            var state = CenteredBuyState(out string droneId);
            if (!state.Visible || _purchaseModal == null) return;

            DronePurchase purchase;
            if (state.UnlocksSlot)
            {
                purchase = DronePurchase.ForSlot(state.Cost, _fleet.UnlockedSlots);
            }
            else
            {
                var def = _registry.GetDrone(droneId);
                purchase = DronePurchase.ForDrone(def.DroneId, def.DisplayName, def.Tier, def.Icon, state.Cost);
            }

            _pendingPurchase = purchase;
            _purchaseModal.Open(DronePurchasePrompt.Confirm(purchase, _wallet?.Coins ?? 0), purchase.Icon,
                DronePurchasePrompt.Busy, () => RequestPurchase(purchase));
        }

        private static void RequestPurchase(DronePurchase purchase)
        {
            if (purchase.IsSlot) EventBus.Publish(new DroneSlotUnlockRequestedEvent());
            else                 EventBus.Publish(new DroneAcquireRequestedEvent { DroneId = purchase.DroneId });
        }

        // The popup is busy only between its Confirm and this result, so any other result is ignored.
        private void OnPurchaseCompleted(DronePurchaseCompletedEvent e)
        {
            if (_purchaseModal == null || !_purchaseModal.IsBusy || e.DroneId != _pendingPurchase.DroneId) return;

            if (e.Success) _purchaseModal.ShowSuccess(DronePurchasePrompt.Success(_pendingPurchase));
            else           _purchaseModal.ShowFailure(e.Message);
        }

        private void Refresh()
        {
            // _fleet/_registry/_config are injected at container build; guard against an early call.
            // The scroll snap only accepts panels once active (i.e. the panel is open).
            if (_registry == null || _rowPrefab == null || _scrollSnap == null || !_scrollSnap.isActiveAndEnabled) return;

            var plan = DroneCardPlan.Build(OwnedDroneIds(), AllDroneIds());
            if (!DroneCardPlan.SameCards(_cards, plan)) RebuildCards(plan);

            BindCards();
            UpdateHeader();
            UpdateBuyButton();
        }

        private void UpdateHeader()
        {
            if (_balanceLabel != null) _balanceLabel.text = $"{(_wallet != null ? _wallet.Coins : 0)}";
            if (_fleetLabel   != null) _fleetLabel.text   = $"Fleet {_fleet.Drones.Count}/{_fleet.UnlockedSlots}";
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
            UpdateArrows(index);
        }

        private void BindCards()
        {
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
                    card.BindOwned(def.Icon, def.DisplayName, def.Tier, droneId == _fleet.ActiveDroneId,
                        () => EventBus.Publish(new SetActiveDroneRequestedEvent { DroneId = droneId }),
                        BuildStatVms(drone, droneId));
                }
                else
                {
                    var def = _registry.GetDrone(droneId);
                    if (def == null) continue;

                    // No price on the card: the buy button below shows it, or the slot it needs first.
                    card.BindAcquirable(def.Icon, def.DisplayName, def.Tier,
                        BuildComparison(def), DroneComparison.TierLine(def.Tier), TierDirection(def));
                }
            }
        }

        private void UpdateBuyButton()
        {
            if (_buyButton == null) return;

            var state = CenteredBuyState(out _);
            _buyButton.gameObject.SetActive(state.Visible);
            if (!state.Visible) return;

            _buyButton.interactable = state.Interactable;
            if (_buyCaption != null) _buyCaption.text = DroneBuyButton.Caption(state);
            if (_buyLabel   != null) _buyLabel.text   = $"{state.Cost}";
        }

        // Hidden (default) until a card is centered, e.g. before the first build.
        private DroneBuyButtonState CenteredBuyState(out string droneId)
        {
            droneId = null;
            if (_registry == null || _fleet == null || _config == null) return default;

            int index = DroneCardPlan.IndexOf(_cards, _centeredDroneId);
            if (index < 0) return default;

            var def = _registry.GetDrone(_cards[index].DroneId);
            if (def == null) return default;

            droneId = def.DroneId;
            int slotCost = DroneUpgradeMath.SlotUnlockCost(_config.SlotUnlockBaseCost, _config.SlotUnlockCostGrowth, _fleet.UnlockedSlots, _config.StartingFleetSlots);
            return DroneBuyButton.For(_cards[index].Owned, def.UnlockCost, _fleet.Drones.Count, _fleet.UnlockedSlots, slotCost,
                _wallet != null ? _wallet.Coins : 0);
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
            float fromYield = active != null ? active.EffectiveYieldMult   : def.YieldMultiplier;
            float fromSpeed = active != null ? active.EffectiveTravelSpeed : def.TravelSpeed;

            return new List<DroneStatDeltaVm>(2)
            {
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
            var list = new List<DroneStatVm>(OfferedUpgradeStats.Count);
            foreach (var stat in OfferedUpgradeStats)
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
