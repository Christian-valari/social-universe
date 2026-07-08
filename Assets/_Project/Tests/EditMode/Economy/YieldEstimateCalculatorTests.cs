using System.Reflection;
using NUnit.Framework;
using SocialUniverse.Config;
using SocialUniverse.Economy;
using UnityEngine;

namespace SocialUniverse.Tests
{
    public class YieldEstimateCalculatorTests
    {
        private EconomyConfig            _config;
        private YieldEstimateCalculator  _calc;

        private const long HourMs = 3600000L;
        private const long Now    = 1_800_000_000_000L; // arbitrary fixed epoch-ms instant

        [SetUp]
        public void SetUp()
        {
            _config = ScriptableObject.CreateInstance<EconomyConfig>();
            SetField(_config, "_baseYieldPerTilePerHour", 2f);
            SetField(_config, "_buildLevelYieldMultiplier", 0.25f);
            SetField(_config, "_visitYieldBonus", 0.1f);
            SetField(_config, "_maxYieldAccrualHours", 24f);
            SetField(_config, "_maxVisitCount", 50);

            _calc = new YieldEstimateCalculator();
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_config);

        private static void SetField(Object target, string field, object value) =>
            target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(target, value);

        private static LandTileEntry Entry(long lastClaimTs, int buildLevel, int visitCount) =>
            new LandTileEntry { LastYieldClaimTs = lastClaimTs, BuildLevel = buildLevel, VisitCount = visitCount };

        [Test]
        public void Zero_elapsed_time_accrues_nothing_but_reports_base_rate()
        {
            var estimate = _calc.Compute(Entry(Now, buildLevel: 0, visitCount: 0), _config, Now);

            Assert.AreEqual(0, estimate.AccruedCoins);
            Assert.AreEqual(2f, estimate.RatePerHour, 0.0001f);
        }

        [Test]
        public void Base_rate_only_accrues_linearly_with_elapsed_hours()
        {
            var estimate = _calc.Compute(Entry(Now - 3 * HourMs, buildLevel: 0, visitCount: 0), _config, Now);

            Assert.AreEqual(6, estimate.AccruedCoins); // 2/hr * 3h
            Assert.AreEqual(2f, estimate.RatePerHour, 0.0001f);
        }

        [Test]
        public void Elapsed_time_past_max_accrual_hours_is_clamped()
        {
            var estimate = _calc.Compute(Entry(Now - 100 * HourMs, buildLevel: 0, visitCount: 0), _config, Now);

            Assert.AreEqual(48, estimate.AccruedCoins); // clamped to 24h: 2/hr * 24h
        }

        [Test]
        public void Build_level_adds_a_multiplicative_bonus_to_rate_and_accrual()
        {
            var estimate = _calc.Compute(Entry(Now - 1 * HourMs, buildLevel: 2, visitCount: 0), _config, Now);

            // buildBonus = 2 * 0.25 = 0.5 -> rate = 2 * 1.5 = 3/hr
            Assert.AreEqual(3f, estimate.RatePerHour, 0.0001f);
            Assert.AreEqual(3, estimate.AccruedCoins);
        }

        [Test]
        public void Visit_count_past_max_visit_count_is_clamped()
        {
            var estimate = _calc.Compute(Entry(Now - 1 * HourMs, buildLevel: 0, visitCount: 60), _config, Now);

            // visitBonus = min(60,50) * 0.1 = 5.0 -> rate = 2 * 6 = 12/hr
            Assert.AreEqual(12f, estimate.RatePerHour, 0.0001f);
            Assert.AreEqual(12, estimate.AccruedCoins);
        }

        [Test]
        public void Build_and_visit_bonuses_combine()
        {
            var estimate = _calc.Compute(Entry(Now - 2 * HourMs, buildLevel: 4, visitCount: 50), _config, Now);

            // buildBonus = 4*0.25=1.0, visitBonus = 50*0.1=5.0 -> rate = 2*(1+1+5) = 14/hr
            Assert.AreEqual(14f, estimate.RatePerHour, 0.0001f);
            Assert.AreEqual(28, estimate.AccruedCoins); // 14/hr * 2h
        }
    }
}
