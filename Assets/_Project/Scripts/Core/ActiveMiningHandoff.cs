using SocialUniverse.Config;

namespace SocialUniverse.Core
{
    // Carries an active-mining session's reward data across the Planet -> ActiveMining ->
    // Planet scene swap. MiningController/IEconomyService/AsteroidSpawner all live inside
    // PlanetSceneScope and are destroyed the moment Planet unloads, so this Root-level
    // singleton (registered in ProjectLifetimeScope) is the only thing that survives the
    // round trip. Deliberately holds no reference to Asteroid/MiningReward (Mining-layer
    // types) — Core must never depend on Mining — callers pass already-unpacked values.
    public class ActiveMiningHandoff
    {
        public string             PlanetId               { get; private set; }
        public string             AsteroidSlotId         { get; private set; }
        public AsteroidDefinition Definition             { get; private set; }
        public int                RemainingYieldAtStart  { get; private set; }
        public int                TapsRequired           { get; private set; }
        public int                MaxErrors              { get; private set; }
        public float              SessionDurationSeconds { get; private set; }

        public bool HasResult { get; private set; }
        public bool Succeeded { get; private set; }

        public void Begin(string planetId, string asteroidSlotId, AsteroidDefinition definition,
            int remainingYieldAtStart, int tapsRequired, int maxErrors, float sessionDurationSeconds)
        {
            PlanetId               = planetId;
            AsteroidSlotId         = asteroidSlotId;
            Definition             = definition;
            RemainingYieldAtStart  = remainingYieldAtStart;
            TapsRequired           = tapsRequired;
            MaxErrors              = maxErrors;
            SessionDurationSeconds = sessionDurationSeconds;
            HasResult = false;
        }

        public void SetResult(bool succeeded)
        {
            HasResult = true;
            Succeeded = succeeded;
        }

        public void Clear()
        {
            PlanetId       = null;
            AsteroidSlotId = null;
            Definition     = null;
            HasResult      = false;
        }
    }
}
