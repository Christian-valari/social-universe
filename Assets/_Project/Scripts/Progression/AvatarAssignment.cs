using System;
using System.Collections.Generic;

namespace SocialUniverse.Progression
{
    // Pure resolution logic for PlanetSceneScope.HydrateServerStateAsync's
    // avatar-assignment fallback — kept free of DatabaseRegistry/ProfileService
    // so it's unit-testable without Unity or network dependencies.
    public static class AvatarAssignment
    {
        public static string ResolveAvatarId(string existingAvatarId, IReadOnlyList<string> catalogAvatarIds, Func<int, int> randomIndex)
        {
            if (!string.IsNullOrEmpty(existingAvatarId))
                return existingAvatarId;

            if (catalogAvatarIds == null || catalogAvatarIds.Count == 0)
                return null;

            int index = randomIndex(catalogAvatarIds.Count);
            return catalogAvatarIds[index];
        }
    }
}
