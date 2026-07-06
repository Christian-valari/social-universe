using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using NUnit.Framework;
using SocialUniverse.Config;
using SocialUniverse.Core;
using SocialUniverse.Economy;
using SocialUniverse.Mining;
using UnityEngine;
using UnityEngine.TestTools;

namespace SocialUniverse.Tests
{
    // Minimal IEconomyService test double that always throws from GrantMiningRewardAsync, to
    // reproduce a fire-and-forget grant call failing (e.g. network error) after the asteroid
    // has already been mined-out and the session torn down.
    public class ThrowingEconomyService : IEconomyService
    {
        public Task<Wallet> GetWalletAsync() => throw new System.NotSupportedException("not used by this test");
        public Task<bool>   SpendCoinsAsync(int amount) => throw new System.NotSupportedException("not used by this test");
        public Task         GrantCoinsAsync(int amount) => throw new System.NotSupportedException("not used by this test");
        public Task         GrantStardustAsync(int amount) => throw new System.NotSupportedException("not used by this test");

        public Task<int> GrantMiningRewardAsync(int claimedCoins, float sessionDurationSec, float coinsPerSec)
            => throw new System.InvalidOperationException("simulated network failure");
    }

    public class MiningControllerTests
    {
        private EconomyConfig          _config;
        private AsteroidDefinition     _asteroidDef;
        private PlanetDefinition       _planet;
        private Wallet                 _wallet;
        private LocalMockEconomy       _economy;
        private MiningRewardCalculator _rewardCalc;
        private ActiveMiningHandoff    _handoff;
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
            SetField(_config, "_asteroidRespawnHours", 4f);

            _asteroidDef = ScriptableObject.CreateInstance<AsteroidDefinition>();
            SetField(_asteroidDef, "_coinsPerUnit", 2);

            _planet = ScriptableObject.CreateInstance<PlanetDefinition>();
            SetField(_planet, "_planetId", "test_planet");

            _wallet     = new Wallet();
            _economy    = new LocalMockEconomy(_wallet, _config);
            _rewardCalc = new MiningRewardCalculator(_config);
            _handoff    = new ActiveMiningHandoff();

            var spawnerGo = new GameObject("TestSpawner");
            _spawner = spawnerGo.AddComponent<AsteroidSpawner>();

            _mining = new MiningController(_economy, _rewardCalc, _spawner, _config, _planet, _handoff);
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
            Assert.AreEqual("slot_1", _handoff.AsteroidSlotId);
        }

        [Test]
        public void BeginIdleMining_fails_when_the_same_asteroid_already_has_an_active_mining_session()
        {
            var asteroid = MakeAndRegisterAsteroid("slot_0", 10);

            Assert.IsTrue(_mining.BeginActiveMining(asteroid));
            Assert.IsFalse(_mining.BeginIdleMining(asteroid));
            Assert.IsNull(_mining.CurrentIdleSession);
        }

        [Test]
        public void BeginActiveMining_fails_when_the_same_asteroid_already_has_an_idle_mining_session()
        {
            var asteroid = MakeAndRegisterAsteroid("slot_0", 10);

            Assert.IsTrue(_mining.BeginIdleMining(asteroid));
            Assert.IsFalse(_mining.BeginActiveMining(asteroid));
            Assert.IsNull(_handoff.AsteroidSlotId);
        }

        [Test]
        public void BeginActiveMining_fails_while_a_previous_active_mining_result_is_still_pending_finalize()
        {
            var a1 = MakeAndRegisterAsteroid("slot_0", 10);
            var a2 = MakeAndRegisterAsteroid("slot_1", 10);

            Assert.IsTrue(_mining.BeginActiveMining(a1));
            Assert.IsFalse(_mining.BeginActiveMining(a2));
        }

        [Test]
        public async Task Initialize_finalizes_a_pending_active_mining_success()
        {
            var asteroid = MakeAndRegisterAsteroid("slot_0", remainingYield: 10);
            Assert.IsTrue(_mining.BeginActiveMining(asteroid));
            _handoff.SetResult(succeeded: true);
            int coinsBefore = _wallet.Coins;

            var droneDef = ScriptableObject.CreateInstance<DroneDefinition>();
            _mining.Initialize(new DroneRuntime(droneDef));
            await Task.Yield(); // let the fire-and-forget payout Task complete

            Assert.AreEqual(coinsBefore + 20, _wallet.Coins); // 10 yield * 2 coins/unit
            Assert.IsTrue(asteroid.IsDepleted);
            Assert.IsNull(_handoff.AsteroidSlotId, "handoff must be cleared after finalizing");

            Object.DestroyImmediate(droneDef);
        }

        [Test]
        public void Initialize_finalizes_a_pending_active_mining_failure()
        {
            var asteroid = MakeAndRegisterAsteroid("slot_0", remainingYield: 10);
            Assert.IsTrue(_mining.BeginActiveMining(asteroid));
            _handoff.SetResult(succeeded: false);
            int coinsBefore = _wallet.Coins;

            var droneDef = ScriptableObject.CreateInstance<DroneDefinition>();
            _mining.Initialize(new DroneRuntime(droneDef));

            Assert.AreEqual(coinsBefore, _wallet.Coins);
            Assert.IsTrue(asteroid.IsDepleted, "a failed asteroid is still consumed with zero payout");
            Assert.IsNull(_handoff.AsteroidSlotId);

            Object.DestroyImmediate(droneDef);
        }

        [Test]
        public void Initialize_clears_a_pending_result_without_throwing_when_the_asteroid_no_longer_resolves()
        {
            var asteroid = MakeAndRegisterAsteroid("slot_0", remainingYield: 10);
            Assert.IsTrue(_mining.BeginActiveMining(asteroid));
            _handoff.SetResult(succeeded: true);

            var active = (List<Asteroid>)typeof(AsteroidSpawner)
                .GetField("_active", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(_spawner);
            active.Remove(asteroid); // simulate the slot no longer resolving (e.g. already respawned)

            var droneDef = ScriptableObject.CreateInstance<DroneDefinition>();
            Assert.DoesNotThrow(() => _mining.Initialize(new DroneRuntime(droneDef)));

            Assert.IsNull(_handoff.AsteroidSlotId);

            Object.DestroyImmediate(droneDef);
        }

        [Test]
        public void Initialize_does_nothing_when_no_active_mining_result_is_pending()
        {
            int coinsBefore = _wallet.Coins;
            var droneDef = ScriptableObject.CreateInstance<DroneDefinition>();

            Assert.DoesNotThrow(() => _mining.Initialize(new DroneRuntime(droneDef)));

            Assert.AreEqual(coinsBefore, _wallet.Coins);

            Object.DestroyImmediate(droneDef);
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

        // Regression test: previously, an exception thrown out of the fire-and-forget
        // GrantMiningRewardAsync call (network error, backend hiccup) inside
        // ClaimIdleSessionAsync was unhandled, which meant the ScheduleRespawn call that runs
        // immediately after it never executed — the asteroid was left mined-out with no
        // payout AND no respawn scheduled, effectively lost forever. The fix wraps the grant
        // call in try/catch so the respawn still happens (the player does lose the coins on a
        // genuine failure — that tradeoff is accepted).
        [Test]
        public async Task ClaimIdleSessionAsync_still_schedules_respawn_when_the_grant_call_throws()
        {
            var throwingEconomy = new ThrowingEconomyService();
            var mining = new MiningController(throwingEconomy, _rewardCalc, _spawner, _config, _planet, _handoff);

            var asteroid = MakeAndRegisterAsteroid("slot_0", remainingYield: 20);
            Assert.IsTrue(mining.BeginIdleMining(asteroid));

            await Task.Delay(100);
            mining.CurrentIdleSession.Tick(0f);
            Assert.AreEqual(IdleMiningStage.ReadyToClaim, mining.CurrentIdleSession.Stage);

            LogAssert.Expect(LogType.Error, new Regex("GrantMiningRewardAsync failed for idle claim.*"));

            // The exception from GrantMiningRewardAsync must be caught internally — it must
            // not propagate out of ClaimIdleSessionAsync itself (which is invoked
            // fire-and-forget in production and would otherwise silently swallow it anyway).
            Assert.DoesNotThrowAsync(async () => await mining.ClaimIdleSessionAsync(asteroid));

            Assert.IsNull(mining.CurrentIdleSession);
            Assert.IsNull(_spawner.FindBySlotId("slot_0"), "the claimed asteroid must no longer be active/findable by its old slot");
            Assert.IsTrue(_spawner.NextRespawnUtc.HasValue, "asteroid must still be scheduled for respawn even though the grant call failed");
        }
    }
}
