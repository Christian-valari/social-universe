using NUnit.Framework;
using SocialUniverse.UI;

namespace SocialUniverse.Tests
{
    // Pure formatting/direction math behind the "why buy this drone" delta shown on acquirable
    // Drone Garage cards. Compares a candidate drone's base stats against the active drone.
    public class DroneComparisonTests
    {
        [Test]
        public void DirectionOf_reports_up_down_and_same()
        {
            Assert.AreEqual(DeltaDirection.Up,   DroneComparison.DirectionOf(from: 50f, to: 120f));
            Assert.AreEqual(DeltaDirection.Down, DroneComparison.DirectionOf(from: 120f, to: 50f));
            Assert.AreEqual(DeltaDirection.Same, DroneComparison.DirectionOf(from: 50f, to: 50f));
        }

        [Test]
        public void DirectionOf_treats_tiny_float_noise_as_same()
        {
            Assert.AreEqual(DeltaDirection.Same, DroneComparison.DirectionOf(from: 1.6f, to: 1.6000001f));
        }

        [Test]
        public void IntStat_rounds_values_and_captures_direction()
        {
            var row = DroneComparison.IntStat("Cargo", from: 50f, to: 119.6f);
            Assert.AreEqual("Cargo", row.Label);
            Assert.AreEqual("50", row.FromText);
            Assert.AreEqual("120", row.ToText);
            Assert.AreEqual(DeltaDirection.Up, row.Direction);
        }

        [Test]
        public void MultStat_formats_with_times_prefix_and_one_decimal()
        {
            var row = DroneComparison.MultStat("Yield", from: 1f, to: 1.6f);
            Assert.AreEqual("×1.0", row.FromText);
            Assert.AreEqual("×1.6", row.ToText);
            Assert.AreEqual(DeltaDirection.Up, row.Direction);
        }

        [Test]
        public void TierLine_reads_as_the_asteroid_tier_it_can_mine()
        {
            Assert.AreEqual("Mines up to Tier 3 asteroids", DroneComparison.TierLine(3));
        }
    }
}
