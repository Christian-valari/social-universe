using NUnit.Framework;
using SocialUniverse.UI;

namespace SocialUniverse.Tests
{
    // The garage's bottom buy button follows the drone in the middle of the carousel: it buys that
    // drone when a slot is free, offers the next fleet slot when none is, and hides on owned drones.
    // It used to be a fixed slot-unlock button, which read as "every drone costs 500".
    public class DroneBuyButtonTests
    {
        [Test]
        public void Hidden_when_the_centered_drone_is_already_owned()
        {
            var state = DroneBuyButton.For(centeredOwned: true, droneCost: 0,
                ownedCount: 1, unlockedSlots: 2, slotCost: 500, coins: 10_000);

            Assert.IsFalse(state.Visible);
        }

        [Test]
        public void Buys_the_centered_drone_at_its_own_price_when_a_slot_is_free()
        {
            var state = DroneBuyButton.For(centeredOwned: false, droneCost: 600,
                ownedCount: 1, unlockedSlots: 2, slotCost: 500, coins: 10_000);

            Assert.IsTrue(state.Visible);
            Assert.IsFalse(state.UnlocksSlot, "a free slot means the button buys the drone");
            Assert.AreEqual(600, state.Cost);
        }

        [Test]
        public void Offers_the_next_slot_at_the_slot_price_when_every_slot_is_full()
        {
            var state = DroneBuyButton.For(centeredOwned: false, droneCost: 600,
                ownedCount: 2, unlockedSlots: 2, slotCost: 500, coins: 10_000);

            Assert.IsTrue(state.Visible);
            Assert.IsTrue(state.UnlocksSlot, "no free slot, so the drone can't be bought yet");
            Assert.AreEqual(500, state.Cost);
        }

        [Test]
        public void Disabled_when_the_drone_costs_more_than_the_wallet_holds()
        {
            var state = DroneBuyButton.For(centeredOwned: false, droneCost: 3000,
                ownedCount: 1, unlockedSlots: 2, slotCost: 500, coins: 2999);

            Assert.IsFalse(state.Interactable);
        }

        [Test]
        public void Disabled_when_the_slot_costs_more_than_the_wallet_holds()
        {
            // 600 coins would buy the drone, but the button is offering the 1000-coin slot.
            var state = DroneBuyButton.For(centeredOwned: false, droneCost: 600,
                ownedCount: 3, unlockedSlots: 3, slotCost: 1000, coins: 600);

            Assert.IsFalse(state.Interactable);
        }

        [Test]
        public void Enabled_when_the_wallet_holds_exactly_the_price()
        {
            var state = DroneBuyButton.For(centeredOwned: false, droneCost: 600,
                ownedCount: 1, unlockedSlots: 2, slotCost: 500, coins: 600);

            Assert.IsTrue(state.Interactable);
        }

        // The button reads "<caption> <coin> <price>", so the caption is what says a slot is on offer.
        [Test]
        public void Caption_says_whether_the_button_buys_the_drone_or_a_slot()
        {
            var drone = DroneBuyButton.For(centeredOwned: false, droneCost: 600,
                ownedCount: 1, unlockedSlots: 2, slotCost: 500, coins: 0);
            var slot  = DroneBuyButton.For(centeredOwned: false, droneCost: 600,
                ownedCount: 2, unlockedSlots: 2, slotCost: 500, coins: 0);

            Assert.AreEqual("Buy", DroneBuyButton.Caption(drone));
            Assert.AreEqual("Unlock slot", DroneBuyButton.Caption(slot));
        }
    }
}
