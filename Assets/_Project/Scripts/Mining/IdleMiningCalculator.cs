using System;
using SocialUniverse.Config;

namespace SocialUniverse.Mining
{
    public class IdleMiningCalculator
    {
        private readonly EconomyConfig _config;

        public IdleMiningCalculator(EconomyConfig config) => _config = config;

        public int Calculate(DateTime lastSessionEnd, DroneRuntime drone)
        {
            var elapsed      = (float)(DateTime.UtcNow - lastSessionEnd).TotalSeconds;
            var capped       = Math.Min(elapsed, _config.MaxOfflineHours * 3600f);
            var rawYield     = (int)(capped * _config.IdleMiningRate);
            var cargoSpace   = drone.Definition.CargoCap - drone.CargoAmount;
            return Math.Min(rawYield, cargoSpace);
        }
    }
}
