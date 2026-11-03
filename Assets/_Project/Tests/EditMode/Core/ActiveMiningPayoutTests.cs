using NUnit.Framework;
using SocialUniverse.Core;

namespace SocialUniverse.Tests
{
    public class ActiveMiningPayoutTests
    {
        [TestCase(80, 1f, 80)]
        [TestCase(80, 0.925f, 74)]
        [TestCase(80, 0.5f, 40)]
        [TestCase(80, 0f, 0)]
        [TestCase(0, 1f, 0)]
        public void Quantity_is_the_banked_share_of_the_full_amount(int full, float banked, int expected) =>
            Assert.AreEqual(expected, ActiveMiningPayout.Quantity(full, banked));

        // A small asteroid pays at least one unit for any hit, so a lone Late hit is never worth nothing.
        [Test]
        public void A_low_yield_asteroid_still_pays_for_any_hit()
        {
            Assert.AreEqual(1, ActiveMiningPayout.Quantity(6, 0.6f / 16f));
            Assert.AreEqual(4, ActiveMiningPayout.Quantity(6, 0.6f));
            Assert.AreEqual(6, ActiveMiningPayout.Quantity(6, 1f));
        }

        [Test]
        public void Quantity_never_exceeds_the_full_amount()
        {
            Assert.AreEqual(80, ActiveMiningPayout.Quantity(80, 1.5f));
        }

        [Test]
        public void A_negative_share_or_amount_pays_nothing()
        {
            Assert.AreEqual(0, ActiveMiningPayout.Quantity(80, -0.5f));
            Assert.AreEqual(0, ActiveMiningPayout.Quantity(-5, 1f));
        }
    }
}
