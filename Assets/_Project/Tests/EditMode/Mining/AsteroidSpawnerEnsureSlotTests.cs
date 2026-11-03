using System.Reflection;
using NUnit.Framework;
using SocialUniverse.Config;
using SocialUniverse.Core;
using SocialUniverse.Mining;
using UnityEngine;

namespace SocialUniverse.Tests
{
    // A server-tracked idle run names its asteroid by slot id. Slot ids are the same on every device,
    // but respawn timers are per device, so the device signing in may have that slot waiting to
    // respawn: EnsureSlot puts it back (docs/superpowers/plans/2026-10-15-server-tracked-idle-mining.md).
    public class AsteroidSpawnerEnsureSlotTests
    {
        private EconomyConfig      _config;
        private AsteroidDefinition _iron;
        private PlanetDefinition   _planet;
        private AsteroidSpawner    _spawner;

        private static void SetField(object target, string field, object value) =>
            target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);

        [SetUp]
        public void SetUp()
        {
            PlayerPrefs.DeleteKey(SaveKeys.AsteroidRespawns);

            _config = ScriptableObject.CreateInstance<EconomyConfig>();
            SetField(_config, "_asteroidYieldRollMin", 1f);
            SetField(_config, "_asteroidYieldRollMax", 1f);

            _iron = ScriptableObject.CreateInstance<AsteroidDefinition>();
            SetField(_iron, "_mineralType", "Iron");
            SetField(_iron, "_baseYield", 80);

            _planet = ScriptableObject.CreateInstance<PlanetDefinition>();
            SetField(_planet, "_planetId", "test_planet");
            SetField(_planet, "_asteroidTypes", new[] { _iron });
            SetField(_planet, "_asteroidFieldSize", 2);


            _spawner = new GameObject("TestSpawner").AddComponent<AsteroidSpawner>();
            SetField(_spawner, "_config", _config);
            _spawner.SpawnForPlanet(_planet); // Iron#0, Iron#1
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_spawner.gameObject);
            Object.DestroyImmediate(_config);
            Object.DestroyImmediate(_iron);
            Object.DestroyImmediate(_planet);
            PlayerPrefs.DeleteKey(SaveKeys.AsteroidRespawns);
        }

        [Test]
        public void EnsureSlot_returns_the_live_asteroid_in_that_slot()
        {
            var live = _spawner.FindBySlotId("Iron#0");
            Assert.IsNotNull(live);

            Assert.AreSame(live, _spawner.EnsureSlot("Iron#0"));
            Assert.AreEqual(2, _spawner.ActiveAsteroids.Count, "nothing extra is spawned");
        }

        [Test]
        public void EnsureSlot_brings_back_a_slot_this_device_has_waiting_to_respawn()
        {
            _spawner.ScheduleRespawn(_spawner.FindBySlotId("Iron#1"), 4f);
            Assert.IsNull(_spawner.FindBySlotId("Iron#1"));
            Assert.IsTrue(_spawner.NextRespawnUtc.HasValue);

            var asteroid = _spawner.EnsureSlot("Iron#1");

            Assert.IsNotNull(asteroid);
            Assert.AreEqual("Iron#1", asteroid.SlotId);
            Assert.AreSame(_iron, asteroid.Definition);
            Assert.IsFalse(asteroid.IsDepleted);
            Assert.AreSame(asteroid, _spawner.FindBySlotId("Iron#1"));
            Assert.AreEqual(2, _spawner.ActiveAsteroids.Count, "the field size stays exact");
            Assert.IsNull(_spawner.NextRespawnUtc, "this device's stale respawn timer is dropped");
            StringAssert.DoesNotContain("Iron#1", PlayerPrefs.GetString(SaveKeys.AsteroidRespawns, ""),
                "and removed from the saved timers, so a reload doesn't reserve the slot again");
        }

        [TestCase("Gold#0")]
        [TestCase("nonsense")]
        [TestCase("Iron#x")]
        [TestCase("")]
        [TestCase(null)]
        public void EnsureSlot_returns_null_for_a_slot_that_cannot_be_on_this_planet(string slotId)
        {
            Assert.IsNull(_spawner.EnsureSlot(slotId));
            Assert.AreEqual(2, _spawner.ActiveAsteroids.Count);
        }
    }
}
