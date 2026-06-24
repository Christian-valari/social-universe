using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer;
using VContainer.Unity;
using SocialUniverse.Config;
using SocialUniverse.Core;
using SocialUniverse.Net;
using SocialUniverse.Progression;
using SocialUniverse.Travel;

namespace SocialUniverse.App
{
    // Scene scope for the Travel scene — loaded by TravelState while a trip
    // started from the Hub is in transit. Hosts only TravelingPanel; HubState
    // (needed for Land's scene transition) resolves through the parent chain
    // like SolarSystemScope's ReturnHomeButtonController already does, no
    // explicit registration needed here.
    // Standalone (no parent): registers a LocalMockBackendClient, same pattern
    // as SolarSystemScope/PlanetSceneScope.
    public class TravelSceneScope : LifetimeScope
    {
        [SerializeField] private DatabaseRegistry _databaseRegistry;

        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterInstance(_databaseRegistry);

            // TravelTripSystem's constructor needs a PlanetDefinition (the planet the
            // trip departed from) to send as originPlanetId when StartTravelAsync is
            // called — but that call only ever happens back in the Hub, before this
            // scene loads, so the value here is never actually used. Still required to
            // satisfy DI; mirrors SolarSystemScope's same resolution with a Parent
            // PlanetState, falling back to Earth/first planet in standalone mode.
            string currentPlanetId = Parent != null
                ? Parent.Container.Resolve<PlanetState>().TargetPlanetId
                : null;
            var currentPlanet = (!string.IsNullOrEmpty(currentPlanetId) ? _databaseRegistry.GetPlanet(currentPlanetId) : null)
                ?? _databaseRegistry.GetPlanet(Constants.PlanetIds.Earth)
                ?? _databaseRegistry.AllPlanets.First();
            builder.RegisterInstance(currentPlanet);

            bool standalone = parentReference.Type == null;
            if (standalone)
            {
                builder.Register<SceneLoader>(Lifetime.Singleton);
                builder.Register<LocalMockBackendClient>(Lifetime.Singleton).As<IBackendClient>();
                builder.Register<ServerTime>(Lifetime.Singleton);
            }
            builder.RegisterInstance(standalone);

            builder.Register<PlayerState>(Lifetime.Singleton);
            builder.Register<TravelTripSystem>(Lifetime.Singleton);

            builder.RegisterComponentInHierarchy<SocialUniverse.UI.TravelingPanel>();

            builder.RegisterEntryPoint<TravelSceneBootstrapper>();
        }
    }

    // Hydrates the in-progress trip (and fuel balance) from the server when the
    // Travel scene starts — the authoritative check, whether we got here by
    // starting a fresh trip or by resuming one across an app restart.
    public class TravelSceneBootstrapper : IStartable
    {
        private readonly TravelTripSystem _trips;
        private readonly SceneLoader      _sceneLoader;
        private readonly bool             _standalone;

        public TravelSceneBootstrapper(TravelTripSystem trips, SceneLoader sceneLoader, bool standalone)
        {
            _trips       = trips;
            _sceneLoader = sceneLoader;
            _standalone  = standalone;
        }

        public async void Start()
        {
            // In standalone mode (Travel scene opened directly without Bootstrap/TravelState),
            // TravelState has not run so the loading scene must be loaded here instead.
            // In production mode this is intentionally skipped even if LoadingScreen isn't
            // loaded — TravelLoadingState may have deliberately not loaded it (see
            // TravelState.SkipLoadingScreen) for the takeoff/land animation legs.
            if (_standalone)
            {
                var ls = SceneManager.GetSceneByName(Constants.SceneNames.LoadingScreen);
                if (!ls.IsValid() || !ls.isLoaded)
                    await _sceneLoader.LoadAsync(Constants.SceneNames.LoadingScreen);
            }

            EventBus.Publish(new LoadingStatusEvent(0.5f));
            try
            {
                await _trips.RefreshAsync();
            }
            catch (System.Exception ex)
            {
                SULog.Warn($"TravelSceneBootstrapper: travel state hydration failed ({ex.Message}), using local state", SULog.Channel.Travel);
            }

            EventBus.Publish(new SceneReadyEvent());
        }
    }
}
