using System;
using System.Collections.Generic;
using SocialUniverse.Config;
using SocialUniverse.Core;

namespace SocialUniverse.Mining
{
    // Serializable snapshot of one owned drone. Upgrades keyed by DroneStat name ("Cargo"/...)
    // so the JSON shape matches the drone_fleet Cloud Save record + ServerCode functions.
    public class DroneSnapshot
    {
        public string                  DroneId;
        public Dictionary<string, int> Upgrades;
    }

    // Serializable snapshot of the whole fleet. Shape MUST MATCH the drone_fleet Cloud Save
    // record and the { fleet } payload returned by the drone ServerCode functions.
    public class DroneFleetSnapshot
    {
        public int                 Slots;
        public string              ActiveDroneId;
        public List<DroneSnapshot> Drones;

        public static DroneFleetSnapshot SingleDrone(string droneId, int slots) => new DroneFleetSnapshot
        {
            Slots = slots, ActiveDroneId = droneId,
            Drones = new List<DroneSnapshot> { new DroneSnapshot { DroneId = droneId, Upgrades = new Dictionary<string, int>() } }
        };
    }

    public class DroneFleetChangedEvent { }

    // Client-side view cache of owned drones + active selection + unlocked slot count.
    // Server (drone_fleet Cloud Save record) is authoritative; this mirrors Wallet/MineralInventory.
    public class DroneFleet
    {
        private readonly List<DroneRuntime> _drones = new();

        public IReadOnlyList<DroneRuntime> Drones => _drones;
        public string ActiveDroneId { get; private set; }
        public int    UnlockedSlots { get; private set; }

        public DroneRuntime Active => Get(ActiveDroneId) ?? (_drones.Count > 0 ? _drones[0] : null);

        public DroneRuntime Get(string droneId) =>
            droneId == null ? null : _drones.Find(d => d.Definition.DroneId == droneId);

        public void Apply(DroneFleetSnapshot snapshot, DatabaseRegistry registry)
        {
            _drones.Clear();
            UnlockedSlots = snapshot?.Slots ?? 0;
            ActiveDroneId = snapshot?.ActiveDroneId;

            var upgradeLookup = BuildUpgradeLookup(registry);

            if (snapshot?.Drones != null)
            {
                foreach (var ds in snapshot.Drones)
                {
                    var def = registry.GetDrone(ds.DroneId);
                    if (def == null) continue; // unknown drone id — skip defensively

                    var levels = new Dictionary<DroneStat, int>();
                    if (ds.Upgrades != null)
                        foreach (var kv in ds.Upgrades)
                            if (Enum.TryParse<DroneStat>(kv.Key, out var stat)) levels[stat] = kv.Value;

                    _drones.Add(new DroneRuntime(def, levels, upgradeLookup));
                }
            }

            EventBus.Publish(new DroneFleetChangedEvent());
        }

        public DroneFleetSnapshot ToSnapshot()
        {
            var list = new List<DroneSnapshot>();
            foreach (var d in _drones)
            {
                var up = new Dictionary<string, int>();
                foreach (var kv in d.Levels) up[kv.Key.ToString()] = kv.Value;
                list.Add(new DroneSnapshot { DroneId = d.Definition.DroneId, Upgrades = up });
            }
            return new DroneFleetSnapshot { Slots = UnlockedSlots, ActiveDroneId = ActiveDroneId, Drones = list };
        }

        private static IReadOnlyDictionary<DroneStat, UpgradeDefinition> BuildUpgradeLookup(DatabaseRegistry registry)
        {
            var map = new Dictionary<DroneStat, UpgradeDefinition>();
            foreach (var u in registry.AllUpgrades) map[u.Stat] = u;
            return map;
        }
    }
}
