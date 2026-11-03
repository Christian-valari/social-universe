namespace SocialUniverse.UI
{
    // Wording and button state for the Drone Garage's purchase popup (TransactionModal), kept pure
    // so it can be unit-tested. The server still validates the purchase; "Not enough coins" here
    // only saves a pointless request.
    public static class DronePurchasePrompt
    {
        public const string Busy           = "Buying…";
        public const string NotEnoughCoins = "Not enough coins";

        public static TransactionScreen Confirm(DronePurchase p, int coins)
        {
            bool canAfford = coins >= p.Cost;
            return new TransactionScreen(
                p.IsSlot ? "Unlock a fleet slot?" : $"Buy {p.DisplayName}?",
                p.IsSlot ? $"Your fleet grows to {p.CurrentSlots + 1} slots" : DroneComparison.TierLine(p.Tier),
                $"Cost {p.Cost} · You have {coins}",
                canAfford ? "" : NotEnoughCoins,
                p.IsSlot ? "Unlock" : "Buy",
                canAfford);
        }

        public static TransactionScreen Success(DronePurchase p) => new TransactionScreen(
            p.IsSlot ? "Fleet slot unlocked" : $"{p.DisplayName} joined your fleet!",
            p.IsSlot ? $"You now have {p.CurrentSlots + 1} slots." : "Select it on its card to mine with it.",
            "", "", "", false);
    }
}
