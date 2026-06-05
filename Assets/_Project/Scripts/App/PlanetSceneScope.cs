using System;
using System.Linq;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using SocialUniverse.Config;
using SocialUniverse.World;
using SocialUniverse.Mining;
using SocialUniverse.Economy;
using SocialUniverse.Progression;
using SocialUniverse.Core;

namespace SocialUniverse.App
{
    // Root LifetimeScope for the Planet scene (standalone for M1 prototype).
    // In M2+, set Parent = typeof(ProjectLifetimeScope) to inherit the FSM and auth services.
    public class PlanetSceneScope : LifetimeScope
    {
        [SerializeField] private EconomyConfig   _economyConfig;
        [SerializeField] private DatabaseRegistry _databaseRegistry;

        protected override void Configure(IContainerBuilder builder)
        {
            // Config
            builder.RegisterInstance(_economyConfig);
            builder.RegisterInstance(_databaseRegistry);

            // Economy
            builder.Register<Wallet>(Lifetime.Singleton);
            builder.Register<IEconomyService, LocalMockEconomy>(Lifetime.Singleton);
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

            builder.RegisterEntryPoint<PlanetSceneBootstrapper>();
        }
    }

    public class PlanetSceneBootstrapper : IStartable
    {
        private readonly PlanetController _planetController;
        private readonly AsteroidSpawner  _asteroidSpawner;
        private readonly MiningController _miningController;
        private readonly DatabaseRegistry _registry;

        public PlanetSceneBootstrapper(PlanetController planetController, AsteroidSpawner asteroidSpawner,
            MiningController miningController, DatabaseRegistry registry)
        {
            _planetController = planetController;
            _asteroidSpawner  = asteroidSpawner;
            _miningController = miningController;
            _registry         = registry;
        }

        public void Start()
        {
            var planet = _registry.AllPlanets.FirstOrDefault();
            if (planet == null)
            {
                SULog.Error("PlanetSceneBootstrapper: no PlanetDefinition in DatabaseRegistry");
                return;
            }

            _planetController.Load(planet);
            _asteroidSpawner.SpawnForPlanet(planet);

            var droneDef = _registry.AllDrones.FirstOrDefault();
            if (droneDef == null)
            {
                SULog.Error("PlanetSceneBootstrapper: no DroneDefinition in DatabaseRegistry");
                return;
            }

            var drone = new DroneRuntime(droneDef);
            _miningController.StartSession(drone, DateTime.UtcNow);
        }
    }
}
