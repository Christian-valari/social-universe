using System.Reflection;
using NUnit.Framework;
using SocialUniverse.Config;
using SocialUniverse.Mining;
using UnityEngine;

namespace SocialUniverse.Tests
{
    public class MiningRewardCalculatorTests
    {
        private EconomyConfig          _config;
        private AsteroidDefinition     _def;
        private MiningRewardCalculator _calc;

        [SetUp]
        public void SetUp()
        {
            _config = ScriptableObject.CreateInstance<EconomyConfig>();
            SetField(_config, "_idleSecondsPerYieldUnit", 3f);
            SetField(_config, "_minIdleSessionSeconds", 30f);
            SetField(_config, "_maxIdleSessionSeconds", 1800f);
            SetField(_config, "_activeYieldPerTap", 8f);
            SetField(_config, "_minActiveTaps", 5);
            SetField(_config, "_maxActiveTaps", 20);

            _def = ScriptableObject.CreateInstance<AsteroidDefinition>();
            SetField(_def, "_coinsPerUnit", 2);

            _calc = new MiningRewardCalculator(_config);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_config);
            Object.DestroyImmediate(_def);
        }

        private static void SetField(Object target, string field, object value) =>
            target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(target, value);

        // RemainingYield is a { get; private set; } auto-property set inside Initialize() from
        // BaseYield * Random.Range(0.8f, 1.2f) — not directly settable. Tests need exact,
        // reproducible values, so this reaches through the auto-property's backing field
        // directly rather than trying to control the randomized Initialize() path.
        private Asteroid MakeAsteroid(int remainingYield)
        {
            var go = new GameObject("TestAsteroid");
            var asteroid = go.AddComponent<Asteroid>();
            asteroid.Initialize(_def, "slot_0");

            typeof(Asteroid).GetField("<RemainingYield>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(asteroid, remainingYield);

            return asteroid;
        }

        [Test]
        public void Mid_range_yield_is_not_clamped_and_coinsPerSec_reproduces_totalCoins_exactly()
        {
            var asteroid = MakeAsteroid(100); // duration = 100*3 = 300s, within [30,1800]

            var reward = _calc.Compute(asteroid);

            Assert.AreEqual(200, reward.TotalCoins);          // 100 * 2 coins/unit
            Assert.AreEqual(300f, reward.IdleDurationSeconds, 0.001f);
            Assert.AreEqual(200f / 300f, reward.CoinsPerSec, 0.0001f);
            Assert.AreEqual(reward.TotalCoins, reward.IdleDurationSeconds * reward.CoinsPerSec, 0.01f,
                "sessionDurationSec * coinsPerSec must reproduce totalCoins exactly so the server cap never under-grants");
        }

        [Test]
        public void Tiny_yield_clamps_duration_to_minimum_and_still_reproduces_totalCoins()
        {
            var asteroid = MakeAsteroid(1); // raw duration = 3s, clamped up to 30s

            var reward = _calc.Compute(asteroid);

            Assert.AreEqual(30f, reward.IdleDurationSeconds, 0.001f);
            Assert.AreEqual(reward.TotalCoins, reward.IdleDurationSeconds * reward.CoinsPerSec, 0.01f);
        }

        [Test]
        public void Huge_yield_clamps_duration_to_maximum_and_still_reproduces_totalCoins()
        {
            var asteroid = MakeAsteroid(10000); // raw duration = 30000s, clamped down to 1800s

            var reward = _calc.Compute(asteroid);

            Assert.AreEqual(1800f, reward.IdleDurationSeconds, 0.001f);
            Assert.AreEqual(20000, reward.TotalCoins); // 10000 * 2
            Assert.AreEqual(reward.TotalCoins, reward.IdleDurationSeconds * reward.CoinsPerSec, 0.5f,
                "even when duration is clamped down, coinsPerSec must be recomputed so the cap still equals totalCoins");
        }

        [Test]
        public void Active_taps_scale_with_yield_and_clamp_at_bounds()
        {
            Assert.AreEqual(5, _calc.Compute(MakeAsteroid(1)).ActiveTapsRequired);     // ceil(1/8)=1, clamped up to min 5
            Assert.AreEqual(13, _calc.Compute(MakeAsteroid(100)).ActiveTapsRequired);  // ceil(100/8)=13
            Assert.AreEqual(20, _calc.Compute(MakeAsteroid(10000)).ActiveTapsRequired); // clamped down to max 20
        }
    }
}
