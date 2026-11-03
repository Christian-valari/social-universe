using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace SocialUniverse.Tests
{
    // Known Issue #16: records the server trusts must live in Cloud Save's Protected access class
    // (player can read, only Cloud Code can write). The Default class (getItems/setItem) is
    // player-writable, so a client could reset its claim budget, null its current planet or raise
    // its drone tier by writing its own Cloud Save.
    public class ServerCodeAccessClassTests
    {
        // Keys (or key prefixes) the server writes and trusts. MUST MATCH ServerCode/*.js.
        private static readonly string[] ProtectedKeys =
        {
            "drone_fleet", "mineral_inventory", "current_planet", "mining_claim_log",
            "fuel_state", "travel_state", "owned_tiles_", "idle_mining_session",
        };

        // Scripts that read or write both classes. Their default-class calls must name a
        // non-protected key directly (no protected literal, no variable bound to one).
        private static readonly string[] MixedScripts = { "GetBootstrapState.js", "GetPlayerProfile.js", "PurchaseLand.js" };

        private static readonly Regex DefaultClassCall = new(@"\.(getItems|setItem|deleteItem)\s*\(");

        [Test]
        public void Protected_keys_are_never_read_or_written_through_the_default_access_class()
        {
            var offenders = new List<string>();
            foreach (var path in ScriptPaths())
            {
                string name = Path.GetFileName(path);
                string code = StripComments(File.ReadAllText(path));
                if (!ProtectedKeys.Any(code.Contains)) continue;

                var calls = DefaultClassCall.Matches(code).Cast<Match>().ToList();
                if (calls.Count == 0) continue;

                if (!MixedScripts.Contains(name))
                {
                    offenders.Add($"{name}: uses {calls[0].Groups[1].Value} in a script that touches protected keys");
                    continue;
                }

                var bound = ProtectedBindings(code);
                foreach (var call in calls)
                {
                    string args = CallArguments(code, call.Index + call.Length);
                    bool namesProtected = ProtectedKeys.Any(args.Contains)
                        || bound.Any(id => Regex.IsMatch(args, $@"\b{id}\b"))
                        || Regex.IsMatch(args, @"\b(body|withLock)\b");
                    if (namesProtected) offenders.Add($"{name}: {call.Groups[1].Value}({args.Trim()})");
                }
            }

            CollectionAssert.IsEmpty(offenders,
                "Use getProtectedItems/setProtectedItem for server-trusted keys (Known Issue #16).");
        }

        [Test]
        public void Every_protected_key_is_written_through_setProtectedItem_somewhere()
        {
            string all = string.Join("\n", ScriptPaths().Select(p => StripComments(File.ReadAllText(p))));
            Assert.IsTrue(all.Contains(".setProtectedItem("), "No script calls setProtectedItem.");

            var unreferenced = ProtectedKeys.Where(k => !all.Contains(k)).ToArray();
            CollectionAssert.IsEmpty(unreferenced, "ProtectedKeys lists a key no script uses — typo or stale entry?");
        }

        // The client must read these with LoadProtectedAsync: a default-class load never sees
        // protected records, so it would silently return the default (an empty fleet, no minerals).
        [Test]
        public void Client_reads_protected_keys_through_LoadProtectedAsync()
        {
            string scripts = Path.Combine(Application.dataPath, "_Project", "Scripts");
            var defaultLoad = new Regex(
                @"\.LoadAsync\s*<[^;]*?>\s*\(\s*SaveKeys\.(MineralInventory|DroneFleet|FuelState|TravelState|OwnedTilesKey)\b");

            var offenders = Directory.GetFiles(scripts, "*.cs", SearchOption.AllDirectories)
                .Where(f => defaultLoad.IsMatch(File.ReadAllText(f)))
                .Select(Path.GetFileName)
                .ToArray();

            CollectionAssert.IsEmpty(offenders, "Load server-written keys with ICloudSave.LoadProtectedAsync.");
        }

        private static IEnumerable<string> ScriptPaths()
        {
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "ServerCode"));
            Assert.IsTrue(Directory.Exists(dir), $"Expected to find ServerCode/ at {dir}");
            return Directory.GetFiles(dir, "*.js");
        }

        private static string StripComments(string js) =>
            Regex.Replace(Regex.Replace(js, @"/\*.*?\*/", "", RegexOptions.Singleline), @"//[^\n]*", "");

        // Identifiers assigned a protected key, e.g. `const FLEET_KEY = "drone_fleet"` or
        // ``const ownedKey = `owned_tiles_${planetId}` ``.
        private static HashSet<string> ProtectedBindings(string code)
        {
            var ids = new HashSet<string>();
            foreach (Match m in Regex.Matches(code, @"\b(?:const|let|var)\s+(\w+)\s*=\s*[""'`]([^""'`]*)"))
                if (ProtectedKeys.Any(k => m.Groups[2].Value.StartsWith(k))) ids.Add(m.Groups[1].Value);
            return ids;
        }

        // Text between the call's opening parenthesis (already consumed) and its matching close.
        private static string CallArguments(string code, int start)
        {
            int depth = 1, i = start;
            for (; i < code.Length && depth > 0; i++)
            {
                if (code[i] == '(') depth++;
                else if (code[i] == ')') depth--;
            }
            return code.Substring(start, i - start - 1);
        }
    }
}
