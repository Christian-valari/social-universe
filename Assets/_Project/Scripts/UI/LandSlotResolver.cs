using SocialUniverse.Config;
using SocialUniverse.Core;

namespace SocialUniverse.UI
{
    // Pure helpers for LandBuildingController — kept out of the MonoBehaviour so they're unit-testable.
    public static class LandSlotResolver
    {
        public static ItemDefinition Resolve(string itemId, DatabaseRegistry registry)
        {
            if (string.IsNullOrEmpty(itemId) || registry == null) return null;
            return registry.GetItem(itemId);
        }

        public static bool CanEdit(LandBuildingHandoff handoff) => handoff != null && handoff.CanEdit;
    }
}
