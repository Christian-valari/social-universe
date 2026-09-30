using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using SocialUniverse.Config;
using UnityEditor;
using UnityEngine;

namespace SocialUniverse.Tests
{
    // Guards the drone constants duplicated in ServerCode/*.js against the shipped SO assets.
    // Known Issue #10 shipped because AcquireDrone.js still listed scout/hauler/prospector after
    // the DroneDefinition assets were renamed, and every other M6 test builds drones in memory.
    // Like MiningCatalogAlignmentTests, this reads the .js source as text (no Node harness).
    public class DroneCatalogAlignmentTests
    {
        private const string RegistryPath = "Assets/_Project/ScriptableObjects/DatabaseRegistry.asset";

        private static readonly string[] FleetFunctions =
            { "AcquireDrone", "UpgradeDrone", "SetActiveDrone", "UnlockDroneSlot", "GetBootstrapState" };

        private static string ReadServerCode(string function)
        {
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "ServerCode", function + ".js"));
            Assert.IsTrue(File.Exists(path), $"Expected to find {function}.js at {path}");
            return File.ReadAllText(path);
        }

        private static DatabaseRegistry LoadRegistry()
        {
            var registry = AssetDatabase.LoadAssetAtPath<DatabaseRegistry>(RegistryPath);
            Assert.IsNotNull(registry, $"Expected a DatabaseRegistry at {RegistryPath}");
            return registry;
        }

        [Test]
        public void AcquireDrone_unlock_costs_match_the_registered_DroneDefinitions()
        {
            string source = ReadServerCode("AcquireDrone");
            var block = Regex.Match(source, @"UNLOCK_COSTS\s*=\s*\{(?<body>[^}]*)\}");
            Assert.IsTrue(block.Success, "Could not find UNLOCK_COSTS in AcquireDrone.js");

            var serverCosts = new Dictionary<string, int>();
            foreach (Match m in Regex.Matches(block.Groups["body"].Value, @"(?<id>[A-Za-z0-9_]+)\s*:\s*(?<cost>[0-9]+)"))
                serverCosts[m.Groups["id"].Value] = int.Parse(m.Groups["cost"].Value);

            var clientCosts = LoadRegistry().AllDrones.ToDictionary(d => d.DroneId, d => d.UnlockCost);

            CollectionAssert.AreEquivalent(clientCosts.Keys, serverCosts.Keys,
                "AcquireDrone.js UNLOCK_COSTS must list exactly the drone ids in DatabaseRegistry.");
            foreach (var kv in clientCosts)
                Assert.AreEqual(kv.Value, serverCosts[kv.Key],
                    $"AcquireDrone.js unlock cost for '{kv.Key}' must match DroneDefinition._unlockCost.");
        }

        [Test]
        public void Every_fleet_function_seeds_the_client_starter_drone()
        {
            // Must pick the same drone as PlanetSceneScope.FirstStarterDrone: Tier 1, zero cost.
            var starter = LoadRegistry().AllDrones.FirstOrDefault(d => d.Tier == 1 && d.UnlockCost == 0);
            Assert.IsNotNull(starter, "DatabaseRegistry has no Tier-1, zero-cost starter drone.");

            foreach (string function in FleetFunctions)
            {
                var match = Regex.Match(ReadServerCode(function), @"STARTER_DRONE_ID\s*=\s*""(?<id>[^""]+)""");
                Assert.IsTrue(match.Success, $"{function}.js must declare STARTER_DRONE_ID and seed it into an empty fleet.");
                Assert.AreEqual(starter.DroneId, match.Groups["id"].Value,
                    $"{function}.js STARTER_DRONE_ID must match the client's starter drone.");
            }
        }

        [Test]
        public void Every_fleet_function_uses_the_configured_starting_slot_count()
        {
            var config = AssetDatabase.LoadAssetAtPath<EconomyConfig>("Assets/_Project/ScriptableObjects/EconomyConfig.asset");
            Assert.IsNotNull(config, "Expected EconomyConfig.asset");

            foreach (string function in FleetFunctions)
            {
                var match = Regex.Match(ReadServerCode(function), @"START_SLOTS\s*=\s*(?<n>[0-9]+)");
                Assert.IsTrue(match.Success, $"{function}.js must declare START_SLOTS.");
                Assert.AreEqual(config.StartingFleetSlots, int.Parse(match.Groups["n"].Value),
                    $"{function}.js START_SLOTS must match EconomyConfig.StartingFleetSlots.");
            }
        }
    }
}
