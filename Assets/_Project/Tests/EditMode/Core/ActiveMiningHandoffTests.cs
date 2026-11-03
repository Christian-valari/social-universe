using System.Reflection;
using NUnit.Framework;
using SocialUniverse.Config;
using SocialUniverse.Core;
using UnityEngine;

namespace SocialUniverse.Tests
{
    public class ActiveMiningHandoffTests
    {
        private AsteroidDefinition     _def;
        private ActiveMiningHandoff    _handoff;
        private ActiveMiningRunSettings _settings;

        private static void SetField(Object target, string field, object value) =>
            target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);

        [SetUp]
        public void SetUp()
        {
            _def      = ScriptableObject.CreateInstance<AsteroidDefinition>();
            _handoff  = new ActiveMiningHandoff();
            _settings = new ActiveMiningRunSettings(1, new ActiveMiningTierSettings(), null, 1f, 0.8f, 0.6f, 0.25f, 0.2f);
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
            _handoff.Begin("earth", "slot_3", _def, fullQuantity: 16, _settings);

            Assert.AreEqual("earth", _handoff.PlanetId);
            Assert.AreEqual("slot_3", _handoff.AsteroidSlotId);
            Assert.AreEqual(_def, _handoff.Definition);
            Assert.AreEqual(16, _handoff.FullQuantity);
            Assert.AreSame(_settings, _handoff.Settings);
            Assert.IsFalse(_handoff.HasResult);
        }

        [Test]
        public void UseSettings_replaces_the_runs_settings()
        {
            var other = new ActiveMiningRunSettings(3, new ActiveMiningTierSettings(), null, 1f, 0.8f, 0.6f, 0.25f, 0.2f);
            _handoff.Begin("earth", "slot_3", _def, fullQuantity: 16, _settings);

            _handoff.UseSettings(other);

            Assert.AreSame(other, _handoff.Settings);
            Assert.AreEqual("slot_3", _handoff.AsteroidSlotId, "The asteroid is untouched");
            Assert.AreEqual(16, _handoff.FullQuantity, "So is what it pays");
        }

        [Test]
        public void SetResult_records_the_outcome_and_the_banked_share()
        {
            _handoff.Begin("earth", "slot_0", _def, 10, _settings);

            _handoff.SetResult(succeeded: true, bankedFraction: 0.925f);

            Assert.IsTrue(_handoff.HasResult);
            Assert.IsTrue(_handoff.Succeeded);
            Assert.AreEqual(0.925f, _handoff.BankedFraction, 0.0001f);
        }

        [Test]
        public void BankedQuantity_is_the_banked_share_of_the_pinned_full_quantity()
        {
            _handoff.Begin("earth", "slot_0", _def, 80, _settings);
            _handoff.SetResult(succeeded: false, bankedFraction: 0.5f);

            Assert.AreEqual(40, _handoff.BankedQuantity);
        }

        [Test]
        public void BankedQuantity_is_zero_before_a_result()
        {
            _handoff.Begin("earth", "slot_0", _def, 80, _settings);

            Assert.AreEqual(0, _handoff.BankedQuantity);
        }

        [Test]
        public void MineralId_prefers_the_mineral_definition_over_the_asteroid_label()
        {
            var mineral = ScriptableObject.CreateInstance<MineralDefinition>();
            SetField(mineral, "_mineralId", "iron");
            SetField(_def, "_mineral", mineral);
            SetField(_def, "_mineralType", "Iron");

            _handoff.Begin("earth", "slot_0", _def, 10, _settings);

            Assert.AreEqual("iron", _handoff.MineralId);
            Object.DestroyImmediate(mineral);
        }

        [Test]
        public void MineralId_falls_back_to_the_asteroid_label_without_a_mineral_definition()
        {
            SetField(_def, "_mineralType", "Iron");

            _handoff.Begin("earth", "slot_0", _def, 10, _settings);

            Assert.AreEqual("Iron", _handoff.MineralId);
        }

        [Test]
        public void Clear_resets_result_and_slot_tracking()
        {
            _handoff.Begin("earth", "slot_0", _def, 10, _settings);
            _handoff.SetResult(succeeded: false, bankedFraction: 0.5f);
            _handoff.DevSettingsForTier = tier => _settings;

            _handoff.Clear();

            Assert.IsNull(_handoff.DevSettingsForTier);
            Assert.IsFalse(_handoff.HasResult);
            Assert.IsNull(_handoff.AsteroidSlotId);
            Assert.AreEqual(0, _handoff.FullQuantity);
            Assert.IsNull(_handoff.Settings);
            Assert.AreEqual(0f, _handoff.BankedFraction);
            Assert.AreEqual(0, _handoff.BankedQuantity);
        }
    }
}
