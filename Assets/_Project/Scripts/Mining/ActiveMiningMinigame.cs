using System;
using SocialUniverse.Config;

namespace SocialUniverse.Mining
{
    public class MiningTapResult
    {
        public int  YieldAmount;
        public bool IsCrit;
    }

    public class ActiveMiningMinigame
    {
        private readonly EconomyConfig _config;
        private readonly Random        _rng = new();

        public event Action<MiningTapResult> OnTap;

        public ActiveMiningMinigame(EconomyConfig config) => _config = config;

        public MiningTapResult Tap(Asteroid target, DroneRuntime drone)
        {
            if (target == null || target.IsDepleted || drone.IsCargoFull)
                return null;

            bool  isCrit = _rng.NextDouble() < _config.CritChance;
            float mult   = isCrit ? _config.CritMultiplier : 1f;
            int   raw    = (int)(_config.ActiveTapYield * mult);
            int   mined  = target.Mine(raw);
            drone.AddCargo(mined);

            var result = new MiningTapResult { YieldAmount = mined, IsCrit = isCrit };
            OnTap?.Invoke(result);
            return result;
        }
    }
}
