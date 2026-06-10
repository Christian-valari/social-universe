using System;
using System.Collections;
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
    // mining a tap session pays out coins, and buying a tile transfers ownership.
    public class PlanetSceneFlowTests
    {
        private const string PlanetScenePath = "Assets/Scenes/Planet.unity";

        private PlanetSceneScope  _scope;
        private MiningController  _mining;
        private Wallet            _wallet;
        private HexasphereManager _hex;
        private LandPurchaseService _purchaseService;
        private EconomyConfig     _economyConfig;
        private PlanetDefinition  _planet;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return SceneManager.LoadSceneAsync(PlanetScenePath, LoadSceneMode.Single);

            _scope = UnityEngine.Object.FindFirstObjectByType<PlanetSceneScope>();
            Assert.IsNotNull(_scope, "PlanetSceneScope not found in Planet scene");
            Assert.IsNotNull(_scope.Container, "PlanetSceneScope.Container not initialized");

            _mining          = (MiningController)_scope.Container.Resolve(typeof(MiningController));
            _wallet          = (Wallet)_scope.Container.Resolve(typeof(Wallet));
            _hex             = (HexasphereManager)_scope.Container.Resolve(typeof(HexasphereManager));
            _purchaseService = (LandPurchaseService)_scope.Container.Resolve(typeof(LandPurchaseService));
            _economyConfig   = (EconomyConfig)_scope.Container.Resolve(typeof(EconomyConfig));
            _planet          = (PlanetDefinition)_scope.Container.Resolve(typeof(PlanetDefinition));

            // Mining session starts as soon as the scene boots; wait for an active target.
            while (_mining.Phase != MiningPhase.Active || _mining.CurrentTarget == null)
                yield return null;
        }

        [UnityTest]
        public IEnumerator Mining_taps_fill_cargo_and_commit_grants_coins()
        {
            var drone        = _mining.Drone;
            int coinsPerUnit = _mining.CurrentTarget.Definition.CoinsPerUnit;
            int coinsBefore  = _wallet.Coins;

            while (!drone.IsCargoFull)
            {
                var result = _mining.Tap();
                Assert.IsNotNull(result, "Tap should yield while a target is active and cargo has space");
                yield return null;
            }

            int hauled = drone.CargoAmount;
            int expectedPayout = hauled * coinsPerUnit;

            var commit = _mining.CommitCargoAsync();
            while (!commit.IsCompleted) yield return null;
            if (commit.Exception != null) throw commit.Exception;

            Assert.AreEqual(0, drone.CargoAmount, "Cargo should be emptied after committing");
            Assert.AreEqual(coinsBefore + expectedPayout, _wallet.Coins,
                "Wallet should increase by hauled units * coins-per-unit after committing cargo");
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
