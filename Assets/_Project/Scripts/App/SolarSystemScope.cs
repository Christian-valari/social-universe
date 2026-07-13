using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer;
using VContainer.Unity;
using SocialUniverse.Config;
using SocialUniverse.Core;
using SocialUniverse.Economy;
using SocialUniverse.Net;
using SocialUniverse.Progression;
using SocialUniverse.Safety;
using SocialUniverse.Travel;

namespace SocialUniverse.App
{
    // Scene scope for the SolarSystem (Hub) scene.
    // Standalone (no parent): registers a LocalMockBackendClient so the star
    // map/sky discovery screens run in isolation (fuel stays at its default
    // 100/100 — the mock backend returns default(T) for every call).
    // Production (parent = RootLifetimeScope): Net services come from the
    // parent; only the Travel layer (fresh per scene, like Wallet/PlayerState
    // in PlanetSceneScope) is registered here.
    public class SolarSystemScope : LifetimeScope
    {
        [SerializeField] private EconomyConfig    _economyConfig;
        [SerializeField] private DatabaseRegistry _databaseRegistry;
        [SerializeField] private TravelTimeTable  _travelTimeTable;
        [SerializeField] private AudioConfig      _audioConfig;   // standalone-mode fallback, mirrors PlanetSceneScope
        [SerializeField] private AudioCatalog     _audioCatalog;  // standalone-mode fallback, mirrors PlanetSceneScope

        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterInstance(_economyConfig);
            builder.RegisterInstance(_databaseRegistry);
            builder.RegisterInstance(_travelTimeTable);

            // The planet the player currently resides on — whatever HubState/PlanetState's
            // TargetPlanetId last pointed at (set on first login, updated by every trip).
            // SkyDiscoveryController places the camera there and excludes it from the sky
            // bodies (you don't see the planet you're standing on floating in your own sky).
            // Standalone (no parent): falls back to the home planet (Earth).
            string currentPlanetId = Parent != null
                ? Parent.Container.Resolve<PlanetState>().TargetPlanetId
                : null;
            var currentPlanet = (!string.IsNullOrEmpty(currentPlanetId) ? _databaseRegistry.GetPlanet(currentPlanetId) : null)
                ?? _databaseRegistry.GetPlanet(Constants.PlanetIds.Earth)
                ?? _databaseRegistry.AllPlanets.First();
            builder.RegisterInstance(currentPlanet);

            if (parentReference.Type == null)
            {
                builder.Register<SceneLoader>(Lifetime.Singleton);
                builder.Register<LocalMockBackendClient>(Lifetime.Singleton).As<IBackendClient>();
                builder.Register<ServerTime>(Lifetime.Singleton);

                builder.RegisterInstance(_audioConfig != null ? _audioConfig : ScriptableObject.CreateInstance<AudioConfig>());
                builder.Register<AudioSettingsService>(Lifetime.Singleton).As<IAudioSettingsService>();
                builder.RegisterInstance(_audioCatalog != null ? _audioCatalog : ScriptableObject.CreateInstance<AudioCatalog>());
                builder.Register<AudioManager>(Lifetime.Singleton).As<IAudioManager>();
            }

            builder.Register<Wallet>(Lifetime.Singleton);
            builder.Register<PlayerState>(Lifetime.Singleton);
            builder.Register<IEconomyService, EconomyService>(Lifetime.Singleton);
            builder.Register<FuelSystem>(Lifetime.Singleton);
            builder.Register<TravelService>(Lifetime.Singleton);
            builder.Register<TravelTripSystem>(Lifetime.Singleton);

            builder.RegisterComponentInHierarchy<SolarSystemController>();
            builder.RegisterComponentInHierarchy<SkyDiscoveryController>();
            builder.RegisterComponentInHierarchy<GyroInputProvider>();
            builder.RegisterComponentInHierarchy<ReturnHomeButtonController>();
            builder.RegisterComponentInHierarchy<SocialUniverse.UI.PlanetPreviewPanel>();
            builder.RegisterComponentInHierarchy<SkyZoomController>();

            builder.RegisterEntryPoint<SolarSystemBootstrapper>();
            builder.RegisterEntryPoint<TravelController>();
        }
    }

    // Hydrates fuel (and wallet, for refill affordability) from the server
    // when the Hub scene starts — same "rehydrate this scene's services from
    // the server" pattern as PlanetSceneBootstrapper.
    public class SolarSystemBootstrapper : IStartable
    {
        private readonly FuelSystem      _fuel;
        private readonly IEconomyService _economy;
        private readonly SceneLoader     _sceneLoader;
        private readonly IAudioManager   _audio;

        public SolarSystemBootstrapper(FuelSystem fuel, IEconomyService economy, SceneLoader sceneLoader, IAudioManager audio)
        {
            _fuel        = fuel;
            _economy     = economy;
            _sceneLoader = sceneLoader;
            _audio       = audio;
        }

        public async void Start()
        {
            // In standalone mode (SolarSystem scene opened directly without Bootstrap/HubState),
            // HubState has not run so the loading scene must be loaded here instead.
            var ls = SceneManager.GetSceneByName(Constants.SceneNames.LoadingScreen);
            if (!ls.IsValid() || !ls.isLoaded)
                await _sceneLoader.LoadAsync(Constants.SceneNames.LoadingScreen);

            _audio.PlaySolarSystemBgm();

            EventBus.Publish(new LoadingStatusEvent(0.3f));
            try
            {
                await _fuel.RefreshAsync();
            }
            catch (System.Exception ex)
            {
                SULog.Warn($"SolarSystemBootstrapper: fuel hydration failed ({ex.Message}), using local state", SULog.Channel.Travel);
            }

            EventBus.Publish(new LoadingStatusEvent(0.7f));
            try
            {
                await _economy.GetWalletAsync();
            }
            catch (System.Exception ex)
            {
                SULog.Warn($"SolarSystemBootstrapper: wallet hydration failed ({ex.Message}), using local state", SULog.Channel.Travel);
            }

            EventBus.Publish(new SceneReadyEvent());
        }
    }
}
