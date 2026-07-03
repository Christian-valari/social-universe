using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using SocialUniverse.Config;
using UnityEngine;

namespace SocialUniverse.Tests
{
    // Guards against ServerCode/ValidateMining.js's ABSOLUTE_SESSION_CAP_SECONDS drifting below
    // EconomyConfig.MaxIdleSessionSeconds again. If the server's cap is lower than the client's
    // clamp ceiling, any legitimate long-duration idle/active claim gets silently under-granted
    // by the server (see ValidateMining.js's cappedDuration = min(sessionDurationSec, ABSOLUTE_SESSION_CAP_SECONDS)).
    // This can't be a live invocation of the JS (no Node test harness in this Unity-only
    // project), so instead it reads the .js source as plain text and extracts the constant.
    public class ValidateMiningCapAlignmentTests
    {
        private static string ValidateMiningJsPath =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", "ServerCode", "ValidateMining.js"));

        [Test]
        public void Server_session_cap_is_at_least_the_client_max_idle_session_seconds()
        {
            string path = ValidateMiningJsPath;
            Assert.IsTrue(File.Exists(path), $"Expected to find ValidateMining.js at {path}");

            string source = File.ReadAllText(path);
            var match = Regex.Match(source, @"ABSOLUTE_SESSION_CAP_SECONDS\s*=\s*(?<value>[0-9]+(\.[0-9]+)?)");
            Assert.IsTrue(match.Success, "Could not find ABSOLUTE_SESSION_CAP_SECONDS in ValidateMining.js");

            float serverCapSeconds = float.Parse(match.Groups["value"].Value);

            var config = ScriptableObject.CreateInstance<EconomyConfig>();
            try
            {
                Assert.GreaterOrEqual(serverCapSeconds, config.MaxIdleSessionSeconds,
                    "ValidateMining.js's ABSOLUTE_SESSION_CAP_SECONDS must be >= EconomyConfig.MaxIdleSessionSeconds, " +
                    "or the server will clip legitimate long-duration idle/active mining claims.");
            }
            finally
            {
                Object.DestroyImmediate(config);
            }
        }
    }
}
