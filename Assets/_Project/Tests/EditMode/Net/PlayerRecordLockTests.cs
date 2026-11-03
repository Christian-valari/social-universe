using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace SocialUniverse.Tests
{
    // Cross-device consistency (docs/superpowers/plans/2026-10-15-cross-device-data-consistency.md):
    // a read-modify-write without the record's writeLock lets an overlapping request — another
    // device, or a second call from this one — erase the change. drone_fleet and owned_tiles_<planet>
    // are each rewritten by several scripts, so each is read and written only through a shared
    // helper block, copied verbatim, whose write carries the writeLock and retries on a 409. So is
    // idle_mining_session (docs/superpowers/plans/2026-10-15-server-tracked-idle-mining.md). Reads
    // the .js as text, like LandRegistryLockTests.
    public class PlayerRecordLockTests
    {
        private static readonly string[] FleetWriters   = { "AcquireDrone", "UpgradeDrone", "UnlockDroneSlot", "SetActiveDrone" };
        private static readonly string[] TilesWriters   = { "PurchaseLand", "SellLand" };
        private static readonly string[] IdleRunWriters = { "StartIdleMining", "ValidateMining" };

        private static string Read(string function) =>
            File.ReadAllText(Path.GetFullPath(Path.Combine(Application.dataPath, "..", "ServerCode", function + ".js"))).Replace("\r\n", "\n");

        private static void AssertSharedLockedBlock(string[] writers, string record, string writeCall, string keyPattern)
        {
            var block = new Regex($@"// ---- {record} access.*?// ---- end {record} access", RegexOptions.Singleline);
            var blocks = new Dictionary<string, string>();
            foreach (var fn in writers)
            {
                string code  = Read(fn);
                var    match = block.Match(code);
                Assert.IsTrue(match.Success, $"{fn}.js: no `{record} access` helper block");
                blocks[fn] = match.Value;

                string outside = code.Remove(match.Index, match.Length);
                Assert.IsFalse(Regex.IsMatch(outside, writeCall + @"\([^)]*(" + keyPattern + ")"),
                    $"{fn}.js writes {record} outside its helper block");
            }

            string canonical = blocks[writers[0]];
            foreach (var kv in blocks)
                Assert.AreEqual(canonical, kv.Value, $"{kv.Key}.js: `{record} access` block differs from {writers[0]}.js — keep the copies identical.");

            StringAssert.Contains("writeLock", canonical, $"The {record} write must carry the writeLock it read");
            StringAssert.Contains("409", canonical, $"The {record} helper must retry on a write-lock conflict (409)");
            Assert.AreEqual(1, Regex.Matches(canonical, writeCall + @"\(").Count, $"The {record} helper should have exactly one write");
        }

        [Test]
        public void Drone_fleet_is_written_only_through_the_shared_locked_helper() =>
            AssertSharedLockedBlock(FleetWriters, "drone_fleet", @"\.setProtectedItem", @"""drone_fleet""");

        [Test]
        public void Owned_tiles_are_written_only_through_the_shared_locked_helper() =>
            AssertSharedLockedBlock(TilesWriters, "owned_tiles", @"\.setProtectedItem", @"ownedKey|owned_tiles");

        [Test]
        public void Idle_mining_session_is_written_only_through_the_shared_locked_helper() =>
            AssertSharedLockedBlock(IdleRunWriters, "idle_mining_session", @"\.setProtectedItem", @"IDLE_SESSION_KEY|idle_mining_session");
    }
}
