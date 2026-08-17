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
        [SerializeField] private ItemDefinition[]     _items;
        [SerializeField] private AvatarDefinition[]   _avatars;
        [SerializeField] private MineralDefinition[]  _minerals;
        [SerializeField] private UpgradeDefinition[]  _upgrades;

        public IEnumerable<PlanetDefinition>   AllPlanets   => _planets   ?? Array.Empty<PlanetDefinition>();
        public IEnumerable<AsteroidDefinition> AllAsteroids => _asteroids ?? Array.Empty<AsteroidDefinition>();
        public IEnumerable<DroneDefinition>    AllDrones    => _drones    ?? Array.Empty<DroneDefinition>();
        public IEnumerable<ItemDefinition>     AllItems     => _items     ?? Array.Empty<ItemDefinition>();
        public IEnumerable<AvatarDefinition>   AllAvatars   => _avatars   ?? Array.Empty<AvatarDefinition>();
        public IEnumerable<MineralDefinition>  AllMinerals  => _minerals  ?? Array.Empty<MineralDefinition>();
        public IEnumerable<UpgradeDefinition>  AllUpgrades  => _upgrades  ?? Array.Empty<UpgradeDefinition>();

        public PlanetDefinition   GetPlanet(string id)          => Array.Find(_planets,   p => p.PlanetId     == id);
        public AsteroidDefinition GetAsteroid(string mineral)   => Array.Find(_asteroids, a => a.MineralType  == mineral);
        public DroneDefinition    GetDrone(string droneId)      => Array.Find(_drones,    d => d.DroneId      == droneId);
        public ItemDefinition     GetItem(string itemId)        => Array.Find(_items,     i => i.ItemId       == itemId);
        public AvatarDefinition   GetAvatar(string avatarId)    => Array.Find(_avatars,   a => a.AvatarId      == avatarId);
        public MineralDefinition  GetMineral(string mineralId)  => Array.Find(_minerals,  m => m.MineralId    == mineralId);
        public UpgradeDefinition  GetUpgrade(DroneStat stat)    => Array.Find(_upgrades,  u => u.Stat         == stat);
    }
}

