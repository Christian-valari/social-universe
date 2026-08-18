using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SocialUniverse.Config;
using SocialUniverse.Core;
using SocialUniverse.Economy;

namespace SocialUniverse.Mining
{
    public class DroneService : IDroneService
    {
        private readonly IBackendClient   _backend;
        private readonly DroneFleet       _fleet;
        private readonly Wallet           _wallet;
        private readonly DatabaseRegistry _registry;

        public DroneService(IBackendClient backend, DroneFleet fleet, Wallet wallet, DatabaseRegistry registry)
        {
            _backend  = backend;
            _fleet    = fleet;
            _wallet   = wallet;
            _registry = registry;
        }

        public Task<DroneActionResult> AcquireDroneAsync(string droneId) =>
            CallAsync("AcquireDrone", new Dictionary<string, object> { { "droneId", droneId } });

        public Task<DroneActionResult> UnlockSlotAsync() =>
            CallAsync("UnlockDroneSlot", new Dictionary<string, object>());

        public Task<DroneActionResult> UpgradeAsync(string droneId, DroneStat stat) =>
            CallAsync("UpgradeDrone", new Dictionary<string, object> { { "droneId", droneId }, { "stat", stat.ToString() } });

        public Task<DroneActionResult> SetActiveAsync(string droneId) =>
            CallAsync("SetActiveDrone", new Dictionary<string, object> { { "droneId", droneId } });

        private async Task<DroneActionResult> CallAsync(string fn, Dictionary<string, object> args)
        {
            DroneActionResult res;
            try
            {
                res = await _backend.CallAsync<DroneActionResult>(fn, args);
            }
            catch (Exception ex)
            {
                SULog.Error($"DroneService.{fn} failed — {ex.Message}", SULog.Channel.Economy);
                return new DroneActionResult { Success = false, Reason = "Network error" };
            }

            if (res != null && res.Success)
            {
                if (res.Fleet != null)   _fleet.Apply(res.Fleet, _registry);
                if (res.NewBalance >= 0) _wallet.SetCoins(res.NewBalance);
            }
            return res ?? new DroneActionResult { Success = false, Reason = "Empty response" };
        }
    }
}
