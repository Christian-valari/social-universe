namespace SocialUniverse.Social
{
    // Resolves the display name baked into the Vivox login session (visible to
    // every other player as the chat sender name and presence nameplate).
    // Prefers displayName, then username, then the "Player" placeholder — never
    // a player id, which must not leak into chat. Strips the "#1234" suffix UGS
    // appends to player names so it never reaches other players' screens.
    public static class ChatDisplayNameResolver
    {
        public const string Fallback = "Player";

        public static string Resolve(string displayName, string username)
        {
            return Sanitize(displayName) ?? Sanitize(username) ?? Fallback;
        }

        private static string Sanitize(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;

            int hash = name.IndexOf('#');
            if (hash >= 0) name = name.Substring(0, hash);

            name = name.Trim();
            return name.Length == 0 ? null : name;
        }
    }
}
