using SocialUniverse.Config;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace SocialUniverse.Core
{
    // Base project scope — registers Core services only.
    // In scene use RootLifetimeScope (App assembly) which extends this and wires in Net.
    public class ProjectLifetimeScope : LifetimeScope
    {
        [SerializeField] private AppConfig _appConfig;

        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterInstance(_appConfig);

            builder.Register<SceneLoader>(Lifetime.Singleton);
            builder.Register<GameStateMachine>(Lifetime.Singleton);
            builder.Register<GameManager>(Lifetime.Singleton);

            builder.Register<BootState>(Lifetime.Singleton);
            builder.Register<AuthState>(Lifetime.Singleton);
            builder.Register<HubState>(Lifetime.Singleton);
            builder.Register<TravelState>(Lifetime.Singleton);
            builder.Register<TravelLoadingState>(Lifetime.Singleton);
            builder.Register<PlanetState>(Lifetime.Singleton);
            builder.Register<ActiveMiningState>(Lifetime.Singleton);
            builder.Register<LandBuildingState>(Lifetime.Singleton);
            builder.Register<LogoutState>(Lifetime.Singleton);

            builder.Register<ActiveMiningHandoff>(Lifetime.Singleton);
            builder.Register<LandBuildingHandoff>(Lifetime.Singleton);

            builder.RegisterEntryPoint<Bootstrapper>();
        }
    }
}
