namespace SocialUniverse.Core
{
    // Resolves which planet to resume on at launch/sign-in: server record wins
    // (it's the cross-device source of truth), then the local PlayerPrefs
    // resume hint (SaveKeys.LastPlanetIdKey — same device, server unreachable
    // or pre-feature account), then the hard default.
    public static class PlanetResumeResolver
    {
        public static string Resolve(string serverPlanetId, string localPlanetId, string defaultPlanetId)
        {
            if (!string.IsNullOrEmpty(serverPlanetId)) return serverPlanetId;
            if (!string.IsNullOrEmpty(localPlanetId))  return localPlanetId;
            return defaultPlanetId;
        }
    }
}
