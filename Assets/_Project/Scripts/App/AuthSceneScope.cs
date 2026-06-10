using SocialUniverse.Core;
using SocialUniverse.Net;
using SocialUniverse.UI;
using VContainer;
using VContainer.Unity;

namespace SocialUniverse.App
{
    // Scene scope for the Auth scene.
    //
    // Standalone (no parent): registers LocalMockAuthService so the scene can run in isolation.
    // Production (parent = RootLifetimeScope): inherits AuthService from the parent container;
    // only registers AuthScreen so VContainer can inject it.
    public class AuthSceneScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            if (parentReference.Type == null)
                builder.Register<IAuthService, LocalMockAuthService>(Lifetime.Singleton);

            builder.RegisterComponentInHierarchy<AuthScreen>();
        }
    }
}
