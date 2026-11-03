using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace SocialUniverse.Tests
{
    // One active device per account (docs/superpowers/plans/2026-10-15-single-active-session.md).
    // Cloud Code has no middleware, so every deployed script checks the caller's session itself
    // through the requireActiveSession block, copied verbatim, before any other I/O — a script
    // without it lets a replaced device keep playing. ClaimSession starts sessions and
    // ReleaseSession ends them, so neither requires one up front; ReleaseSession reads the record
    // itself because its write needs the writeLock. Reads the .js as text, like LandRegistryLockTests.
    public class ActiveSessionGuardTests
    {
        private static readonly Regex GuardBlock = new(
            @"// ---- active session check.*?// ---- end active session check", RegexOptions.Singleline);
        private const string GuardCall = "await requireActiveSession(context, params.sessionId);";
        private static readonly string[] SessionScripts = { "ClaimSession", "ReleaseSession" };

        private static Dictionary<string, string> Deployed()
        {
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "ServerCode"));
            Assert.IsTrue(Directory.Exists(dir), $"Expected ServerCode/ at {dir}");
            return File.ReadAllLines(Path.Combine(dir, "deploy-list.txt"))
                .Select(l => l.Trim())
                .Where(l => l.Length > 0 && !l.StartsWith("#"))
                .ToDictionary(Path.GetFileNameWithoutExtension, l => File.ReadAllText(Path.Combine(dir, l)).Replace("\r\n", "\n"));
        }

        [Test]
        public void Every_deployed_script_carries_the_identical_guard_block()
        {
            var scripts = Deployed();
            foreach (var name in SessionScripts)
                Assert.IsTrue(scripts.ContainsKey(name), $"{name}.js must be on deploy-list.txt");

            string canonical = GuardBlock.Match(scripts["ClaimSession"]).Value;
            StringAssert.Contains("throw Error(\"SESSION_REPLACED\")", canonical, "The guard must reject with SESSION_REPLACED");
            StringAssert.Contains("getProtectedItems", canonical, "The session record is Protected: only Cloud Code may write it");

            foreach (var kv in scripts.Where(kv => kv.Key != "ReleaseSession"))
            {
                var block = GuardBlock.Match(kv.Value);
                Assert.IsTrue(block.Success, $"{kv.Key}.js: no active session check block");
                Assert.AreEqual(canonical, block.Value, $"{kv.Key}.js: active session check differs from ClaimSession.js — keep the copies identical.");
            }
        }

        [Test]
        public void Every_deployed_script_checks_the_session_before_any_other_await()
        {
            var offenders = new List<string>();
            foreach (var kv in Deployed().Where(kv => !SessionScripts.Contains(kv.Key)))
            {
                int handler = kv.Value.IndexOf("module.exports = async");
                int firstAwait = handler < 0 ? -1 : kv.Value.IndexOf("await ", handler);
                if (firstAwait < 0 || string.CompareOrdinal(kv.Value, firstAwait, GuardCall, 0, GuardCall.Length) != 0)
                    offenders.Add(kv.Key);
            }

            CollectionAssert.IsEmpty(offenders, $"The handler's first await must be `{GuardCall}`");
        }
    }
}
