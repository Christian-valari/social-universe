namespace SocialUniverse.Core
{
    // Response shape for the "GetCurrentPlanet" Cloud Code function. Public so
    // tests can construct it for a fake IBackendClient.
    public class CurrentPlanetResult
    {
        public string PlanetId;
        public string TravelTargetId; // target of a trip not landed yet (any device); null if none
    }
}
