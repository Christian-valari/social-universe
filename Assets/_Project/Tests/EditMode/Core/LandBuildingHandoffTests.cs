using NUnit.Framework;
using SocialUniverse.Core;

namespace SocialUniverse.Tests
{
    public class LandBuildingHandoffTests
    {
        [Test]
        public void Begin_stores_all_fields()
        {
            var handoff = new LandBuildingHandoff();
            var slots = new[] { "a", null, "b" };

            handoff.Begin("12", "earth", "player_a", true, slots, 500);

            Assert.AreEqual("12",       handoff.TileId);
            Assert.AreEqual("earth",    handoff.PlanetId);
            Assert.AreEqual("player_a", handoff.OwnerId);
            Assert.IsTrue(handoff.CanEdit);
            Assert.AreEqual(500,        handoff.Coins);
            Assert.AreSame(slots,       handoff.Slots);
        }

        [Test]
        public void Clear_resets_reference_fields()
        {
            var handoff = new LandBuildingHandoff();
            handoff.Begin("12", "earth", "player_a", true, new[] { "a" }, 500);

            handoff.Clear();

            Assert.IsNull(handoff.TileId);
            Assert.IsNull(handoff.PlanetId);
            Assert.IsNull(handoff.Slots);
        }
    }
}
