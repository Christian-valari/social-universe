namespace SocialUniverse.Social
{
    // A player's public profile as returned by the "GetPlayerProfile" Cloud
    // Code function (field names match its lowercase JSON keys — the backend
    // deserializer is case-insensitive, same as the Economy result DTOs).
    // Public top-level class so tests can construct it for a fake
    // IBackendClient.
    public class PlayerProfile
    {
        public string   PlayerId;
        public string   DisplayName;
        public string   AvatarId;
        public int      Level;
        public int      Xp;
        public string[] Badges;
        public int      TilesOwned;
        public bool     EmailVerified;
    }
}
