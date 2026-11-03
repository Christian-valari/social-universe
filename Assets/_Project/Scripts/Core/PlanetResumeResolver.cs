namespace SocialUniverse.Core
{
    // Resolves where to resume at launch/sign-in: server record wins
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

        // The trip to resume into, or null to resume on a planet. Once the server has answered, its
        // answer is final: a trip started on another device resumes here too, and a local hint left
        // by a trip landed elsewhere is stale. The local hint (SaveKeys.TravelTargetId) only stands
        // in when the server can't be reached.
        public static string ResolveTravelTarget(bool serverAnswered, string serverTravelTargetId, string localTravelTargetId)
        {
            string target = serverAnswered ? serverTravelTargetId : localTravelTargetId;
            return string.IsNullOrEmpty(target) ? null : target;
        }
    }
}
