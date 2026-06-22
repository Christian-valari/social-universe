using SocialUniverse.Config;

namespace SocialUniverse.Travel
{
    // Published by StarMapController/SkyDiscoveryController when the player
    // confirms a trip. TravelController (App layer) is the sole subscriber —
    // it owns the fuel spend + scene transition + rocket animation sequencing.
    public class TravelRequestedEvent
    {
        public PlanetDefinition Planet;
    }
}
