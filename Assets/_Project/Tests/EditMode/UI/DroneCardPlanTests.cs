using NUnit.Framework;
using SocialUniverse.UI;

namespace SocialUniverse.Tests
{
    // The garage carousel may only be torn down when the SET of cards changes. A plain state change
    // — selecting a drone, upgrading a stat — must re-bind the existing cards in place, because
    // removing and re-adding panels resets SimpleScrollSnap to the first card. DroneCardPlan owns
    // that decision and the card order.
    public class DroneCardPlanTests
    {
        private static readonly string[] AllDrones = { "scout", "extractor", "excavator" };

        [Test]
        public void Build_lists_owned_drones_first_then_the_rest_in_registry_order()
        {
            var cards = DroneCardPlan.Build(new[] { "extractor" }, AllDrones);

            CollectionAssert.AreEqual(
                new[] { "extractor", "scout", "excavator" },
                cards.ConvertAll(c => c.DroneId));
        }

        [Test]
        public void Build_marks_owned_drones_owned_and_the_rest_acquirable()
        {
            var cards = DroneCardPlan.Build(new[] { "extractor" }, AllDrones);

            Assert.IsTrue(cards[0].Owned,  "the owned drone");
            Assert.IsFalse(cards[1].Owned, "a drone that is not owned yet");
        }

        [Test]
        public void Build_ignores_owned_ids_the_registry_does_not_know()
        {
            var cards = DroneCardPlan.Build(new[] { "retired_drone", "scout" }, AllDrones);

            CollectionAssert.AreEqual(
                new[] { "scout", "extractor", "excavator" },
                cards.ConvertAll(c => c.DroneId));
        }

        [Test]
        public void SameCards_is_true_when_only_drone_state_changed()
        {
            var before = DroneCardPlan.Build(new[] { "scout" }, AllDrones);
            var after  = DroneCardPlan.Build(new[] { "scout" }, AllDrones);

            Assert.IsTrue(DroneCardPlan.SameCards(before, after));
        }

        [Test]
        public void SameCards_is_false_once_a_drone_is_acquired()
        {
            var before = DroneCardPlan.Build(new[] { "scout" }, AllDrones);
            var after  = DroneCardPlan.Build(new[] { "scout", "extractor" }, AllDrones);

            Assert.IsFalse(DroneCardPlan.SameCards(before, after));
        }

        [Test]
        public void IndexOf_locates_a_card_by_drone_id_and_returns_minus_one_when_absent()
        {
            var cards = DroneCardPlan.Build(new[] { "scout" }, AllDrones);

            Assert.AreEqual(1,  DroneCardPlan.IndexOf(cards, "extractor"));
            Assert.AreEqual(-1, DroneCardPlan.IndexOf(cards, "titan"));
        }
    }
}
