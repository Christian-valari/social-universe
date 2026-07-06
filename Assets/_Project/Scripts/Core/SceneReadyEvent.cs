namespace SocialUniverse.Core
{
    // Published once a gameplay scene's async hydration/session setup is complete —
    // by PlanetSceneBootstrapper for Planet, SolarSystemBootstrapper for the Hub,
    // ActiveMiningSceneBootstrapper for ActiveMining.
    // LoadingScreenView is the sole subscriber: it fills to 100% and unloads itself.
    public readonly struct SceneReadyEvent { }
}
