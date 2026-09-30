using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using SocialUniverse.Config;
using SocialUniverse.Mining;
using UnityEditor;
using UnityEngine;

namespace SocialUniverse.Tests
{
    // Guards the mineral content itself: SellMinerals.js prices every mineral the game ships, the
    // mineral/asteroid tiers agree, and every planet's field actually spawns each of its asteroid
    // types (a rare type rounding to zero slots would make that mineral unobtainable there).
    public class MineralContentAlignmentTests
    {
        private DatabaseRegistry _registry;

        [SetUp]
        public void SetUp()
        {
            _registry = AssetDatabase.LoadAssetAtPath<DatabaseRegistry>("Assets/_Project/ScriptableObjects/DatabaseRegistry.asset");
            Assert.IsNotNull(_registry);
        }

        [Test]
        public void SellMinerals_sell_values_match_every_registered_mineral()
        {
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "ServerCode", "SellMinerals.js"));
            Assert.IsTrue(File.Exists(path), $"Expected to find SellMinerals.js at {path}");

            var block = Regex.Match(File.ReadAllText(path), @"SELL_VALUES\s*=\s*\{(?<body>[^}]*)\}");
            Assert.IsTrue(block.Success, "Could not find SELL_VALUES in SellMinerals.js");
            var server = Regex.Matches(block.Groups["body"].Value, @"(?<id>\w+)\s*:\s*(?<v>\d+)")
                .Cast<Match>().ToDictionary(m => m.Groups["id"].Value, m => int.Parse(m.Groups["v"].Value));

            var minerals = _registry.AllMinerals.ToList();
            CollectionAssert.AreEquivalent(minerals.Select(m => m.MineralId), server.Keys);
            foreach (var m in minerals)
                Assert.AreEqual(m.SellValue, server[m.MineralId], $"SELL_VALUES.{m.MineralId}");
        }

        [Test]
        public void Every_asteroid_is_registered_with_a_registered_mineral_of_the_same_tier()
        {
            var minerals = _registry.AllMinerals.ToList();
            foreach (var a in _registry.AllAsteroids)
            {
                Assert.IsNotNull(a.Mineral, $"{a.name} has no mineral");
                CollectionAssert.Contains(minerals, a.Mineral, $"{a.name}'s mineral is not in DatabaseRegistry._minerals");
                Assert.AreEqual(a.Tier, a.Mineral.Tier, $"{a.name} tier differs from its mineral's tier");
            }
        }

        [Test]
        public void Every_planet_field_spawns_each_of_its_asteroid_types()
        {
            var registered = _registry.AllAsteroids.ToList();
            foreach (var planet in _registry.AllPlanets)
            {
                var counts = AsteroidSpawner.DistributeFieldSize(planet.AsteroidTypes, planet.AsteroidFieldSize);
                Assert.AreEqual(planet.AsteroidFieldSize, counts.Sum(), $"{planet.PlanetId} field size");
                for (int i = 0; i < counts.Length; i++)
                {
                    Assert.GreaterOrEqual(counts[i], 1, $"{planet.PlanetId}: {planet.AsteroidTypes[i].name} gets no slot");
                    CollectionAssert.Contains(registered, planet.AsteroidTypes[i], $"{planet.PlanetId}: {planet.AsteroidTypes[i].name} not registered");
                }
            }
        }

        [Test]
        public void Tier_four_and_five_drones_have_something_to_mine()
        {
            int maxAsteroidTier = _registry.AllAsteroids.Max(a => a.Tier);
            Assert.AreEqual(_registry.AllDrones.Max(d => d.Tier), maxAsteroidTier,
                "The highest drone tier should have an asteroid tier to match");
            Assert.IsTrue(_registry.AllAsteroids.Any(a => a.Tier == 4), "No tier-4 asteroid");
        }
    }
}
