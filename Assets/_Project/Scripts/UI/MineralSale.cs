using UnityEngine;

namespace SocialUniverse.UI
{
    // What the Mineral Inventory is about to sell: every unit of one mineral (a row's Sell) or
    // everything held (Sell all). Values are the client's SellValue estimate; the server pays.
    public readonly struct MineralSale
    {
        public readonly string MineralId;    // null for Sell all
        public readonly string DisplayName;
        public readonly Sprite Icon;
        public readonly int    Qty;          // units; for Sell all, every unit held
        public readonly int    UnitValue;    // coins each; 0 for Sell all
        public readonly int    Value;        // coins expected in total

        public bool IsAll => MineralId == null;

        private MineralSale(string mineralId, string displayName, Sprite icon, int qty, int unitValue, int value)
        {
            MineralId = mineralId; DisplayName = displayName; Icon = icon; Qty = qty; UnitValue = unitValue; Value = value;
        }

        public static MineralSale ForMineral(string mineralId, string displayName, Sprite icon, int qty, int unitValue) =>
            new MineralSale(mineralId, displayName, icon, qty, unitValue, qty * unitValue);

        public static MineralSale ForAll(int qty, int value) =>
            new MineralSale(null, null, null, qty, 0, value);
    }
}
