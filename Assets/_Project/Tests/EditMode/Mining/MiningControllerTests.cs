using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using NUnit.Framework;
using SocialUniverse.Config;
using SocialUniverse.Core;
using SocialUniverse.Mining;
using SocialUniverse.Safety;
using UnityEngine;
using UnityEngine.TestTools;

namespace SocialUniverse.Tests
{
    // Minimal IMineralService test double that captures the last grant call, or throws from
    // GrantMiningAsync (to reproduce a fire-and-forget grant call failing, e.g. network error,
    // after the asteroid has already been mined-out and the session torn down).
    public class CapturingMineralService : IMineralService
    {
        public string LastMineralId;
        public int    LastQty;
        public bool   Throw;

        public Task<SellResult> SellAsync(string mineralId, int qty) => Task.FromResult(new SellResult { Success = true });
        public Task<SellResult> SellAllAsync() => Task.FromResult(new SellResult { Success = true });

        public Task<int> GrantMiningAsync(string mineralId, int qty, float sessionDurationSec, float unitsPerSec)
        {
            if (Throw) throw new System.InvalidOperationException("simulated");
            LastMineralId = mineralId;
            LastQty       = qty;
            return Task.FromResult(qty);
        }
    }

    public class FakeAudioManager : IAudioManager
    {
        public void PlaySfx(SfxId id) { }
        public void PlayBgmForPlanet(PlanetDefinition planet) { }
        public void PlaySolarSystemBgm() { }
        public void PlayTravelBgm() { }
    }

    public class MiningControllerTests
    {
        private EconomyConfig          _config;
        private AsteroidDefinition     _asteroidDef;
        private MineralDefinition      _mineralDef;
        private PlanetDefinition       _planet;
        private CapturingMineralService _minerals;
        private MiningRewardCalculator _rewardCalc;
        private ActiveMiningHandoff    _handoff;
        private AsteroidSpawner        _spawner;
        private DroneFleet             _fleet;
        private DroneDefinition        _droneDef;
        private DatabaseRegistry       _registry;
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

            _mineralDef = ScriptableObject.CreateInstance<MineralDefinition>();
            SetField(_mineralDef, "_mineralId", "iron");
            SetField(_mineralDef, "_tier", 1);

            _asteroidDef = ScriptableObject.CreateInstance<AsteroidDefinition>();
            SetField(_asteroidDef, "_coinsPerUnit", 2);
            SetField(_asteroidDef, "_mineral", _mineralDef);
            SetField(_asteroidDef, "_tier", 1);

            _planet = ScriptableObject.CreateInstance<PlanetDefinition>();
            SetField(_planet, "_planetId", "test_planet");

            _droneDef = ScriptableObject.CreateInstance<DroneDefinition>();
            SetField(_droneDef, "_droneId", "starter_drone");
            SetField(_droneDef, "_tier", 1);
            SetField(_droneDef, "_yieldMultiplier", 1f);

            _registry = MakeRegistry(_droneDef);
            _fleet = new DroneFleet();
            _fleet.Apply(DroneFleetSnapshot.SingleDrone("starter_drone", 1), _registry);

            _minerals   = new CapturingMineralService();
            _rewardCalc = new MiningRewardCalculator(_config);
            _handoff    = new ActiveMiningHandoff();

            var spawnerGo = new GameObject("TestSpawner");
            _spawner = spawnerGo.AddComponent<AsteroidSpawner>();

            _mining = new MiningController(_minerals, _rewardCalc, _spawner, _config, _planet, _handoff, new FakeAudioManager(), _fleet);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_config);
            Object.DestroyImmediate(_asteroidDef);
            Object.DestroyImmediate(_mineralDef);
            Object.DestroyImmediate(_planet);
            Object.DestroyImmediate(_droneDef);
            Object.DestroyImmediate(_registry);
            PlayerPrefs.DeleteKey(SocialUniverse.Core.SaveKeys.IdleMiningSession);
        }

        // Minimal registry stub carrying just the one drone def DroneFleet.Apply needs to
        // resolve DroneSnapshot.DroneId -> DroneDefinition.
        private static DatabaseRegistry MakeRegistry(DroneDefinition droneDef)
        {
            var registry = ScriptableObject.CreateInstance<DatabaseRegistry>();
            SetField(registry, "_drones", new[] { droneDef });
            SetField(registry, "_upgrades", System.Array.Empty<UpgradeDefinition>());
            return registry;
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

            await _mining.ClaimIdleSessionAsync(asteroid);

            Assert.IsNull(_mining.CurrentIdleSession);
            Assert.AreEqual("iron", _minerals.LastMineralId);
            Assert.AreEqual(20, _minerals.LastQty); // 20 yield * 1.0 effective mult
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

            _mining.Initialize();
            await Task.Yield(); // let the fire-and-forget payout Task complete

            Assert.AreEqual("iron", _minerals.LastMineralId);
            Assert.AreEqual(10, _minerals.LastQty); // 10 yield * 1.0 effective mult
            Assert.IsTrue(asteroid.IsDepleted);
            Assert.IsNull(_handoff.AsteroidSlotId, "handoff must be cleared after finalizing");
        }

        [Test]
        public void Initialize_finalizes_a_pending_active_mining_failure()
        {
            var asteroid = MakeAndRegisterAsteroid("slot_0", remainingYield: 10);
            Assert.IsTrue(_mining.BeginActiveMining(asteroid));
            _handoff.SetResult(succeeded: false);

            _mining.Initialize();

            Assert.IsNull(_minerals.LastMineralId);
            Assert.IsTrue(asteroid.IsDepleted, "a failed asteroid is still consumed with zero payout");
            Assert.IsNull(_handoff.AsteroidSlotId);
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

            Assert.DoesNotThrow(() => _mining.Initialize());

            Assert.IsNull(_handoff.AsteroidSlotId);
        }

        [Test]
        public void Initialize_does_nothing_when_no_active_mining_result_is_pending()
        {
            Assert.DoesNotThrow(() => _mining.Initialize());

            Assert.IsNull(_minerals.LastMineralId);
        }

        // Regression test: reaching Planet with a handoff that was started but never resolved
        // (SetResult was never called) used to leave _handoff.AsteroidSlotId set forever, which
        // would permanently block BeginActiveMining/BeginIdleMining on that asteroid. This can
        // only happen in practice via the standalone dev workflow (no FSM to consume
        // ActiveMiningRequestedEvent), but Initialize must still clear it defensively.
        [Test]
        public void Initialize_clears_an_abandoned_handoff_that_never_got_a_result()
        {
            var asteroid = MakeAndRegisterAsteroid("slot_0", remainingYield: 10);
            Assert.IsTrue(_mining.BeginActiveMining(asteroid));

            Assert.DoesNotThrow(() => _mining.Initialize());

            Assert.IsNull(_minerals.LastMineralId, "no result was ever set, so no grant/mine should happen");
            Assert.IsFalse(asteroid.IsDepleted);
            Assert.IsNull(_handoff.AsteroidSlotId, "the abandoned handoff must still be cleared");
        }

        [Test]
        public void Initialize_restores_a_persisted_idle_session_for_the_current_planet()
        {
            var asteroid = MakeAndRegisterAsteroid("slot_0", remainingYield: 20);
            string value = $"test_planet|slot_0|{System.DateTime.UtcNow.AddMinutes(-10):O}|60";
            PlayerPrefs.SetString(SocialUniverse.Core.SaveKeys.IdleMiningSession, value);

            _mining.Initialize();

            Assert.IsNotNull(_mining.CurrentIdleSession);
            Assert.AreEqual(asteroid, _mining.CurrentIdleSession.Asteroid);
            Assert.AreEqual(IdleMiningStage.ReadyToClaim, _mining.CurrentIdleSession.Stage,
                "10 minutes elapsed against a 60s duration should already be ready to claim");
        }

        [Test]
        public void Initialize_discards_a_persisted_session_for_a_different_planet()
        {
            MakeAndRegisterAsteroid("slot_0", remainingYield: 20);
            string value = $"other_planet|slot_0|{System.DateTime.UtcNow:O}|60";
            PlayerPrefs.SetString(SocialUniverse.Core.SaveKeys.IdleMiningSession, value);

            _mining.Initialize();

            Assert.IsNull(_mining.CurrentIdleSession);
        }

        // Regression test: previously, an exception thrown out of the fire-and-forget
        // GrantMiningAsync call (network error, backend hiccup) inside ClaimIdleSessionAsync
        // was unhandled, which meant the ScheduleRespawn call that runs immediately after it
        // never executed — the asteroid was left mined-out with no payout AND no respawn
        // scheduled, effectively lost forever. The fix wraps the grant call in try/catch so the
        // respawn still happens (the player does lose the minerals on a genuine failure — that
        // tradeoff is accepted).
        [Test]
        public async Task ClaimIdleSessionAsync_still_schedules_respawn_when_the_grant_call_throws()
        {
            var throwingMinerals = new CapturingMineralService { Throw = true };
            var mining = new MiningController(throwingMinerals, _rewardCalc, _spawner, _config, _planet, _handoff, new FakeAudioManager(), _fleet);

            var asteroid = MakeAndRegisterAsteroid("slot_0", remainingYield: 20);
            Assert.IsTrue(mining.BeginIdleMining(asteroid));

            await Task.Delay(100);
            mining.CurrentIdleSession.Tick(0f);
            Assert.AreEqual(IdleMiningStage.ReadyToClaim, mining.CurrentIdleSession.Stage);

            LogAssert.Expect(LogType.Error, new Regex("GrantMiningAsync.*"));

            // The exception from GrantMiningAsync must be caught internally — it must
            // not propagate out of ClaimIdleSessionAsync itself (which is invoked
            // fire-and-forget in production and would otherwise silently swallow it anyway).
            Assert.DoesNotThrowAsync(async () => await mining.ClaimIdleSessionAsync(asteroid));

            Assert.IsNull(mining.CurrentIdleSession);
            Assert.IsNull(_spawner.FindBySlotId("slot_0"), "the claimed asteroid must no longer be active/findable by its old slot");
            Assert.IsTrue(_spawner.NextRespawnUtc.HasValue, "asteroid must still be scheduled for respawn even though the grant call failed");
        }

        [Test]
        public void BeginIdleMining_blocks_and_publishes_when_drone_tier_below_asteroid_tier()
        {
            // asteroid def Tier 2, active drone Tier 1
            SetField(_asteroidDef, "_tier", 2);
            MiningBlockedEvent captured = null;
            EventBus.Subscribe<MiningBlockedEvent>(e => captured = e);

            var asteroid = MakeAndRegisterAsteroid("slot_0", 10);
            bool started = _mining.BeginIdleMining(asteroid);

            Assert.IsFalse(started);
            Assert.IsNull(_mining.CurrentIdleSession);
            Assert.IsNotNull(captured);
            Assert.AreEqual(2, captured.RequiredTier);

            EventBus.Clear();
        }
    }
}
