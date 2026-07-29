using NUnit.Framework;
using UnityEngine;
using SocialUniverse.Config;

namespace SocialUniverse.Tests
{
    public class FirebaseAuthConfigTests
    {
        [Test]
        public void Defaults_are_placeholders_so_missing_setup_is_detectable()
        {
            var cfg = ScriptableObject.CreateInstance<FirebaseAuthConfig>();
            StringAssert.StartsWith("YOUR_", cfg.ProjectId);
            Assert.IsNotNull(cfg.GoogleWebClientId);
        }
    }
}
