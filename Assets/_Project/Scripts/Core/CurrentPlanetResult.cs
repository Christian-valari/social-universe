namespace SocialUniverse.Core
{
    // Response shape for the "GetCurrentPlanet" Cloud Code function. Public so
    // tests can construct it for a fake IBackendClient.
    public class CurrentPlanetResult
    {
        public string PlanetId;
    }
}
