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
        private Asteroid                _asteroid;

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
            SetField(_config, "_activeSecondsPerTap", 3f);
            SetField(_config, "_minActiveSessionSeconds", 20f);
            SetField(_config, "_maxActiveSessionSeconds", 45f);

            _def = ScriptableObject.CreateInstance<AsteroidDefinition>();
            SetField(_def, "_coinsPerUnit", 2); // retained (legacy); the calculator no longer reads it

            _calc = new MiningRewardCalculator(_config);

            _asteroid = MakeAsteroid(10);
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
        public void Mid_range_yield_is_not_clamped_and_unitsPerSec_reproduces_mineralQuantity_exactly()
        {
            var asteroid = MakeAsteroid(100); // duration = 100*3 = 300s, within [30,1800]

            var reward = _calc.Compute(asteroid, 1f);

            Assert.AreEqual(100, reward.MineralQuantity);          // 100 remaining yield * 1.0 mult
            Assert.AreEqual(300f, reward.IdleDurationSeconds, 0.001f);
            Assert.AreEqual(100f / 300f, reward.UnitsPerSec, 0.0001f);
            Assert.AreEqual(reward.MineralQuantity, reward.IdleDurationSeconds * reward.UnitsPerSec, 0.01f,
                "sessionDurationSec * unitsPerSec must reproduce mineralQuantity exactly so the server cap never under-grants");
        }

        [Test]
        public void Tiny_yield_clamps_duration_to_minimum_and_still_reproduces_mineralQuantity()
        {
            var asteroid = MakeAsteroid(1); // raw duration = 3s, clamped up to 30s

            var reward = _calc.Compute(asteroid, 1f);

            Assert.AreEqual(30f, reward.IdleDurationSeconds, 0.001f);
            Assert.AreEqual(reward.MineralQuantity, reward.IdleDurationSeconds * reward.UnitsPerSec, 0.01f);
        }

        [Test]
        public void Huge_yield_clamps_duration_to_maximum_and_still_reproduces_mineralQuantity()
        {
            var asteroid = MakeAsteroid(10000); // raw duration = 30000s, clamped down to 1800s

            var reward = _calc.Compute(asteroid, 1f);

            Assert.AreEqual(1800f, reward.IdleDurationSeconds, 0.001f);
            Assert.AreEqual(10000, reward.MineralQuantity); // 10000 * 1.0
            Assert.AreEqual(reward.MineralQuantity, reward.IdleDurationSeconds * reward.UnitsPerSec, 0.5f,
                "even when duration is clamped down, unitsPerSec must be recomputed so the cap still equals mineralQuantity");
        }

        [Test]
        public void Active_taps_scale_with_yield_and_clamp_at_bounds()
        {
            Assert.AreEqual(5, _calc.Compute(MakeAsteroid(1), 1f).ActiveTapsRequired);     // ceil(1/8)=1, clamped up to min 5
            Assert.AreEqual(13, _calc.Compute(MakeAsteroid(100), 1f).ActiveTapsRequired);  // ceil(100/8)=13
            Assert.AreEqual(20, _calc.Compute(MakeAsteroid(10000), 1f).ActiveTapsRequired); // clamped down to max 20
        }

        [Test]
        public void Active_session_duration_scales_with_taps_and_clamps_at_bounds()
        {
            // taps=5 (clamped up from ceil(1/8)=1) -> raw 5*3=15s, clamped up to min 20s
            Assert.AreEqual(20f, _calc.Compute(MakeAsteroid(1), 1f).ActiveSessionDurationSeconds, 0.001f);
            // taps=13 -> raw 13*3=39s, within [20,45]
            Assert.AreEqual(39f, _calc.Compute(MakeAsteroid(100), 1f).ActiveSessionDurationSeconds, 0.001f);
            // taps=20 (clamped down from a huge yield) -> raw 20*3=60s, clamped down to max 45s
            Assert.AreEqual(45f, _calc.Compute(MakeAsteroid(10000), 1f).ActiveSessionDurationSeconds, 0.001f);
        }

        [Test]
        public void Compute_scales_mineral_quantity_by_effective_yield_multiplier()
        {
            // config with 1:1 pacing; asteroid remaining yield = 10
            var reward1 = _calc.Compute(_asteroid, 1f);
            var reward2 = _calc.Compute(_asteroid, 2f);
            Assert.AreEqual(reward1.MineralQuantity * 2, reward2.MineralQuantity);
            // pacing (duration/taps) is independent of the yield multiplier
            Assert.AreEqual(reward1.IdleDurationSeconds, reward2.IdleDurationSeconds);
        }
    }
}
