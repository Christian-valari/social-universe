namespace SocialUniverse.Mining
{
    // UI -> App intent: sell a specific mineral qty, or all minerals when All == true.
    public class SellMineralsRequestedEvent
    {
        public string MineralId;
        public int    Qty;
        public bool   All;
    }
}
