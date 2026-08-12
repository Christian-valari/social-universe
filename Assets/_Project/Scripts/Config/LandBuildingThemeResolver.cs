namespace SocialUniverse.Config
{
    // Single source of truth for picking the active LandBuilding theme. Used by both consumers
    // (PlotHexBoard for hex materials, LandBuildingThemeApplier for sky + ambient). Explicit
    // != null checks throughout — these are UnityEngine.Objects, so ?? / ?. must not be used.
    public static class LandBuildingThemeResolver
    {
        public static LandBuildingThemeDefinition Resolve(
            DatabaseRegistry registry, string planetId, LandBuildingThemeDefinition fallback)
        {
            if (registry != null && !string.IsNullOrEmpty(planetId))
            {
                var planet = registry.GetPlanet(planetId);
                if (planet != null && planet.LandBuildingTheme != null)
                    return planet.LandBuildingTheme;
            }
            return fallback;
        }
    }
}
