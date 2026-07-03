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
using SocialUniverse.World;

namespace SocialUniverse.Tests
{
    // Covers the M1 exit-criteria loop end to end against LocalMock services:
    // idle mining a claimed asteroid pays out coins, and buying a tile transfers ownership.
    public class PlanetSceneFlowTests
    {
        private const string PlanetScenePath = "Assets/Scenes/Planet.unity";

        private PlanetSceneScope     _scope;
        private MiningController     _mining;
        private AsteroidSpawner      _spawner;
        private Wallet               _wallet;
        private HexasphereManager    _hex;
        private LandPurchaseService  _purchaseService;
        private EconomyConfig        _economyConfig;
        private PlanetDefinition     _planet;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return SceneManager.LoadSceneAsync(PlanetScenePath, LoadSceneMode.Single);

            _scope = UnityEngine.Object.FindFirstObjectByType<PlanetSceneScope>();
            Assert.IsNotNull(_scope, "PlanetSceneScope not found in Planet scene");
            Assert.IsNotNull(_scope.Container, "PlanetSceneScope.Container not initialized");

            _mining          = (MiningController)_scope.Container.Resolve(typeof(MiningController));
            _spawner         = (AsteroidSpawner)_scope.Container.Resolve(typeof(AsteroidSpawner));
            _wallet          = (Wallet)_scope.Container.Resolve(typeof(Wallet));
            _hex             = (HexasphereManager)_scope.Container.Resolve(typeof(HexasphereManager));
            _purchaseService = (LandPurchaseService)_scope.Container.Resolve(typeof(LandPurchaseService));
            _economyConfig   = (EconomyConfig)_scope.Container.Resolve(typeof(EconomyConfig));
            _planet          = (PlanetDefinition)_scope.Container.Resolve(typeof(PlanetDefinition));

            // _economyConfig is the actual project asset (tuned for real play — durations can
            // run into minutes for higher-yield asteroids). Force every idle session in this
            // test run to a fixed 1-second duration by mutating the resolved in-memory instance's
            // private fields directly, the same reflection pattern the EditMode tests in this
            // plan already use. This only changes the runtime object held by this test session —
            // it is not saved back to the .asset file on disk (no AssetDatabase.SaveAssets call).
            SetField(_economyConfig, "_idleSecondsPerYieldUnit", 0f);
            SetField(_economyConfig, "_minIdleSessionSeconds", 1f);
            SetField(_economyConfig, "_maxIdleSessionSeconds", 1f);

            // Asteroids spawn synchronously during PlanetSceneBootstrapper.Start(); wait for the field to populate.
            float timeout = Time.realtimeSinceStartup + 5f;
            while (_spawner.ActiveAsteroids.Count == 0 && Time.realtimeSinceStartup < timeout)
                yield return null;
        }

        private static void SetField(object target, string field, object value) =>
            target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);

        [UnityTest]
        public IEnumerator Idle_mining_a_claimed_asteroid_grants_coins_and_schedules_respawn()
        {
            var asteroid = _spawner.ActiveAsteroids.FirstOrDefault(a => !a.IsDepleted);
            Assert.IsNotNull(asteroid, "Expected at least one active asteroid after scene boot");

            int expectedCoins = asteroid.RemainingYield * asteroid.Definition.CoinsPerUnit;
            int coinsBefore   = _wallet.Coins;

            Assert.IsTrue(_mining.BeginIdleMining(asteroid));

            // SetUp forced a fixed 1-second duration, so this only ever waits ~1 real second
            // regardless of the asteroid's actual yield or this scene's production EconomyConfig.
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
            Assert.AreEqual(coinsBefore + expectedCoins, _wallet.Coins,
                "Wallet should increase by the asteroid's full yield * coins-per-unit after claiming");
        }

        [UnityTest]
        public IEnumerator Selecting_an_available_tile_purchases_it_and_transfers_ownership()
        {
            TileData tile = null;
            foreach (var kv in _hex.Tiles)
            {
                if (kv.Value.State == TileState.Available) { tile = kv.Value; break; }
            }
            Assert.IsNotNull(tile, "Expected at least one Available tile on the planet");

            int price = (int)Math.Round(_economyConfig.BaseLandPrice * _planet.LandPriceMultiplier);
            Assert.GreaterOrEqual(_wallet.Coins, price, "Test setup expects enough coins to afford the tile");
            int coinsBefore = _wallet.Coins;

            EventBus.Publish(new TileSelectedEvent { Tile = tile });

            // TilePurchaseHandler.OnTileSelected runs PurchaseAsync — wait for the state transition.
            float timeout = Time.realtimeSinceStartup + 5f;
            while (tile.State == TileState.Available && Time.realtimeSinceStartup < timeout)
                yield return null;

            Assert.AreEqual(TileState.OwnedByPlayer, tile.State, "Tile should become OwnedByPlayer after purchase");
            Assert.AreEqual("local_player", tile.OwnerId);
            Assert.AreEqual(coinsBefore - price, _wallet.Coins, "Wallet should be debited the tile price");
        }
    }
}
