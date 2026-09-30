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

        // ── SpawnForPlanet fixture (respawn timers are persisted in the editor PlayerPrefs) ──

        private const string CurrentPlanetId = "earth";
        private const string OtherPlanetId   = "jupiter";

        private AsteroidDefinition _ironDef;
        private DatabaseRegistry   _registry;
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

            _registry = ScriptableObject.CreateInstance<DatabaseRegistry>();
            SetField(_registry, "_asteroids", new[] { _ironDef });

            _planet = ScriptableObject.CreateInstance<PlanetDefinition>();
            SetField(_planet, "_planetId", CurrentPlanetId);
            SetField(_planet, "_asteroidTypes", new[] { _ironDef });
            SetField(_planet, "_asteroidFieldSize", 3);

            _config = ScriptableObject.CreateInstance<EconomyConfig>();

            _spawnerGo = new GameObject("TestSpawner");
            _spawner   = _spawnerGo.AddComponent<AsteroidSpawner>();
            SetField(_spawner, "_registry", _registry);
            SetField(_spawner, "_config", _config);
        }

        [TearDown]
        public void TearDown()
        {
            PlayerPrefs.DeleteKey(SaveKeys.AsteroidRespawns);
            Object.DestroyImmediate(_spawnerGo);
            Object.DestroyImmediate(_planet);
            Object.DestroyImmediate(_registry);
            Object.DestroyImmediate(_ironDef);
            Object.DestroyImmediate(_config);
        }

        // A future RespawnAtUtc keeps an entry pending for the duration of a test —
        // AsteroidSpawner.Update() (which would eventually respawn it) never runs in an
        // EditMode test since there's no play-mode frame loop.
        private static long FarFutureUnixSeconds() => System.DateTimeOffset.UtcNow.AddHours(4).ToUnixTimeSeconds();

        private List<string> ActiveSlotIds() => _spawner.ActiveAsteroids.Select(a => a.SlotId).ToList();

        // Regression test for a SlotId collision bug: SpawnForPlanet used to assume pending
        // (claimed, awaiting-respawn) asteroids always occupy the lowest contiguous indices
        // [0, pendingCount). In reality a player can claim ANY asteroid of a type. Repro:
        // field has Iron#0, Iron#1, Iron#2; the player claims Iron#2 (not the lowest index),
        // leaving it pending. On the next SpawnForPlanet (e.g. after an app restart), the new
        // spawns must land on Iron#0 and Iron#1 — never re-using Iron#2 (which would collide
        // with the pending entry's eventual respawn) and never skipping Iron#0 (which would
        // silently orphan anything keyed to that slot, e.g. a persisted idle-mining session).
        [Test]
        public void SpawnForPlanet_does_not_collide_with_a_pending_respawn_at_a_non_lowest_index()
        {
            PlayerPrefs.SetString(SaveKeys.AsteroidRespawns, $"{CurrentPlanetId}|Iron|Iron#2|{FarFutureUnixSeconds()}");

            _spawner.SpawnForPlanet(_planet);

            var activeSlotIds = ActiveSlotIds();
            Assert.AreEqual(2, activeSlotIds.Count, "field size 3 minus 1 pending should spawn 2 live asteroids");
            CollectionAssert.DoesNotContain(activeSlotIds, "Iron#2",
                "must not collide with the pending respawn's reserved slot");
            CollectionAssert.Contains(activeSlotIds, "Iron#0",
                "the lower, unclaimed index must not be silently skipped");
            CollectionAssert.Contains(activeSlotIds, "Iron#1");
            Assert.IsTrue(_spawner.NextRespawnUtc.HasValue, "the current planet's pending entry drives the HUD countdown");
        }

        // Respawn timers are per planet: an asteroid claimed on Jupiter must not reduce — or
        // later respawn into — Earth's field, or the server rejects the resulting claims
        // (MINERAL_NOT_ON_PLANET / BUDGET_EXHAUSTED).
        [Test]
        public void SpawnForPlanet_ignores_a_pending_respawn_belonging_to_another_planet()
        {
            PlayerPrefs.SetString(SaveKeys.AsteroidRespawns, $"{OtherPlanetId}|Iron|Iron#2|{FarFutureUnixSeconds()}");

            _spawner.SpawnForPlanet(_planet);

            CollectionAssert.AreEquivalent(new[] { "Iron#0", "Iron#1", "Iron#2" }, ActiveSlotIds(),
                "the current field must be exactly AsteroidFieldSize, unaffected by another planet's timer");
            Assert.IsNull(_spawner.NextRespawnUtc, "another planet's timer must not drive this planet's HUD countdown");
        }

        [Test]
        public void Another_planets_pending_respawn_survives_a_spawn_and_a_save()
        {
            long other = FarFutureUnixSeconds();
            PlayerPrefs.SetString(SaveKeys.AsteroidRespawns, $"{OtherPlanetId}|Iron|Iron#2|{other}");

            _spawner.SpawnForPlanet(_planet);
            _spawner.ScheduleRespawn(_spawner.FindBySlotId("Iron#0"), 4f); // triggers a save

            var saved = PlayerPrefs.GetString(SaveKeys.AsteroidRespawns, "");
            StringAssert.Contains($"{OtherPlanetId}|Iron|Iron#2|{other}", saved,
                "other planets' timers must be persisted untouched");
            StringAssert.Contains($"{CurrentPlanetId}|Iron|Iron#0|", saved,
                "a new claim is stamped with the current planet id");
        }

        [Test]
        public void Legacy_three_part_entry_is_dropped()
        {
            PlayerPrefs.SetString(SaveKeys.AsteroidRespawns, $"Iron|Iron#2|{FarFutureUnixSeconds()}");

            _spawner.SpawnForPlanet(_planet);

            CollectionAssert.AreEquivalent(new[] { "Iron#0", "Iron#1", "Iron#2" }, ActiveSlotIds(),
                "a legacy entry (no planet id) must not reserve a slot");
            Assert.IsNull(_spawner.NextRespawnUtc);

            _spawner.ScheduleRespawn(_spawner.FindBySlotId("Iron#1"), 4f); // triggers a save
            var entries = PlayerPrefs.GetString(SaveKeys.AsteroidRespawns, "").Split(';');
            Assert.AreEqual(1, entries.Length, "the legacy entry must not be re-persisted");
            StringAssert.StartsWith($"{CurrentPlanetId}|Iron|Iron#1|", entries[0]);
        }

        [Test]
        public void Current_planets_expired_entry_is_no_longer_pending()
        {
            long past = System.DateTimeOffset.UtcNow.AddHours(-1).ToUnixTimeSeconds();
            PlayerPrefs.SetString(SaveKeys.AsteroidRespawns, $"{CurrentPlanetId}|Iron|Iron#2|{past}");

            _spawner.SpawnForPlanet(_planet);

            CollectionAssert.AreEquivalent(new[] { "Iron#0", "Iron#1", "Iron#2" }, ActiveSlotIds(),
                "an expired entry is resolved by the normal fill, keeping the field size exact");
            Assert.IsNull(_spawner.NextRespawnUtc);
            Assert.AreEqual("", PlayerPrefs.GetString(SaveKeys.AsteroidRespawns, ""),
                "the resolved entry must be removed from persistence");
        }
    }
}
