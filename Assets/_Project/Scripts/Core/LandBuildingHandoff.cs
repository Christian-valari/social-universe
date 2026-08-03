namespace SocialUniverse.Core
{
    // Carries a tile's plot layout across the Planet -> LandBuilding -> Planet scene swap.
    // LandRegistryService/Wallet live in PlanetSceneScope and are destroyed the moment Planet
    // unloads, so this Root-level singleton (registered in ProjectLifetimeScope) is the only
    // thing that survives the round trip — same pattern as ActiveMiningHandoff. Holds only
    // primitives/strings; Core must never depend on Economy/World types.
    public class LandBuildingHandoff
    {
        public string   TileId   { get; private set; }
        public string   PlanetId { get; private set; }
        public string   OwnerId  { get; private set; }
        public bool     CanEdit  { get; private set; }
        public int      Coins    { get; private set; }
        public string[] Slots    { get; private set; }

        public void Begin(string tileId, string planetId, string ownerId, bool canEdit, string[] slots, int coins)
        {
            TileId   = tileId;
            PlanetId = planetId;
            OwnerId  = ownerId;
            CanEdit  = canEdit;
            Slots    = slots;
            Coins    = coins;
        }

        public void Clear()
        {
            TileId   = null;
            PlanetId = null;
            OwnerId  = null;
            Slots    = null;
        }
    }
}
