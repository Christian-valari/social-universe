using UnityEngine;

namespace SocialUniverse.Core
{
    // What an active run pays: the share of the asteroid's full quantity that its hits banked.
    // Any banked share pays at least one unit, so a small asteroid is never worth nothing for a hit,
    // and nothing pays above the full quantity (the server caps a claim at that amount).
    public static class ActiveMiningPayout
    {
        public static int Quantity(int fullQuantity, float bankedFraction)
        {
            if (fullQuantity <= 0 || bankedFraction <= 0f) return 0;

            return Mathf.Clamp(Mathf.RoundToInt(fullQuantity * bankedFraction), 1, fullQuantity);
        }
    }
}
