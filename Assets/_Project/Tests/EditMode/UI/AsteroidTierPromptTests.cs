using NUnit.Framework;
using SocialUniverse.UI;

namespace SocialUniverse.Tests
{
    // Tapping an asteroid tells the player its tier and whether their active drone can mine it, by
    // the same rule MiningController enforces (drone tier >= asteroid tier).
    public class AsteroidTierPromptTests
    {
        [Test]
        public void Title_names_the_mineral_and_its_tier()
        {
            Assert.AreEqual("Mine Nickel? · Tier 2", AsteroidTierPrompt.For("Nickel", 2, "Scout Drone", 1).Title);
        }

        [Test]
        public void A_drone_of_the_asteroids_tier_can_mine_it()
        {
            var v = AsteroidTierPrompt.For("Nickel", 2, "Extractor Drone", 2);

            Assert.IsTrue(v.CanMine);
            Assert.AreEqual("Your Extractor Drone can mine this", v.Status);
        }

        [Test]
        public void A_higher_tier_drone_can_mine_a_lower_tier_asteroid()
        {
            Assert.IsTrue(AsteroidTierPrompt.For("Carbon", 1, "Titan Drone", 5).CanMine);
        }

        [Test]
        public void A_lower_tier_drone_cannot_and_the_prompt_says_what_is_needed()
        {
            var v = AsteroidTierPrompt.For("Nickel", 2, "Scout Drone", 1);

            Assert.IsFalse(v.CanMine);
            Assert.AreEqual("Needs a Tier 2+ drone (your Scout Drone is Tier 1)", v.Status);
        }

        [Test]
        public void With_no_active_drone_nothing_can_be_mined()
        {
            var v = AsteroidTierPrompt.For("Iron", 1, null, 0);

            Assert.IsFalse(v.CanMine);
            Assert.AreEqual("Needs a Tier 1+ drone", v.Status);
        }

        // MiningController runs one idle session at a time, so while the drone is out on one
        // asteroid every other asteroid is refused — the prompt says so instead of offering buttons
        // that do nothing.
        [Test]
        public void A_drone_busy_mining_another_asteroid_cannot_mine_this_one()
        {
            var v = AsteroidTierPrompt.For("Nickel", 1, "Scout Drone", 1, busyMineral: "Iron");

            Assert.IsFalse(v.CanMine);
            Assert.AreEqual("Your drone is currently mining Iron", v.Status);
        }

        [Test]
        public void A_drone_whose_run_is_ready_to_claim_asks_for_the_claim_first()
        {
            var v = AsteroidTierPrompt.For("Nickel", 1, "Scout Drone", 1, busyMineral: "Iron", busyReadyToClaim: true);

            Assert.IsFalse(v.CanMine);
            Assert.AreEqual("Your drone is done — claim your Iron first", v.Status);
        }

        // Freeing the drone would not help with an asteroid above its tier, so "busy" would mislead.
        [Test]
        public void The_tier_message_wins_over_the_busy_message()
        {
            var v = AsteroidTierPrompt.For("Nickel", 2, "Scout Drone", 1, busyMineral: "Iron");

            Assert.IsFalse(v.CanMine);
            Assert.AreEqual("Needs a Tier 2+ drone (your Scout Drone is Tier 1)", v.Status);
        }

        [Test]
        public void A_free_drone_can_still_mine()
        {
            Assert.IsTrue(AsteroidTierPrompt.For("Nickel", 1, "Scout Drone", 1, busyMineral: null).CanMine);
        }

        // Between the claim tap and the server's answer the drone is still busy: the server holds the
        // run being claimed, so MiningController refuses a new start until the claim has an answer.
        [Test]
        public void A_drone_bringing_back_a_claimed_haul_cannot_mine_yet()
        {
            var v = AsteroidTierPrompt.For("Nickel", 1, "Scout Drone", 1, returningMineral: "Iron");

            Assert.IsFalse(v.CanMine);
            Assert.AreEqual("Your drone is bringing back your Iron", v.Status);
        }

        [Test]
        public void The_tier_message_wins_over_the_returning_message()
        {
            var v = AsteroidTierPrompt.For("Nickel", 2, "Scout Drone", 1, returningMineral: "Iron");

            Assert.AreEqual("Needs a Tier 2+ drone (your Scout Drone is Tier 1)", v.Status);
        }
    }
}
