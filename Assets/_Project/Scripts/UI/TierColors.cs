using UnityEngine;

namespace SocialUniverse.UI
{
    // One colour per tier, shared by the drone cards and the mineral rows: a tier-N drone mines
    // tier-N asteroids, so a tier should read the same on both screens.
    public static class TierColors
    {
        private static readonly Color[] Palette =
        {
            new Color(0.60f, 0.66f, 0.75f), // T1 steel
            new Color(0.37f, 0.83f, 0.60f), // T2 green
            new Color(0.31f, 0.66f, 1.00f), // T3 blue
            new Color(0.71f, 0.48f, 1.00f), // T4 violet
            new Color(1.00f, 0.71f, 0.28f), // T5 gold
        };

        // Tiers outside 1–5 take the nearest end of the scale.
        public static Color For(int tier) => Palette[Mathf.Clamp(tier - 1, 0, Palette.Length - 1)];
    }
}
