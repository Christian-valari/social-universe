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
using TMPro;
using SocialUniverse.App;
using SocialUniverse.Config;
using SocialUniverse.Core;
using SocialUniverse.Economy;
using SocialUniverse.Mining;
using SocialUniverse.Safety;
using SocialUniverse.UI;

namespace SocialUniverse.Tests
{
    // The Mineral Inventory's sale popup in the real Planet scene (parent scope: PlanetTestRootScope,
    // scripted FakeBackendClient), plus a check that every transaction popup gets its audio.
    public class MineralSaleFlowTests
    {
        private const string PlanetScenePath = "Assets/Scenes/Planet.unity";

        private GameObject           _rootScopeObject;
        private FakeBackendClient    _backend;
        private MineralInventoryView _view;
        private DroneGarageView      _garage;
        private MineralInventory     _inventory;
        private DatabaseRegistry     _registry;

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

            _view      = scope.Container.Resolve(typeof(MineralInventoryView)) as MineralInventoryView;
            _garage    = scope.Container.Resolve(typeof(DroneGarageView)) as DroneGarageView;
            _inventory = scope.Container.Resolve(typeof(MineralInventory)) as MineralInventory;
            _registry  = scope.Container.Resolve(typeof(DatabaseRegistry)) as DatabaseRegistry;

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
        public IEnumerator Every_transaction_popup_gets_its_audio()
        {
            Private<IAudioManager>(Private<TransactionModal>(_garage, "_purchaseModal"), "_audio");
            Private<IAudioManager>(Private<TransactionModal>(_view, "_saleModal"), "_audio");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Selling_a_mineral_asks_first_then_shows_the_result()
        {
            var mineral = _registry.AllMinerals.First(m => m != null);
            _inventory.SetAll(new Dictionary<string, int> { { mineral.MineralId, 12 } });
            _backend.On("SellMinerals", _ => "{\"Success\":true,\"Reason\":\"\",\"NewBalance\":5000}");

            _view.Open();
            yield return null;
            RowSellButton(mineral.MineralId).onClick.Invoke();

            var modal = Private<TransactionModal>(_view, "_saleModal");
            Assert.IsTrue(modal.gameObject.activeInHierarchy, "Sell opens the sale popup");
            Assert.AreEqual($"Sell 12 {mineral.DisplayName}?", Private<TMP_Text>(modal, "_titleText").text);
            Assert.IsFalse(_backend.Calls.Any(c => c.Function == "SellMinerals"), "Nothing is sold before the player confirms");

            Private<Button>(modal, "_confirmButton").onClick.Invoke();
            yield return null;

            Assert.IsTrue(_backend.Calls.Any(c => c.Function == "SellMinerals"
                    && (string)c.Args["mineralId"] == mineral.MineralId && (int)c.Args["qty"] == 12),
                "Confirm sells the quantity the popup showed");
            Assert.AreEqual($"Sold 12 {mineral.DisplayName}", Private<TMP_Text>(modal, "_titleText").text);
            var ok = Private<Button>(modal, "_okButton");
            Assert.IsTrue(ok.gameObject.activeSelf, "The result screen offers OK");

            ok.onClick.Invoke();
            yield return WaitUntil(() => !modal.gameObject.activeSelf, "OK to close the popup");
        }

        // PopupTransition: the popup pops in (fade + grow), and on close fades out before hiding,
        // ignoring taps meanwhile, then resets for the next open.
        [UnityTest]
        public IEnumerator The_popup_pops_in_and_fades_out()
        {
            var mineral = _registry.AllMinerals.First(m => m != null);
            _inventory.SetAll(new Dictionary<string, int> { { mineral.MineralId, 12 } });

            _view.Open();
            yield return null;
            RowSellButton(mineral.MineralId).onClick.Invoke();

            var modal  = Private<TransactionModal>(_view, "_saleModal");
            var window = Private<RectTransform>(modal, "_window");
            var group  = modal.GetComponent<CanvasGroup>();
            Assert.IsNotNull(group, "Opening the popup adds its fade");
            Assert.Less(group.alpha, 1f, "The popup starts faded out");

            yield return WaitUntil(() => group.alpha >= 1f, "the popup to finish appearing");
            yield return null;
            Assert.AreEqual(1f, window.localScale.x, 1e-3f, "The window settles at full size");

            Private<Button>(modal, "_cancelButton").onClick.Invoke();
            Assert.IsTrue(modal.gameObject.activeSelf, "The popup stays up while it fades out");
            Assert.IsFalse(group.blocksRaycasts, "A closing popup ignores taps");

            yield return WaitUntil(() => !modal.gameObject.activeSelf, "the popup to close");
            Assert.AreEqual(1f, group.alpha, 1e-3f, "Closed, it is reset for the next open");
            Assert.AreEqual(1f, window.localScale.x, 1e-3f, "Closed, it is reset for the next open");
            Assert.IsTrue(group.blocksRaycasts, "Closed, it is reset for the next open");
        }

        private static IEnumerator WaitUntil(Func<bool> condition, string what)
        {
            float timeout = Time.realtimeSinceStartup + 5f;
            while (!condition() && Time.realtimeSinceStartup < timeout)
                yield return null;
            Assert.IsTrue(condition(), $"Timed out waiting for {what}");
        }

        // The panel covers the HUD, so it carries the balance; Sell all carries what selling everything
        // pays; and the header counts the hold. All three follow changes while the panel is open.
        [UnityTest]
        public IEnumerator Header_and_sell_all_show_the_hold_and_the_balance()
        {
            var minerals = _registry.AllMinerals.Where(m => m != null).Take(2).ToList();
            _inventory.SetAll(new Dictionary<string, int> { { minerals[0].MineralId, 12 }, { minerals[1].MineralId, 3 } });
            var wallet = Private<Wallet>(_view, "_wallet");
            wallet.SetCoins(1234);

            _view.Open();
            yield return null;

            var held    = Private<TMP_Text>(_view, "_heldLabel");
            var total   = Private<TMP_Text>(_view, "_totalValueLabel");
            var balance = Private<TMP_Text>(_view, "_balanceLabel");
            Assert.AreEqual("15 minerals held", held.text);
            Assert.AreEqual($"{12 * minerals[0].SellValue + 3 * minerals[1].SellValue}", total.text);
            Assert.AreEqual("1234", balance.text);
            Assert.IsTrue(Private<Button>(_view, "_sellAllButton").interactable);

            _inventory.SetAll(new Dictionary<string, int>());
            wallet.SetCoins(5000);
            yield return null;

            Assert.AreEqual("0 minerals held", held.text);
            Assert.AreEqual("0", total.text);
            Assert.AreEqual("5000", balance.text, "The balance follows the wallet, e.g. after a sale");
            Assert.IsFalse(Private<Button>(_view, "_sellAllButton").interactable, "Nothing to sell");
        }

        [UnityTest]
        public IEnumerator A_rejected_sale_shows_the_reason_and_lets_the_player_retry()
        {
            var mineral = _registry.AllMinerals.First(m => m != null);
            _inventory.SetAll(new Dictionary<string, int> { { mineral.MineralId, 12 } });
            _backend.On("SellMinerals", _ => "{\"Success\":false,\"Reason\":\"INSUFFICIENT_QTY\"}");

            _view.Open();
            yield return null;
            RowSellButton(mineral.MineralId).onClick.Invoke();

            var modal   = Private<TransactionModal>(_view, "_saleModal");
            var confirm = Private<Button>(modal, "_confirmButton");
            confirm.onClick.Invoke();
            yield return null;

            Assert.IsTrue(modal.gameObject.activeSelf, "A failure keeps the popup open");
            Assert.AreEqual("Not enough minerals to sell", Private<TMP_Text>(modal, "_statusText").text);
            Assert.IsTrue(confirm.gameObject.activeSelf && confirm.interactable, "Sell stays available to retry");
        }

        // Rows are index-aligned with _mineralIds (registry order).
        private Button RowSellButton(string mineralId)
        {
            var ids  = Private<List<string>>(_view, "_mineralIds");
            var rows = Private<List<MineralRowView>>(_view, "_rows");
            int i = ids.IndexOf(mineralId);
            Assert.GreaterOrEqual(i, 0, $"No row for {mineralId}");
            return Private<Button>(rows[i], "_sellButton");
        }

        private static T Private<T>(object target, string name) where T : class
        {
            var value = target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(target) as T;
            Assert.IsNotNull(value, $"{target.GetType().Name}.{name} is not set");
            return value;
        }
    }
}
