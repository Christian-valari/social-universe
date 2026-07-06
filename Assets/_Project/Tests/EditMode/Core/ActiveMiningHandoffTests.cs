using NUnit.Framework;
using SocialUniverse.Config;
using SocialUniverse.Core;
using UnityEngine;

namespace SocialUniverse.Tests
{
    public class ActiveMiningHandoffTests
    {
        private AsteroidDefinition  _def;
        private ActiveMiningHandoff _handoff;

        [SetUp]
        public void SetUp()
        {
            _def     = ScriptableObject.CreateInstance<AsteroidDefinition>();
            _handoff = new ActiveMiningHandoff();
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_def);

        [Test]
        public void HasResult_starts_false()
        {
            Assert.IsFalse(_handoff.HasResult);
        }

        [Test]
        public void AsteroidSlotId_starts_null()
        {
            Assert.IsNull(_handoff.AsteroidSlotId);
        }

        [Test]
        public void Begin_captures_everything_needed_to_resume_and_finalize()
        {
            _handoff.Begin("earth", "slot_3", _def, remainingYieldAtStart: 16,
                tapsRequired: 2, maxErrors: 3, sessionDurationSeconds: 6f);

            Assert.AreEqual("earth", _handoff.PlanetId);
            Assert.AreEqual("slot_3", _handoff.AsteroidSlotId);
            Assert.AreEqual(_def, _handoff.Definition);
            Assert.AreEqual(16, _handoff.RemainingYieldAtStart);
            Assert.AreEqual(2, _handoff.TapsRequired);
            Assert.AreEqual(3, _handoff.MaxErrors);
            Assert.AreEqual(6f, _handoff.SessionDurationSeconds, 0.001f);
            Assert.IsFalse(_handoff.HasResult);
        }

        [Test]
        public void SetResult_records_the_outcome()
        {
            _handoff.Begin("earth", "slot_0", _def, 10, 2, 3, 6f);

            _handoff.SetResult(succeeded: true);

            Assert.IsTrue(_handoff.HasResult);
            Assert.IsTrue(_handoff.Succeeded);
        }

        [Test]
        public void Clear_resets_result_and_slot_tracking()
        {
            _handoff.Begin("earth", "slot_0", _def, 10, 2, 3, 6f);
            _handoff.SetResult(succeeded: false);

            _handoff.Clear();

            Assert.IsFalse(_handoff.HasResult);
            Assert.IsNull(_handoff.AsteroidSlotId);
        }
    }
}
