using VContainer;
using VContainer.Unity;
using SocialUniverse.Mining;
using SocialUniverse.Core;

namespace SocialUniverse.App
{
    // Scene scope for the ActiveMining minigame — loaded by SocialUniverse.Core.ActiveMiningState
    // as the sole running gameplay scene (Planet is unloaded first, see ActiveMiningState).
    // Parents to RootLifetimeScope (parentReference.TypeName in the scene file), not to
    // PlanetSceneScope — nothing here needs Planet-scoped services (IEconomyService,
    // AsteroidSpawner, MiningController); everything needed comes from ActiveMiningHandoff, a
    // Root-level singleton that survives the scene swap.
    public class ActiveMiningSceneScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterComponentInHierarchy<ActiveMiningAsteroidStage>();
            builder.RegisterComponentInHierarchy<SocialUniverse.UI.ActiveMiningMinigameView>();

            // Registered as both an entry point (IStartable/ITickable) and directly injectable
            // (AsSelf) — ActiveMiningMinigameView needs to inject the concrete type to read
            // .Session, which RegisterEntryPoint alone doesn't guarantee.
            builder.Register<ActiveMiningSessionRunner>(Lifetime.Singleton).AsSelf().AsImplementedInterfaces();

            builder.RegisterEntryPoint<ActiveMiningSceneBootstrapper>();
        }
    }

    // Spawns the visual asteroid clone from the handoff's AsteroidDefinition as soon as this
    // scene finishes loading — the handoff was already populated back in Planet, before the
    // scene swap, by MiningController.BeginActiveMining.
    public class ActiveMiningSceneBootstrapper : IStartable
    {
        private readonly ActiveMiningHandoff       _handoff;
        private readonly ActiveMiningAsteroidStage _stage;

        public ActiveMiningSceneBootstrapper(ActiveMiningHandoff handoff, ActiveMiningAsteroidStage stage)
        {
            _handoff = handoff;
            _stage   = stage;
        }

        public void Start()
        {
            _stage.SpawnClone(_handoff.Definition);

            // LoadingScreenView (see LoadingScreenView.cs) only unloads itself in response to
            // this event — every other scene bootstrapper shown behind a loading screen
            // (PlanetSceneBootstrapper, TravelSceneBootstrapper) already publishes it once its
            // own setup completes; this one needs to as well or LoadingScreen never unloads.
            EventBus.Publish(new SceneReadyEvent());
        }
    }
}
