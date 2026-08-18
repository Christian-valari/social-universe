using UnityEngine;
using SocialUniverse.Config;

namespace SocialUniverse.Mining
{
    // Pure upgrade/economy math for drones. NextCost and SlotUnlockCost are DUPLICATED in
    // ServerCode/UpgradeDrone.js and ServerCode/UnlockDroneSlot.js ("must match") — keep in sync.
    public static class DroneUpgradeMath
    {
        // Coin cost to advance a stat track from currentLevel to currentLevel+1.
        public static int NextCost(UpgradeDefinition def, int currentLevel)
        {
            if (def == null) return 0;
            return Mathf.RoundToInt(def.BaseCost * Mathf.Pow(def.CostGrowth, Mathf.Max(0, currentLevel)));
        }

        // Effective stat value at a given upgrade level: base + level * deltaPerLevel.
        public static float EffectiveStat(float baseValue, UpgradeDefinition def, int level)
        {
            if (def == null || level <= 0) return baseValue;
            return baseValue + level * def.DeltaPerLevel;
        }

        // Coin cost to unlock one more fleet slot, scaling from the starting slot count.
        public static int SlotUnlockCost(int baseCost, float growth, int currentSlots, int startSlots)
        {
            int steps = Mathf.Max(0, currentSlots - startSlots);
            return Mathf.RoundToInt(baseCost * Mathf.Pow(growth, steps));
        }
    }
}
