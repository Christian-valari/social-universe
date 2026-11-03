using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using SocialUniverse.Config;
using SocialUniverse.Core;
using SocialUniverse.Mining;
using UnityEngine;

namespace SocialUniverse.Tests
{
    public class AsteroidSpawnerDistributionTests
    {
        private static AsteroidDefinition MakeDef(float rarity)
        {
            var def = ScriptableObject.CreateInstance<AsteroidDefinition>();
            typeof(AsteroidDefinition).GetField("_rarity", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(def, rarity);
            return def;
        }

        private static void SetField(object target, string field, object value) =>
            target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);

        [Test]
        public void Counts_always_sum_to_field_size()
        {
            var types = new[] { MakeDef(0.7f), MakeDef(0.5f), MakeDef(0.1f) };

            foreach (int fieldSize in new[] { 1, 3, 6, 10, 25 })
            {
                var counts = AsteroidSpawner.DistributeFieldSize(types, fieldSize);
                Assert.AreEqual(fieldSize, counts.Sum(), $"fieldSize={fieldSize}");
            }
        }

        [Test]
        public void Rarer_types_get_fewer_slots_than_common_types()
        {
            var types  = new[] { MakeDef(0.8f), MakeDef(0.1f) }; // [0]=rare, [1]=common
            var counts = AsteroidSpawner.DistributeFieldSize(types, 20);

            Assert.Less(counts[0], counts[1]);
        }

        [Test]
        public void Zero_field_size_yields_all_zero_counts()
        {
            var types  = new[] { MakeDef(0.5f), MakeDef(0.5f) };
            var counts = AsteroidSpawner.DistributeFieldSize(types, 0);

            Assert.AreEqual(new[] { 0, 0 }, counts);
        }

        [Test]
        public void Single_type_gets_the_full_field_size()
        {
            var types  = new[] { MakeDef(0.9f) };
            var counts = AsteroidSpawner.DistributeFieldSize(types, 7);

            Assert.AreEqual(new[] { 7 }, counts);
        }

        // ── SpawnForPlanet fixture (respawn timers come from the server: GetAsteroidRespawns) ──

        private AsteroidDefinition _ironDef;
        private PlanetDefinition   _planet;
        private EconomyConfig      _config;
        private GameObject         _spawnerGo;
        private AsteroidSpawner    _spawner;

        [SetUp]
        public void SetUp()
        {
            PlayerPrefs.DeleteKey(SaveKeys.AsteroidRespawns);

            _ironDef = MakeDef(0f);
            SetField(_ironDef, "_mineralType", "Iron");
            SetField(_ironDef, "_baseYield", 10);


            _planet = ScriptableObject.CreateInstance<PlanetDefinition>();
            SetField(_planet, "_planetId", "earth");
            SetField(_planet, "_asteroidTypes", new[] { _ironDef });
            SetField(_planet, "_asteroidFieldSize", 3);

            _config = ScriptableObject.CreateInstance<EconomyConfig>();

            _spawnerGo = new GameObject("TestSpawner");
            _spawner   = _spawnerGo.AddComponent<AsteroidSpawner>();
            SetField(_spawner, "_config", _config);
        }

        [TearDown]
        public void TearDown()
        {
            PlayerPrefs.DeleteKey(SaveKeys.AsteroidRespawns);
            Object.DestroyImmediate(_spawnerGo);
            Object.DestroyImmediate(_planet);
            Object.DestroyImmediate(_ironDef);
            Object.DestroyImmediate(_config);
        }

        // What GetAsteroidRespawns reports for a slot claimed in the last few hours. A positive
        // SecondsLeft keeps the entry pending for the whole test — AsteroidSpawner.Update() (which
        // would eventually respawn it) never runs in an EditMode test.
        private static AsteroidRespawn Respawn(string slotId, float secondsLeft = 3600f) =>
            new AsteroidRespawn { SlotId = slotId, MineralId = "iron", SecondsLeft = secondsLeft };

        private List<string> ActiveSlotIds() => _spawner.ActiveAsteroids.Select(a => a.SlotId).ToList();

        // A slot the server says was claimed (on this device or any other) stays empty until it
        // respawns, so every device shows the same field.
        [Test]
        public void SpawnForPlanet_leaves_the_slots_the_server_reports_as_respawning_empty()
        {
            _spawner.SpawnForPlanet(_planet, new[] { Respawn("Iron#1", secondsLeft: 600f) });

            CollectionAssert.AreEquivalent(new[] { "Iron#0", "Iron#2" }, ActiveSlotIds());
            Assert.IsTrue(_spawner.NextRespawnUtc.HasValue, "the server's timer drives the HUD countdown");
            Assert.AreEqual(600, (_spawner.NextRespawnUtc.Value - System.DateTime.UtcNow).TotalSeconds, 5,
                "the countdown is the server's time left, not a device-side estimate");
        }

        // Regression test for a SlotId collision bug: pending (claimed, awaiting-respawn) asteroids
        // can occupy ANY index — a player can claim any asteroid of a type, not only the lowest one.
        // New spawns must land on the free indices, never on the pending slot (which would collide
        // with its eventual respawn) and never skipping a lower free index.
        [Test]
        public void SpawnForPlanet_does_not_collide_with_a_pending_respawn_at_a_non_lowest_index()
        {
            _spawner.SpawnForPlanet(_planet, new[] { Respawn("Iron#2") });

            var activeSlotIds = ActiveSlotIds();
            Assert.AreEqual(2, activeSlotIds.Count, "field size 3 minus 1 pending should spawn 2 live asteroids");
            CollectionAssert.DoesNotContain(activeSlotIds, "Iron#2",
                "must not collide with the pending respawn's reserved slot");
            CollectionAssert.Contains(activeSlotIds, "Iron#0",
                "the lower, unclaimed index must not be silently skipped");
            CollectionAssert.Contains(activeSlotIds, "Iron#1");
        }

        [Test]
        public void SpawnForPlanet_fills_a_slot_whose_respawn_is_already_due()
        {
            _spawner.SpawnForPlanet(_planet, new[] { Respawn("Iron#2", secondsLeft: 0f) });

            CollectionAssert.AreEquivalent(new[] { "Iron#0", "Iron#1", "Iron#2" }, ActiveSlotIds());
            Assert.IsNull(_spawner.NextRespawnUtc);
        }

        // A slot whose asteroid type isn't on this planet (or a malformed id) can't be placed, so it
        // must not shrink the field.
        [TestCase("Gold#0")]
        [TestCase("Iron")]
        [TestCase(null)]
        public void SpawnForPlanet_ignores_a_respawn_it_cannot_place_on_this_planet(string slotId)
        {
            _spawner.SpawnForPlanet(_planet, new[] { Respawn(slotId) });

            CollectionAssert.AreEquivalent(new[] { "Iron#0", "Iron#1", "Iron#2" }, ActiveSlotIds());
            Assert.IsNull(_spawner.NextRespawnUtc);
        }

        // A slot can appear twice in the server's log (claimed, brought back for an idle run, claimed
        // again): it is one empty slot, ready when the later claim's timer ends.
        [Test]
        public void SpawnForPlanet_counts_a_slot_reported_twice_once_using_the_later_respawn()
        {
            _spawner.SpawnForPlanet(_planet, new[] { Respawn("Iron#1", 300f), Respawn("Iron#1", 900f) });

            CollectionAssert.AreEquivalent(new[] { "Iron#0", "Iron#2" }, ActiveSlotIds());
            Assert.AreEqual(900, (_spawner.NextRespawnUtc.Value - System.DateTime.UtcNow).TotalSeconds, 5);
        }

        // Respawn timers used to live in this device's PlayerPrefs (shared by every account on it).
        // They are neither read nor kept: the server's claim log is the only source.
        [Test]
        public void SpawnForPlanet_ignores_and_deletes_the_old_device_respawn_timers()
        {
            long future = System.DateTimeOffset.UtcNow.AddHours(4).ToUnixTimeSeconds();
            PlayerPrefs.SetString(SaveKeys.AsteroidRespawns, $"earth|Iron|Iron#2|{future}");

            _spawner.SpawnForPlanet(_planet);

            CollectionAssert.AreEquivalent(new[] { "Iron#0", "Iron#1", "Iron#2" }, ActiveSlotIds());
            Assert.IsNull(_spawner.NextRespawnUtc);
            Assert.IsFalse(PlayerPrefs.HasKey(SaveKeys.AsteroidRespawns));
        }

        [Test]
        public void ScheduleRespawn_keeps_the_slot_empty_without_saving_anything_on_the_device()
        {
            _spawner.SpawnForPlanet(_planet);

            _spawner.ScheduleRespawn(_spawner.FindBySlotId("Iron#0"), 4f);

            CollectionAssert.AreEquivalent(new[] { "Iron#1", "Iron#2" }, ActiveSlotIds());
            Assert.IsTrue(_spawner.NextRespawnUtc.HasValue);
            Assert.IsFalse(PlayerPrefs.HasKey(SaveKeys.AsteroidRespawns), "the server's claim log records the claim");
        }
    }
}
