using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using SocialUniverse.App;
using SocialUniverse.Core;
using SocialUniverse.Config;
using SocialUniverse.Economy;
using SocialUniverse.Mining;
using SocialUniverse.Net;
using SocialUniverse.World;

namespace SocialUniverse.Tests
{
    // Covers the core loop end to end in the real Planet scene: idle mining a claimed asteroid
    // grants minerals, and confirming a tile purchase transfers ownership. The scene's parent
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

            // A session persisted by an earlier run would block BeginIdleMining.
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
            PlanetTestRootScope.Backend = null;
            if (_rootScopeObject != null) UnityEngine.Object.Destroy(_rootScopeObject);
            yield return null;
        }

        private static void SetField(object target, string field, object value) =>
            target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);

        [UnityTest]
        public IEnumerator Idle_mining_a_claimed_asteroid_grants_its_mineral()
        {
            // The starter drone is Tier 1, so pick an asteroid it is allowed to mine.
            var asteroid = _spawner.ActiveAsteroids.FirstOrDefault(a =>
                !a.IsDepleted && a.Definition.Tier <= 1 && a.Definition.Mineral != null);
            Assert.IsNotNull(asteroid, "Expected a Tier-1 asteroid with a mineral after scene boot");

            string mineralId = asteroid.Definition.Mineral.MineralId;
            int heldBefore   = _inventory.Get(mineralId);

            Assert.IsTrue(_mining.BeginIdleMining(asteroid), "Starter drone should be allowed to mine a Tier-1 asteroid");

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
