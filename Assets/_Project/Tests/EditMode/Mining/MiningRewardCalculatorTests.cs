using System.Reflection;
using NUnit.Framework;
using SocialUniverse.Config;
using SocialUniverse.Mining;
using UnityEngine;

namespace SocialUniverse.Tests
{
    public class MiningRewardCalculatorTests
    {
        private const float RefSpeed = 5f; // EconomyConfig._referenceDroneSpeed default: durations unchanged at this speed

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
            SetField(_config, "_idleYieldFraction", 0.7f);
            SetField(_config, "_referenceDroneSpeed", RefSpeed);

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
        // BaseYield × a random roll (Asteroid.RollYield) — not directly settable. Tests need
        // exact, reproducible values, so this reaches through the auto-property's backing field
        // directly rather than trying to control the randomized Initialize() path.
        private Asteroid MakeAsteroid(int remainingYield)
        {
            var go = new GameObject("TestAsteroid");
            var asteroid = go.AddComponent<Asteroid>();
            asteroid.Initialize(_def, "slot_0", 1f, 1f);

            typeof(Asteroid).GetField("<RemainingYield>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(asteroid, remainingYield);

            return asteroid;
        }

        [Test]
        public void Mid_range_yield_is_not_clamped()
        {
            var asteroid = MakeAsteroid(100); // duration = 100*3 = 300s, within [30,1800]

            var reward = _calc.Compute(asteroid, 1f, RefSpeed);

            Assert.AreEqual(100, reward.MineralQuantity);          // 100 remaining yield * 1.0 mult
            Assert.AreEqual(300f, reward.IdleDurationSeconds, 0.001f);
        }

        [Test]
        public void Tiny_yield_clamps_duration_to_minimum()
        {
            var asteroid = MakeAsteroid(1); // raw duration = 3s, clamped up to 30s

            var reward = _calc.Compute(asteroid, 1f, RefSpeed);

            Assert.AreEqual(30f, reward.IdleDurationSeconds, 0.001f);
        }

        [Test]
        public void Huge_yield_clamps_duration_to_maximum()
        {
            var asteroid = MakeAsteroid(10000); // raw duration = 30000s, clamped down to 1800s

            var reward = _calc.Compute(asteroid, 1f, RefSpeed);

            Assert.AreEqual(1800f, reward.IdleDurationSeconds, 0.001f);
            Assert.AreEqual(10000, reward.MineralQuantity); // 10000 * 1.0
        }

        [Test]
        public void Idle_pays_its_fraction_of_the_full_quantity()
        {
            Assert.AreEqual(56, _calc.Compute(MakeAsteroid(80), 1f, RefSpeed).IdleQuantity);
            Assert.AreEqual(84, _calc.Compute(MakeAsteroid(80), 1.5f, RefSpeed).IdleQuantity);
            Assert.AreEqual(80, _calc.Compute(MakeAsteroid(80), 1f, RefSpeed).MineralQuantity);
        }

        [Test]
        public void Idle_never_pays_more_than_the_full_quantity()
        {
            SetField(_config, "_idleYieldFraction", 1.5f);

            Assert.AreEqual(80, _calc.Compute(MakeAsteroid(80), 1f, RefSpeed).IdleQuantity);
        }

        [Test]
        public void Compute_scales_mineral_quantity_by_effective_yield_multiplier()
        {
            // config with 1:1 pacing; asteroid remaining yield = 10
            var reward1 = _calc.Compute(_asteroid, 1f, RefSpeed);
            var reward2 = _calc.Compute(_asteroid, 2f, RefSpeed);
            Assert.AreEqual(reward1.MineralQuantity * 2, reward2.MineralQuantity);
            // pacing (duration) is independent of the yield multiplier
            Assert.AreEqual(reward1.IdleDurationSeconds, reward2.IdleDurationSeconds);
        }
    

        [Test]
        public void A_faster_drone_shortens_the_idle_duration_proportionally()
        {
            var asteroid = MakeAsteroid(100); // 300s at the reference speed

            Assert.AreEqual(300f, _calc.Compute(asteroid, 1f, RefSpeed).IdleDurationSeconds, 0.001f);
            Assert.AreEqual(150f, _calc.Compute(asteroid, 1f, RefSpeed * 2f).IdleDurationSeconds, 0.001f);
        }

        [Test]
        public void Speed_does_not_push_the_idle_duration_below_the_minimum()
        {
            var asteroid = MakeAsteroid(12); // 36s at the reference speed

            Assert.AreEqual(30f, _calc.Compute(asteroid, 1f, RefSpeed * 4f).IdleDurationSeconds, 0.001f);
        }

        [Test]
        public void Speed_does_not_change_the_mined_quantity()
        {
            var asteroid = MakeAsteroid(100);
            var slow = _calc.Compute(asteroid, 1.5f, RefSpeed);
            var fast = _calc.Compute(asteroid, 1.5f, RefSpeed * 3f);

            Assert.AreEqual(slow.MineralQuantity, fast.MineralQuantity);
        }

        [Test]
        public void A_non_positive_speed_falls_back_to_the_reference_duration()
        {
            Assert.AreEqual(300f, _calc.Compute(MakeAsteroid(100), 1f, 0f).IdleDurationSeconds, 0.001f);
        }
    }
}
