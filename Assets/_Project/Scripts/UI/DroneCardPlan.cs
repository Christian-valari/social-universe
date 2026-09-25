using System.Collections.Generic;

namespace SocialUniverse.UI
{
    // One card in the Drone Garage carousel, identified by what it shows rather than by position.
    public readonly struct DroneCardKey
    {
        public readonly string DroneId;
        public readonly bool   Owned;

        public DroneCardKey(string droneId, bool owned)
        {
            DroneId = droneId;
            Owned   = owned;
        }
    }

    // Works out which cards the garage carousel holds, and whether that set has changed.
    //
    // Panels may only be added or removed when the set actually changes: SimpleScrollSnap re-centres
    // on its starting panel inside every Setup(), and Setup() runs on each Add/Remove — which is what
    // threw the player back to the first drone after selecting one. A plain state change (selected
    // drone, upgrade level, affordability) re-binds the existing cards instead.
    public static class DroneCardPlan
    {
        // Owned drones first, in fleet order, then everything else in registry order.
        public static List<DroneCardKey> Build(IReadOnlyList<string> ownedDroneIds, IReadOnlyList<string> allDroneIds)
        {
            var cards = new List<DroneCardKey>(allDroneIds?.Count ?? 0);
            if (allDroneIds == null) return cards;

            if (ownedDroneIds != null)
                foreach (var id in ownedDroneIds)
                    if (Contains(allDroneIds, id)) // an id the registry no longer knows has no card
                        cards.Add(new DroneCardKey(id, true));

            foreach (var id in allDroneIds)
                if (!Contains(ownedDroneIds, id))
                    cards.Add(new DroneCardKey(id, false));

            return cards;
        }

        public static bool SameCards(IReadOnlyList<DroneCardKey> a, IReadOnlyList<DroneCardKey> b)
        {
            if (a == null || b == null) return ReferenceEquals(a, b);
            if (a.Count != b.Count)     return false;

            for (int i = 0; i < a.Count; i++)
                if (a[i].Owned != b[i].Owned || a[i].DroneId != b[i].DroneId) return false;

            return true;
        }

        public static int IndexOf(IReadOnlyList<DroneCardKey> cards, string droneId)
        {
            if (cards == null || droneId == null) return -1;

            for (int i = 0; i < cards.Count; i++)
                if (cards[i].DroneId == droneId) return i;

            return -1;
        }

        private static bool Contains(IReadOnlyList<string> ids, string id)
        {
            if (ids == null) return false;

            for (int i = 0; i < ids.Count; i++)
                if (ids[i] == id) return true;

            return false;
        }
    }
}
