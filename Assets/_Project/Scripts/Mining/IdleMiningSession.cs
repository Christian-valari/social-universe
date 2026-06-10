using System;
using UnityEngine;

namespace SocialUniverse.Mining
{
    public enum IdleMiningStage { Traveling, Mining, ReadyToClaim, Complete }

    // Tracks one player-directed idle-mining run against a single asteroid:
    // drone travels there, mines for a fixed duration, then waits for claim taps.
    public class IdleMiningSession
    {
        public Asteroid        Asteroid           { get; }
        public IdleMiningStage Stage              { get; private set; }
        public float           MiningProgress01   { get; private set; }
        public int             ClaimTapsRequired  { get; }
        public int             ClaimTapsRemaining { get; private set; }

        public event Action<IdleMiningStage> OnStageChanged;

        private readonly float _miningDuration;
        private float _elapsed;

        public IdleMiningSession(Asteroid asteroid, float miningDuration, int claimTapsRequired)
        {
            Asteroid           = asteroid;
            _miningDuration    = Mathf.Max(0.01f, miningDuration);
            ClaimTapsRequired  = Mathf.Max(1, claimTapsRequired);
            ClaimTapsRemaining = ClaimTapsRequired;
            Stage              = IdleMiningStage.Traveling;
        }

        public void BeginMining()
        {
            if (Stage != IdleMiningStage.Traveling) return;

            _elapsed = 0f;
            SetStage(IdleMiningStage.Mining);
        }

        public void Tick(float deltaTime)
        {
            if (Stage != IdleMiningStage.Mining) return;

            _elapsed        += deltaTime;
            MiningProgress01 = Mathf.Clamp01(_elapsed / _miningDuration);

            if (_elapsed >= _miningDuration)
                SetStage(IdleMiningStage.ReadyToClaim);
        }

        // Returns true once the final claim tap completes the session.
        public bool RegisterClaimTap()
        {
            if (Stage != IdleMiningStage.ReadyToClaim) return false;

            ClaimTapsRemaining = Mathf.Max(0, ClaimTapsRemaining - 1);
            if (ClaimTapsRemaining == 0)
                SetStage(IdleMiningStage.Complete);

            return Stage == IdleMiningStage.Complete;
        }

        private void SetStage(IdleMiningStage stage)
        {
            Stage = stage;
            OnStageChanged?.Invoke(stage);
        }
    }
}
