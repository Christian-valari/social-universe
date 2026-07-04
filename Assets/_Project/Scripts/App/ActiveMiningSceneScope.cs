using VContainer;
using VContainer.Unity;
using SocialUniverse.Mining;
using SocialUniverse.Core;

namespace SocialUniverse.App
{
    // Scene scope for the ActiveMining minigame overlay — loaded additively on top of the
    // Planet scene while an active-mining session is running (see ActiveMiningSceneController,
    // which owns the load/unload). Always runs as a child of PlanetSceneScope (parentReference
    // set in the Inspector, wired in the scene file), so MiningController and everything else in
    // PlanetSceneScope resolve through the parent chain automatically — this scope only
    // registers the components that live in ActiveMining.unity itself.
    public class ActiveMiningSceneScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterComponentInHierarchy<ActiveMiningAsteroidStage>();
            builder.RegisterComponentInHierarchy<SocialUniverse.UI.ActiveMiningMinigameView>();

            builder.RegisterEntryPoint<ActiveMiningSceneBootstrapper>();
        }
    }

    // Spawns the visual asteroid clone for the in-progress active-mining session as soon as this
    // scene finishes loading. MiningController's session already exists by the time this scene
    // loads (ActiveMiningSceneController only loads it after a session has started).
    public class ActiveMiningSceneBootstrapper : IStartable
    {
        private readonly MiningController          _mining;
        private readonly ActiveMiningAsteroidStage _stage;

        public ActiveMiningSceneBootstrapper(MiningController mining, ActiveMiningAsteroidStage stage)
        {
            _mining = mining;
            _stage  = stage;
        }

        public void Start()
        {
            var session = _mining.CurrentActiveSession;
            if (session == null)
            {
                SULog.Warn("ActiveMiningSceneBootstrapper: no active-mining session in progress", SULog.Channel.Mining);
                return;
            }

            _stage.SpawnClone(session.Asteroid.Definition);
        }
    }
}
