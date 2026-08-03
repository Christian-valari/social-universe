namespace SocialUniverse.Core
{
    // Published by TileInfoModal's "View Land" button. Indirected through the event bus
    // (rather than the modal calling PlanetState directly) so Planet's standalone/no-Bootstrap
    // dev mode — which never registers PlanetState — doesn't break; same reasoning as
    // ActiveMiningRequestedEvent. Carries the layout snapshot because the handler (App scope)
    // cannot resolve the Planet-scoped LandRegistryService.
    public class ViewLandRequestedEvent
    {
        public string   TileId;
        public string   OwnerId;
        public bool     CanEdit;
        public string[] Slots;
        public int      Coins;
    }
}
