using System.Reflection;
using NUnit.Framework;
using SocialUniverse.Config;
using SocialUniverse.Core;
using SocialUniverse.Mining;
using SocialUniverse.Net;
using UnityEngine;
using UnityEngine.TestTools;

namespace SocialUniverse.Tests
{
    public class ActiveMiningSessionRunnerTests
    {
        private AsteroidDefinition        _definition;
        private MineralDefinition         _mineral;
        private ActiveMiningHandoff       _handoff;
        private ActiveMiningRunStore      _store;
        private ActiveMiningSessionRunner _runner;

        private static void SetField(object target, string field, object value) =>
            target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);

        // No spawn gap or grace, and a tap on a fresh ring grades Perfect, which banks a full share.
        private static ActiveMiningRunSettings Settings(int tierNumber = 1) =>
            new ActiveMiningRunSettings(tierNumber, new ActiveMiningTierSettings(2, 1.6f, 0.8f, 1.2f, 3), null, 1f, 0.8f, 0.6f, 0f, 0f);

        private static void HitNextTarget(ActiveMiningSession session)
        {
            session.Tick(0f);
            session.TapTarget(session.LiveTargets[0].Id);
        }

        [SetUp]
        public void SetUp()
        {
            PlayerPrefs.DeleteKey(SaveKeys.ActiveMiningRun);

            _mineral = ScriptableObject.CreateInstance<MineralDefinition>();
            SetField(_mineral, "_mineralId", "iron");
            _definition = ScriptableObject.CreateInstance<AsteroidDefinition>();
            SetField(_definition, "_mineral", _mineral);

            var auth = new LocalMockAuthService();
            SetField(auth, "_playerId", "p1");

            _handoff = new ActiveMiningHandoff();
            _store   = new ActiveMiningRunStore(auth);
            _runner  = new ActiveMiningSessionRunner(_handoff, _store);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_definition);
            Object.DestroyImmediate(_mineral);
            PlayerPrefs.DeleteKey(SaveKeys.ActiveMiningRun);
        }

        // Leaving from the pre-game panel costs nothing: the asteroid is still there.
        [Test]
        public void Nothing_is_saved_before_the_run_begins()
        {
            _handoff.Begin("earth", "Iron#1", _definition, 80, Settings());

            _runner.SaveProgress();

            Assert.IsNull(_store.Load());
        }

        [Test]
        public void BeginRun_saves_a_marker_with_nothing_banked()
        {
            _handoff.Begin("earth", "Iron#1", _definition, 80, Settings());

            _runner.BeginRun();

            var record = _store.Load();
            Assert.AreEqual("earth", record.PlanetId);
            Assert.AreEqual("Iron#1", record.SlotId);
            Assert.AreEqual("iron", record.MineralId);
            Assert.AreEqual(80, record.FullQuantity);
            Assert.AreEqual(0f, record.BankedFraction);
        }

        // A standalone ActiveMining scene has no asteroid to settle later.
        [Test]
        public void A_run_not_begun_through_the_handoff_saves_nothing()
        {
            _handoff.Begin(null, null, _definition, 0, Settings());

            var session = _runner.BeginRun();
            HitNextTarget(session);
            _runner.SaveProgress();

            Assert.IsNull(_store.Load());
        }

        [Test]
        public void A_hit_alone_saves_nothing_new()
        {
            _handoff.Begin("earth", "Iron#1", _definition, 80, Settings());
            var session = _runner.BeginRun();

            HitNextTarget(session);

            Assert.AreEqual(0f, _store.Load().BankedFraction);
        }

        [Test]
        public void SaveProgress_records_the_banked_share()
        {
            _handoff.Begin("earth", "Iron#1", _definition, 80, Settings());
            var session = _runner.BeginRun();
            HitNextTarget(session);

            _runner.SaveProgress();

            Assert.AreEqual(0.5f, _store.Load().BankedFraction);
        }

        [Test]
        public void The_final_result_is_saved_and_handed_off()
        {
            _handoff.Begin("earth", "Iron#1", _definition, 80, Settings());
            var session = _runner.BeginRun();

            HitNextTarget(session);
            HitNextTarget(session);

            Assert.AreEqual(ActiveMiningStage.Success, session.Stage);
            Assert.IsTrue(_handoff.HasResult);
            Assert.IsTrue(_handoff.Succeeded);
            Assert.AreEqual(1f, _handoff.BankedFraction);
            Assert.AreEqual(_handoff.BankedFraction, _store.Load().BankedFraction);
        }

        [Test]
        public void The_end_of_a_run_logs_the_tier_it_played()
        {
            _handoff.Begin("earth", "Iron#1", _definition, 80, Settings(tierNumber: 4));
            var session = _runner.BeginRun();
            HitNextTarget(session);

            LogAssert.Expect(LogType.Log, "[SU:Mining] ActiveMining run: tier=4 perfect=2 good=0 late=0 rings=0 decoys=0 wrong=0 banked=1.00 result=Success");
            HitNextTarget(session);
        }

        // Planet clears the handoff when it settles the run, and a pause then must not bring the marker back.
        [Test]
        public void SaveProgress_after_the_handoff_is_cleared_saves_nothing()
        {
            _handoff.Begin("earth", "Iron#1", _definition, 80, Settings());
            _runner.BeginRun();
            _store.Clear();
            _handoff.Clear();

            _runner.SaveProgress();

            Assert.IsNull(_store.Load());
        }
    }
}
