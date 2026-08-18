namespace SocialUniverse.Mining
{
    // Published when a mining session is refused because the active drone's tier is below
    // the asteroid's tier. The HUD surfaces "Requires a Tier N drone."
    public class MiningBlockedEvent
    {
        public Asteroid Asteroid;
        public int      RequiredTier;
    }
}
