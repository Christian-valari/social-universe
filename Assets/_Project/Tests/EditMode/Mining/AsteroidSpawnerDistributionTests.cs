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
            var ironDef = MakeDef(0f);
            SetField(ironDef, "_mineralType", "Iron");
            SetField(ironDef, "_baseYield", 10);

            var registry = ScriptableObject.CreateInstance<DatabaseRegistry>();
            SetField(registry, "_asteroids", new[] { ironDef });

            var planet = ScriptableObject.CreateInstance<PlanetDefinition>();
            SetField(planet, "_asteroidTypes", new[] { ironDef });
            SetField(planet, "_asteroidFieldSize", 3);

            var spawnerGo = new GameObject("TestSpawner_SlotCollision");
            var spawner   = spawnerGo.AddComponent<AsteroidSpawner>();
            SetField(spawner, "_registry", registry);

            // A future RespawnAtUtc keeps the entry pending for the duration of this test —
            // AsteroidSpawner.Update() (which would eventually respawn it) never runs in an
            // EditMode test since there's no play-mode frame loop.
            long farFutureUnixSeconds = System.DateTimeOffset.UtcNow.AddHours(4).ToUnixTimeSeconds();
            PlayerPrefs.SetString(SaveKeys.AsteroidRespawns, $"Iron|Iron#2|{farFutureUnixSeconds}");

            try
            {
                spawner.SpawnForPlanet(planet);

                var activeSlotIds = spawner.ActiveAsteroids.Select(a => a.SlotId).ToList();

                Assert.AreEqual(2, activeSlotIds.Count, "field size 3 minus 1 pending should spawn 2 live asteroids");
                CollectionAssert.DoesNotContain(activeSlotIds, "Iron#2",
                    "must not collide with the pending respawn's reserved slot");
                CollectionAssert.Contains(activeSlotIds, "Iron#0",
                    "the lower, unclaimed index must not be silently skipped");
                CollectionAssert.Contains(activeSlotIds, "Iron#1");
            }
            finally
            {
                PlayerPrefs.DeleteKey(SaveKeys.AsteroidRespawns);
                Object.DestroyImmediate(spawnerGo);
                Object.DestroyImmediate(planet);
                Object.DestroyImmediate(registry);
                Object.DestroyImmediate(ironDef);
            }
        }
    }
}
