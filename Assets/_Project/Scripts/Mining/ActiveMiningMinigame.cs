using System;
using SocialUniverse.Config;

namespace SocialUniverse.Mining
{
    // Owns the currently-running active-mining minigame session (if any). Standalone from
    // the drone — active mining never travels and can run concurrently with an idle-mining
    // session on a different asteroid.
    public class ActiveMiningMinigame
    {
        private readonly EconomyConfig           _config;
        private readonly MiningRewardCalculator  _rewardCalc;

        public ActiveMiningSession CurrentSession { get; private set; }

        public event Action<ActiveMiningSession> OnSessionChanged;

        public ActiveMiningMinigame(EconomyConfig config, MiningRewardCalculator rewardCalc)
        {
            _config     = config;
            _rewardCalc = rewardCalc;
        }

        public bool Begin(Asteroid asteroid)
        {
            if (asteroid == null || asteroid.IsDepleted || CurrentSession != null)
                return false;

            var reward = _rewardCalc.Compute(asteroid);
            CurrentSession = new ActiveMiningSession(asteroid, reward.ActiveTapsRequired,
                _config.ActiveMaxErrors, _config.ActiveTapWindowSeconds);
            CurrentSession.OnStageChanged += _ => OnSessionChanged?.Invoke(CurrentSession);

            OnSessionChanged?.Invoke(CurrentSession);
            return true;
        }

        public void Tick(float deltaTime) => CurrentSession?.Tick(deltaTime);

        public void RegisterTap(bool hitTarget)
        {
            if (CurrentSession == null) return;
            if (hitTarget) CurrentSession.RegisterHit();
            else           CurrentSession.RegisterMiss();
        }

        public void Clear() => CurrentSession = null;
    }
}
