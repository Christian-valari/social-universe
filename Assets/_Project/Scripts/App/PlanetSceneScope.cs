using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer;
using VContainer.Unity;
using SocialUniverse.Config;
using SocialUniverse.World;
using SocialUniverse.Mining;
using SocialUniverse.Economy;
using SocialUniverse.Progression;
using SocialUniverse.Core;
using SocialUniverse.Net;
using SocialUniverse.Social;
using SocialUniverse.Travel;

namespace SocialUniverse.App
{
    // Root LifetimeScope for the Planet scene.
    // Standalone mode (no parent set): registers Net mock/stubs so the scene runs without Bootstrap.
    // Production mode: set Parent = RootLifetimeScope in the Inspector — Net services come from parent.
    public class PlanetSceneScope : LifetimeScope
    {
        [SerializeField] private EconomyConfig    _economyConfig;
        [SerializeField] private DatabaseRegistry _databaseRegistry;
        [SerializeField] private PlanetDefinition _startPlanet;
        [SerializeField] private SocialConfig     _socialConfig;   // used in standalone mode only; production gets it from RootLifetimeScope

        protected override void Configure(IContainerBuilder builder)
        {
            // Config
            builder.RegisterInstance(_economyConfig);
            builder.RegisterInstance(_databaseRegistry);

            // Production: the planet to load is whatever HubState.TravelToPlanet last set
            // on the (already-built, DontDestroyOnLoad) parent's PlanetState.TargetPlanetId —
            // this is how the M5 star map actually switches which planet loads, instead of
            // every trip landing back on the Inspector-fixed _startPlanet.
            // Standalone (no parent, e.g. opening Planet.unity directly): fall back to
            // _startPlanet / the registry's first planet, as before.
            PlanetState parentPlanetState = Parent != null ? Parent.Container.Resolve<PlanetState>() : null;
            string targetPlanetId = parentPlanetState?.TargetPlanetId;
            var planet = (!string.IsNullOrEmpty(targetPlanetId) ? _databaseRegistry.GetPlanet(targetPlanetId) : null)
                ?? _startPlanet
                ?? _databaseRegistry.AllPlanets.First();
            builder.RegisterInstance(planet);

            // LaunchButton → Hub: only wired when running under the full app flow (Bootstrap
            // already built RootLifetimeScope/PlanetState before this scene loaded). Standalone
            // mode has no FSM to return to, so the button has nothing to call.
            if (parentPlanetState != null)
            {
                builder.RegisterInstance(parentPlanetState);
                builder.RegisterEntryPoint<LaunchButtonHandler>();
                builder.RegisterEntryPoint<ActiveMiningRequestHandler>();
            }

            // Net layer — only register here when running standalone (no parent scope).
            // In the full app flow (Bootstrap → Planet), RootLifetimeScope provides these.
            bool standalone = parentReference.Type == null;
            builder.RegisterInstance(standalone);
            if (standalone)
            {
                builder.Register<SceneLoader>(Lifetime.Singleton);
                builder.Register<ActiveMiningHandoff>(Lifetime.Singleton);
                builder.Register<NetworkBootstrap>(Lifetime.Singleton).AsImplementedInterfaces();
                builder.Register<LocalMockAuthService>(Lifetime.Singleton).As<IAuthService>();
                builder.Register<LocalMockBackendClient>(Lifetime.Singleton).As<IBackendClient>();
                builder.Register<LocalMockCloudSave>(Lifetime.Singleton).As<ICloudSave>();

                // M4 social layer — mocks, mirroring RootLifetimeScope's dev-mode set.
                builder.RegisterInstance(_socialConfig != null ? _socialConfig : ScriptableObject.CreateInstance<SocialConfig>());
                builder.Register<LocalMockChatService>(Lifetime.Singleton).As<IChatService>();
                builder.Register<LocalMockFriendsService>(Lifetime.Singleton).As<IFriendsService>();
                builder.Register<LocalMockPresenceService>(Lifetime.Singleton).As<IPresenceService>();
                builder.Register<ChatModerationFilter>(Lifetime.Singleton);
                builder.Register<ReportService>(Lifetime.Singleton);
                builder.Register<ChatChannelController>(Lifetime.Singleton);
                builder.Register<DirectMessageService>(Lifetime.Singleton);
                builder.Register<ProfileService>(Lifetime.Singleton);
            }

            // Economy
            builder.Register<Wallet>(Lifetime.Singleton);
            builder.Register<LandRegistry>(Lifetime.Singleton);
            builder.Register<LandRegistryService>(Lifetime.Singleton);
            builder.Register<IEconomyService, EconomyService>(Lifetime.Singleton);
            builder.Register<LandPurchaseService>(Lifetime.Singleton);
            builder.Register<BuildPaletteService>(Lifetime.Singleton);
            builder.Register<YieldService>(Lifetime.Singleton);
            builder.Register<VisitorTracker>(Lifetime.Singleton);
            builder.Register<UpkeepService>(Lifetime.Singleton);
            builder.Register<LandSaleService>(Lifetime.Singleton);

            // Progression
            builder.Register<PlayerState>(Lifetime.Singleton);

            // Travel — fuel is spent in the SolarSystem scene; rehydrated here so the
            // Planet scene's HUD/PlayerState reflects the server's authoritative balance.
            builder.Register<FuelSystem>(Lifetime.Singleton);

            // World
            builder.Register<LandmarkService>(Lifetime.Singleton);
            builder.RegisterComponentInHierarchy<HexasphereManager>();
            builder.RegisterComponentInHierarchy<TileSelectionController>();
            builder.RegisterComponentInHierarchy<TileColorizer>();
            builder.RegisterComponentInHierarchy<TileExtrusionView>();
            builder.RegisterComponentInHierarchy<PlanetController>();
            builder.RegisterComponentInHierarchy<PlanetCameraController>();

            // Mining
            builder.Register<MiningRewardCalculator>(Lifetime.Singleton);
            builder.Register<MiningController>(Lifetime.Singleton);
            builder.RegisterComponentInHierarchy<AsteroidSpawner>();
            builder.RegisterComponentInHierarchy<DroneController>();
            builder.RegisterComponentInHierarchy<AsteroidSelectionController>();

            // UI
            builder.RegisterComponentInHierarchy<SocialUniverse.UI.HUDController>();
            builder.RegisterComponentInHierarchy<SocialUniverse.UI.MiningModePromptView>();
            builder.RegisterComponentInHierarchy<SocialUniverse.UI.SocialDebugPanel>();
            builder.RegisterComponentInHierarchy<SocialUniverse.UI.DisplayNameModal>();
            builder.RegisterComponentInHierarchy<SocialUniverse.UI.AvatarSelectionModal>();
            builder.RegisterComponentInHierarchy<SocialUniverse.UI.EmailVerificationModal>();

            builder.RegisterEntryPoint<PlanetSceneBootstrapper>();
            builder.RegisterEntryPoint<IdleMiningSessionController>();
            builder.RegisterEntryPoint<TilePurchaseHandler>();
            builder.RegisterEntryPoint<LandRegistrySyncController>();
            builder.RegisterEntryPoint<BuildModeController>();
            builder.RegisterEntryPoint<VisitorTrackingController>();
            builder.RegisterEntryPoint<UpkeepController>();
            builder.RegisterEntryPoint<LandSaleHandler>();
            builder.RegisterEntryPoint<PlanetPresenceController>();
        }
    }

    public class PlanetSceneBootstrapper : IStartable
    {
        private readonly PlanetController  _planetController;
        private readonly AsteroidSpawner   _asteroidSpawner;
        private readonly MiningController  _miningController;
        private readonly DatabaseRegistry  _registry;
        private readonly PlanetDefinition  _startPlanet;
        private readonly IEconomyService   _economy;
        private readonly FuelSystem        _fuel;
        private readonly ICloudSave        _cloudSave;
        private readonly LandRegistry      _landRegistry;
        private readonly HexasphereManager _hexasphere;
        private readonly TileColorizer     _colorizer;
        private readonly IAuthService      _auth;
        private readonly PlayerState       _playerState;
        private readonly ProfileService    _profileService;
        private readonly SceneLoader       _sceneLoader;
        private readonly bool              _standalone;

        public PlanetSceneBootstrapper(
            PlanetController  planetController,
            AsteroidSpawner   asteroidSpawner,
            MiningController  miningController,
            DatabaseRegistry  registry,
            PlanetDefinition  startPlanet,
            IEconomyService   economy,
            FuelSystem        fuel,
            ICloudSave        cloudSave,
            LandRegistry      landRegistry,
            HexasphereManager hexasphere,
            TileColorizer     colorizer,
            IAuthService      auth,
            PlayerState       playerState,
            ProfileService    profileService,
            SceneLoader       sceneLoader,
            bool              standalone)
        {
            _planetController = planetController;
            _asteroidSpawner  = asteroidSpawner;
            _miningController = miningController;
            _registry         = registry;
            _startPlanet      = startPlanet;
            _economy          = economy;
            _fuel             = fuel;
            _cloudSave        = cloudSave;
            _landRegistry     = landRegistry;
            _hexasphere       = hexasphere;
            _colorizer        = colorizer;
            _auth             = auth;
            _playerState      = playerState;
            _profileService   = profileService;
            _sceneLoader      = sceneLoader;
            _standalone       = standalone;
        }

        public async void Start()
        {
            // In standalone mode (Planet scene opened directly without Bootstrap/PlanetState),
            // PlanetState has not run so the loading scene must be loaded here instead.
            // In production mode this is intentionally skipped even if LoadingScreen isn't
            // loaded — TravelLoadingState may have deliberately not loaded it (see
            // PlanetState.SkipLoadingScreen) for the land animation leg.
            if (_standalone)
            {
                var ls = SceneManager.GetSceneByName(Constants.SceneNames.LoadingScreen);
                if (!ls.IsValid() || !ls.isLoaded)
                    await _sceneLoader.LoadAsync(Constants.SceneNames.LoadingScreen);
            }

            EventBus.Publish(new LoadingStatusEvent(0.15f));
            _planetController.Load(_startPlanet);
            _asteroidSpawner.SpawnForPlanet(_startPlanet);

            await HydrateServerStateAsync();

            var droneDef = _registry.AllDrones.FirstOrDefault();
            if (droneDef == null)
            {
                SULog.Error("PlanetSceneBootstrapper: no DroneDefinition in DatabaseRegistry");
                EventBus.Publish(new SceneReadyEvent());
                return;
            }

            EventBus.Publish(new LoadingStatusEvent(0.90f));
            var drone = new DroneRuntime(droneDef);
            _miningController.Initialize(drone);

            EventBus.Publish(new SceneReadyEvent());
        }

        private async Task HydrateServerStateAsync()
        {
            // Hydrate wallet — non-fatal; falls back to 0 balance if server is unreachable.
            EventBus.Publish(new LoadingStatusEvent(0.35f));
            try
            {
                await _economy.GetWalletAsync();
                SULog.Info("PlanetSceneBootstrapper: wallet hydrated from server", SULog.Channel.Economy);
            }
            catch (Exception ex)
            {
                SULog.Warn($"PlanetSceneBootstrapper: wallet hydration failed ({ex.Message}), using local state", SULog.Channel.Economy);
            }

            // Hydrate fuel — non-fatal; falls back to PlayerState's default if the server is
            // unreachable. Fuel is spent only in the SolarSystem scene (TravelService), so this
            // is how the Planet scene picks up the post-travel balance.
            try
            {
                await _fuel.RefreshAsync();
                SULog.Info("PlanetSceneBootstrapper: fuel hydrated from server", SULog.Channel.Economy);
            }
            catch (Exception ex)
            {
                SULog.Warn($"PlanetSceneBootstrapper: fuel hydration failed ({ex.Message}), using local state", SULog.Channel.Economy);
            }

            // Hydrate display name — prefer DisplayName (set during registration), fall back to Username, then "Player".
            EventBus.Publish(new LoadingStatusEvent(0.55f));
            string authId = _auth.IsSignedIn
                ? (!string.IsNullOrEmpty(_auth.DisplayName) ? _auth.DisplayName
                   : !string.IsNullOrEmpty(_auth.Username)  ? _auth.Username
                   : "Player")
                : "Player";
            _playerState.SetDisplayName(authId);

            try
            {
                var profile = await _profileService.GetProfileAsync(_auth.PlayerId);
                if (profile != null)
                {
                    if (!string.IsNullOrEmpty(profile.DisplayName))
                        _playerState.SetDisplayName(profile.DisplayName);

                    _playerState.SetEmailVerified(profile.EmailVerified);

                    var catalogIds = _registry.AllAvatars.Select(a => a.AvatarId).ToList();
                    string resolvedAvatarId = AvatarAssignment.ResolveAvatarId(profile.AvatarId, catalogIds, n => UnityEngine.Random.Range(0, n));
                    _playerState.SetAvatarId(resolvedAvatarId);

                    if (string.IsNullOrEmpty(profile.AvatarId) && !string.IsNullOrEmpty(resolvedAvatarId))
                    {
                        try
                        {
                            await _profileService.UpdateAvatarAsync(resolvedAvatarId);
                        }
                        catch (Exception ex)
                        {
                            SULog.Warn($"PlanetSceneBootstrapper: avatar assignment failed to persist ({ex.Message}), using local pick", SULog.Channel.Net);
                        }
                    }

                    if (!profile.EmailVerified)
                    {
                        string promptedKey = SaveKeys.EmailVerificationPromptedKey(_auth.PlayerId);
                        if (!PlayerPrefs.HasKey(promptedKey))
                        {
                            PlayerPrefs.SetInt(promptedKey, 1);
                            PlayerPrefs.Save();
                            EventBus.Publish(new ShowEmailVerificationPromptEvent());
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                SULog.Warn($"PlanetSceneBootstrapper: profile fetch failed ({ex.Message}), using auth id", SULog.Channel.Net);
            }

            // Restore owned tiles for this planet from Cloud Save.
            EventBus.Publish(new LoadingStatusEvent(0.75f));
            try
            {
                string saveKey  = SaveKeys.OwnedTilesKey(_startPlanet.name);
                var    ownedIds = await _cloudSave.LoadAsync<List<string>>(saveKey, null);

                if (ownedIds is { Count: > 0 })
                {
                    _landRegistry.Hydrate(ownedIds);

                    string playerId = _auth.IsSignedIn ? _auth.PlayerId : "player";
                    foreach (var tileId in ownedIds)
                    {
                        var tile = _hexasphere.GetTile(tileId);
                        if (tile == null) continue;
                        tile.State   = TileState.OwnedByPlayer;
                        tile.OwnerId = playerId;
                        _colorizer.RefreshTile(tile);
                    }
                    SULog.Info($"PlanetSceneBootstrapper: restored {ownedIds.Count} owned tiles for {_startPlanet.name}", SULog.Channel.World);
                }
            }
            catch (Exception ex)
            {
                SULog.Warn($"PlanetSceneBootstrapper: tile restore failed ({ex.Message})", SULog.Channel.World);
            }
        }
    }
}
