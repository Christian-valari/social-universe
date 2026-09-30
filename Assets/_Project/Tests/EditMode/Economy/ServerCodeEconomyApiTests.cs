using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace SocialUniverse.Tests
{
    // Known Issue #12: CurrenciesApi in @unity-services/economy-2.5 has no getPlayerCurrencyBalance
    // (only getPlayerCurrencies + increment/decrement/setPlayerCurrencyBalance), so every call
    // threw at runtime. Comments may still mention the name; only an actual call fails this test.
    public class ServerCodeEconomyApiTests
    {
        [Test]
        public void No_cloud_code_function_calls_the_nonexistent_getPlayerCurrencyBalance()
        {
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "ServerCode"));
            Assert.IsTrue(Directory.Exists(dir), $"Expected to find ServerCode/ at {dir}");

            var offenders = Directory.GetFiles(dir, "*.js")
                .Where(f => Regex.IsMatch(File.ReadAllText(f), @"\.getPlayerCurrencyBalance\s*\("))
                .Select(Path.GetFileName)
                .ToArray();

            CollectionAssert.IsEmpty(offenders,
                "Read balances via getPlayerCurrencies and find the currency (see PurchaseLand.js).");
        }
    }
}
