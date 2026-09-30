using SocialUniverse.App;
using SocialUniverse.Config;
using SocialUniverse.Core;
using SocialUniverse.Net;
using SocialUniverse.Safety;
using SocialUniverse.Social;
using UnityEngine;
using VContainer;

namespace SocialUniverse.Tests
{
    // Stand-in for the Bootstrap scene's RootLifetimeScope in PlayMode tests (Known Issue #7).
    // Planet.unity's PlanetSceneScope names RootLifetimeScope as its parent type and waits until
    // an instance exists, so loading the scene on its own never builds its container. Subclassing
    // RootLifetimeScope lets VContainer find this as that parent.
    //
    // Configure deliberately does NOT call base: that would register Bootstrapper (which starts
    // the app FSM and loads the Auth scene) and the real UGS services. Instead it registers what
    // the Planet scene resolves from its parent, using the dev-mode mocks plus a scripted backend.
    public class PlanetTestRootScope : RootLifetimeScope
    {
        // Set before AddComponent: the scope builds its container inside Awake.
        public static FakeBackendClient Backend;

        protected override void Configure(IContainerBuilder builder)
        {
            // From ProjectLifetimeScope — only what the Planet scene and its handlers need.
            builder.RegisterInstance(ScriptableObject.CreateInstance<AppConfig>());
            builder.Register<SceneLoader>(Lifetime.Singleton);
            builder.Register<GameStateMachine>(Lifetime.Singleton);
            builder.Register<PlanetState>(Lifetime.Singleton);
            builder.Register<ActiveMiningHandoff>(Lifetime.Singleton);
            builder.Register<LandBuildingHandoff>(Lifetime.Singleton);

            // From RootLifetimeScope's dev-mode branch, with the scripted backend.
            builder.Register<MockNetworkBootstrap>(Lifetime.Singleton).AsImplementedInterfaces();
            builder.Register<LocalMockAuthService>(Lifetime.Singleton).As<IAuthService>();
            builder.RegisterInstance<IBackendClient>(Backend ?? new FakeBackendClient());
            builder.Register<LocalMockCloudSave>(Lifetime.Singleton).As<ICloudSave>();
            builder.Register<LocalMockChatService>(Lifetime.Singleton).As<IChatService>();
            builder.Register<LocalMockFriendsService>(Lifetime.Singleton).As<IFriendsService>();
            builder.Register<LocalMockPresenceService>(Lifetime.Singleton).As<IPresenceService>();
            builder.Register<ServerTime>(Lifetime.Singleton);

            builder.RegisterInstance(ScriptableObject.CreateInstance<SocialConfig>());
            builder.Register<ChatModerationFilter>(Lifetime.Singleton);
            builder.Register<ReportService>(Lifetime.Singleton);
            builder.Register<ChatChannelController>(Lifetime.Singleton);
            builder.Register<DirectMessageService>(Lifetime.Singleton);
            builder.Register<ProfileService>(Lifetime.Singleton);

            builder.RegisterInstance(ScriptableObject.CreateInstance<AudioConfig>());
            builder.Register<AudioSettingsService>(Lifetime.Singleton).As<IAudioSettingsService>();
            builder.RegisterInstance(ScriptableObject.CreateInstance<AudioCatalog>());
            builder.Register<AudioManager>(Lifetime.Singleton).As<IAudioManager>();
        }
    }
}
