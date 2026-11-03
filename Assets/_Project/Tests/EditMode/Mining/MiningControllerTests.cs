using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using NUnit.Framework;
using SocialUniverse.Config;
using SocialUniverse.Core;
using SocialUniverse.Mining;
using SocialUniverse.Net;
using SocialUniverse.Safety;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace SocialUniverse.Tests
{
    // Minimal IMineralService test double that captures the last grant call, or throws from
    // GrantMiningAsync (to reproduce a fire-and-forget grant call failing, e.g. network error,
    // after the asteroid has already been mined-out and the session torn down).
    public class CapturingMineralService : IMineralService
    {
        public string LastPlanetId;
        public string LastMineralId;
        public int    LastQty;
        public string LastIdleSlotId;
        public string LastSlotId;
        public int    GrantCalls;
        public bool   Throw;
        public (string PlanetId, string MineralId, string SlotId)? LastForfeit;
        public bool   ThrowOnForfeit;
        public string ForfeitReply = "FORFEITED";
        public string RejectReason; // when set, the grant is rejected with this reason
        public TaskCompletionSource<MiningGrantResult> Pending; // when set, the grant waits on it

        public Task<SellResult> SellAsync(string mineralId, int qty) => Task.FromResult(new SellResult { Success = true });
        public Task<SellResult> SellAllAsync() => Task.FromResult(new SellResult { Success = true });

        public Task<MiningGrantResult> ForfeitMiningAsync(string planetId, string mineralId, string slotId)
        {
            LastForfeit = (planetId, mineralId, slotId);
            if (ThrowOnForfeit) throw new System.InvalidOperationException("simulated");
            return Task.FromResult(new MiningGrantResult { Granted = 0, MineralId = mineralId, Reason = ForfeitReply });
        }

        public Task<MiningGrantResult> GrantMiningAsync(string planetId, string mineralId, int qty, string idleSlotId = null,
            string slotId = null)
        {
            GrantCalls++;
            if (Throw) throw new System.InvalidOperationException("simulated");
            LastPlanetId   = planetId;
            LastMineralId  = mineralId;
            LastQty        = qty;
            LastIdleSlotId = idleSlotId;
            LastSlotId     = slotId;
            if (Pending != null) return Pending.Task;
            return Task.FromResult(RejectReason != null
                ? new MiningGrantResult { Granted = 0, MineralId = mineralId, Reason = RejectReason }
                : new MiningGrantResult { Granted = qty, MineralId = mineralId });
        }
    }

    // IIdleMiningService double: by default every start succeeds and echoes the run back, like the
    // server's StartIdleMining. Replies (in order) or StartReply replace that reply; Pending holds
    // the start open.
    public class FakeIdleMiningService : IIdleMiningService
    {
        public IdleMiningSessionResult StartReply;
        public readonly Queue<IdleMiningSessionResult> Replies = new();
        public TaskCompletionSource<IdleMiningSessionResult> Pending;
        public (string PlanetId, string SlotId, string MineralId, float DurationSeconds)? LastStart;
        public string LastReplaceSlotId;
        public int StartCalls;

        public Task<IdleMiningSessionResult> StartAsync(string planetId, string slotId, string mineralId, float durationSeconds,
            string replaceSlotId = null)
        {
            StartCalls++;
            LastStart         = (planetId, slotId, mineralId, durationSeconds);
            LastReplaceSlotId = replaceSlotId;
            if (Pending != null) return Pending.Task;
            if (Replies.Count > 0) return Task.FromResult(Replies.Dequeue());
            return Task.FromResult(StartReply ?? new IdleMiningSessionResult
            {
                Success = true, PlanetId = planetId, SlotId = slotId, MineralId = mineralId, DurationSeconds = durationSeconds
            });
        }

        public Task<IdleMiningSessionResult> GetAsync() => Task.FromResult(new IdleMiningSessionResult { Success = true });
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
        private FakeIdleMiningService  _idle;
        private MiningRewardCalculator _rewardCalc;
        private ActiveMiningHandoff    _handoff;
        private AsteroidSpawner        _spawner;
        private DroneFleet             _fleet;
        private DroneDefinition        _droneDef;
        private DatabaseRegistry       _registry;
        private ActiveMiningRunStore   _runStore;
        private MiningController       _mining;

        private static void SetField(object target, string field, object value) =>
            target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);

        [SetUp]
        public void SetUp()
        {
            EventBus.Clear();
            PlayerPrefs.DeleteKey(SaveKeys.IdleMiningSession);
            PlayerPrefs.DeleteKey(SaveKeys.AsteroidRespawns);
            PlayerPrefs.DeleteKey(SaveKeys.ActiveMiningRun);

            _config = ScriptableObject.CreateInstance<EconomyConfig>();
            SetField(_config, "_idleSecondsPerYieldUnit", 0f);
            SetField(_config, "_minIdleSessionSeconds", 0.05f);
            SetField(_config, "_maxIdleSessionSeconds", 0.05f);
            SetField(_config, "_idleYieldFraction", 1f);
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
            _idle       = new FakeIdleMiningService();
            _rewardCalc = new MiningRewardCalculator(_config);
            _handoff    = new ActiveMiningHandoff();

            var spawnerGo = new GameObject("TestSpawner");
            _spawner = spawnerGo.AddComponent<AsteroidSpawner>();

            var auth = new LocalMockAuthService();
            SetField(auth, "_playerId", "p1");
            _runStore = new ActiveMiningRunStore(auth);

            _mining = NewController(_minerals);
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
            Object.DestroyImmediate(_spawner.gameObject);
            PlayerPrefs.DeleteKey(SaveKeys.IdleMiningSession);
            PlayerPrefs.DeleteKey(SaveKeys.AsteroidRespawns);
            PlayerPrefs.DeleteKey(SaveKeys.ActiveMiningRun);
            EventBus.Clear();
        }

        private MiningController NewController(IMineralService minerals) =>
            new MiningController(minerals, _rewardCalc, _spawner, _config, _planet, _handoff, new FakeAudioManager(), _fleet, _idle, _runStore);

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
            asteroid.Initialize(_asteroidDef, slotId, 1f, 1f);
            typeof(Asteroid).GetField("<RemainingYield>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(asteroid, remainingYield);

            var active = (List<Asteroid>)typeof(AsteroidSpawner)
                .GetField("_active", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(_spawner);
            active.Add(asteroid);

            return asteroid;
        }

        // What GetIdleMining returns for a run in progress.
        private static IdleMiningSessionResult ServerRun(string planetId, string slotId, float elapsed, float duration) =>
            new IdleMiningSessionResult
            {
                Success = true, PlanetId = planetId, SlotId = slotId, MineralId = "iron",
                ElapsedSeconds = elapsed, DurationSeconds = duration
            };

        private static string LegacySession(string planetId, string slotId, DateTime startUtc) =>
            $"{planetId}|{slotId}|{startUtc:O}|60";

        // Starts a run and waits out its 0.05 s duration, so it is ready to claim.
        private async Task StartAndFinish(MiningController mining, Asteroid asteroid)
        {
            Assert.IsTrue(await mining.BeginIdleMiningAsync(asteroid));
            await Task.Delay(100);
            mining.CurrentIdleSession.Tick(0f);
            Assert.AreEqual(IdleMiningStage.ReadyToClaim, mining.CurrentIdleSession.Stage);
        }

        [Test]
        public async Task ClaimIdleSessionAsync_grants_full_yield_and_schedules_respawn()
        {
            var asteroid = MakeAndRegisterAsteroid("slot_0", remainingYield: 20);
            await StartAndFinish(_mining, asteroid);

            await _mining.ClaimIdleSessionAsync(asteroid);

            Assert.IsNull(_mining.CurrentIdleSession);
            Assert.AreEqual("iron", _minerals.LastMineralId);
            Assert.AreEqual(20, _minerals.LastQty); // 20 yield * 1.0 effective mult
            Assert.IsTrue(asteroid.IsDepleted);
        }

        // The server's claim log records which asteroid each claim emptied, so every device keeps that
        // slot empty until it respawns (GetAsteroidRespawns).
        [Test]
        public async Task ClaimIdleSessionAsync_names_the_claimed_asteroid_slot()
        {
            var asteroid = MakeAndRegisterAsteroid("Iron#2", remainingYield: 20);
            await StartAndFinish(_mining, asteroid);

            await _mining.ClaimIdleSessionAsync(asteroid);

            Assert.AreEqual("Iron#2", _minerals.LastSlotId);
        }

        [Test]
        public async Task Active_mining_success_names_the_claimed_asteroid_slot()
        {
            var asteroid = MakeAndRegisterAsteroid("Iron#1", remainingYield: 10);
            Assert.IsTrue(_mining.BeginActiveMining(asteroid));
            _handoff.SetResult(succeeded: true, bankedFraction: 1f);

            _mining.Initialize();
            await Task.Yield();

            Assert.AreEqual("Iron#1", _minerals.LastSlotId);
        }

        // A failed run banked something: the asteroid is still lost, but the banked share is paid and
        // nothing is forfeited.
        [Test]
        public async Task A_failed_run_with_a_banked_share_claims_that_share()
        {
            var asteroid = MakeAndRegisterAsteroid("Iron#1", remainingYield: 80);
            Assert.IsTrue(_mining.BeginActiveMining(asteroid));
            _handoff.SetResult(succeeded: false, bankedFraction: 0.5f);

            _mining.Initialize();
            await Task.Yield();

            Assert.AreEqual(40, _minerals.LastQty);
            Assert.IsNull(_minerals.LastForfeit);
            Assert.AreEqual("Iron#1", _minerals.LastSlotId);
            Assert.IsTrue(asteroid.IsDepleted);
        }

        [Test]
        public async Task A_cleared_run_claims_its_banked_share()
        {
            var asteroid = MakeAndRegisterAsteroid("Iron#1", remainingYield: 80);
            Assert.IsTrue(_mining.BeginActiveMining(asteroid));
            _handoff.SetResult(succeeded: true, bankedFraction: 0.925f);

            _mining.Initialize();
            await Task.Yield();

            Assert.AreEqual(74, _minerals.LastQty);
        }

        // Planet's reload re-rolls an asteroid's yield, so the claim must not be recomputed from it.
        [Test]
        public async Task The_claim_uses_the_quantity_pinned_when_the_run_began()
        {
            var asteroid = MakeAndRegisterAsteroid("Iron#1", remainingYield: 10);
            Assert.IsTrue(_mining.BeginActiveMining(asteroid));
            typeof(Asteroid).GetField("<RemainingYield>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(asteroid, 12);
            _handoff.SetResult(succeeded: true, bankedFraction: 1f);

            _mining.Initialize();
            await Task.Yield();

            Assert.AreEqual(10, _minerals.LastQty);
        }

        [Test]
        public async Task A_run_that_banked_nothing_forfeits_the_asteroid()
        {
            var asteroid = MakeAndRegisterAsteroid("Iron#1", remainingYield: 80);
            Assert.IsTrue(_mining.BeginActiveMining(asteroid));
            _handoff.SetResult(succeeded: false, bankedFraction: 0f);

            _mining.Initialize();
            await Task.Yield();

            Assert.AreEqual(("test_planet", "iron", "Iron#1"), _minerals.LastForfeit);
            Assert.IsNull(_minerals.LastMineralId);
        }

        [Test]
        public void BeginActiveMining_pins_the_full_quantity_and_the_tier_settings()
        {
            var asteroid = MakeAndRegisterAsteroid("Iron#1", remainingYield: 80);

            Assert.IsTrue(_mining.BeginActiveMining(asteroid));

            Assert.AreEqual(80, _handoff.FullQuantity);
            Assert.AreEqual(1, _handoff.Settings.TierNumber);
        }

        [Test]
        public void BeginActiveMining_offers_the_dev_picker_the_configs_tier_settings()
        {
            var asteroid = MakeAndRegisterAsteroid("Iron#1", remainingYield: 80);

            Assert.IsTrue(_mining.BeginActiveMining(asteroid));

            Assert.IsNotNull(_handoff.DevSettingsForTier);
            Assert.IsNotNull(_handoff.DevSettingsForTier(3));
        }

        [Test]
        public async Task An_idle_claim_pays_the_idle_fraction()
        {
            SetField(_config, "_idleYieldFraction", 0.7f);
            var asteroid = MakeAndRegisterAsteroid("Iron#1", remainingYield: 80);
            await StartAndFinish(_mining, asteroid);

            await _mining.ClaimIdleSessionAsync(asteroid);

            Assert.AreEqual(56, _minerals.LastQty);
        }

        [Test]
        public async Task ClaimIdleSessionAsync_publishes_claim_completed_with_granted_mineral_and_quantity()
        {
            var asteroid = MakeAndRegisterAsteroid("slot_0", remainingYield: 20);
            await StartAndFinish(_mining, asteroid);

            IdleClaimCompletedEvent captured = null;
            EventBus.Subscribe<IdleClaimCompletedEvent>(e => captured = e);

            await _mining.ClaimIdleSessionAsync(asteroid);

            Assert.IsNotNull(captured, "a successful claim must publish IdleClaimCompletedEvent");
            Assert.AreEqual("iron", captured.MineralId);
            Assert.AreEqual(20, captured.Quantity); // matches the granted amount
        }

        [Test]
        public async Task ClaimIdleSessionAsync_does_not_publish_claim_completed_when_the_grant_throws()
        {
            var mining = NewController(new CapturingMineralService { Throw = true });

            var asteroid = MakeAndRegisterAsteroid("slot_0", remainingYield: 20);
            await StartAndFinish(mining, asteroid);

            IdleClaimCompletedEvent captured = null;
            EventBus.Subscribe<IdleClaimCompletedEvent>(e => captured = e);
            LogAssert.Expect(LogType.Error, new Regex("GrantMiningAsync.*"));

            await mining.ClaimIdleSessionAsync(asteroid);

            Assert.IsNull(captured, "a failed grant must not publish IdleClaimCompletedEvent — the player got nothing to celebrate");
        }

        [Test]
        public async Task BeginIdleMiningAsync_fails_while_a_session_is_already_running()
        {
            var a1 = MakeAndRegisterAsteroid("slot_0", 10);
            var a2 = MakeAndRegisterAsteroid("slot_1", 10);

            Assert.IsTrue(await _mining.BeginIdleMiningAsync(a1));
            Assert.IsFalse(await _mining.BeginIdleMiningAsync(a2));
            Assert.AreEqual(1, _idle.StartCalls, "a refused start never reaches the server");
        }

        [Test]
        public async Task BeginActiveMining_fails_while_the_drone_is_idle_mining_another_asteroid()
        {
            var idleAsteroid   = MakeAndRegisterAsteroid("slot_0", 10);
            var activeAsteroid = MakeAndRegisterAsteroid("slot_1", 10);

            Assert.IsTrue(await _mining.BeginIdleMiningAsync(idleAsteroid));
            Assert.IsFalse(_mining.BeginActiveMining(activeAsteroid));

            Assert.IsNotNull(_mining.CurrentIdleSession);
            Assert.IsNull(_handoff.AsteroidSlotId);
        }

        [Test]
        public async Task BeginIdleMiningAsync_fails_when_the_same_asteroid_already_has_an_active_mining_session()
        {
            var asteroid = MakeAndRegisterAsteroid("slot_0", 10);

            Assert.IsTrue(_mining.BeginActiveMining(asteroid));
            Assert.IsFalse(await _mining.BeginIdleMiningAsync(asteroid));
            Assert.IsNull(_mining.CurrentIdleSession);
        }

        [Test]
        public async Task BeginActiveMining_fails_when_the_same_asteroid_already_has_an_idle_mining_session()
        {
            var asteroid = MakeAndRegisterAsteroid("slot_0", 10);

            Assert.IsTrue(await _mining.BeginIdleMiningAsync(asteroid));
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
            _handoff.SetResult(succeeded: true, bankedFraction: 1f);

            _mining.Initialize();
            await Task.Yield(); // let the fire-and-forget payout Task complete

            Assert.AreEqual("iron", _minerals.LastMineralId);
            Assert.AreEqual(10, _minerals.LastQty); // 10 yield * 1.0 effective mult
            Assert.IsNull(_minerals.LastIdleSlotId, "active mining never ends an idle run");
            Assert.IsTrue(asteroid.IsDepleted);
            Assert.IsNull(_handoff.AsteroidSlotId, "handoff must be cleared after finalizing");
        }

        [Test]
        public void Initialize_finalizes_a_pending_active_mining_failure()
        {
            var asteroid = MakeAndRegisterAsteroid("slot_0", remainingYield: 10);
            Assert.IsTrue(_mining.BeginActiveMining(asteroid));
            _handoff.SetResult(succeeded: false, bankedFraction: 0f);

            _mining.Initialize();

            Assert.IsNull(_minerals.LastMineralId);
            Assert.IsTrue(asteroid.IsDepleted, "a failed asteroid is still consumed with zero payout");
            Assert.IsNull(_handoff.AsteroidSlotId);
        }

        // A failed minigame loses its asteroid: the server records the slot like a claim (using one of
        // the planet's claims, granting nothing), so it stays empty on every device until it respawns.
        [Test]
        public async Task Active_mining_failure_forfeits_the_asteroid_on_the_server()
        {
            var asteroid = MakeAndRegisterAsteroid("Iron#1", remainingYield: 10);
            Assert.IsTrue(_mining.BeginActiveMining(asteroid));
            _handoff.SetResult(succeeded: false, bankedFraction: 0f);

            _mining.Initialize();
            await Task.Yield();

            Assert.AreEqual(("test_planet", "iron", "Iron#1"), _minerals.LastForfeit);
            Assert.IsNull(_minerals.LastMineralId, "nothing is granted");
            Assert.IsTrue(_spawner.NextRespawnUtc.HasValue, "the slot is empty here straight away");
        }

        // The player already saw the run fail; a server that won't record the forfeit (an old server, a
        // full claim budget) changes nothing they can act on, so no rejection toast.
        [Test]
        public async Task Active_mining_failure_shows_no_rejection_when_the_server_refuses_the_forfeit()
        {
            var mining = NewController(new CapturingMineralService { ForfeitReply = "INVALID_PARAMS" });
            var asteroid = MakeAndRegisterAsteroid("Iron#1", remainingYield: 10);
            Assert.IsTrue(mining.BeginActiveMining(asteroid));
            _handoff.SetResult(succeeded: false, bankedFraction: 0f);
            MiningClaimRejectedEvent rejected = null;
            EventBus.Subscribe<MiningClaimRejectedEvent>(e => rejected = e);

            mining.Initialize();
            await Task.Yield();

            Assert.IsNull(rejected);
        }

        [Test]
        public async Task Active_mining_failure_still_empties_the_slot_when_the_forfeit_call_fails()
        {
            var minerals = new CapturingMineralService { ThrowOnForfeit = true };
            var mining   = NewController(minerals);
            var asteroid = MakeAndRegisterAsteroid("Iron#1", remainingYield: 10);
            Assert.IsTrue(mining.BeginActiveMining(asteroid));
            _handoff.SetResult(succeeded: false, bankedFraction: 0f);

            mining.Initialize();
            await Task.Yield();

            Assert.IsNotNull(minerals.LastForfeit, "the forfeit was attempted");
            Assert.IsTrue(_spawner.NextRespawnUtc.HasValue, "the asteroid is lost on this device either way");
        }

        [Test]
        public void Initialize_clears_a_pending_result_without_throwing_when_the_asteroid_no_longer_resolves()
        {
            var asteroid = MakeAndRegisterAsteroid("slot_0", remainingYield: 10);
            Assert.IsTrue(_mining.BeginActiveMining(asteroid));
            _handoff.SetResult(succeeded: true, bankedFraction: 1f);

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
        // would permanently block BeginActiveMining/BeginIdleMiningAsync on that asteroid. This can
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

        // ---- runs the player quit (the marker ActiveMiningSessionRunner saves) ----

        // The reload re-rolled the asteroid's yield, so the claim comes from the marker's pinned quantity.
        [Test]
        public async Task A_leftover_marker_claims_its_banked_share_and_uses_up_the_asteroid()
        {
            var asteroid = MakeAndRegisterAsteroid("Iron#1", remainingYield: 12);
            _runStore.Save("test_planet", "Iron#1", "iron", 80, 0.5f);

            _mining.Initialize();
            await Task.Yield();

            Assert.AreEqual(40, _minerals.LastQty);
            Assert.AreEqual("test_planet", _minerals.LastPlanetId);
            Assert.AreEqual("iron", _minerals.LastMineralId);
            Assert.AreEqual("Iron#1", _minerals.LastSlotId);
            Assert.IsNull(_minerals.LastForfeit);
            Assert.IsTrue(asteroid.IsDepleted);
            Assert.IsTrue(_spawner.NextRespawnUtc.HasValue, "the slot is empty here straight away");
            Assert.IsNull(_runStore.Load());
        }

        [Test]
        public async Task A_leftover_marker_with_nothing_banked_forfeits()
        {
            var asteroid = MakeAndRegisterAsteroid("Iron#1", remainingYield: 80);
            _runStore.Save("test_planet", "Iron#1", "iron", 80, 0f);

            _mining.Initialize();
            await Task.Yield();

            Assert.AreEqual(("test_planet", "iron", "Iron#1"), _minerals.LastForfeit);
            Assert.AreEqual(0, _minerals.GrantCalls);
            Assert.IsTrue(asteroid.IsDepleted);
            Assert.IsNull(_runStore.Load());
        }

        [Test]
        public async Task A_marker_for_another_planet_is_dropped_without_a_claim()
        {
            var asteroid = MakeAndRegisterAsteroid("Iron#1", remainingYield: 80);
            _runStore.Save("other_planet", "Iron#1", "iron", 80, 0.5f);

            _mining.Initialize();
            await Task.Yield();

            Assert.AreEqual(0, _minerals.GrantCalls);
            Assert.IsNull(_minerals.LastForfeit);
            Assert.IsFalse(asteroid.IsDepleted);
            Assert.IsNull(_runStore.Load());
        }

        // No answer came back, so the next Planet load tries again. The asteroid is still used up now.
        [Test]
        public async Task The_marker_stays_when_the_claim_throws()
        {
            MakeAndRegisterAsteroid("Iron#1", remainingYield: 80);
            _runStore.Save("test_planet", "Iron#1", "iron", 80, 0.5f);
            _minerals.Throw = true;
            LogAssert.Expect(LogType.Error, new Regex("GrantMiningAsync.*"));

            _mining.Initialize();
            await Task.Yield();

            Assert.IsNotNull(_runStore.Load());
            Assert.IsNull(_spawner.FindBySlotId("Iron#1"));
            Assert.IsTrue(_spawner.NextRespawnUtc.HasValue);
        }

        [Test]
        public async Task The_marker_stays_when_the_forfeit_throws()
        {
            MakeAndRegisterAsteroid("Iron#1", remainingYield: 80);
            _runStore.Save("test_planet", "Iron#1", "iron", 80, 0f);
            _minerals.ThrowOnForfeit = true;

            _mining.Initialize();
            await Task.Yield();

            Assert.IsNotNull(_runStore.Load());
            Assert.IsNull(_spawner.FindBySlotId("Iron#1"));
            Assert.IsTrue(_spawner.NextRespawnUtc.HasValue);
        }

        [Test]
        public async Task The_marker_stays_until_the_claim_is_answered()
        {
            MakeAndRegisterAsteroid("Iron#1", remainingYield: 80);
            _runStore.Save("test_planet", "Iron#1", "iron", 80, 0.5f);
            _minerals.Pending = new TaskCompletionSource<MiningGrantResult>();

            _mining.Initialize();
            Assert.IsNotNull(_runStore.Load());

            _minerals.Pending.SetResult(new MiningGrantResult { Granted = 40, MineralId = "iron" });
            await Task.Yield();

            Assert.IsNull(_runStore.Load());
        }

        [Test]
        public async Task The_marker_clears_when_the_server_rejects()
        {
            MakeAndRegisterAsteroid("Iron#1", remainingYield: 80);
            _runStore.Save("test_planet", "Iron#1", "iron", 80, 0.5f);
            _minerals.RejectReason = "BUDGET_EXHAUSTED";

            _mining.Initialize();
            await Task.Yield();

            Assert.AreEqual(1, _minerals.GrantCalls);
            Assert.IsNull(_runStore.Load());
        }

        [Test]
        public async Task The_marker_clears_when_the_server_refuses_the_forfeit()
        {
            MakeAndRegisterAsteroid("Iron#1", remainingYield: 80);
            _runStore.Save("test_planet", "Iron#1", "iron", 80, 0f);
            _minerals.ForfeitReply = "INVALID_PARAMS";

            _mining.Initialize();
            await Task.Yield();

            Assert.IsNotNull(_minerals.LastForfeit);
            Assert.IsNull(_runStore.Load());
        }

        [Test]
        public async Task The_normal_return_and_the_marker_claim_only_once()
        {
            var asteroid = MakeAndRegisterAsteroid("Iron#1", remainingYield: 80);
            Assert.IsTrue(_mining.BeginActiveMining(asteroid));
            _runStore.Save("test_planet", "Iron#1", "iron", 80, 1f);
            _handoff.SetResult(succeeded: true, bankedFraction: 1f);

            _mining.Initialize();
            await Task.Yield();

            Assert.AreEqual(1, _minerals.GrantCalls);
            Assert.AreEqual(80, _minerals.LastQty);
            Assert.IsNull(_runStore.Load());
        }

        // The normal return's claim failed, so its marker stays for the next load, not this one.
        [Test]
        public async Task A_normal_return_whose_claim_throws_does_not_retry_until_the_next_load()
        {
            var asteroid = MakeAndRegisterAsteroid("Iron#1", remainingYield: 80);
            Assert.IsTrue(_mining.BeginActiveMining(asteroid));
            _runStore.Save("test_planet", "Iron#1", "iron", 80, 1f);
            _handoff.SetResult(succeeded: true, bankedFraction: 1f);
            _minerals.Throw = true;
            LogAssert.Expect(LogType.Error, new Regex("GrantMiningAsync.*"));

            _mining.Initialize();
            await Task.Yield();

            Assert.AreEqual(1, _minerals.GrantCalls);
            Assert.AreEqual(1f, _runStore.Load().BankedFraction);
        }

        // The app was closed before the server's answer reached it, but the server committed: its claim
        // log keeps the slot off the field.
        [Test]
        public async Task A_marker_whose_slot_is_gone_clears_without_a_claim()
        {
            _runStore.Save("test_planet", "Iron#1", "iron", 80, 0.5f);

            _mining.Initialize();
            await Task.Yield();

            Assert.AreEqual(0, _minerals.GrantCalls);
            Assert.IsNull(_minerals.LastForfeit);
            Assert.IsNull(_runStore.Load());
        }

        [Test]
        public async Task A_marker_whose_asteroid_is_depleted_clears_without_a_claim()
        {
            var asteroid = MakeAndRegisterAsteroid("Iron#1", remainingYield: 80);
            asteroid.Mine(asteroid.RemainingYield);
            _runStore.Save("test_planet", "Iron#1", "iron", 80, 0.5f);

            _mining.Initialize();
            await Task.Yield();

            Assert.AreEqual(0, _minerals.GrantCalls);
            Assert.IsNull(_minerals.LastForfeit);
            Assert.IsNull(_runStore.Load());
        }

        // No claim can be sent for it, so keeping the marker would use up the asteroid on every load.
        [Test]
        public async Task A_marker_on_an_asteroid_with_no_mineral_clears_without_a_claim()
        {
            SetField(_asteroidDef, "_mineral", null);
            var asteroid = MakeAndRegisterAsteroid("Iron#1", remainingYield: 80);
            _runStore.Save("test_planet", "Iron#1", "iron", 80, 0.5f);

            _mining.Initialize();
            await Task.Yield();

            Assert.AreEqual(0, _minerals.GrantCalls);
            Assert.IsTrue(asteroid.IsDepleted);
            Assert.IsNull(_runStore.Load());
        }

        [Test]
        public void A_pending_result_whose_slot_is_gone_clears_the_marker()
        {
            var asteroid = MakeAndRegisterAsteroid("Iron#1", remainingYield: 80);
            Assert.IsTrue(_mining.BeginActiveMining(asteroid));
            _runStore.Save("test_planet", "Iron#1", "iron", 80, 1f);
            _handoff.SetResult(succeeded: true, bankedFraction: 1f);
            var active = (List<Asteroid>)typeof(AsteroidSpawner)
                .GetField("_active", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(_spawner);
            active.Remove(asteroid);

            _mining.Initialize();

            Assert.AreEqual(0, _minerals.GrantCalls);
            Assert.IsNull(_runStore.Load());
        }

        // A handoff that never got a result hands nothing over, so the marker is what settles the run.
        [Test]
        public async Task An_abandoned_handoff_still_settles_its_marker()
        {
            var asteroid = MakeAndRegisterAsteroid("Iron#1", remainingYield: 80);
            Assert.IsTrue(_mining.BeginActiveMining(asteroid));
            _runStore.Save("test_planet", "Iron#1", "iron", 80, 0.5f);

            _mining.Initialize();
            await Task.Yield();

            Assert.AreEqual(40, _minerals.LastQty);
            Assert.IsNull(_handoff.AsteroidSlotId);
            Assert.IsNull(_runStore.Load());
        }

        [Test]
        public async Task Initialize_never_leaves_the_idle_run_on_an_asteroid_a_marker_consumed()
        {
            MakeAndRegisterAsteroid("slot_0", remainingYield: 10);
            _runStore.Save("test_planet", "slot_0", "iron", 10, 1f);

            _mining.Initialize(ServerRun("test_planet", "slot_0", elapsed: 600f, duration: 60f));
            await Task.Yield();

            var session = _mining.CurrentIdleSession;
            Assert.IsTrue(session == null || session.Asteroid != null,
                "the idle session must never point at a destroyed asteroid");
        }

        // ---- legacy PlayerPrefs sessions (written by builds before server-tracked runs) ----

        [Test]
        public void Initialize_restores_a_persisted_idle_session_for_the_current_planet()
        {
            var asteroid = MakeAndRegisterAsteroid("slot_0", remainingYield: 20);
            PlayerPrefs.SetString(SaveKeys.IdleMiningSession, LegacySession("test_planet", "slot_0", DateTime.UtcNow.AddMinutes(-10)));

            _mining.Initialize();

            Assert.IsNotNull(_mining.CurrentIdleSession);
            Assert.AreEqual(asteroid, _mining.CurrentIdleSession.Asteroid);
            Assert.AreEqual(IdleMiningStage.ReadyToClaim, _mining.CurrentIdleSession.Stage,
                "10 minutes elapsed against a 60s duration should already be ready to claim");
            Assert.IsFalse(_mining.CurrentIdleSession.IsServerTracked, "a legacy session stays local");
        }

        [Test]
        public void Initialize_discards_a_persisted_session_for_a_different_planet()
        {
            MakeAndRegisterAsteroid("slot_0", remainingYield: 20);
            PlayerPrefs.SetString(SaveKeys.IdleMiningSession, LegacySession("other_planet", "slot_0", DateTime.UtcNow));

            _mining.Initialize();

            Assert.IsNull(_mining.CurrentIdleSession);
        }

        [Test]
        public async Task Claiming_a_legacy_session_names_no_idle_slot_and_deletes_the_key()
        {
            var asteroid = MakeAndRegisterAsteroid("slot_0", remainingYield: 20);
            PlayerPrefs.SetString(SaveKeys.IdleMiningSession, LegacySession("test_planet", "slot_0", DateTime.UtcNow.AddMinutes(-10)));
            _mining.Initialize();

            await _mining.ClaimIdleSessionAsync(asteroid);

            Assert.AreEqual("iron", _minerals.LastMineralId);
            Assert.IsNull(_minerals.LastIdleSlotId, "the server has no run to end for a legacy session");
            Assert.IsFalse(PlayerPrefs.HasKey(SaveKeys.IdleMiningSession));
        }

        // ---- server-tracked runs (docs/superpowers/plans/2026-10-15-server-tracked-idle-mining.md) ----

        [Test]
        public async Task BeginIdleMiningAsync_records_the_run_on_the_server_not_in_PlayerPrefs()
        {
            var asteroid = MakeAndRegisterAsteroid("slot_0", remainingYield: 20);

            Assert.IsTrue(await _mining.BeginIdleMiningAsync(asteroid));

            Assert.AreEqual(("test_planet", "slot_0", "iron", 0.05f), _idle.LastStart.Value);
            Assert.AreSame(asteroid, _mining.CurrentIdleSession.Asteroid);
            Assert.IsTrue(_mining.CurrentIdleSession.IsServerTracked);
            Assert.IsFalse(PlayerPrefs.HasKey(SaveKeys.IdleMiningSession), "new builds never write the legacy key");
        }

        [Test]
        public async Task A_rejected_start_starts_nothing_and_says_why()
        {
            var asteroid = MakeAndRegisterAsteroid("slot_0", 10);
            _idle.StartReply = new IdleMiningSessionResult { Success = false, Reason = "WRONG_PLANET" };
            MiningClaimRejectedEvent rejected = null;
            EventBus.Subscribe<MiningClaimRejectedEvent>(e => rejected = e);

            Assert.IsFalse(await _mining.BeginIdleMiningAsync(asteroid));

            Assert.IsNull(_mining.CurrentIdleSession);
            Assert.IsNotNull(rejected, "the toast handler turns this into text");
            Assert.AreEqual("WRONG_PLANET", rejected.Reason);
            Assert.AreEqual("iron", rejected.MineralId);
            Assert.IsTrue(_mining.BeginActiveMining(asteroid), "a finished start no longer blocks the drone");
        }

        [Test]
        public async Task A_start_refused_with_ALREADY_MINING_takes_over_the_servers_run_here()
        {
            var tapped = MakeAndRegisterAsteroid("slot_0", 10);
            var mined  = MakeAndRegisterAsteroid("slot_1", 10);
            _idle.StartReply = new IdleMiningSessionResult
            {
                Success = false, Reason = "ALREADY_MINING", PlanetId = "test_planet", SlotId = "slot_1",
                MineralId = "iron", ElapsedSeconds = 600f, DurationSeconds = 60f
            };
            MiningClaimRejectedEvent rejected = null;
            EventBus.Subscribe<MiningClaimRejectedEvent>(e => rejected = e);

            bool started = await _mining.BeginIdleMiningAsync(tapped);

            Assert.IsFalse(started, "the tapped asteroid did not start");
            Assert.AreSame(mined, _mining.CurrentIdleSession?.Asteroid, "the drone joins the run the server has");
            Assert.IsTrue(_mining.CurrentIdleSession.IsServerTracked);
            Assert.AreEqual(IdleMiningStage.ReadyToClaim, _mining.CurrentIdleSession.Stage, "600 s into a 60 s run");
            Assert.IsNull(rejected, "taking the run over is not a failure");
        }

        [Test]
        public async Task While_a_start_is_in_flight_another_start_and_active_mining_are_refused()
        {
            var a0 = MakeAndRegisterAsteroid("slot_0", 10);
            var a1 = MakeAndRegisterAsteroid("slot_1", 10);
            _idle.Pending = new TaskCompletionSource<IdleMiningSessionResult>();

            var first = _mining.BeginIdleMiningAsync(a0);

            Assert.IsFalse(await _mining.BeginIdleMiningAsync(a1));
            Assert.IsFalse(_mining.BeginActiveMining(a1), "the drone is about to leave");
            Assert.AreEqual(1, _idle.StartCalls);

            _idle.Pending.SetResult(new IdleMiningSessionResult
            {
                Success = true, PlanetId = "test_planet", SlotId = "slot_0", MineralId = "iron", DurationSeconds = 60f
            });

            Assert.IsTrue(await first);
            Assert.AreSame(a0, _mining.CurrentIdleSession.Asteroid);
            Assert.IsNull(_handoff.AsteroidSlotId);
        }

        // Final review #1: until ValidateMining answers, the server still holds the run being claimed,
        // so a start sent meanwhile would be told ALREADY_MINING about it (or take it over again).
        [Test]
        public async Task A_start_while_a_claim_is_in_flight_is_refused_without_reaching_the_server()
        {
            var claimed = MakeAndRegisterAsteroid("slot_0", remainingYield: 20);
            var next    = MakeAndRegisterAsteroid("slot_1", remainingYield: 20);
            await StartAndFinish(_mining, claimed);
            _minerals.Pending = new TaskCompletionSource<MiningGrantResult>();

            var claim = _mining.ClaimIdleSessionAsync(claimed);

            Assert.IsFalse(await _mining.BeginIdleMiningAsync(next));
            Assert.AreEqual(1, _idle.StartCalls, "only the claimed run's own start reached the server");

            _minerals.Pending.SetResult(new MiningGrantResult { Granted = 20, MineralId = "iron" });
            await claim;

            Assert.IsTrue(await _mining.BeginIdleMiningAsync(next), "once the claim has an answer the drone is free");
        }

        // Final review #2: a run on this planet whose asteroid this device can't place would otherwise
        // answer ALREADY_MINING forever, locking idle mining here.
        [Test]
        public async Task A_run_here_that_cannot_be_placed_is_replaced_by_the_start()
        {
            var asteroid = MakeAndRegisterAsteroid("slot_0", 10);
            _idle.Replies.Enqueue(new IdleMiningSessionResult
            {
                Success = false, Reason = "ALREADY_MINING", PlanetId = "test_planet", SlotId = "Gold#0",
                MineralId = "gold", ElapsedSeconds = 30f, DurationSeconds = 60f
            });
            MiningClaimRejectedEvent rejected = null;
            EventBus.Subscribe<MiningClaimRejectedEvent>(e => rejected = e);

            Assert.IsTrue(await _mining.BeginIdleMiningAsync(asteroid));

            Assert.AreEqual(2, _idle.StartCalls, "one retry");
            Assert.AreEqual("Gold#0", _idle.LastReplaceSlotId, "the retry replaces exactly the run it couldn't place");
            Assert.AreSame(asteroid, _mining.CurrentIdleSession?.Asteroid);
            Assert.IsNull(rejected);
        }

        // Final review #5: an active-mining result on the run's slot must not leave the restored idle
        // run pointing at the asteroid it consumed.
        [Test]
        public async Task Initialize_never_leaves_the_idle_run_on_an_asteroid_the_active_result_consumed()
        {
            var asteroid = MakeAndRegisterAsteroid("slot_0", remainingYield: 10);
            Assert.IsTrue(_mining.BeginActiveMining(asteroid));
            _handoff.SetResult(succeeded: true, bankedFraction: 1f);

            _mining.Initialize(ServerRun("test_planet", "slot_0", elapsed: 600f, duration: 60f));
            await Task.Yield();

            var session = _mining.CurrentIdleSession;
            Assert.IsTrue(session == null || session.Asteroid != null,
                "the idle session must never point at a destroyed asteroid");
        }

        // The mining prompt shows the drone as busy while its start is with the server.
        [Test]
        public async Task StartingAsteroid_names_the_asteroid_only_while_its_start_is_in_flight()
        {
            var asteroid = MakeAndRegisterAsteroid("slot_0", 10);
            _idle.Pending = new TaskCompletionSource<IdleMiningSessionResult>();
            Assert.IsNull(_mining.StartingAsteroid);

            var start = _mining.BeginIdleMiningAsync(asteroid);
            Assert.AreSame(asteroid, _mining.StartingAsteroid);

            _idle.Pending.SetResult(new IdleMiningSessionResult
            {
                Success = true, PlanetId = "test_planet", SlotId = "slot_0", MineralId = "iron", DurationSeconds = 60f
            });
            Assert.IsTrue(await start);
            Assert.IsNull(_mining.StartingAsteroid);
        }

        // Review Focus 3: the server recorded the run, but the Planet scene (and its asteroid) went
        // away before the answer arrived. The run is restored on the next load; nothing happens now.
        [Test]
        public async Task A_start_answered_after_its_asteroid_was_destroyed_starts_nothing_and_says_nothing()
        {
            var asteroid = MakeAndRegisterAsteroid("slot_0", 10);
            _idle.Pending = new TaskCompletionSource<IdleMiningSessionResult>();
            MiningClaimRejectedEvent rejected = null;
            EventBus.Subscribe<MiningClaimRejectedEvent>(e => rejected = e);

            var start = _mining.BeginIdleMiningAsync(asteroid);
            Object.DestroyImmediate(asteroid.gameObject);
            _idle.Pending.SetResult(new IdleMiningSessionResult
            {
                Success = true, PlanetId = "test_planet", SlotId = "slot_0", MineralId = "iron", DurationSeconds = 60f
            });

            Assert.IsFalse(await start);
            Assert.IsNull(_mining.CurrentIdleSession);
            Assert.IsNull(rejected, "no toast for a scene the player already left");
        }

        // Review Focus 1, end to end: the run is on a slot this device has waiting to respawn.
        [Test]
        public void Initialize_brings_back_the_server_runs_asteroid_this_device_has_waiting_to_respawn()
        {
            SetField(_asteroidDef, "_mineralType", "Iron");
            SetField(_planet, "_asteroidTypes", new[] { _asteroidDef });
            SetField(_planet, "_asteroidFieldSize", 2);
            SetField(_spawner, "_config", _config);
            _spawner.SpawnForPlanet(_planet); // Iron#0, Iron#1
            _spawner.ScheduleRespawn(_spawner.FindBySlotId("Iron#1"), 4f);

            _mining.Initialize(ServerRun("test_planet", "Iron#1", elapsed: 600f, duration: 60f));

            var session = _mining.CurrentIdleSession;
            Assert.IsNotNull(session, "the run must not be dropped because this device's timer says the slot is empty");
            Assert.AreEqual("Iron#1", session.Asteroid.SlotId);
            Assert.AreSame(session.Asteroid, _spawner.FindBySlotId("Iron#1"));
            Assert.AreEqual(IdleMiningStage.ReadyToClaim, session.Stage);
            Assert.IsNull(_spawner.NextRespawnUtc, "the stale timer is gone");
        }

        [Test]
        public async Task A_run_that_can_be_neither_placed_nor_replaced_shows_why_the_start_failed()
        {
            var asteroid = MakeAndRegisterAsteroid("slot_0", 10);
            _idle.Replies.Enqueue(new IdleMiningSessionResult
            {
                Success = false, Reason = "ALREADY_MINING", PlanetId = "test_planet", SlotId = "Gold#0",
                MineralId = "gold", DurationSeconds = 60f
            });
            _idle.Replies.Enqueue(new IdleMiningSessionResult { Success = false, Reason = "CONFLICT" });
            MiningClaimRejectedEvent rejected = null;
            EventBus.Subscribe<MiningClaimRejectedEvent>(e => rejected = e);

            Assert.IsFalse(await _mining.BeginIdleMiningAsync(asteroid));

            Assert.IsNull(_mining.CurrentIdleSession);
            Assert.AreEqual(2, _idle.StartCalls);
            Assert.AreEqual("CONFLICT", rejected?.Reason, "the toast explains the failed replace");
        }

        // A server-tracked run must reach ValidateMining even when it is worth nothing, or the server
        // never ends it and it comes back on every load.
        [Test]
        public async Task A_server_tracked_claim_worth_nothing_still_ends_the_run_on_the_server()
        {
            var asteroid = MakeAndRegisterAsteroid("slot_0", remainingYield: 20);
            await StartAndFinish(_mining, asteroid);
            SetField(_droneDef, "_yieldMultiplier", 0f); // round(20 × 0) = 0 units

            await _mining.ClaimIdleSessionAsync(asteroid);

            Assert.AreEqual("slot_0", _minerals.LastIdleSlotId, "the claim must still reach the server");
            Assert.AreEqual(0, _minerals.LastQty);
            Assert.IsTrue(_spawner.NextRespawnUtc.HasValue, "the asteroid is consumed as usual");
        }

        [Test]
        public void Initialize_restores_the_servers_run_on_this_planet_from_server_elapsed_time()
        {
            var asteroid = MakeAndRegisterAsteroid("slot_0", 20);

            _mining.Initialize(ServerRun("test_planet", "slot_0", elapsed: 600f, duration: 60f));

            var session = _mining.CurrentIdleSession;
            Assert.IsNotNull(session);
            Assert.AreSame(asteroid, session.Asteroid);
            Assert.IsTrue(session.WasRestored, "the drone snaps to the asteroid");
            Assert.IsTrue(session.IsServerTracked);
            Assert.AreEqual(IdleMiningStage.ReadyToClaim, session.Stage);
        }

        [Test]
        public void Initialize_anchors_an_unfinished_server_run_to_its_server_elapsed_time()
        {
            MakeAndRegisterAsteroid("slot_0", 20);

            _mining.Initialize(ServerRun("test_planet", "slot_0", elapsed: 10f, duration: 60f));

            var session = _mining.CurrentIdleSession;
            Assert.AreNotEqual(IdleMiningStage.ReadyToClaim, session.Stage);
            Assert.AreEqual(10f / 60f, session.MiningProgress01, 0.02f);
        }

        [Test]
        public void Initialize_prefers_the_servers_run_and_deletes_a_legacy_session()
        {
            MakeAndRegisterAsteroid("slot_0", 20);
            MakeAndRegisterAsteroid("slot_1", 20);
            PlayerPrefs.SetString(SaveKeys.IdleMiningSession, LegacySession("test_planet", "slot_1", DateTime.UtcNow));

            _mining.Initialize(ServerRun("test_planet", "slot_0", 0f, 60f));

            Assert.AreEqual("slot_0", _mining.CurrentIdleSession.Asteroid.SlotId);
            Assert.IsFalse(PlayerPrefs.HasKey(SaveKeys.IdleMiningSession));
        }

        [Test]
        public void Initialize_ignores_a_server_run_on_another_planet_and_restores_a_legacy_session_here()
        {
            var asteroid = MakeAndRegisterAsteroid("slot_0", 20);
            PlayerPrefs.SetString(SaveKeys.IdleMiningSession, LegacySession("test_planet", "slot_0", DateTime.UtcNow.AddMinutes(-10)));

            _mining.Initialize(ServerRun("other_planet", "slot_0", 0f, 60f));

            Assert.AreSame(asteroid, _mining.CurrentIdleSession?.Asteroid);
            Assert.IsFalse(_mining.CurrentIdleSession.IsServerTracked);
        }

        [Test]
        public void Initialize_shows_nothing_for_a_server_run_on_another_planet()
        {
            MakeAndRegisterAsteroid("slot_0", 20);

            _mining.Initialize(ServerRun("other_planet", "slot_0", 0f, 60f));

            Assert.IsNull(_mining.CurrentIdleSession);
        }

        [Test]
        public void Initialize_shows_nothing_for_a_server_run_here_whose_slot_cannot_be_placed()
        {
            MakeAndRegisterAsteroid("slot_0", 20);
            PlayerPrefs.SetString(SaveKeys.IdleMiningSession, LegacySession("test_planet", "slot_0", DateTime.UtcNow));

            _mining.Initialize(ServerRun("test_planet", "Gold#0", 0f, 60f));

            Assert.IsNull(_mining.CurrentIdleSession, "no fallback to the legacy session: the server's run wins");
            Assert.IsFalse(PlayerPrefs.HasKey(SaveKeys.IdleMiningSession));
        }

        [Test]
        public async Task Claiming_a_server_tracked_run_names_its_slot()
        {
            var asteroid = MakeAndRegisterAsteroid("slot_0", remainingYield: 20);
            await StartAndFinish(_mining, asteroid);

            await _mining.ClaimIdleSessionAsync(asteroid);

            Assert.AreEqual("slot_0", _minerals.LastIdleSlotId, "the claim ends the server's run");
        }

        [Test]
        public async Task Claiming_a_restored_server_run_names_its_slot()
        {
            var asteroid = MakeAndRegisterAsteroid("slot_0", remainingYield: 20);
            _mining.Initialize(ServerRun("test_planet", "slot_0", elapsed: 600f, duration: 60f));

            await _mining.ClaimIdleSessionAsync(asteroid);

            Assert.AreEqual("slot_0", _minerals.LastIdleSlotId);
            Assert.AreEqual(20, _minerals.LastQty);
        }

        // Regression test: previously, an exception thrown out of the fire-and-forget
        // GrantMiningAsync call (network error, backend hiccup) inside ClaimIdleSessionAsync
        // was unhandled, which meant the ScheduleRespawn call that runs immediately after it
        // never executed — the asteroid was left mined-out with no payout AND no respawn
        // scheduled, effectively lost forever. The fix wraps the grant call in try/catch so the
        // respawn still happens (for a server-tracked run the server keeps the run, so it comes
        // back ready on the next load).
        [Test]
        public async Task ClaimIdleSessionAsync_still_schedules_respawn_when_the_grant_call_throws()
        {
            var mining = NewController(new CapturingMineralService { Throw = true });

            var asteroid = MakeAndRegisterAsteroid("slot_0", remainingYield: 20);
            await StartAndFinish(mining, asteroid);

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
        public async Task ClaimIdleSessionAsync_sends_the_scene_planet_id()
        {
            var asteroid = MakeAndRegisterAsteroid("slot_0", remainingYield: 20);
            await StartAndFinish(_mining, asteroid);

            await _mining.ClaimIdleSessionAsync(asteroid);

            Assert.AreEqual("test_planet", _minerals.LastPlanetId);
        }

        [Test]
        public async Task ClaimIdleSessionAsync_rejection_publishes_rejected_event_not_claim_completed_and_still_respawns()
        {
            var mining = NewController(new CapturingMineralService { RejectReason = "BUDGET_EXHAUSTED" });

            var asteroid = MakeAndRegisterAsteroid("slot_0", remainingYield: 20);
            await StartAndFinish(mining, asteroid);

            IdleClaimCompletedEvent completed = null;
            MiningClaimRejectedEvent rejected = null;
            EventBus.Subscribe<IdleClaimCompletedEvent>(e => completed = e);
            EventBus.Subscribe<MiningClaimRejectedEvent>(e => rejected = e);

            await mining.ClaimIdleSessionAsync(asteroid);

            Assert.IsNull(completed, "no reward modal for a rejected claim");
            Assert.IsNotNull(rejected);
            Assert.AreEqual("iron", rejected.MineralId);
            Assert.AreEqual("BUDGET_EXHAUSTED", rejected.Reason);
            Assert.IsTrue(_spawner.NextRespawnUtc.HasValue, "the asteroid is consumed and respawns as usual");
        }

        [Test]
        public async Task Active_mining_rejection_publishes_rejected_event()
        {
            var mining = NewController(new CapturingMineralService { RejectReason = "TIER_TOO_LOW" });

            var asteroid = MakeAndRegisterAsteroid("slot_0", remainingYield: 10);
            Assert.IsTrue(mining.BeginActiveMining(asteroid));
            _handoff.SetResult(succeeded: true, bankedFraction: 1f);

            MiningClaimRejectedEvent rejected = null;
            EventBus.Subscribe<MiningClaimRejectedEvent>(e => rejected = e);

            mining.Initialize();
            await Task.Yield();

            Assert.IsNotNull(rejected);
            Assert.AreEqual("TIER_TOO_LOW", rejected.Reason);
        }

        [Test]
        public async Task BeginIdleMiningAsync_blocks_and_publishes_when_drone_tier_below_asteroid_tier()
        {
            // asteroid def Tier 2, active drone Tier 1
            SetField(_asteroidDef, "_tier", 2);
            MiningBlockedEvent captured = null;
            EventBus.Subscribe<MiningBlockedEvent>(e => captured = e);

            var asteroid = MakeAndRegisterAsteroid("slot_0", 10);
            bool started = await _mining.BeginIdleMiningAsync(asteroid);

            Assert.IsFalse(started);
            Assert.IsNull(_mining.CurrentIdleSession);
            Assert.IsNotNull(captured);
            Assert.AreEqual(2, captured.RequiredTier);
            Assert.AreEqual(0, _idle.StartCalls, "a locally refused start never reaches the server");
        }
    }
}
