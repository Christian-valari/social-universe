namespace SocialUniverse.Core
{
    public static class SaveKeys
    {
        public const string PlayerProfile = "player_profile";
        public const string Wallet        = "wallet";
        public const string FuelState     = "fuel_state";
        public const string TravelState   = "travel_state";

        // Client-cached resume hint (like LastPlanetIdKey) — presence means "was traveling
        // last we heard"; the Travel scene re-validates against the server on entry,
        // since this is not itself the source of truth.
        public const string TravelTargetId = "travel_target_planet_id";
        public const string QuestProgress = "quest_progress";
        public const string DailyStreak   = "daily_streak";
        public const string Inventory     = "inventory";
        public const string IdleMiningSession = "idle_mining_session";
        public const string AsteroidRespawns = "asteroid_respawns";
        public const string AuthSession    = "auth_session_player_id";
        public const string MusicVolume    = "settings_music_volume";
        public const string SfxVolume      = "settings_sfx_volume";

        // Returns the Cloud Save key for a planet's owned-tile list.
        public static string OwnedTilesKey(string planetId) => $"owned_tiles_{planetId.ToLowerInvariant()}";

        // Local-only (PlayerPrefs) resume hint: which planet to land on at next launch.
        // Keyed per-player like EmailVerificationPromptedKey — PlayerPrefs is a single
        // device-global store, so a bare key would leak the last-visited planet from
        // whichever account last wrote it to the next account that signs in on the same
        // device (the account-switch bug this key format fixes).
        public static string LastPlanetIdKey(string playerId) => $"last_planet_id_{playerId}";

        // Local-only (PlayerPrefs) flag: has this player already been shown the
        // one-time email-verification prompt on this device? Deliberately not
        // server-side — see PlanetSceneScope.HydrateServerStateAsync.
        public static string EmailVerificationPromptedKey(string playerId) => $"email_verification_prompted_{playerId}";
    }
}
