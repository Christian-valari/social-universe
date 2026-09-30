using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using SocialUniverse.Config;
using UnityEditor;
using UnityEngine;

namespace SocialUniverse.Tests
{
    // ServerCode/ValidateMining.js mirrors planet/asteroid/drone data to validate mining claims
    // server-side (docs/superpowers/specs/2026-09-29-mining-claim-budget-design.md). This reads the
    // JS as text (no Node harness) and checks every mirrored value against the real assets.
    public class MiningCatalogAlignmentTests
    {
        private const string SoRoot = "Assets/_Project/ScriptableObjects/";
        private string _js;
        private DatabaseRegistry _registry;
        private EconomyConfig _config;

        [SetUp]
        public void SetUp()
        {
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "ServerCode", "ValidateMining.js"));
            Assert.IsTrue(File.Exists(path), $"Expected to find ValidateMining.js at {path}");
            _js = File.ReadAllText(path);
            _registry = AssetDatabase.LoadAssetAtPath<DatabaseRegistry>(SoRoot + "DatabaseRegistry.asset");
            _config   = AssetDatabase.LoadAssetAtPath<EconomyConfig>(SoRoot + "EconomyConfig.asset");
            Assert.IsNotNull(_registry);
            Assert.IsNotNull(_config);
        }

        private string Block(string name)
        {
            var m = Regex.Match(_js, @"const\s+" + name + @"\s*=\s*\{(?<body>.*?)\n\};", RegexOptions.Singleline);
            Assert.IsTrue(m.Success, $"Could not find the {name} block in ValidateMining.js");
            return m.Groups["body"].Value;
        }

        private float Number(string name)
        {
            var m = Regex.Match(_js, @"const\s+" + name + @"\s*=\s*(?<v>[0-9]+(\.[0-9]+)?)");
            Assert.IsTrue(m.Success, $"Could not find {name} in ValidateMining.js");
            return float.Parse(m.Groups["v"].Value, CultureInfo.InvariantCulture);
        }

        [Test]
        public void Planet_minerals_match_each_planets_asteroid_types()
        {
            var server = new Dictionary<string, string[]>();
            foreach (Match m in Regex.Matches(Block("PLANET_MINERALS"), @"(?<id>\w+)\s*:\s*\[(?<list>[^\]]*)\]"))
                server[m.Groups["id"].Value] = Regex.Matches(m.Groups["list"].Value, "\"(\\w+)\"")
                    .Cast<Match>().Select(x => x.Groups[1].Value).OrderBy(x => x).ToArray();

            var planets = _registry.AllPlanets.ToList();
            CollectionAssert.AreEquivalent(planets.Select(p => p.PlanetId), server.Keys, "PLANET_MINERALS must list every registered planet");
            foreach (var p in planets)
            {
                var expected = p.AsteroidTypes.Select(a => a.Mineral.MineralId).Distinct().OrderBy(x => x).ToArray();
                CollectionAssert.AreEqual(expected, server[p.PlanetId], $"PLANET_MINERALS.{p.PlanetId}");
            }
        }

        [Test]
        public void Minerals_match_asteroid_base_yield_and_tier()
        {
            var server = Regex.Matches(Block("MINERALS"), @"(?<id>\w+)\s*:\s*\{\s*baseYield:\s*(?<y>\d+),\s*tier:\s*(?<t>\d+)\s*\}")
                .Cast<Match>().ToDictionary(m => m.Groups["id"].Value, m => (int.Parse(m.Groups["y"].Value), int.Parse(m.Groups["t"].Value)));

            var asteroids = _registry.AllPlanets.SelectMany(p => p.AsteroidTypes).Distinct().ToList();
            CollectionAssert.AreEquivalent(asteroids.Select(a => a.Mineral.MineralId), server.Keys);
            foreach (var a in asteroids)
                Assert.AreEqual((a.BaseYield, a.Tier), server[a.Mineral.MineralId], $"MINERALS.{a.Mineral.MineralId}");
        }

        [Test]
        public void Drones_match_tier_and_yield_multiplier()
        {
            var server = Regex.Matches(Block("DRONES"), @"(?<id>\w+)\s*:\s*\{\s*tier:\s*(?<t>\d+),\s*yieldMult:\s*(?<m>[0-9.]+)\s*\}")
                .Cast<Match>().ToDictionary(m => m.Groups["id"].Value,
                    m => (int.Parse(m.Groups["t"].Value), float.Parse(m.Groups["m"].Value, CultureInfo.InvariantCulture)));

            CollectionAssert.AreEquivalent(_registry.AllDrones.Select(d => d.DroneId), server.Keys);
            foreach (var d in _registry.AllDrones)
            {
                Assert.AreEqual(d.Tier, server[d.DroneId].Item1, $"DRONES.{d.DroneId}.tier");
                Assert.AreEqual(d.YieldMultiplier, server[d.DroneId].Item2, 1e-6f, $"DRONES.{d.DroneId}.yieldMult");
            }
        }

        [Test]
        public void Scalar_constants_match_config_and_upgrades()
        {
            var yieldUpgrade = _registry.GetUpgrade(DroneStat.Yield);
            Assert.IsNotNull(yieldUpgrade, "DatabaseRegistry has no Yield upgrade");
            Assert.AreEqual(yieldUpgrade.DeltaPerLevel, Number("YIELD_DELTA_PER_LEVEL"), 1e-6f);
            Assert.AreEqual(_config.AsteroidRespawnHours, Number("RESPAWN_HOURS"), 1e-6f);
            Assert.AreEqual(_config.AsteroidYieldRollMax, Number("YIELD_ROLL_MAX"), 1e-6f);

            float fieldSize = Number("FIELD_SIZE");
            foreach (var p in _registry.AllPlanets)
                Assert.LessOrEqual(p.AsteroidFieldSize, fieldSize, $"{p.PlanetId} field is larger than FIELD_SIZE");
        }
    }
}
