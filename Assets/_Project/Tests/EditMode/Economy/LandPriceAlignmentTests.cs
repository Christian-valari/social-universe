using System;
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
    // Known Issue #19: PurchaseLand.js and SellLand.js price land server-side from a LAND_PRICES map
    // (and SellLand from RESALE_RATE) instead of trusting the client's price/refund. This reads the
    // JS as text (no Node harness) and checks the maps against EconomyConfig and every
    // PlanetDefinition asset, so the server charges and refunds what the client displays.
    public class LandPriceAlignmentTests
    {
        private const string SoRoot = "Assets/_Project/ScriptableObjects/";

        private static readonly string[] PricedFunctions = { "PurchaseLand", "SellLand" };

        private EconomyConfig _config;
        private List<PlanetDefinition> _planets;

        [SetUp]
        public void SetUp()
        {
            _config = AssetDatabase.LoadAssetAtPath<EconomyConfig>(SoRoot + "EconomyConfig.asset");
            Assert.IsNotNull(_config, "Expected EconomyConfig.asset");

            _planets = AssetDatabase.FindAssets("t:PlanetDefinition", new[] { "Assets" })
                .Select(guid => AssetDatabase.LoadAssetAtPath<PlanetDefinition>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(p => p != null)
                .ToList();
            Assert.IsNotEmpty(_planets, "Expected PlanetDefinition assets");
        }

        private static string ReadServerCode(string function)
        {
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "ServerCode", function + ".js"));
            Assert.IsTrue(File.Exists(path), $"Expected to find {function}.js at {path}");
            return File.ReadAllText(path);
        }

        private static Dictionary<string, int> LandPrices(string function)
        {
            var block = Regex.Match(ReadServerCode(function), @"const\s+LAND_PRICES\s*=\s*\{(?<body>[^}]*)\}");
            Assert.IsTrue(block.Success, $"Could not find LAND_PRICES in {function}.js");

            var prices = new Dictionary<string, int>();
            foreach (Match m in Regex.Matches(block.Groups["body"].Value, @"(?<id>\w+)\s*:\s*(?<price>[0-9]+)\s*,?"))
                prices[m.Groups["id"].Value] = int.Parse(m.Groups["price"].Value);
            return prices;
        }

        // Every land function receives PlanetDefinition.name as "planetId" and lowercases it
        // (the land_registry customId), so that is the map key.
        private static string ServerKey(PlanetDefinition planet) => planet.name.ToLowerInvariant();

        // Same expression as LandPurchaseService / LandSaleService.
        private int ClientPrice(PlanetDefinition planet) =>
            (int)Math.Round(_config.BaseLandPrice * planet.LandPriceMultiplier);

        private int ClientRefund(PlanetDefinition planet) =>
            (int)Math.Round(_config.BaseLandPrice * planet.LandPriceMultiplier * _config.LandResaleRate);

        [Test]
        public void Land_prices_list_every_planet_at_the_client_price([ValueSource(nameof(PricedFunctions))] string function)
        {
            var server = LandPrices(function);

            CollectionAssert.AreEquivalent(_planets.Select(ServerKey), server.Keys,
                $"{function}.js LAND_PRICES must list exactly the PlanetDefinition assets, keyed by asset name lowercased.");
            foreach (var planet in _planets)
                Assert.AreEqual(ClientPrice(planet), server[ServerKey(planet)],
                    $"{function}.js LAND_PRICES.{ServerKey(planet)} must equal round(BaseLandPrice * {planet.name}.LandPriceMultiplier).");
        }

        [Test]
        public void Every_registered_planet_has_a_land_price()
        {
            var registry = AssetDatabase.LoadAssetAtPath<DatabaseRegistry>(SoRoot + "DatabaseRegistry.asset");
            Assert.IsNotNull(registry, "Expected DatabaseRegistry.asset");

            var server = LandPrices("PurchaseLand");
            foreach (var planet in registry.AllPlanets)
                Assert.IsTrue(server.ContainsKey(ServerKey(planet)), $"PurchaseLand.js has no land price for registered planet {planet.name}.");
        }

        [Test]
        public void SellLand_refunds_what_the_client_shows()
        {
            var rate = Regex.Match(ReadServerCode("SellLand"), @"const\s+RESALE_RATE\s*=\s*(?<v>[0-9]+(\.[0-9]+)?)");
            Assert.IsTrue(rate.Success, "Could not find RESALE_RATE in SellLand.js");
            double resaleRate = double.Parse(rate.Groups["v"].Value, CultureInfo.InvariantCulture);
            Assert.AreEqual(_config.LandResaleRate, resaleRate, 1e-6, "SellLand.js RESALE_RATE must match EconomyConfig.LandResaleRate.");

            var server = LandPrices("SellLand");
            foreach (var planet in _planets)
            {
                // SellLand.js: Math.round(LAND_PRICES[key] * RESALE_RATE). JS rounds halves up.
                int serverRefund = (int)Math.Round(server[ServerKey(planet)] * resaleRate, MidpointRounding.AwayFromZero);
                Assert.AreEqual(ClientRefund(planet), serverRefund, $"SellLand refund for {planet.name} differs from LandSaleService's.");
            }
        }
    }
}
