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
            var unlocked = new[] { true, true, false };

            handoff.Begin("12", "earth", "Planet_Earth", "player_a", true, slots, unlocked, 500, 25, "Ada", "avatar_3");

            Assert.AreEqual("12",           handoff.TileId);
            Assert.AreEqual("earth",        handoff.PlanetId);
            Assert.AreEqual("Planet_Earth", handoff.RegistryPlanetId);
            Assert.AreEqual("player_a",     handoff.OwnerId);
            Assert.IsTrue(handoff.CanEdit);
            Assert.AreEqual(500,            handoff.Coins);
            Assert.AreSame(slots,           handoff.Slots);
            Assert.AreSame(unlocked,        handoff.Unlocked);
            Assert.AreEqual(25,             handoff.Stardust);
            Assert.AreEqual("Ada",          handoff.DisplayName);
            Assert.AreEqual("avatar_3",     handoff.AvatarId);
        }

        [Test]
        public void Clear_resets_reference_fields()
        {
            var handoff = new LandBuildingHandoff();
            handoff.Begin("12", "earth", "Planet_Earth", "player_a", true, new[] { "a" }, new[] { true }, 500, 25, "Ada", "avatar_3");

            handoff.Clear();

            Assert.IsNull(handoff.TileId);
            Assert.IsNull(handoff.PlanetId);
            Assert.IsNull(handoff.RegistryPlanetId);
            Assert.IsNull(handoff.Slots);
            Assert.IsNull(handoff.Unlocked);
            Assert.IsNull(handoff.DisplayName);
            Assert.IsNull(handoff.AvatarId);
        }
    }
}
