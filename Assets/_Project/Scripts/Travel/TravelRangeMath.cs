using SocialUniverse.Config;

namespace SocialUniverse.Travel
{
    // Pure math for the "only neighboring planets are in travel range" rule —
    // kept separate from TravelService so it's unit-testable without DI.
    // A target is in range if its OrbitOrder is at most one step away from
    // the current planet's (ties, e.g. Earth/Moon sharing an OrbitOrder,
    // count as in range of each other too).
    public static class TravelRangeMath
    {
        public static bool IsInRange(PlanetDefinition current, PlanetDefinition target)
        {
            if (current == null || target == null) return false;
            if (target.PlanetId == current.PlanetId) return false;
            return System.Math.Abs(target.OrbitOrder - current.OrbitOrder) <= 1;
        }
    }
}
