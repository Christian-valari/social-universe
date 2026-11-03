using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace SocialUniverse.Tests
{
    // Known Issue #19: a planet's land_registry is one Cloud Save Custom Data item holding every
    // tile, so each writer reads the whole map and writes it back. A write without the writeLock it
    // read silently undoes a concurrent write — e.g. ClaimYield putting back a tile SellLand just
    // sold, which the owner can then sell again. Every script that writes the registry must do all
    // of its registry reads and writes through the shared helper block (loadRegistry /
    // updateRegistry), copied verbatim, whose write carries the writeLock. Reads the .js as text
    // (no Node harness), like LandPriceAlignmentTests.
    public class LandRegistryLockTests
    {
        private static readonly Regex HelperBlock = new(
            @"// ---- land_registry access.*?// ---- end land_registry access", RegexOptions.Singleline);

        private static Dictionary<string, string> RegistryWriters()
        {
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "ServerCode"));
            Assert.IsTrue(Directory.Exists(dir), $"Expected ServerCode/ at {dir}");
            return Directory.GetFiles(dir, "*.js")
                .Select(p => (name: Path.GetFileName(p), code: File.ReadAllText(p)))
                .Where(f => f.code.Contains("\"land_registry\"") && f.code.Contains(".setCustomItem("))
                .ToDictionary(f => f.name, f => f.code);
        }

        [Test]
        public void Every_registry_writer_goes_through_the_shared_locked_helper()
        {
            var writers = RegistryWriters();
            Assert.GreaterOrEqual(writers.Count, 9, "Expected to find the land_registry writers (PurchaseLand, SellLand, ClaimYield, …)");

            var offenders = new List<string>();
            foreach (var kv in writers)
            {
                string name = kv.Key, code = kv.Value;
                var block = HelperBlock.Match(code);
                if (!block.Success) { offenders.Add($"{name}: no land_registry helper block"); continue; }

                string outside = code.Remove(block.Index, block.Length);
                if (outside.Contains(".setCustomItem(")) offenders.Add($"{name}: setCustomItem outside updateRegistry");
                if (outside.Contains(".getCustomItems(")) offenders.Add($"{name}: getCustomItems outside loadRegistry");
            }

            CollectionAssert.IsEmpty(offenders,
                "Read and write land_registry only through loadRegistry/updateRegistry (Known Issue #19).");
        }

        [Test]
        public void The_helper_block_is_identical_everywhere_and_writes_with_the_lock()
        {
            var blocks = RegistryWriters()
                .Select(kv => (name: kv.Key, block: HelperBlock.Match(kv.Value)))
                .Where(b => b.block.Success)
                .ToDictionary(b => b.name, b => b.block.Value.Replace("\r\n", "\n"));
            Assert.IsTrue(blocks.ContainsKey("PurchaseLand.js"), "PurchaseLand.js must contain the land_registry helper block");

            string canonical = blocks["PurchaseLand.js"];
            foreach (var kv in blocks)
                Assert.AreEqual(canonical, kv.Value, $"{kv.Key}: land_registry helper block differs from PurchaseLand.js — keep the copies identical.");

            Assert.AreEqual(1, Regex.Matches(canonical, @"\.setCustomItem\(").Count, "The helper should have exactly one registry write.");
            StringAssert.Contains("writeLock ? { key: REGISTRY_KEY, value: registry, writeLock }", canonical,
                "The helper's write must pass the writeLock it read.");
            StringAssert.Contains("writeLock: item.writeLock", canonical, "loadRegistry must return the record's writeLock.");
            StringAssert.Contains("409", canonical, "updateRegistry must retry on a write-lock conflict (409).");
        }
    }
}
