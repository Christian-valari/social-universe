using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using DanielLochner.Assets.SimpleScrollSnap;
using TMPro;
using SocialUniverse.App;
using SocialUniverse.Config;
using SocialUniverse.Core;
using SocialUniverse.Economy;
using SocialUniverse.Mining;
using SocialUniverse.UI;

namespace SocialUniverse.Tests
{
    // The Drone Garage in the real Planet scene (parent scope: PlanetTestRootScope, as in
    // PlanetSceneFlowTests). Covers what the pure DroneBuyButton tests can't: that the open garage
    // re-prices its buy button when the balance changes.
    public class DroneGarageFlowTests
    {
        private const string PlanetScenePath = "Assets/Scenes/Planet.unity";

        private GameObject        _rootScopeObject;
        private FakeBackendClient _backend;
        private DroneGarageView   _garage;
        private DroneFleet       _fleet;
        private Wallet           _wallet;
        private DatabaseRegistry _registry;
        private EconomyConfig    _config;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _backend = new FakeBackendClient();
            PlanetTestRootScope.Backend = _backend;
            _rootScopeObject = new GameObject("PlanetTestRootScope");
            UnityEngine.Object.DontDestroyOnLoad(_rootScopeObject);
            _rootScopeObject.AddComponent<PlanetTestRootScope>();

            bool sceneReady = false;
            Action<SceneReadyEvent> onReady = _ => sceneReady = true;
            EventBus.Subscribe(onReady);

            yield return SceneManager.LoadSceneAsync(PlanetScenePath, LoadSceneMode.Single);

            var scope = UnityEngine.Object.FindFirstObjectByType<PlanetSceneScope>();
            Assert.IsNotNull(scope?.Container, "PlanetSceneScope container not built");

            _garage   = scope.Container.Resolve(typeof(DroneGarageView)) as DroneGarageView;
            _fleet    = scope.Container.Resolve(typeof(DroneFleet)) as DroneFleet;
            _wallet   = scope.Container.Resolve(typeof(Wallet)) as Wallet;
            _registry = scope.Container.Resolve(typeof(DatabaseRegistry)) as DatabaseRegistry;
            _config   = scope.Container.Resolve(typeof(EconomyConfig)) as EconomyConfig;

            float timeout = Time.realtimeSinceStartup + 10f;
            while (!sceneReady && Time.realtimeSinceStartup < timeout)
                yield return null;
            EventBus.Unsubscribe(onReady);
            Assert.IsTrue(sceneReady, "Planet scene never published SceneReadyEvent");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            PlanetTestRootScope.Backend = null;
            if (_rootScopeObject != null) UnityEngine.Object.Destroy(_rootScopeObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Buy_button_rechecks_affordability_when_the_balance_arrives_after_the_fleet()
        {
            // A full fleet: two drones in two slots, so a locked drone's card offers the next slot.
            var owned = _registry.AllDrones.Take(2).Select(d => d.DroneId).ToList();
            _fleet.Apply(Fleet(slots: 2, owned), _registry);

            int slotCost = DroneUpgradeMath.SlotUnlockCost(_config.SlotUnlockBaseCost, _config.SlotUnlockCostGrowth, 2, _config.StartingFleetSlots);
            var target   = _registry.AllDrones.FirstOrDefault(d => !owned.Contains(d.DroneId) && d.UnlockCost > slotCost);
            Assert.IsNotNull(target, "Expected a locked drone that costs more than a slot");

            // Exactly the drone's price: enough for the slot now, but not for the drone once the
            // slot is paid for.
            _wallet.SetCoins(target.UnlockCost);

            yield return CenterGarageOn(owned, target.DroneId, "Unlock slot", $"{slotCost}");
            var button  = Field<Button>("_buyButton");
            var label   = Field<TMP_Text>("_buyLabel");
            var caption = Field<TMP_Text>("_buyCaption");
            Assert.IsTrue(button.interactable, "The player can afford the slot");

            // The server's UnlockDroneSlot reply, applied in DroneService's order: fleet, then balance.
            _fleet.Apply(Fleet(slots: 3, owned), _registry);
            _wallet.SetCoins(target.UnlockCost - slotCost);
            yield return null;

            Assert.AreEqual("Buy", caption.text, "With a free slot the button sells the drone");
            Assert.AreEqual($"{target.UnlockCost}", label.text);
            Assert.IsFalse(button.interactable,
                "After paying for the slot the player can no longer afford the drone, so the button must be disabled");
        }

        [UnityTest]
        public IEnumerator Buying_a_drone_asks_first_then_shows_the_result()
        {
            var owned  = new List<string> { _registry.AllDrones.First().DroneId };
            _fleet.Apply(Fleet(slots: 2, owned), _registry);
            var target = _registry.AllDrones.First(d => !owned.Contains(d.DroneId));
            _wallet.SetCoins(target.UnlockCost + 100);
            _backend.On("AcquireDrone", _ => "{\"Success\":true,\"Reason\":\"\",\"NewBalance\":100}");

            yield return CenterGarageOn(owned, target.DroneId, "Buy", $"{target.UnlockCost}");

            Field<Button>("_buyButton").onClick.Invoke();
            var modal = Field<TransactionModal>("_purchaseModal");
            Assert.IsTrue(modal.gameObject.activeInHierarchy, "The buy button opens the purchase popup");
            Assert.AreEqual($"Buy {target.DisplayName}?", Private<TMP_Text>(modal, "_titleText").text);
            Assert.IsTrue(Private<GameObject>(modal, "_iconGlow").activeSelf, "The drone's icon is lit");
            Assert.IsFalse(_backend.Calls.Any(c => c.Function == "AcquireDrone"), "Nothing is bought before the player confirms");

            Private<Button>(modal, "_confirmButton").onClick.Invoke();
            yield return null;

            Assert.IsTrue(_backend.Calls.Any(c => c.Function == "AcquireDrone" && (string)c.Args["droneId"] == target.DroneId),
                "Confirm sends the purchase to the server");
            Assert.AreEqual($"{target.DisplayName} joined your fleet!", Private<TMP_Text>(modal, "_titleText").text);
            var ok = Private<Button>(modal, "_okButton");
            Assert.IsTrue(ok.gameObject.activeSelf, "The result screen offers OK");

            ok.onClick.Invoke();
            yield return WaitUntil(() => !modal.gameObject.activeSelf, "OK to close the popup");
        }

        [UnityTest]
        public IEnumerator A_rejected_purchase_shows_the_reason_and_lets_the_player_retry()
        {
            var owned  = new List<string> { _registry.AllDrones.First().DroneId };
            _fleet.Apply(Fleet(slots: 2, owned), _registry);
            var target = _registry.AllDrones.First(d => !owned.Contains(d.DroneId));
            _wallet.SetCoins(target.UnlockCost + 100);
            _backend.On("AcquireDrone", _ => "{\"Success\":false,\"Reason\":\"INSUFFICIENT_FUNDS\"}");

            yield return CenterGarageOn(owned, target.DroneId, "Buy", $"{target.UnlockCost}");

            Field<Button>("_buyButton").onClick.Invoke();
            var modal   = Field<TransactionModal>("_purchaseModal");
            var confirm = Private<Button>(modal, "_confirmButton");
            confirm.onClick.Invoke();
            yield return null;

            Assert.IsTrue(modal.gameObject.activeSelf, "A failure keeps the popup open");
            Assert.AreEqual("Not enough coins", Private<TMP_Text>(modal, "_statusText").text);
            Assert.IsTrue(confirm.gameObject.activeSelf && confirm.interactable, "Buy stays available to retry");
        }

        // The garage covers the HUD, so its header carries the balance and how full the fleet is.
        [UnityTest]
        public IEnumerator Header_shows_the_balance_and_the_fleet_size()
        {
            var owned = new List<string> { _registry.AllDrones.First().DroneId };
            _fleet.Apply(Fleet(slots: 2, owned), _registry);
            _wallet.SetCoins(1234);

            _garage.Open();
            var balance = Field<TMP_Text>("_balanceLabel");
            var fleet   = Field<TMP_Text>("_fleetLabel");
            yield return WaitUntil(() => balance.text == "1234", "balance to show 1234");
            Assert.AreEqual("Fleet 1/2", fleet.text);

            _wallet.SetCoins(99);
            _fleet.Apply(Fleet(slots: 3, owned), _registry);
            yield return null;

            Assert.AreEqual("99", balance.text, "The balance follows the wallet while the garage is open");
            Assert.AreEqual("Fleet 1/3", fleet.text, "The fleet size follows an unlocked slot");
        }

        // An arrow that can't go anywhere reads as a broken button, so the ends of the carousel hide it.
        [UnityTest]
        public IEnumerator Arrows_hide_at_the_ends_of_the_carousel()
        {
            var owned = new List<string> { _registry.AllDrones.First().DroneId };
            _fleet.Apply(Fleet(slots: 2, owned), _registry);
            var cards = DroneCardPlan.Build(owned, _registry.AllDrones.Select(d => d.DroneId).ToList());

            yield return CenterGarageOn(owned, cards[0].DroneId, null, null);
            var snap = Field<SimpleScrollSnap>("_scrollSnap");
            Assert.IsFalse(snap.PreviousButton.gameObject.activeSelf, "Nothing before the first card");
            Assert.IsTrue(snap.NextButton.gameObject.activeSelf);

            snap.GoToPanel(cards.Count - 1);
            yield return WaitUntil(() => !snap.NextButton.gameObject.activeSelf, "next arrow to hide on the last card");
            Assert.IsTrue(snap.PreviousButton.gameObject.activeSelf);
        }

        // Open the garage and wait until droneId's card is centered and the buy button reads
        // "<expectedCaption> <expectedLabel>". A null caption/label skips that check.
        private IEnumerator CenterGarageOn(List<string> owned, string droneId, string expectedCaption, string expectedLabel)
        {
            _garage.Open();
            var snap    = Field<SimpleScrollSnap>("_scrollSnap");
            var label   = Field<TMP_Text>("_buyLabel");
            var caption = Field<TMP_Text>("_buyCaption");

            var cards = DroneCardPlan.Build(owned, _registry.AllDrones.Select(d => d.DroneId).ToList());
            yield return WaitUntil(() => snap.NumberOfPanels == cards.Count, "garage carousel to fill");

            int index = DroneCardPlan.IndexOf(cards, droneId);
            snap.GoToPanel(index);
            yield return WaitUntil(() => snap.CenteredPanel == index
                                         && (expectedCaption == null || caption.text == expectedCaption)
                                         && (expectedLabel == null || label.text == expectedLabel),
                $"buy button to show '{expectedCaption} {expectedLabel}' for {droneId}");
        }

        private static DroneFleetSnapshot Fleet(int slots, List<string> droneIds) => new DroneFleetSnapshot
        {
            Slots         = slots,
            ActiveDroneId = droneIds[0],
            Drones        = droneIds.Select(id => new DroneSnapshot { DroneId = id, Upgrades = new Dictionary<string, int>() }).ToList()
        };

        private T Field<T>(string name) where T : class => Private<T>(_garage, name);

        private static T Private<T>(object target, string name) where T : class
        {
            var value = target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(target) as T;
            Assert.IsNotNull(value, $"{target.GetType().Name}.{name} is not set");
            return value;
        }

        private static IEnumerator WaitUntil(Func<bool> condition, string what)
        {
            float timeout = Time.realtimeSinceStartup + 5f;
            while (!condition() && Time.realtimeSinceStartup < timeout)
                yield return null;
            Assert.IsTrue(condition(), $"Timed out waiting for {what}");
        }
    }
}
