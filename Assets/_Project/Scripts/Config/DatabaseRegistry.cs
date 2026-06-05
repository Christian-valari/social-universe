using System;
using System.Collections.Generic;
using UnityEngine;

namespace SocialUniverse.Config
{
    // TODO: Migrate to Addressables when asset count grows beyond prototype scale.
    [CreateAssetMenu(menuName = "SocialUniverse/Config/DatabaseRegistry", fileName = "DatabaseRegistry")]
    public class DatabaseRegistry : ScriptableObject
    {
        [SerializeField] private PlanetDefinition[]   _planets;
        [SerializeField] private AsteroidDefinition[] _asteroids;
        [SerializeField] private DroneDefinition[]    _drones;

        public IEnumerable<PlanetDefinition>   AllPlanets   => _planets   ?? Array.Empty<PlanetDefinition>();
        public IEnumerable<AsteroidDefinition> AllAsteroids => _asteroids ?? Array.Empty<AsteroidDefinition>();
        public IEnumerable<DroneDefinition>    AllDrones    => _drones    ?? Array.Empty<DroneDefinition>();

        public PlanetDefinition   GetPlanet(string id)          => Array.Find(_planets,   p => p.PlanetId     == id);
        public AsteroidDefinition GetAsteroid(string mineral)   => Array.Find(_asteroids, a => a.MineralType  == mineral);
        public DroneDefinition    GetDrone(string droneId)      => Array.Find(_drones,    d => d.DroneId      == droneId);
    }
}
