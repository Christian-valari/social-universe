namespace SocialUniverse.Core
{
    public static class SaveKeys
    {
        public const string PlayerProfile = "player_profile";
        public const string Wallet        = "wallet";
        public const string FuelState     = "fuel_state";
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
