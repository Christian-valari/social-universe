using UnityEngine;
using VContainer;
using VContainer.Unity;
using SocialUniverse.Config;
using SocialUniverse.Core;
using SocialUniverse.Economy;
using SocialUniverse.Net;

namespace SocialUniverse.App
{
    // Root LifetimeScope for the LandBuilding scene.
    // DatabaseRegistry/EconomyConfig are provided by THIS scope unconditionally (production and
    // standalone alike) — no ancestor scope registers them, they are only ever registered by leaf
    // scene scopes, same as PlanetSceneScope. Production mode: set Parent = RootLifetimeScope so
    // IBackendClient/LandBuildingHandoff come from the parent. Standalone mode (opening
    // LandBuilding.unity directly) additionally registers a mock backend + empty handoff so it
    // doesn't crash.
    public class LandBuildingSceneScope : LifetimeScope
    {
        [SerializeField] private DatabaseRegistry _databaseRegistry;
        [SerializeField] private EconomyConfig    _economyConfig;

        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterInstance(_databaseRegistry);
            builder.RegisterInstance(_economyConfig);

            bool standalone = parentReference.Type == null;
            if (standalone)
            {
                builder.Register<LandBuildingHandoff>(Lifetime.Singleton);
                builder.Register<NetworkBootstrap>(Lifetime.Singleton).AsImplementedInterfaces();
                builder.Register<LocalMockAuthService>(Lifetime.Singleton).As<IAuthService>();
                builder.Register<BackendClient>(Lifetime.Singleton).As<IBackendClient>();
            }

            builder.Register<LandBuildService>(Lifetime.Singleton);
            builder.Register<BuildPaletteService>(Lifetime.Singleton);

            builder.RegisterComponentInHierarchy<SocialUniverse.UI.LandBuildingController>();
            builder.RegisterComponentInHierarchy<SocialUniverse.UI.LandBuildPaletteView>();

            builder.RegisterEntryPoint<LandBuildingSceneBootstrapper>();
        }
    }

    // Publishes SceneReadyEvent once the LandBuilding scene's container is up, so LoadingScreenView
    // unloads itself. The plot renders synchronously from LandBuildingHandoff (LandBuildingController),
    // so there is no async hydration to await — unlike ActiveMiningSceneBootstrapper, this scene has
    // nothing else to set up. Without this, the LoadingScreen (its sole subscriber) never unloads and
    // the transition into LandBuilding hangs on the loading screen — same contract every other
    // gameplay scene scope (Planet/SolarSystem/Travel/ActiveMining) already honors.
    public class LandBuildingSceneBootstrapper : IStartable
    {
        public void Start() => EventBus.Publish(new SceneReadyEvent());
    }
}
