using SocialUniverse.Config;

namespace SocialUniverse.Core
{
    // Published by TravelLoadingState once the TravelLoading scene has finished
    // loading, for the Hub -> Travel leg of a trip. Defined in Core (rather than
    // the Travel module, alongside TravelConfirmedEvent) so this FSM state can
    // signal the scene without Core depending on Travel — TravelLoadingController
    // (Travel module) is the sole subscriber.
    // Planet shown is the player's CURRENT planet (you take off from where you
    // are, not the destination).
    public class TravelLoadingTakeOffRequestedEvent
    {
        public PlanetDefinition Planet;
    }

    // Published by TravelLoadingState for the Travel -> Planet (landing) leg.
    // Planet shown is the trip's destination (you land on where you're arriving).
    public class TravelLoadingLandRequestedEvent
    {
        public PlanetDefinition Planet;
    }
}
