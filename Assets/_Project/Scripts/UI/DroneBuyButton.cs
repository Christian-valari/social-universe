namespace SocialUniverse.UI
{
    // What the garage's bottom buy button shows and does for the drone in the middle of the carousel.
    public readonly struct DroneBuyButtonState
    {
        public readonly bool Visible;
        public readonly bool UnlocksSlot;  // true: buys the next fleet slot; false: buys the centered drone
        public readonly int  Cost;
        public readonly bool Interactable;

        public DroneBuyButtonState(bool visible, bool unlocksSlot, int cost, bool interactable)
        {
            Visible = visible; UnlocksSlot = unlocksSlot; Cost = cost; Interactable = interactable;
        }
    }

    // The buy button follows the centered card: it buys that drone while the fleet has a free slot,
    // offers the next slot once every slot is taken (the drone can't be bought until then), and hides
    // on drones the player already owns. It used to be a fixed slot-unlock button, which read as
    // "every drone costs 500". The server still validates every purchase.
    public static class DroneBuyButton
    {
        public static DroneBuyButtonState For(bool centeredOwned, int droneCost, int ownedCount, int unlockedSlots, int slotCost, int coins)
        {
            if (centeredOwned) return default;

            bool slotFree = ownedCount < unlockedSlots;
            int  cost     = slotFree ? droneCost : slotCost;
            return new DroneBuyButtonState(true, !slotFree, cost, coins >= cost);
        }

        // The words before the coin and price on the button.
        public static string Caption(DroneBuyButtonState state) => state.UnlocksSlot ? "Unlock slot" : "Buy";
    }
}
