using SocialUniverse.Core;
using SocialUniverse.Net;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace SocialUniverse.App
{
    // Bootstrap scene scope. Extends ProjectLifetimeScope to add Net services.
    //
    // Enable _devMode in the Inspector to run without a live UGS project:
    // mocks replace all network/backend services so the full scene-change flow
    // works locally (Bootstrap → Auth → Planet).
    public class RootLifetimeScope : ProjectLifetimeScope
    {
        [SerializeField] private bool _devMode = false;

        protected override void Configure(IContainerBuilder builder)
        {
            base.Configure(builder);

            if (_devMode)
            {
                builder.Register<MockNetworkBootstrap>(Lifetime.Singleton).AsImplementedInterfaces();
                builder.Register<LocalMockAuthService>(Lifetime.Singleton).As<IAuthService>();
                builder.Register<LocalMockBackendClient>(Lifetime.Singleton).As<IBackendClient>();
                builder.Register<LocalMockCloudSave>(Lifetime.Singleton).As<ICloudSave>();
            }
            else
            {
                builder.Register<NetworkBootstrap>(Lifetime.Singleton).AsImplementedInterfaces();
                builder.Register<AuthService>(Lifetime.Singleton).As<IAuthService>();
                builder.Register<BackendClient>(Lifetime.Singleton).As<IBackendClient>();
                builder.Register<CloudSaveService>(Lifetime.Singleton).As<ICloudSave>();
            }

            builder.Register<ServerTime>(Lifetime.Singleton);
            builder.Register<ConnectionManager>(Lifetime.Singleton);

            // Dev-only: optional, present only if a CloudCodeTestHarness is in the scene hierarchy.
            builder.RegisterComponentInHierarchy<CloudCodeTestHarness>();
        }
    }
}
