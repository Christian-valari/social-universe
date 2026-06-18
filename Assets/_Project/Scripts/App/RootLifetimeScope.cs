using SocialUniverse.Config;
using SocialUniverse.Core;
using SocialUniverse.Net;
using SocialUniverse.Social;
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
        [SerializeField] private SocialConfig _socialConfig;

        protected override void Configure(IContainerBuilder builder)
        {
            base.Configure(builder);

            if (_devMode)
            {
                builder.Register<MockNetworkBootstrap>(Lifetime.Singleton).AsImplementedInterfaces();
                builder.Register<LocalMockAuthService>(Lifetime.Singleton).As<IAuthService>();
                builder.Register<LocalMockBackendClient>(Lifetime.Singleton).As<IBackendClient>();
                builder.Register<LocalMockCloudSave>(Lifetime.Singleton).As<ICloudSave>();

                builder.Register<LocalMockChatService>(Lifetime.Singleton).As<IChatService>();
                builder.Register<LocalMockFriendsService>(Lifetime.Singleton).As<IFriendsService>();
            }
            else
            {
                builder.Register<NetworkBootstrap>(Lifetime.Singleton).AsImplementedInterfaces();
                builder.Register<AuthService>(Lifetime.Singleton).As<IAuthService>();
                builder.Register<BackendClient>(Lifetime.Singleton).As<IBackendClient>();
                builder.Register<CloudSaveService>(Lifetime.Singleton).As<ICloudSave>();

                builder.Register<ChatService>(Lifetime.Singleton).As<IChatService>();
                builder.Register<FriendsService>(Lifetime.Singleton).As<IFriendsService>();
            }

            builder.Register<ServerTime>(Lifetime.Singleton);

            // M4 social layer (app-wide: chat, friends, DMs, profiles span scenes).
            builder.RegisterInstance(_socialConfig);
            builder.Register<ChatModerationFilter>(Lifetime.Singleton);
            builder.Register<ReportService>(Lifetime.Singleton);
            builder.Register<ChatChannelController>(Lifetime.Singleton);
            builder.Register<DirectMessageService>(Lifetime.Singleton);
            builder.Register<ProfileService>(Lifetime.Singleton);

            if (_devMode)
                builder.Register<LocalMockPresenceService>(Lifetime.Singleton).As<IPresenceService>();
            else
                builder.Register<VivoxPresenceService>(Lifetime.Singleton).As<IPresenceService>();

            builder.RegisterEntryPoint<SocialServicesInitializer>();

            // Dev-only: optional, present only if a CloudCodeTestHarness is in the scene hierarchy.
            builder.RegisterComponentInHierarchy<CloudCodeTestHarness>();
        }
    }
}
