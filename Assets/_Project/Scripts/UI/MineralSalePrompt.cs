namespace SocialUniverse.UI
{
    // Wording and button state for the Mineral Inventory's sale popup (TransactionModal), kept pure
    // so it can be unit-tested. The coin figures are estimates; the server decides the payout.
    public static class MineralSalePrompt
    {
        public const string Busy = "Selling…";

        public static TransactionScreen Confirm(MineralSale s) => new TransactionScreen(
            s.IsAll ? "Sell all minerals?" : $"Sell {s.Qty} {s.DisplayName}?",
            s.IsAll ? Units(s.Qty) : $"{s.UnitValue} coins each",
            $"You'll get {s.Value} coins",
            "",
            s.IsAll ? "Sell all" : "Sell",
            s.Qty > 0);

        public static TransactionScreen Success(MineralSale s) => new TransactionScreen(
            s.IsAll ? $"Sold {Units(s.Qty)}" : $"Sold {s.Qty} {s.DisplayName}",
            $"+{s.Value} coins",
            "", "", "", false);

        private static string Units(int qty) => $"{qty} mineral{(qty == 1 ? "" : "s")}";
    }
}
