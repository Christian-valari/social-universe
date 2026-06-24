using SocialUniverse.Config;

namespace SocialUniverse.Travel
{
    // Published by SkyDiscoveryController's Launch button — opens the planet
    // preview panel. PlanetPreviewPanel is the sole subscriber.
    public class TravelPreviewRequestedEvent
    {
        public PlanetDefinition Planet;
    }

    // Published by PlanetPreviewPanel's own Launch button — this is the actual
    // "commit to the trip" signal. TravelController (App layer) is the sole
    // subscriber — it owns the fuel spend + trip start + rocket departure
    // animation sequencing. The scene/FSM transition no longer happens here;
    // see TravelLandedEvent.
    public class TravelConfirmedEvent
    {
        public PlanetDefinition Planet;
    }

    // Published by TravelingPanel's Land button right before it calls
    // HubState.TravelToPlanet to perform the actual scene transition.
    public class TravelLandedEvent
    {
        public string PlanetId;
    }

    // Published by PlanetPreviewPanel.Close() (Cancel, and also after a
    // confirmed Launch since Close() runs there too — harmless, the scene
    // unloads shortly after). Signals SkyZoomController to blend the camera
    // back out to the dome view and resume manual gyro/drag control.
    public class TravelPreviewClosedEvent
    {
    }
}
