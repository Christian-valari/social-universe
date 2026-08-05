namespace SocialUniverse.Core
{
    // Carries a tile's plot layout across the Planet -> LandBuilding -> Planet scene swap.
    // LandRegistryService/Wallet live in PlanetSceneScope and are destroyed the moment Planet
    // unloads, so this Root-level singleton (registered in ProjectLifetimeScope) is the only
    // thing that survives the round trip — same pattern as ActiveMiningHandoff. Holds only
    // primitives/strings; Core must never depend on Economy/World types.
    public class LandBuildingHandoff
    {
        public string   TileId           { get; private set; }
        // Navigation id (PlanetDefinition._planetId, e.g. "earth") — used by LandBuildingState.Finish
        // to return to the origin planet via PlanetState.TargetPlanetId (DatabaseRegistry.GetPlanet
        // keys on _planetId).
        public string   PlanetId         { get; private set; }
        // Land-registry key (PlanetDefinition.name, e.g. "Planet_Earth"). This — NOT PlanetId — is the
        // "planetId" every land Cloud Code function uses (PurchaseLand/GetLandRegistry/PlaceBuild/...);
        // the registry is keyed by planet.name.toLowerCase(). The two ids differ (see PlanetDefinition),
        // so build server calls must send this or PlaceBuild reads the wrong registry -> NOT_OWNER.
        public string   RegistryPlanetId { get; private set; }
        public string   OwnerId          { get; private set; }
        public bool     CanEdit          { get; private set; }
        public int      Coins            { get; private set; }
        public string[] Slots            { get; private set; }

        public void Begin(string tileId, string planetId, string registryPlanetId, string ownerId, bool canEdit, string[] slots, int coins)
        {
            TileId           = tileId;
            PlanetId         = planetId;
            RegistryPlanetId = registryPlanetId;
            OwnerId          = ownerId;
            CanEdit          = canEdit;
            Slots            = slots;
            Coins            = coins;
        }

        public void Clear()
        {
            TileId           = null;
            PlanetId         = null;
            RegistryPlanetId = null;
            OwnerId          = null;
            Slots            = null;
        }
    }
}
