using System;
using NUnit.Framework;
using SocialUniverse.Config;
using SocialUniverse.Mining;
using UnityEngine;

namespace SocialUniverse.Tests
{
    public class IdleMiningCalculatorTests
    {
        private EconomyConfig           _config;
        private IdleMiningCalculator    _calc;
        private DroneDefinition         _droneDef;

        [SetUp]
        public void SetUp()
        {
            _config              = ScriptableObject.CreateInstance<EconomyConfig>();
            _droneDef            = ScriptableObject.CreateInstance<DroneDefinition>();
            _calc                = new IdleMiningCalculator(_config);
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_config);
            UnityEngine.Object.DestroyImmediate(_droneDef);
        }

        private DroneRuntime MakeDrone(int cargoCap = 100, int cargoAmount = 0)
        {
            var runtime = new DroneRuntime(_droneDef);
            if (cargoAmount > 0) runtime.AddCargo(cargoAmount);
            return runtime;
        }

        [Test]
        public void Returns_zero_for_same_session_time()
        {
            var drone = MakeDrone();
            int yield = _calc.Calculate(DateTime.UtcNow, drone);
            Assert.AreEqual(0, yield);
        }

        [Test]
        public void Caps_yield_at_cargo_space()
        {
            var drone = MakeDrone(cargoCap: 10, cargoAmount: 8);
            // Simulate 1 hour offline with rate=1/s → would yield 3600 but cargo space is 2
            var lastSession = DateTime.UtcNow.AddHours(-1);
            int yield = _calc.Calculate(lastSession, drone);
            Assert.AreEqual(2, yield);
        }

        [Test]
        public void Does_not_exceed_max_offline_hours()
        {
            var drone       = MakeDrone(cargoCap: 999999);
            var lastSession = DateTime.UtcNow.AddDays(-7);
            int yield       = _calc.Calculate(lastSession, drone);

            // Default MaxOfflineHours=8, IdleMiningRate=1 → max = 8*3600 = 28800
            int expectedMax = (int)(_config.MaxOfflineHours * 3600f * _config.IdleMiningRate);
            Assert.LessOrEqual(yield, expectedMax);
        }
    }
}
