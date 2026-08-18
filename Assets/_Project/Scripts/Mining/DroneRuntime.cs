using System;
using System.Collections.Generic;
using UnityEngine;
using SocialUniverse.Config;

namespace SocialUniverse.Mining
{
    // Live drone: its definition (base stats) + per-stat upgrade levels. Exposes effective
    // stats via DroneUpgradeMath. Each owned drone has its own DroneRuntime in the DroneFleet.
    public class DroneRuntime
    {
        public DroneDefinition Definition { get; }

        private readonly Dictionary<DroneStat, int> _levels;
        private readonly IReadOnlyDictionary<DroneStat, UpgradeDefinition> _upgrades;

        public DroneRuntime(DroneDefinition definition,
            IDictionary<DroneStat, int> levels = null,
            IReadOnlyDictionary<DroneStat, UpgradeDefinition> upgrades = null)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            _levels    = levels != null ? new Dictionary<DroneStat, int>(levels) : new Dictionary<DroneStat, int>();
            _upgrades  = upgrades;
        }

        public IReadOnlyDictionary<DroneStat, int> Levels => _levels;

        public int Level(DroneStat stat) => _levels.TryGetValue(stat, out var l) ? l : 0;

        public void SetLevel(DroneStat stat, int level) => _levels[stat] = Mathf.Max(0, level);

        private UpgradeDefinition Upgrade(DroneStat stat) =>
            _upgrades != null && _upgrades.TryGetValue(stat, out var u) ? u : null;

        public int   EffectiveCargoCap    => Mathf.RoundToInt(DroneUpgradeMath.EffectiveStat(Definition.CargoCap,        Upgrade(DroneStat.Cargo), Level(DroneStat.Cargo)));
        public float EffectiveYieldMult   => DroneUpgradeMath.EffectiveStat(Definition.YieldMultiplier, Upgrade(DroneStat.Yield), Level(DroneStat.Yield));
        public float EffectiveTravelSpeed => DroneUpgradeMath.EffectiveStat(Definition.TravelSpeed,     Upgrade(DroneStat.Speed), Level(DroneStat.Speed));
    }
}
