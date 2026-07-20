using NUnit.Framework;
using SocialUniverse.Travel;

namespace SocialUniverse.Tests
{
    public class FuelRechargeEstimatorTests
    {
        // --- PredictFuel ---

        [Test]
        public void PredictFuel_NoElapsedTime_ReturnsSyncedFuel()
        {
            Assert.AreEqual(40f, FuelRechargeEstimator.PredictFuel(40f, 100f, 10f, 0));
        }

        [Test]
        public void PredictFuel_RechargesAtConfiguredRate()
        {
            // 10/hour for half an hour = +5
            Assert.AreEqual(45f, FuelRechargeEstimator.PredictFuel(40f, 100f, 10f, 1800), 0.01f);
        }

        [Test]
        public void PredictFuel_ClampsAtMaxFuel()
        {
            Assert.AreEqual(100f, FuelRechargeEstimator.PredictFuel(99f, 100f, 10f, 7200));
        }

        [Test]
        public void PredictFuel_ZeroRate_ReturnsSyncedFuel()
        {
            Assert.AreEqual(40f, FuelRechargeEstimator.PredictFuel(40f, 100f, 0f, 3600));
        }

        [Test]
        public void PredictFuel_SyncedAboveMax_ClampsToMax()
        {
            Assert.AreEqual(100f, FuelRechargeEstimator.PredictFuel(120f, 100f, 10f, 0));
        }

        // --- SecondsToFull ---

        [Test]
        public void SecondsToFull_AlreadyFull_ReturnsZero()
        {
            Assert.AreEqual(0f, FuelRechargeEstimator.SecondsToFull(100f, 100f, 10f));
        }

        [Test]
        public void SecondsToFull_ComputesFromMissingFuelAndRate()
        {
            // 60 missing at 10/hour = 6 hours
            Assert.AreEqual(6f * 3600f, FuelRechargeEstimator.SecondsToFull(40f, 100f, 10f), 0.01f);
        }

        [Test]
        public void SecondsToFull_ZeroRate_ReturnsNever()
        {
            Assert.AreEqual(-1f, FuelRechargeEstimator.SecondsToFull(40f, 100f, 0f));
        }
    }
}
