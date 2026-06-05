using SocialUniverse.Config;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace SocialUniverse.Core
{
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
            builder.Register<PlanetState>(Lifetime.Singleton);

            builder.RegisterEntryPoint<Bootstrapper>();
        }
    }
}
