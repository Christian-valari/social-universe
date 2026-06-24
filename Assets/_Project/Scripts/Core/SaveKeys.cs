namespace SocialUniverse.Core
{
    public static class SaveKeys
    {
        public const string PlayerProfile = "player_profile";
        public const string Wallet        = "wallet";
        public const string FuelState     = "fuel_state";
        public const string TravelState   = "travel_state";

        // Client-cached resume hint (like LastPlanetId) — presence means "was traveling
        // last we heard"; the Travel scene re-validates against the server on entry,
        // since this is not itself the source of truth.
        public const string TravelTargetId = "travel_target_planet_id";
        public const string QuestProgress = "quest_progress";
        public const string DailyStreak   = "daily_streak";
        public const string Inventory     = "inventory";
        public const string LastSessionEnd = "last_session_end";
        public const string LastPlanetId   = "last_planet_id";
        public const string AsteroidRespawns = "asteroid_respawns";
        public const string AuthSession    = "auth_session_player_id";

        // Returns the Cloud Save key for a planet's owned-tile list.
        public static string OwnedTilesKey(string planetId) => $"owned_tiles_{planetId.ToLowerInvariant()}";
    }
}
