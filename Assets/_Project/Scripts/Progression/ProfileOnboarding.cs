namespace SocialUniverse.Progression
{
    // Pure decision for PlanetSceneScope: does this account still need to choose an
    // in-game name? True when no real name exists on the profile or the auth session
    // (a fresh Google/SSO account, whose UGS PlayerName is null). The sanitize rules
    // mirror SocialUniverse.Social.ChatDisplayNameResolver (trim, strip the UGS
    // "#1234" suffix, ignore whitespace-only) but are kept local so Progression need
    // not reference SocialUniverse.Social.
    public static class ProfileOnboarding
    {
        public static bool NeedsOnboarding(string profileDisplayName, string authDisplayName, string authUsername)
            => !HasRealName(profileDisplayName)
            && !HasRealName(authDisplayName)
            && !HasRealName(authUsername);

        private static bool HasRealName(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;

            int hash = name.IndexOf('#');
            if (hash >= 0) name = name.Substring(0, hash);

            return name.Trim().Length > 0;
        }
    }
}
