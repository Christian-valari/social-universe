using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
using SocialUniverse.Config;
using SocialUniverse.Economy;
using SocialUniverse.Mining;
using UnityEngine;

namespace SocialUniverse.Tests
{
    public class MiningControllerTests
    {
        private EconomyConfig          _config;
        private AsteroidDefinition     _asteroidDef;
        private PlanetDefinition       _planet;
        private Wallet                 _wallet;
        private LocalMockEconomy       _economy;
        private MiningRewardCalculator _rewardCalc;
        private ActiveMiningMinigame   _activeMinigame;
        private AsteroidSpawner        _spawner;
        private MiningController       _mining;

        private static void SetField(object target, string field, object value) =>
            target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);

        [SetUp]
        public void SetUp()
        {
            PlayerPrefs.DeleteKey(SocialUniverse.Core.SaveKeys.IdleMiningSession);

            _config = ScriptableObject.CreateInstance<EconomyConfig>();
            SetField(_config, "_idleSecondsPerYieldUnit", 0f);
            SetField(_config, "_minIdleSessionSeconds", 0.05f);
            SetField(_config, "_maxIdleSessionSeconds", 0.05f);
            SetField(_config, "_activeYieldPerTap", 1f);
            SetField(_config, "_minActiveTaps", 1);
            SetField(_config, "_maxActiveTaps", 99);
            SetField(_config, "_activeMaxErrors", 3);
            SetField(_config, "_activeTapWindowSeconds", 5f);
            SetField(_config, "_asteroidRespawnHours", 4f);

            _asteroidDef = ScriptableObject.CreateInstance<AsteroidDefinition>();
            SetField(_asteroidDef, "_coinsPerUnit", 2);

            _planet = ScriptableObject.CreateInstance<PlanetDefinition>();
            SetField(_planet, "_planetId", "test_planet");

            _wallet     = new Wallet();
            _economy    = new LocalMockEconomy(_wallet, _config);
            _rewardCalc = new MiningRewardCalculator(_config);

            var spawnerGo = new GameObject("TestSpawner");
            _spawner = spawnerGo.AddComponent<AsteroidSpawner>();

            _activeMinigame = new ActiveMiningMinigame(_config, _rewardCalc);
            _mining = new MiningController(_economy, _rewardCalc, _activeMinigame, _spawner, _config, _planet);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_config);
            Object.DestroyImmediate(_asteroidDef);
            Object.DestroyImmediate(_planet);
            PlayerPrefs.DeleteKey(SocialUniverse.Core.SaveKeys.IdleMiningSession);
        }

        private Asteroid MakeAndRegisterAsteroid(string slotId, int remainingYield)
        {
            var go = new GameObject("TestAsteroid");
            var asteroid = go.AddComponent<Asteroid>();
            asteroid.Initialize(_asteroidDef, slotId);
            typeof(Asteroid).GetField("<RemainingYield>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(asteroid, remainingYield);

            var active = (List<Asteroid>)typeof(AsteroidSpawner)
                .GetField("_active", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(_spawner);
            active.Add(asteroid);

            return asteroid;
        }

        [Test]
        public async Task ClaimIdleSessionAsync_grants_full_yield_and_schedules_respawn()
        {
            var asteroid = MakeAndRegisterAsteroid("slot_0", remainingYield: 20);
            Assert.IsTrue(_mining.BeginIdleMining(asteroid));

            await Task.Delay(100);
            _mining.CurrentIdleSession.Tick(0f);
            Assert.AreEqual(IdleMiningStage.ReadyToClaim, _mining.CurrentIdleSession.Stage);

            int coinsBefore = _wallet.Coins;

            await _mining.ClaimIdleSessionAsync(asteroid);

            Assert.IsNull(_mining.CurrentIdleSession);
            Assert.AreEqual(coinsBefore + 40, _wallet.Coins); // 20 yield * 2 coins/unit
            Assert.IsTrue(asteroid.IsDepleted);
        }

        [Test]
        public void BeginIdleMining_fails_while_a_session_is_already_running()
        {
            var a1 = MakeAndRegisterAsteroid("slot_0", 10);
            var a2 = MakeAndRegisterAsteroid("slot_1", 10);

            Assert.IsTrue(_mining.BeginIdleMining(a1));
            Assert.IsFalse(_mining.BeginIdleMining(a2));
        }

        [Test]
        public void BeginActiveMining_does_not_require_the_drone_and_can_run_alongside_an_idle_session()
        {
            var idleAsteroid   = MakeAndRegisterAsteroid("slot_0", 10);
            var activeAsteroid = MakeAndRegisterAsteroid("slot_1", 10);

            Assert.IsTrue(_mining.BeginIdleMining(idleAsteroid));
            Assert.IsTrue(_mining.BeginActiveMining(activeAsteroid));

            Assert.IsNotNull(_mining.CurrentIdleSession);
            Assert.IsNotNull(_mining.CurrentActiveSession);
        }

        [Test]
        public async Task Active_mining_success_grants_full_yield()
        {
            var asteroid = MakeAndRegisterAsteroid("slot_0", remainingYield: 10);
            Assert.IsTrue(_mining.BeginActiveMining(asteroid));
            int tapsRequired = _mining.CurrentActiveSession.TapsRequired;
            int coinsBefore  = _wallet.Coins;

            for (int i = 0; i < tapsRequired; i++)
                _mining.RegisterActiveTap(true);

            await Task.Yield(); // let the fire-and-forget payout Task complete

            Assert.IsNull(_mining.CurrentActiveSession);
            Assert.AreEqual(coinsBefore + 20, _wallet.Coins); // 10 yield * 2 coins/unit
            Assert.IsTrue(asteroid.IsDepleted);
        }

        [Test]
        public void Active_mining_failure_grants_nothing_and_clears_the_session()
        {
            var asteroid = MakeAndRegisterAsteroid("slot_0", remainingYield: 10);
            Assert.IsTrue(_mining.BeginActiveMining(asteroid));
            int coinsBefore = _wallet.Coins;

            _mining.RegisterActiveTap(false);
            _mining.RegisterActiveTap(false);
            _mining.RegisterActiveTap(false); // 3rd miss -> Failed

            Assert.IsNull(_mining.CurrentActiveSession);
            Assert.AreEqual(coinsBefore, _wallet.Coins);
            Assert.IsTrue(asteroid.IsDepleted, "a failed asteroid is still consumed with zero payout");
        }

        [Test]
        public void Initialize_restores_a_persisted_idle_session_for_the_current_planet()
        {
            var asteroid = MakeAndRegisterAsteroid("slot_0", remainingYield: 20);
            string value = $"test_planet|slot_0|{System.DateTime.UtcNow.AddMinutes(-10):O}|60";
            PlayerPrefs.SetString(SocialUniverse.Core.SaveKeys.IdleMiningSession, value);

            var droneDef = ScriptableObject.CreateInstance<DroneDefinition>();
            _mining.Initialize(new DroneRuntime(droneDef));

            Assert.IsNotNull(_mining.CurrentIdleSession);
            Assert.AreEqual(asteroid, _mining.CurrentIdleSession.Asteroid);
            Assert.AreEqual(IdleMiningStage.ReadyToClaim, _mining.CurrentIdleSession.Stage,
                "10 minutes elapsed against a 60s duration should already be ready to claim");

            Object.DestroyImmediate(droneDef);
        }

        [Test]
        public void Initialize_discards_a_persisted_session_for_a_different_planet()
        {
            MakeAndRegisterAsteroid("slot_0", remainingYield: 20);
            string value = $"other_planet|slot_0|{System.DateTime.UtcNow:O}|60";
            PlayerPrefs.SetString(SocialUniverse.Core.SaveKeys.IdleMiningSession, value);

            var droneDef = ScriptableObject.CreateInstance<DroneDefinition>();
            _mining.Initialize(new DroneRuntime(droneDef));

            Assert.IsNull(_mining.CurrentIdleSession);

            Object.DestroyImmediate(droneDef);
        }
    }
}
