using NUnit.Framework;
using SocialUniverse.UI;

namespace SocialUniverse.Tests
{
    // The drone purchase popup's wording and button state (TransactionModal shows these as-is).
    public class DronePurchasePromptTests
    {
        private static readonly DronePurchase Extractor = DronePurchase.ForDrone("extractor", "Extractor Drone", 2, null, 600);
        private static readonly DronePurchase ThirdSlot = DronePurchase.ForSlot(500, currentSlots: 2);

        [Test]
        public void Confirming_a_drone_names_it_and_its_tier()
        {
            var s = DronePurchasePrompt.Confirm(Extractor, coins: 1250);

            Assert.AreEqual("Buy Extractor Drone?", s.Title);
            Assert.AreEqual("Mines up to Tier 2 asteroids", s.Body);
            Assert.AreEqual("Buy", s.ConfirmLabel);
        }

        [Test]
        public void Confirming_a_slot_says_how_big_the_fleet_gets()
        {
            var s = DronePurchasePrompt.Confirm(ThirdSlot, coins: 1250);

            Assert.AreEqual("Unlock a fleet slot?", s.Title);
            Assert.AreEqual("Your fleet grows to 3 slots", s.Body);
            Assert.AreEqual("Unlock", s.ConfirmLabel);
        }

        [Test]
        public void Confirm_shows_the_cost_and_the_balance()
        {
            var s = DronePurchasePrompt.Confirm(Extractor, coins: 1250);

            Assert.AreEqual("Cost 600 · You have 1250", s.Amount);
            Assert.AreEqual("", s.Status);
            Assert.IsTrue(s.ConfirmEnabled);
        }

        [Test]
        public void Confirm_is_disabled_with_a_reason_when_the_wallet_is_short()
        {
            var s = DronePurchasePrompt.Confirm(Extractor, coins: 599);

            Assert.IsFalse(s.ConfirmEnabled);
            Assert.AreEqual("Not enough coins", s.Status);
        }

        [Test]
        public void Confirm_is_enabled_when_the_wallet_holds_exactly_the_cost()
        {
            Assert.IsTrue(DronePurchasePrompt.Confirm(Extractor, coins: 600).ConfirmEnabled);
        }

        [Test]
        public void Success_for_a_drone_welcomes_it_to_the_fleet()
        {
            var s = DronePurchasePrompt.Success(Extractor);

            Assert.AreEqual("Extractor Drone joined your fleet!", s.Title);
            Assert.AreEqual("Select it on its card to mine with it.", s.Body);
        }

        [Test]
        public void Success_for_a_slot_gives_the_new_slot_count()
        {
            var s = DronePurchasePrompt.Success(ThirdSlot);

            Assert.AreEqual("Fleet slot unlocked", s.Title);
            Assert.AreEqual("You now have 3 slots.", s.Body);
        }
    }
}
