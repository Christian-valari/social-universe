using System;
using System.Collections;
using System.Globalization;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using SocialUniverse.App;
using SocialUniverse.Core;
using SocialUniverse.Config;
using SocialUniverse.Economy;
using SocialUniverse.Mining;
using SocialUniverse.Net;
using SocialUniverse.UI;
using SocialUniverse.World;

namespace SocialUniverse.Tests
{
    // Covers the core loop end to end in the real Planet scene: tapping an asteroid shows its tier,
    // idle mining a claimed asteroid grants minerals, and confirming a tile purchase transfers ownership. The scene's parent
    // scope is PlanetTestRootScope (mocks + a scripted FakeBackendClient) — see Known Issue #7.
    public class PlanetSceneFlowTests
    {
        private const string PlanetScenePath = "Assets/Scenes/Planet.unity";

        private GameObject           _rootScopeObject;
        private FakeBackendClient    _backend;
        private PlanetSceneScope     _scope;
        private MiningController     _mining;
        private AsteroidSpawner      _spawner;
        private Wallet               _wallet;
        private MineralInventory     _inventory;
        private HexasphereManager    _hex;
        private IAuthService         _auth;
        private EconomyConfig        _economyConfig;
        private PlanetDefinition     _planet;

        private float _savedSecondsPerUnit, _savedMinSeconds, _savedMaxSeconds;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            // Scripted server: ValidateMining grants what was claimed; PurchaseLand debits the price.
            _backend = new FakeBackendClient();
            _backend.On("ValidateMining", args =>
                $"{{\"Granted\":{args["claimedQty"]},\"MineralId\":\"{args["mineralId"]}\"}}");
            // StartIdleMining records the run and echoes it back (GetIdleMining stays unscripted: no run).
            _backend.On("StartIdleMining", args =>
                $"{{\"Success\":true,\"PlanetId\":\"{args["planetId"]}\",\"SlotId\":\"{args["slotId"]}\",\"MineralId\":\"{args["mineralId"]}\"," +
                $"\"ElapsedSeconds\":0,\"DurationSeconds\":{Convert.ToSingle(args["durationSec"]).ToString(CultureInfo.InvariantCulture)}}}");
            _backend.On("PurchaseLand", args =>
                $"{{\"Success\":true,\"Reason\":\"\",\"NewBalance\":{_wallet.Coins - (int)args["price"]}}}");

            // The parent scope must exist (and survive the Single scene load) before Planet.unity
            // loads, or PlanetSceneScope waits for a RootLifetimeScope forever.
            PlanetTestRootScope.Backend = _backend;
            _rootScopeObject = new GameObject("PlanetTestRootScope");
            UnityEngine.Object.DontDestroyOnLoad(_rootScopeObject);
            _rootScopeObject.AddComponent<PlanetTestRootScope>();

            // SceneReadyEvent fires after the fleet, wallet and tiles are hydrated and
            // MiningController.Initialize() has run — the earliest point the loop is playable.
            bool sceneReady = false;
            Action<SceneReadyEvent> onReady = _ => sceneReady = true;
            EventBus.Subscribe(onReady);

            yield return SceneManager.LoadSceneAsync(PlanetScenePath, LoadSceneMode.Single);

            _scope = UnityEngine.Object.FindFirstObjectByType<PlanetSceneScope>();
            Assert.IsNotNull(_scope, "PlanetSceneScope not found in Planet scene");
            Assert.IsNotNull(_scope.Container, "PlanetSceneScope.Container not initialized");

            _mining        = _scope.Container.Resolve(typeof(MiningController)) as MiningController;
            _spawner       = _scope.Container.Resolve(typeof(AsteroidSpawner)) as AsteroidSpawner;
            _wallet        = _scope.Container.Resolve(typeof(Wallet)) as Wallet;
            _inventory     = _scope.Container.Resolve(typeof(MineralInventory)) as MineralInventory;
            _hex           = _scope.Container.Resolve(typeof(HexasphereManager)) as HexasphereManager;
            _auth          = _scope.Container.Resolve(typeof(IAuthService)) as IAuthService;
            _economyConfig = _scope.Container.Resolve(typeof(EconomyConfig)) as EconomyConfig;
            _planet        = _scope.Container.Resolve(typeof(PlanetDefinition)) as PlanetDefinition;

            // _economyConfig is the real project asset. Force every idle session to 1 second, and
            // restore the values in TearDown so the in-memory asset isn't left modified.
            _savedSecondsPerUnit = _economyConfig.IdleSecondsPerYieldUnit;
            _savedMinSeconds     = _economyConfig.MinIdleSessionSeconds;
            _savedMaxSeconds     = _economyConfig.MaxIdleSessionSeconds;
            SetField(_economyConfig, "_idleSecondsPerYieldUnit", 0f);
            SetField(_economyConfig, "_minIdleSessionSeconds", 1f);
            SetField(_economyConfig, "_maxIdleSessionSeconds", 1f);

            float timeout = Time.realtimeSinceStartup + 10f;
            while (!sceneReady && Time.realtimeSinceStartup < timeout)
                yield return null;
            EventBus.Unsubscribe(onReady);
            Assert.IsTrue(sceneReady, "Planet scene never published SceneReadyEvent");

            // A session persisted by an earlier run would block BeginIdleMiningAsync.
            Assert.IsNull(_mining.CurrentIdleSession, "Expected no idle session restored at scene start");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_economyConfig != null)
            {
                SetField(_economyConfig, "_idleSecondsPerYieldUnit", _savedSecondsPerUnit);
                SetField(_economyConfig, "_minIdleSessionSeconds", _savedMinSeconds);
                SetField(_economyConfig, "_maxIdleSessionSeconds", _savedMaxSeconds);
            }
            // A test that leaves an idle session running must not hand it to the next test's scene.
            PlayerPrefs.DeleteKey(SaveKeys.IdleMiningSession);
            PlanetTestRootScope.Backend = null;
            if (_rootScopeObject != null) UnityEngine.Object.Destroy(_rootScopeObject);
            yield return null;
        }

        private static void SetField(object target, string field, object value) =>
            target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);

        private static T GetField<T>(object target, string field) where T : class
        {
            var value = target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(target) as T;
            Assert.IsNotNull(value, $"{target.GetType().Name}.{field} is not set");
            return value;
        }

        // Starts an idle run the way the prompt does (the server records it first) and waits for it.
        private IEnumerator StartIdle(Asteroid asteroid)
        {
            var start = _mining.BeginIdleMiningAsync(asteroid);
            while (!start.IsCompleted) yield return null;
            if (start.Exception != null) throw start.Exception;
            Assert.IsTrue(start.Result, "Starter drone should be allowed to mine a Tier-1 asteroid");
        }

        // Everything the Planet scene shows about the player comes from the server, read together before
        // the scene opens (PlayerDataPull starts every read at once; PlayerDataPullTests).
        [UnityTest]
        public IEnumerator Planet_load_reads_the_players_state_from_the_server()
        {
            foreach (var fn in new[] { "GetFuelState", "GetPlayerProfile", "GetLandRegistry", "GetIdleMining", "GetAsteroidRespawns" })
                Assert.IsTrue(_backend.Calls.Any(c => c.Function == fn), $"the Planet scene reads {fn}");

            var respawns = _backend.Calls.First(c => c.Function == "GetAsteroidRespawns");
            Assert.AreEqual(_planet.PlanetId, respawns.Args["planetId"], "asteroid respawns are read for this planet");

            // A normal run reaches the server for mining; only dev mode swaps in the on-device stand-ins.
            Assert.IsInstanceOf<MineralService>(_scope.Container.Resolve(typeof(IMineralService)));
            Assert.IsInstanceOf<DroneService>(_scope.Container.Resolve(typeof(IDroneService)));
            Assert.IsInstanceOf<IdleMiningService>(_scope.Container.Resolve(typeof(IIdleMiningService)));
            Assert.IsInstanceOf<AsteroidRespawnService>(_scope.Container.Resolve(typeof(IAsteroidRespawnService)));
            yield return null;
        }

        [UnityTest]
        public IEnumerator Tapping_an_asteroid_shows_its_tier_and_whether_the_active_drone_can_mine_it()
        {
            var asteroid = _spawner.ActiveAsteroids.FirstOrDefault(a => !a.IsDepleted);
            Assert.IsNotNull(asteroid, "Expected an asteroid after scene boot");
            var drone = (_scope.Container.Resolve(typeof(DroneFleet)) as DroneFleet)?.Active;
            Assert.IsNotNull(drone, "Expected the starter drone to be active");

            EventBus.Publish(new AsteroidSelectedEvent { Asteroid = asteroid });
            yield return null;

            var prompt  = UnityEngine.Object.FindFirstObjectByType<MiningModePromptView>(FindObjectsInactive.Include);
            int tier    = asteroid.Definition.Tier;
            bool canMine = drone.Definition.Tier >= tier;

            StringAssert.EndsWith($"? · Tier {tier}", GetField<TMPro.TMP_Text>(prompt, "_titleText").text);
            StringAssert.Contains(drone.Definition.DisplayName, GetField<TMPro.TMP_Text>(prompt, "_tierStatusText").text);
            Assert.AreEqual(canMine, GetField<Button>(prompt, "_idleMineButton").interactable, "Idle mining follows the tier check");
            Assert.AreEqual(canMine, GetField<Button>(prompt, "_activeMineButton").interactable, "Active mining follows the tier check");
        }

        [UnityTest]
        public IEnumerator Tapping_another_asteroid_while_the_drone_is_mining_says_it_is_busy()
        {
            var tierOne = _spawner.ActiveAsteroids.Where(a =>
                !a.IsDepleted && a.Definition.Tier <= 1 && a.Definition.Mineral != null).Take(2).ToList();
            Assert.AreEqual(2, tierOne.Count, "Expected two Tier-1 asteroids after scene boot");

            yield return StartIdle(tierOne[0]);

            EventBus.Publish(new AsteroidSelectedEvent { Asteroid = tierOne[1] });
            yield return null;

            var prompt = UnityEngine.Object.FindFirstObjectByType<MiningModePromptView>(FindObjectsInactive.Include);
            StringAssert.StartsWith("Your drone is ", GetField<TMPro.TMP_Text>(prompt, "_tierStatusText").text);
            StringAssert.Contains(tierOne[0].Definition.Mineral.DisplayName, GetField<TMPro.TMP_Text>(prompt, "_tierStatusText").text);
            Assert.IsFalse(GetField<Button>(prompt, "_idleMineButton").interactable, "Idle mining is refused while the drone is busy");
            Assert.IsFalse(GetField<Button>(prompt, "_activeMineButton").interactable, "Active mining is refused while the drone is busy");
        }

        [UnityTest]
        public IEnumerator Tapping_the_asteroid_being_mined_shows_a_busy_toast()
        {
            var asteroid = _spawner.ActiveAsteroids.FirstOrDefault(a =>
                !a.IsDepleted && a.Definition.Tier <= 1 && a.Definition.Mineral != null);
            Assert.IsNotNull(asteroid, "Expected a Tier-1 asteroid with a mineral after scene boot");

            yield return StartIdle(asteroid);
            Assert.AreNotEqual(IdleMiningStage.ReadyToClaim, _mining.CurrentIdleSession.Stage, "A tap now must not claim");

            EventBus.Publish(new AsteroidSelectedEvent { Asteroid = asteroid });
            yield return null;

            var toast = UnityEngine.Object.FindFirstObjectByType<ToastView>();
            Assert.IsNotNull(toast, "The HUD creates the toast at scene start");
            Assert.AreEqual("Your drone is currently mining this asteroid", GetField<TMPro.TMP_Text>(toast, "_label").text);
            Assert.Greater(toast.GetComponent<CanvasGroup>().alpha, 0f, "The toast is showing");
        }

        [UnityTest]
        public IEnumerator Idle_mining_a_claimed_asteroid_grants_its_mineral()
        {
            // The starter drone is Tier 1, so pick an asteroid it is allowed to mine.
            var asteroid = _spawner.ActiveAsteroids.FirstOrDefault(a =>
                !a.IsDepleted && a.Definition.Tier <= 1 && a.Definition.Mineral != null);
            Assert.IsNotNull(asteroid, "Expected a Tier-1 asteroid with a mineral after scene boot");

            string mineralId = asteroid.Definition.Mineral.MineralId;
            int heldBefore   = _inventory.Get(mineralId);

            yield return StartIdle(asteroid);

            float timeout = Time.realtimeSinceStartup + 5f;
            while (_mining.CurrentIdleSession != null
                   && _mining.CurrentIdleSession.Stage != IdleMiningStage.ReadyToClaim
                   && Time.realtimeSinceStartup < timeout)
            {
                yield return null;
            }

            Assert.AreEqual(IdleMiningStage.ReadyToClaim, _mining.CurrentIdleSession?.Stage,
                "Idle session should reach ReadyToClaim well within the 5s timeout given the 1s forced duration");

            var claimTask = _mining.ClaimIdleSessionAsync(asteroid);
            while (!claimTask.IsCompleted) yield return null;
            if (claimTask.Exception != null) throw claimTask.Exception;

            Assert.IsNull(_mining.CurrentIdleSession);

            var call = _backend.Calls.LastOrDefault(c => c.Function == "ValidateMining");
            Assert.IsNotNull(call.Args, "Claim should be validated by the server (ValidateMining)");
            Assert.AreEqual(mineralId, call.Args["mineralId"]);
            Assert.AreEqual(_planet.PlanetId, call.Args["planetId"], "Claim should name the planet it was mined on");
            Assert.IsFalse(call.Args.ContainsKey("unitsPerSec"), "The client no longer sends a mining rate");
            Assert.AreEqual(asteroid.SlotId, call.Args["idleSlotId"], "The claim ends the server-tracked run");
            Assert.AreEqual(asteroid.SlotId, call.Args["slotId"], "The server's claim log records which asteroid was emptied");
            Assert.IsTrue(_backend.Calls.Any(c => c.Function == "StartIdleMining"), "The run was recorded on the server first");

            int claimed = Convert.ToInt32(call.Args["claimedQty"]);
            Assert.Greater(claimed, 0, "Claim should request a positive mineral quantity");
            Assert.AreEqual(heldBefore + claimed, _inventory.Get(mineralId),
                "Mineral inventory should grow by the quantity the server granted");
        }

        [UnityTest]
        public IEnumerator Confirming_an_available_tile_purchases_it_and_transfers_ownership()
        {
            TileData tile = _hex.Tiles.Values.FirstOrDefault(t => t.State == TileState.Available);
            Assert.IsNotNull(tile, "Expected at least one Available tile on the planet");

            // The wallet hydrates from UGS, which isn't running here — fund it directly.
            int price = (int)Math.Round(_economyConfig.BaseLandPrice * _planet.LandPriceMultiplier);
            _wallet.SetCoins(price + 100);
            int coinsBefore = _wallet.Coins;

            // LandPurchaseModal publishes this when the player confirms; TilePurchaseHandler buys.
            EventBus.Publish(new TilePurchaseConfirmedEvent { Tile = tile });

            float timeout = Time.realtimeSinceStartup + 5f;
            while (tile.State == TileState.Available && Time.realtimeSinceStartup < timeout)
                yield return null;

            string expectedOwner = _auth.IsSignedIn ? _auth.PlayerId : "local_player";
            Assert.AreEqual(TileState.OwnedByPlayer, tile.State, "Tile should become OwnedByPlayer after purchase");
            Assert.AreEqual(expectedOwner, tile.OwnerId);
            Assert.AreEqual(coinsBefore - price, _wallet.Coins, "Wallet should be debited the tile price");
        }
    }
}
