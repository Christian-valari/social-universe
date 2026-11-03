using UnityEngine;

namespace SocialUniverse.UI
{
    // What the garage's buy button is about to purchase: the centered drone, or the next fleet slot
    // (DroneBuyButton decides which). DroneGarageView shows it in the purchase popup (TransactionModal).
    public readonly struct DronePurchase
    {
        public readonly string DroneId;       // null for a slot
        public readonly string DisplayName;
        public readonly int    Tier;
        public readonly Sprite Icon;
        public readonly int    Cost;
        public readonly int    CurrentSlots;  // fleet slots before a slot purchase

        public bool IsSlot => DroneId == null;

        private DronePurchase(string droneId, string displayName, int tier, Sprite icon, int cost, int currentSlots)
        {
            DroneId = droneId; DisplayName = displayName; Tier = tier; Icon = icon; Cost = cost; CurrentSlots = currentSlots;
        }

        public static DronePurchase ForDrone(string droneId, string displayName, int tier, Sprite icon, int cost) =>
            new DronePurchase(droneId, displayName, tier, icon, cost, 0);

        public static DronePurchase ForSlot(int cost, int currentSlots) =>
            new DronePurchase(null, null, 0, null, cost, currentSlots);
    }
}
