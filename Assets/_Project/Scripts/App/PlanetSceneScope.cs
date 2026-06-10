using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using SocialUniverse.Config;
using SocialUniverse.World;
using SocialUniverse.Mining;
using SocialUniverse.Economy;
using SocialUniverse.Progression;
using SocialUniverse.Core;
using SocialUniverse.Net;

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

        protected override void Configure(IContainerBuilder builder)
        {
            // Config
            builder.RegisterInstance(_economyConfig);
            builder.RegisterInstance(_databaseRegistry);

            var planet = _startPlanet != null
                ? _startPlanet
                : _databaseRegistry.AllPlanets.First();
            builder.RegisterInstance(planet);

            // Net layer — only register here when running standalone (no parent scope).
            // In the full app flow (Bootstrap → Planet), RootLifetimeScope provides these.
            if (parentReference.Type == null)
            {
                builder.Register<NetworkBootstrap>(Lifetime.Singleton).AsImplementedInterfaces();
                builder.Register<LocalMockAuthService>(Lifetime.Singleton).As<IAuthService>();
                builder.Register<BackendClient>(Lifetime.Singleton).As<IBackendClient>();
                builder.Register<CloudSaveService>(Lifetime.Singleton).As<ICloudSave>();
            }

            // Economy
            builder.Register<Wallet>(Lifetime.Singleton);
            builder.Register<LandRegistry>(Lifetime.Singleton);
            builder.Register<IEconomyService, EconomyService>(Lifetime.Singleton);
            builder.Register<LandPurchaseService>(Lifetime.Singleton);

            // Progression
            builder.Register<PlayerState>(Lifetime.Singleton);

            // World
            builder.Register<LandmarkService>(Lifetime.Singleton);
            builder.RegisterComponentInHierarchy<HexasphereManager>();
            builder.RegisterComponentInHierarchy<TileSelectionController>();
            builder.RegisterComponentInHierarchy<TileColorizer>();
            builder.RegisterComponentInHierarchy<PlanetController>();
            builder.RegisterComponentInHierarchy<PlanetCameraController>();

            // Mining
            builder.Register<IdleMiningCalculator>(Lifetime.Singleton);
            builder.Register<ActiveMiningMinigame>(Lifetime.Singleton);
            builder.Register<MiningController>(Lifetime.Singleton);
            builder.RegisterComponentInHierarchy<AsteroidSpawner>();
            builder.RegisterComponentInHierarchy<DroneController>();
            builder.RegisterComponentInHierarchy<AsteroidSelectionController>();

            // UI
            builder.RegisterComponentInHierarchy<SocialUniverse.UI.HUDController>();
            builder.RegisterComponentInHierarchy<SocialUniverse.UI.MiningModePromptView>();

            builder.RegisterEntryPoint<PlanetSceneBootstrapper>();
            builder.RegisterEntryPoint<MiningInputHandler>();
            builder.RegisterEntryPoint<IdleMiningSessionController>();
            builder.RegisterEntryPoint<TilePurchaseHandler>();
        }

        private void OnApplicationPause(bool pausing)
        {
            if (pausing) SaveSessionEnd();
        }

        private void OnApplicationQuit() => SaveSessionEnd();

        private static void SaveSessionEnd()
        {
            PlayerPrefs.SetString(SaveKeys.LastSessionEnd, DateTime.UtcNow.ToString("O"));
            PlayerPrefs.Save();
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
        private readonly ICloudSave        _cloudSave;
        private readonly LandRegistry      _landRegistry;
        private readonly HexasphereManager _hexasphere;
        private readonly TileColorizer     _colorizer;
        private readonly IAuthService      _auth;

        public PlanetSceneBootstrapper(
            PlanetController  planetController,
            AsteroidSpawner   asteroidSpawner,
            MiningController  miningController,
            DatabaseRegistry  registry,
            PlanetDefinition  startPlanet,
            IEconomyService   economy,
            ICloudSave        cloudSave,
            LandRegistry      landRegistry,
            HexasphereManager hexasphere,
            TileColorizer     colorizer,
            IAuthService      auth)
        {
            _planetController = planetController;
            _asteroidSpawner  = asteroidSpawner;
            _miningController = miningController;
            _registry         = registry;
            _startPlanet      = startPlanet;
            _economy          = economy;
            _cloudSave        = cloudSave;
            _landRegistry     = landRegistry;
            _hexasphere       = hexasphere;
            _colorizer        = colorizer;
            _auth             = auth;
        }

        public async void Start()
        {
            _planetController.Load(_startPlanet);
            _asteroidSpawner.SpawnForPlanet(_startPlanet);

            // M2: Hydrate wallet and owned tiles from server before starting the mining session.
            await HydrateServerStateAsync();

            var droneDef = _registry.AllDrones.FirstOrDefault();
            if (droneDef == null)
            {
                SULog.Error("PlanetSceneBootstrapper: no DroneDefinition in DatabaseRegistry");
                return;
            }

            var saved       = PlayerPrefs.GetString(SaveKeys.LastSessionEnd, "");
            var lastSession = DateTime.TryParse(saved, out var dt) ? dt : DateTime.UtcNow;

            var drone = new DroneRuntime(droneDef);
            _miningController.StartSession(drone, lastSession);
        }

        private async Task HydrateServerStateAsync()
        {
            // Hydrate wallet — non-fatal; falls back to 0 balance if server is unreachable.
            try
            {
                await _economy.GetWalletAsync();
                SULog.Info("PlanetSceneBootstrapper: wallet hydrated from server", SULog.Channel.Economy);
            }
            catch (Exception ex)
            {
                SULog.Warn($"PlanetSceneBootstrapper: wallet hydration failed ({ex.Message}), using local state", SULog.Channel.Economy);
            }

            // Restore owned tiles for this planet from Cloud Save.
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
