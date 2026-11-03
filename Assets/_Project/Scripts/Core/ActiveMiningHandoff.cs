using SocialUniverse.Config;

namespace SocialUniverse.Core
{
    // Carries an active-mining session's reward data across the Planet -> ActiveMining ->
    // Planet scene swap. MiningController/IEconomyService/AsteroidSpawner all live inside
    // PlanetSceneScope and are destroyed the moment Planet unloads, so this Root-level
    // singleton (registered in ProjectLifetimeScope) is the only thing that survives the
    // round trip. Deliberately holds no reference to Asteroid/MiningReward (Mining-layer
    // types) — Core must never depend on Mining — callers pass already-unpacked values.
    // FullQuantity is pinned when the run begins: Planet's reload re-rolls asteroid yields, so
    // the payout is the banked share of this amount, never recomputed from the reloaded asteroid.
    public class ActiveMiningHandoff
    {
        public string                  PlanetId       { get; private set; }
        public string                  AsteroidSlotId { get; private set; }
        public AsteroidDefinition      Definition     { get; private set; }
        public int                     FullQuantity   { get; private set; }
        public ActiveMiningRunSettings Settings       { get; private set; }

        public bool  HasResult      { get; private set; }
        public bool  Succeeded      { get; private set; }
        public float BankedFraction { get; private set; }

        public int BankedQuantity => ActiveMiningPayout.Quantity(FullQuantity, BankedFraction);

        // The mineral this run pays out: the definition's mineral, else the asteroid's type label.
        public string MineralId =>
            Definition == null ? null
            : Definition.Mineral != null ? Definition.Mineral.MineralId
            : Definition.MineralType;

        public void Begin(string planetId, string asteroidSlotId, AsteroidDefinition definition,
            int fullQuantity, ActiveMiningRunSettings settings)
        {
            PlanetId       = planetId;
            AsteroidSlotId = asteroidSlotId;
            Definition     = definition;
            FullQuantity   = fullQuantity;
            Settings       = settings;
            HasResult      = false;
            Succeeded      = false;
            BankedFraction = 0f;
        }

        public void SetResult(bool succeeded, float bankedFraction)
        {
            HasResult      = true;
            Succeeded      = succeeded;
            BankedFraction = bankedFraction;
        }

        public void Clear()
        {
            PlanetId       = null;
            AsteroidSlotId = null;
            Definition     = null;
            FullQuantity   = 0;
            Settings       = null;
            HasResult      = false;
            BankedFraction = 0f;
        }
    }
}
