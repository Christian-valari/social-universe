using System.Threading.Tasks;
using SocialUniverse.Config;
using SocialUniverse.Economy;

namespace SocialUniverse.Mining
{
    // Dev-mode drone service: validates against the current DroneFleet snapshot + wallet,
    // deducts coins locally, and re-applies a mutated snapshot. No server round-trip.
    // Validation logic MUST MATCH the drone ServerCode functions.
    public class LocalMockDroneService : IDroneService
    {
        private readonly DroneFleet       _fleet;
        private readonly Wallet           _wallet;
        private readonly DatabaseRegistry _registry;
        private readonly EconomyConfig    _config;

        public LocalMockDroneService(DroneFleet fleet, Wallet wallet, DatabaseRegistry registry, EconomyConfig config)
        {
            _fleet    = fleet;
            _wallet   = wallet;
            _registry = registry;
            _config   = config;
        }

        public Task<DroneActionResult> AcquireDroneAsync(string droneId)
        {
            var def = _registry.GetDrone(droneId);
            if (def == null)                              return Fail("UNKNOWN_DRONE");
            var snap = _fleet.ToSnapshot();
            if (snap.Drones.Exists(d => d.DroneId == droneId)) return Fail("ALREADY_OWNED");
            if (snap.Drones.Count >= snap.Slots)          return Fail("SLOTS_FULL");
            if (!_wallet.CanAfford(def.UnlockCost))       return Fail("INSUFFICIENT_FUNDS");

            _wallet.SetCoins(_wallet.Coins - def.UnlockCost);
            snap.Drones.Add(new DroneSnapshot { DroneId = droneId, Upgrades = new System.Collections.Generic.Dictionary<string, int>() });
            return Apply(snap);
        }

        public Task<DroneActionResult> UnlockSlotAsync()
        {
            var snap = _fleet.ToSnapshot();
            int cost = DroneUpgradeMath.SlotUnlockCost(_config.SlotUnlockBaseCost, _config.SlotUnlockCostGrowth, snap.Slots, _config.StartingFleetSlots);
            if (!_wallet.CanAfford(cost)) return Fail("INSUFFICIENT_FUNDS");

            _wallet.SetCoins(_wallet.Coins - cost);
            snap.Slots += 1;
            return Apply(snap);
        }

        public Task<DroneActionResult> UpgradeAsync(string droneId, DroneStat stat)
        {
            var snap = _fleet.ToSnapshot();
            var ds   = snap.Drones.Find(d => d.DroneId == droneId);
            var def  = _registry.GetUpgrade(stat);
            if (ds == null || def == null) return Fail("INVALID");

            ds.Upgrades ??= new System.Collections.Generic.Dictionary<string, int>();
            ds.Upgrades.TryGetValue(stat.ToString(), out int level);
            if (level >= def.MaxLevel) return Fail("MAX_LEVEL");

            int cost = DroneUpgradeMath.NextCost(def, level);
            if (!_wallet.CanAfford(cost)) return Fail("INSUFFICIENT_FUNDS");

            _wallet.SetCoins(_wallet.Coins - cost);
            ds.Upgrades[stat.ToString()] = level + 1;
            return Apply(snap);
        }

        public Task<DroneActionResult> SetActiveAsync(string droneId)
        {
            var snap = _fleet.ToSnapshot();
            if (!snap.Drones.Exists(d => d.DroneId == droneId)) return Fail("NOT_OWNED");
            snap.ActiveDroneId = droneId;
            return Apply(snap);
        }

        private Task<DroneActionResult> Apply(DroneFleetSnapshot snap)
        {
            _fleet.Apply(snap, _registry);
            return Task.FromResult(new DroneActionResult { Success = true, NewBalance = _wallet.Coins, Fleet = snap });
        }

        private static Task<DroneActionResult> Fail(string reason) =>
            Task.FromResult(new DroneActionResult { Success = false, Reason = reason });
    }
}
