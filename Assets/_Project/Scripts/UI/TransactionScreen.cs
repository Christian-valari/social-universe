namespace SocialUniverse.UI
{
    // One screen of the shared transaction popup (TransactionModal): the text to show and whether
    // Confirm can be pressed. Built by a panel's prompt helper (DronePurchasePrompt, MineralSalePrompt).
    // Amount is the money line: a price for a purchase, the proceeds for a sale.
    public readonly struct TransactionScreen
    {
        public readonly string Title, Body, Amount, Status, ConfirmLabel;
        public readonly bool   ConfirmEnabled;

        public TransactionScreen(string title, string body, string amount, string status, string confirmLabel, bool confirmEnabled)
        {
            Title = title; Body = body; Amount = amount; Status = status; ConfirmLabel = confirmLabel; ConfirmEnabled = confirmEnabled;
        }
    }
}
