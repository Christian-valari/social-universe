namespace SocialUniverse.UI
{
    // What the asteroid tap prompt says about tier: the title, the status line, and whether the
    // mine buttons should be enabled.
    public readonly struct AsteroidTierVerdict
    {
        public readonly string Title;    // "Mine Nickel? · Tier 2"
        public readonly string Status;   // whether the active drone can mine it
        public readonly bool   CanMine;

        public AsteroidTierVerdict(string title, string status, bool canMine)
        {
            Title = title; Status = status; CanMine = canMine;
        }
    }

    // Tells the player an asteroid's tier and whether their active drone can mine it, by the rules
    // MiningController enforces (drone tier >= asteroid tier; one idle session at a time). Kept pure
    // so it can be unit-tested.
    public static class AsteroidTierPrompt
    {
        // Shown as a toast when the player taps the asteroid their drone is already mining.
        public const string MiningThisAsteroid = "Your drone is currently mining this asteroid";

        // droneName is null when there is no active drone. busyMineral is the mineral the drone is
        // idle-mining right now, or about to (null when it is free); busyReadyToClaim is whether that
        // run has finished and is waiting to be claimed. returningMineral is the haul of a claim the
        // server hasn't answered yet. The drone isn't named in the busy messages: the player can
        // switch the active drone mid-session, so the name could be the wrong one.
        public static AsteroidTierVerdict For(string mineral, int asteroidTier, string droneName, int droneTier,
            string busyMineral = null, bool busyReadyToClaim = false, string returningMineral = null)
        {
            string title = $"Mine {mineral}? · Tier {asteroidTier}";

            if (droneName == null)
                return new AsteroidTierVerdict(title, $"Needs a Tier {asteroidTier}+ drone", false);

            // The tier check comes first: freeing the drone would not help, so "busy" would mislead.
            if (droneTier < asteroidTier)
                return new AsteroidTierVerdict(title, $"Needs a Tier {asteroidTier}+ drone (your {droneName} is Tier {droneTier})", false);

            if (busyMineral != null)
                return busyReadyToClaim
                    ? new AsteroidTierVerdict(title, $"Your drone is done — claim your {busyMineral} first", false)
                    : new AsteroidTierVerdict(title, $"Your drone is currently mining {busyMineral}", false);

            if (returningMineral != null)
                return new AsteroidTierVerdict(title, $"Your drone is bringing back your {returningMineral}", false);

            return new AsteroidTierVerdict(title, $"Your {droneName} can mine this", true);
        }
    }
}
