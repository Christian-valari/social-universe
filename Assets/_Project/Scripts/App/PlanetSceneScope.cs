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
using SocialUniverse.Safety;

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
        [SerializeField] private AudioConfig      _audioConfig;    // used in standalone mode only; production gets it from RootLifetimeScope
        [SerializeField] private AudioCatalog     _audioCatalog;   // used in standalone mode only; production gets it from RootLifetimeScope

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
                builder.RegisterEntryPoint<ViewLandRequestHandler>();
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
                builder.Register<BackendClient>(Lifetime.Singleton).As<IBackendClient>();
                builder.Register<CloudSaveService>(Lifetime.Singleton).As<ICloudSave>();

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
                builder.Register<BlockListSync>(Lifetime.Singleton);

                // Standalone runs against mocks (and an unauthenticated backend), so a failed data
                // load opens the scene with defaults rather than waiting on a retry.
                builder.RegisterInstance(new PlayerDataSyncPolicy(required: false));

                // Audio settings — mirrors RootLifetimeScope's registration, needed here too
                // since standalone mode has no parent scope to provide it.
                builder.RegisterInstance(_audioConfig != null ? _audioConfig : ScriptableObject.CreateInstance<AudioConfig>());
                builder.Register<AudioSettingsService>(Lifetime.Singleton).As<IAudioSettingsService>();

                builder.RegisterInstance(_audioCatalog != null ? _audioCatalog : ScriptableObject.CreateInstance<AudioCatalog>());
                builder.Register<AudioManager>(Lifetime.Singleton).As<IAudioManager>();

                // GameStateMachine — has no real use standalone (nothing transitions it), but
                // SettingsPanel's [Inject] GameStateMachine field is force-resolved at container
                // build time (RegisterComponentInHierarchy), so it must resolve to something here
                // too, or the whole standalone container build throws.
                builder.Register<GameStateMachine>(Lifetime.Singleton);
            }

            // Economy
            builder.Register<Wallet>(Lifetime.Singleton);
            builder.Register<LandRegistry>(Lifetime.Singleton);
            builder.Register<LandRegistryService>(Lifetime.Singleton);
            builder.Register<IEconomyService, EconomyService>(Lifetime.Singleton);
            builder.Register<LandPurchaseService>(Lifetime.Singleton);
            builder.Register<BuildPaletteService>(Lifetime.Singleton);
            builder.Register<YieldService>(Lifetime.Singleton);
            builder.Register<YieldEstimateCalculator>(Lifetime.Singleton);
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

            // Mining — M6: mineral inventory + drone fleet + mineral service
            builder.Register<MineralInventory>(Lifetime.Singleton);
            builder.Register<DroneFleet>(Lifetime.Singleton);
            if (standalone)
            {
                builder.Register<LocalMockMineralService>(Lifetime.Singleton).As<IMineralService>();
            }
            else
            {
                builder.Register<MineralService>(Lifetime.Singleton).As<IMineralService>();
            }

            if (standalone)
                builder.Register<LocalMockDroneService>(Lifetime.Singleton).As<IDroneService>();
            else
                builder.Register<DroneService>(Lifetime.Singleton).As<IDroneService>();

            // Server-tracked idle mining runs (StartIdleMining / GetIdleMining).
            if (standalone)
                builder.Register<LocalMockIdleMiningService>(Lifetime.Singleton).As<IIdleMiningService>();
            else
                builder.Register<IdleMiningService>(Lifetime.Singleton).As<IIdleMiningService>();

            // Asteroid respawn timers from the server's claim log (GetAsteroidRespawns).
            if (standalone)
                builder.Register<LocalMockAsteroidRespawnService>(Lifetime.Singleton).As<IAsteroidRespawnService>();
            else
                builder.Register<AsteroidRespawnService>(Lifetime.Singleton).As<IAsteroidRespawnService>();

            builder.RegisterEntryPoint<DroneGarageHandler>();

            // UI
            builder.RegisterComponentInHierarchy<SocialUniverse.UI.HUDController>();
            builder.RegisterComponentInHierarchy<SocialUniverse.UI.MiningModePromptView>();
            builder.RegisterComponentInHierarchy<SocialUniverse.UI.SocialDebugPanel>();
            builder.RegisterComponentInHierarchy<SocialUniverse.UI.DisplayNameModal>();
            builder.RegisterComponentInHierarchy<SocialUniverse.UI.AvatarSelectionModal>();
            builder.RegisterComponentInHierarchy<SocialUniverse.UI.EmailVerificationModal>();
            builder.RegisterComponentInHierarchy<SocialUniverse.UI.LandPurchaseModal>();
            builder.RegisterComponentInHierarchy<SocialUniverse.UI.TileInfoModal>();
            builder.RegisterComponentInHierarchy<SocialUniverse.UI.SettingsPanel>();
            builder.RegisterComponentInHierarchy<SocialUniverse.UI.FuelPanel>();
            builder.RegisterComponentInHierarchy<SocialUniverse.UI.MineralInventoryView>();
            builder.RegisterComponentInHierarchy<SocialUniverse.UI.MiningClaimRewardModal>();
            builder.RegisterComponentInHierarchy<SocialUniverse.UI.DroneGarageView>();
            // One TransactionModal per panel (Drone Garage, Mineral Inventory), so each is injected
            // here: RegisterComponentInHierarchy would inject only the first one it finds.
            builder.RegisterBuildCallback(container =>
            {
                foreach (var root in gameObject.scene.GetRootGameObjects())
                    foreach (var modal in root.GetComponentsInChildren<SocialUniverse.UI.TransactionModal>(true))
                        container.Inject(modal);
            });


            builder.RegisterEntryPoint<PlanetSceneBootstrapper>();
            builder.RegisterEntryPoint<IdleMiningSessionController>();
            builder.RegisterEntryPoint<TilePurchaseHandler>();
            // AsSelf() alongside the entry-point registration: PlanetSceneBootstrapper
            // injects the concrete type directly to load and apply the registry before the
            // scene opens (see PullPlayerDataAsync), same pattern as ActiveMiningSessionRunner.
            builder.RegisterEntryPoint<LandRegistrySyncController>().AsSelf();
            builder.RegisterEntryPoint<VisitorTrackingController>();
            builder.RegisterEntryPoint<UpkeepController>();
            builder.RegisterEntryPoint<LandSaleHandler>();
            builder.RegisterEntryPoint<YieldClaimHandler>();
            builder.RegisterEntryPoint<PlanetPresenceController>();
            builder.RegisterEntryPoint<MineralSaleHandler>();
            builder.RegisterEntryPoint<MiningClaimFeedbackHandler>();
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
        private readonly LandRegistrySyncController _landRegistrySync;
        private readonly HexasphereManager _hexasphere;
        private readonly TileColorizer     _colorizer;
        private readonly IAuthService      _auth;
        private readonly PlayerState       _playerState;
        private readonly ProfileService    _profileService;
        private readonly SceneLoader       _sceneLoader;
        private readonly bool              _standalone;
        private readonly IAudioManager     _audio;
        private readonly DroneFleet        _fleet;
        private readonly MineralInventory  _inventory;
        private readonly EconomyConfig     _config;
        private readonly IIdleMiningService _idleMining;
        private readonly IAsteroidRespawnService _asteroidRespawns;
        private readonly ReportService     _reports;
        private readonly BlockListSync     _blockList;
        private readonly PlayerDataSyncPolicy _syncPolicy;

        // What PullPlayerDataAsync fetched; a step that failed (only possible when the sync isn't
        // required) leaves its field at the default and its Apply* falls back.
        private Dictionary<string, int>        _heldMinerals;
        private DroneFleetSnapshot             _fleetSnapshot;
        private PlayerProfile                  _profile;
        private bool                           _profileLoaded;
        private List<string>                   _ownedTileIds;
        private IdleMiningSessionResult        _idleRun;
        private IReadOnlyList<AsteroidRespawn> _respawns;

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
            LandRegistrySyncController landRegistrySync,
            HexasphereManager hexasphere,
            TileColorizer     colorizer,
            IAuthService      auth,
            PlayerState       playerState,
            ProfileService    profileService,
            SceneLoader       sceneLoader,
            bool              standalone,
            IAudioManager     audio,
            DroneFleet        fleet,
            MineralInventory  inventory,
            EconomyConfig     config,
            IIdleMiningService idleMining,
            IAsteroidRespawnService asteroidRespawns,
            ReportService     reports,
            BlockListSync     blockList,
            PlayerDataSyncPolicy syncPolicy)
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
            _landRegistrySync = landRegistrySync;
            _hexasphere       = hexasphere;
            _colorizer        = colorizer;
            _auth             = auth;
            _playerState      = playerState;
            _profileService   = profileService;
            _sceneLoader      = sceneLoader;
            _standalone       = standalone;
            _audio            = audio;
            _fleet            = fleet;
            _inventory        = inventory;
            _config           = config;
            _idleMining       = idleMining;
            _asteroidRespawns = asteroidRespawns;
            _reports          = reports;
            _blockList        = blockList;
            _syncPolicy       = syncPolicy;
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
            _audio.PlayBgmForPlanet(_startPlanet);

            await PullPlayerDataAsync();

            EventBus.Publish(new LoadingStatusEvent(0.80f));
            if (_heldMinerals != null) _inventory.SetAll(_heldMinerals);
            ApplyFleet();
            await ApplyProfileAsync();
            ApplyOwnedTiles();
            _landRegistrySync.ApplyOwnership();

            // The field is spawned only now, with the server's respawn timers, so a slot emptied on
            // any device stays empty here too.
            _asteroidSpawner.SpawnForPlanet(_startPlanet, _respawns);

            EventBus.Publish(new LoadingStatusEvent(0.90f));
            _miningController.Initialize(_idleRun);

            EventBus.Publish(new SceneReadyEvent());
        }

        // Everything the Planet scene shows from the server, fetched together before it opens
        // (docs/superpowers/plans/2026-10-18-cloud-synced-planet-data.md). With the real backend a
        // step that fails is retried behind the retry dialog until it loads, so the scene never
        // opens with defaults standing in for the player's data. The block list is normally already
        // loaded at sign-in (SocialServicesInitializer); it is fetched here only if that failed.
        private Task PullPlayerDataAsync() =>
            PlayerDataPull.RunAsync(new (string, Func<Task>)[]
                {
                    ("wallet",           () => _economy.GetWalletAsync()),
                    ("fuel",             () => _fuel.RefreshAsync()),
                    ("mineral inventory", async () => _heldMinerals = await _cloudSave.LoadProtectedAsync<Dictionary<string, int>>(SaveKeys.MineralInventory, null)),
                    ("drone fleet",      async () => _fleetSnapshot = await _cloudSave.LoadProtectedAsync<DroneFleetSnapshot>(SaveKeys.DroneFleet, null)),
                    ("profile",          LoadProfileAsync),
                    ("owned tiles",      async () => _ownedTileIds = await _cloudSave.LoadProtectedAsync<List<string>>(SaveKeys.OwnedTilesKey(_startPlanet.name), null)),
                    ("land registry",    () => _landRegistrySync.RefreshAsync()),
                    ("idle mining run",  LoadIdleRunAsync),
                    ("asteroid respawns", async () => _respawns = await _asteroidRespawns.GetAsync(_startPlanet.PlanetId)),
                    ("block list",       () => _reports.HasBlockList ? Task.CompletedTask : _blockList.LoadAsync()),
                },
                _syncPolicy.Required, _syncPolicy.AskToRetry,
                progress => EventBus.Publish(new LoadingStatusEvent(Mathf.Lerp(0.2f, 0.75f, progress))));

        private async Task LoadProfileAsync()
        {
            _profile       = await _profileService.GetProfileAsync(_auth.PlayerId);
            _profileLoaded = true;
        }

        // The player's idle mining run, wherever it was started. If it can't be read (only when the
        // sync isn't required), MiningController shows only a legacy local session, and the next idle
        // start takes over any run the server still has (ALREADY_MINING).
        private async Task LoadIdleRunAsync()
        {
            var run = await _idleMining.GetAsync();
            SULog.Info(run.HasSession
                ? $"PlanetSceneBootstrapper: server idle run {run.SlotId} on {run.PlanetId} ({run.ElapsedSeconds:0}/{run.DurationSeconds:0}s)"
                : "PlanetSceneBootstrapper: no server idle run", SULog.Channel.Mining);
            _idleRun = run;
        }

        // New player (no record): own the starter Scout (spec Q1). Also the fallback when the fleet
        // couldn't be read, which only happens when the sync isn't required.
        private void ApplyFleet()
        {
            var snapshot = _fleetSnapshot;
            if (snapshot == null || snapshot.Drones == null || snapshot.Drones.Count == 0)
            {
                var scout = FirstStarterDrone();
                snapshot = DroneFleetSnapshot.SingleDrone(scout?.DroneId ?? _registry.AllDrones.First().DroneId, _config.StartingFleetSlots);
            }
            _fleet.Apply(snapshot, _registry);
        }

        private async Task ApplyProfileAsync()
        {
            // Display name — prefer DisplayName (set during registration), fall back to Username,
            // then "Player". The resolver also strips the UGS "#1234" suffix so it never reaches the HUD.
            string authId = _auth.IsSignedIn
                ? ChatDisplayNameResolver.Resolve(_auth.DisplayName, _auth.Username)
                : ChatDisplayNameResolver.Fallback;
            _playerState.SetDisplayName(authId);

            string profileDisplayName = null;
            if (_profileLoaded && _profile != null)
            {
                var profile = _profile;
                profileDisplayName = profile.DisplayName;

                if (!string.IsNullOrEmpty(profile.DisplayName))
                    _playerState.SetDisplayName(profile.DisplayName);

                _playerState.SetEmailVerified(_auth.IsEmailVerified);

                // Backfill the profile's display name from the UGS auth name when the
                // profile record has none. Email-registered players set their name in
                // UGS auth only (RegisterAsync → UpdatePlayerNameAsync), never in the
                // Cloud Save profile — so a cross-player lookup (e.g. TileInfoModal
                // reading another owner's profile) sees an empty name. authId is the
                // clean, #suffix-stripped UGS name; persisting it here self-heals
                // existing accounts on their next login. Mirrors the avatar backfill below.
                if (string.IsNullOrEmpty(profile.DisplayName) && authId != ChatDisplayNameResolver.Fallback)
                {
                    try
                    {
                        await _profileService.UpdateDisplayNameAsync(authId);
                    }
                    catch (Exception ex)
                    {
                        SULog.Warn($"PlanetSceneBootstrapper: display-name backfill failed ({ex.Message})", SULog.Channel.Net);
                    }
                }

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

                if (!_auth.IsEmailVerified)
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
            else if (_profileLoaded)
            {
                // New player — no server profile yet (e.g. just registered via
                // Firebase). Assign a random avatar so AvatarId isn't null (the
                // avatar picker would null-ref), and persist it for next login.
                var catalogIds = _registry.AllAvatars.Select(a => a.AvatarId).ToList();
                string resolvedAvatarId = AvatarAssignment.ResolveAvatarId(null, catalogIds, n => UnityEngine.Random.Range(0, n));
                _playerState.SetAvatarId(resolvedAvatarId);

                if (!string.IsNullOrEmpty(resolvedAvatarId))
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
            }

            // A first-time nameless account (e.g. Google/SSO, whose UGS PlayerName is
            // null) has never chosen an in-game name. Prompt them to before they play.
            // Published in the same phase as ShowEmailVerificationPromptEvent, so
            // HUDController (subscribed in Start) receives it the same way.
            if (ProfileOnboarding.NeedsOnboarding(profileDisplayName, _auth.DisplayName, _auth.Username))
            {
                EventBus.Publish(new ShowProfileOnboardingEvent());
            }
        }

        // The player's own tiles on this planet (owned_tiles_<planet>).
        private void ApplyOwnedTiles()
        {
            if (_ownedTileIds is not { Count: > 0 }) return;

            _landRegistry.Hydrate(_ownedTileIds);

            string playerId = _auth.IsSignedIn ? _auth.PlayerId : "player";
            foreach (var tileId in _ownedTileIds)
            {
                var tile = _hexasphere.GetTile(tileId);
                if (tile == null) continue;
                tile.State   = TileState.OwnedByPlayer;
                tile.OwnerId = playerId;
                _colorizer.RefreshTile(tile);
            }
            SULog.Info($"PlanetSceneBootstrapper: restored {_ownedTileIds.Count} owned tiles for {_startPlanet.name}", SULog.Channel.World);
        }

        // The Tier-1, zero-cost starter drone (Scout); falls back to the first registered drone.
        private DroneDefinition FirstStarterDrone()
        {
            foreach (var d in _registry.AllDrones)
                if (d.Tier == 1 && d.UnlockCost == 0) return d;
            return _registry.AllDrones.FirstOrDefault();
        }
    }
}
