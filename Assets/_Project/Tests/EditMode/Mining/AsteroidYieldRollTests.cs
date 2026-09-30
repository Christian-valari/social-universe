using NUnit.Framework;
using SocialUniverse.Config;
using SocialUniverse.Mining;
using UnityEditor;

namespace SocialUniverse.Tests
{
    // The server caps a mining grant at ceil(ceil(baseYield × YIELD_ROLL_MAX) × mult)
    // (ServerCode/ValidateMining.js). That cap is only safe if a spawned asteroid can never roll
    // above EconomyConfig.AsteroidYieldRollMax.
    public class AsteroidYieldRollTests
    {
        [Test]
        public void Roll_endpoints_hit_the_configured_min_and_max()
        {
            Assert.AreEqual(64, Asteroid.RollYield(80, 0.8f, 1.2f, 0f));
            Assert.AreEqual(96, Asteroid.RollYield(80, 0.8f, 1.2f, 1f));
        }

        [Test]
        public void Roll_never_exceeds_base_times_max()
        {
            for (int i = 0; i <= 100; i++)
                Assert.LessOrEqual(Asteroid.RollYield(15, 0.8f, 1.2f, i / 100f), 18);
        }

        [Test]
        public void Project_config_uses_the_documented_roll_range()
        {
            var config = AssetDatabase.LoadAssetAtPath<EconomyConfig>("Assets/_Project/ScriptableObjects/EconomyConfig.asset");
            Assert.IsNotNull(config);
            Assert.AreEqual(0.8f, config.AsteroidYieldRollMin, 1e-6f);
            Assert.AreEqual(1.2f, config.AsteroidYieldRollMax, 1e-6f);
        }
    }
}
