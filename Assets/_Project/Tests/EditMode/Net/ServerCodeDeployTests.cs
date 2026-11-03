using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using SocialUniverse.Config;
using UnityEditor;
using UnityEngine;

namespace SocialUniverse.Tests
{
    // Pins what ServerCode/deploy.ps1 sends to UGS (docs/deploy-runbook.md):
    // - the UGS CLI reads Cloud Code parameters only from `module.exports.params`, declared after
    //   `module.exports = ...` (which would otherwise overwrite it) — JSDoc @param is ignored;
    // - deploy-list.txt covers every function the client calls and never the unsafe grant functions;
    // - the Economy currency files match EconomyConfig.
    public class ServerCodeDeployTests
    {
        private static readonly string[] NeverDeploy = { "GrantCoins", "GrantStardust", "GrantOfflineIncome" };

        private static string Root => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        private static string ServerCode => Path.Combine(Root, "ServerCode");

        private static IEnumerable<string> ScriptFiles() => Directory.GetFiles(ServerCode, "*.js");

        private static HashSet<string> DeployList() => new(
            File.ReadAllLines(Path.Combine(ServerCode, "deploy-list.txt"))
                .Select(l => l.Trim())
                .Where(l => l.Length > 0 && !l.StartsWith("#"))
                .Select(l => Path.GetFileNameWithoutExtension(l)));

        [Test]
        public void Every_script_declares_its_params_after_module_exports()
        {
            foreach (var file in ScriptFiles())
            {
                string js   = File.ReadAllText(file);
                string name = Path.GetFileName(file);
                int exportsAt = js.IndexOf("module.exports = ");
                var decl = Regex.Match(js, @"module\.exports\.params\s*=\s*\{(?<body>[^}]*)\};");

                Assert.GreaterOrEqual(exportsAt, 0, $"{name}: no `module.exports = `");
                Assert.IsTrue(decl.Success, $"{name}: no `module.exports.params = {{...}};`");
                Assert.Greater(decl.Index, exportsAt, $"{name}: params must be declared after `module.exports = ` or it is overwritten");

                var declared = new HashSet<string>(Regex.Matches(decl.Groups["body"].Value, @"(\w+)\s*:").Cast<Match>().Select(m => m.Groups[1].Value));
                foreach (var used in ParamsRead(js))
                    CollectionAssert.Contains(declared, used, $"{name} reads params.{used} but does not declare it");
            }
        }

        // Parameters a script reads: `const { a, b } = params` and `params.x`.
        private static IEnumerable<string> ParamsRead(string js)
        {
            foreach (Match m in Regex.Matches(js, @"\{([^{}]*)\}\s*=\s*params\b"))
                foreach (var part in m.Groups[1].Value.Split(','))
                {
                    var key = part.Split(':', '=')[0].Trim();
                    if (key.Length > 0) yield return key;
                }
            foreach (Match m in Regex.Matches(js, @"\bparams\.(\w+)"))
                yield return m.Groups[1].Value;
        }

        [Test]
        public void Deploy_list_names_existing_scripts_and_never_the_unsafe_grants()
        {
            var list = DeployList();
            foreach (var fn in list)
                Assert.IsTrue(File.Exists(Path.Combine(ServerCode, fn + ".js")), $"deploy-list.txt names missing {fn}.js");
            foreach (var fn in NeverDeploy)
                CollectionAssert.DoesNotContain(list, fn, $"{fn} must never be deployed (Known Issue #16)");
        }

        [Test]
        public void Every_function_the_client_calls_is_deployed()
        {
            var list = DeployList();
            var called = new SortedSet<string>();
            foreach (var cs in Directory.GetFiles(Path.Combine(Application.dataPath, "_Project", "Scripts"), "*.cs", SearchOption.AllDirectories))
            {
                if (Path.GetFileName(cs) == "CloudCodeTestHarness.cs") continue; // Editor-only debug buttons
                foreach (Match m in Regex.Matches(File.ReadAllText(cs), @"CallAsync(?:<[^>(]*>)?\(\s*""(\w+)"""))
                    called.Add(m.Groups[1].Value);
            }

            Assert.Greater(called.Count, 20, "Expected to find the client's Cloud Code calls");
            foreach (var fn in called)
                CollectionAssert.Contains(list, fn, $"The client calls {fn}, but deploy-list.txt does not deploy it");
            foreach (var fn in NeverDeploy)
                CollectionAssert.DoesNotContain(called, fn, $"Client code must not call {fn}");
        }

        [Test]
        public void Economy_currency_files_match_EconomyConfig()
        {
            var config = AssetDatabase.LoadAssetAtPath<EconomyConfig>("Assets/_Project/ScriptableObjects/EconomyConfig.asset");
            Assert.IsNotNull(config);

            var files = Directory.GetFiles(Path.Combine(ServerCode, "Economy"), "*.ecc")
                .ToDictionary(Path.GetFileNameWithoutExtension, f => JObject.Parse(File.ReadAllText(f)));

            CollectionAssert.AreEquivalent(new[] { config.CoinsCurrencyId, config.StardustCurrencyId }, files.Keys,
                "One .ecc per currency, named by its ID");
            Assert.AreEqual(config.StartingCoins,    (int)files[config.CoinsCurrencyId]["initial"]);
            Assert.AreEqual(config.StartingStardust, (int)files[config.StardustCurrencyId]["initial"]);
        }
    }
}
