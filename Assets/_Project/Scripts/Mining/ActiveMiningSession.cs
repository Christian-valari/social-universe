using System;
using UnityEngine;

namespace SocialUniverse.Mining
{
    public enum ActiveMiningStage { InProgress, Success, Failed }

    // Player-vs-asteroid tap minigame: the whole session runs under one overall countdown
    // (SessionDurationSeconds, scaled by the asteroid's size via MiningRewardCalculator).
    // Running out of time fails the session directly. A "miss" only happens when the player
    // taps the wrong spot (ActiveMiningMinigameView.RegisterTap(false)) — there is no per-point
    // timeout. MaxErrors misses fails the asteroid; TapsRequired hits succeeds it.
    public class ActiveMiningSession
    {
        public Asteroid Asteroid               { get; }
        public int      TapsRequired           { get; }
        public int      SuccessfulTaps          { get; private set; }
        public int      MaxErrors               { get; }
        public int      ErrorCount              { get; private set; }
        public float    SessionDurationSeconds  { get; }
        public float    TimeRemainingSeconds    { get; private set; }

        public ActiveMiningStage Stage { get; private set; } = ActiveMiningStage.InProgress;

        public event Action<ActiveMiningStage> OnStageChanged;

        public ActiveMiningSession(Asteroid asteroid, int tapsRequired, int maxErrors, float sessionDurationSeconds)
        {
            Asteroid               = asteroid;
            TapsRequired           = Mathf.Max(1, tapsRequired);
            MaxErrors              = Mathf.Max(1, maxErrors);
            SessionDurationSeconds = Mathf.Max(0.1f, sessionDurationSeconds);
            TimeRemainingSeconds   = SessionDurationSeconds;
        }

        // Call every frame while Stage == InProgress; running out of time fails the session.
        public void Tick(float deltaTime)
        {
            if (Stage != ActiveMiningStage.InProgress) return;

            TimeRemainingSeconds -= deltaTime;
            if (TimeRemainingSeconds <= 0f)
                SetStage(ActiveMiningStage.Failed);
        }

        // The live target point was tapped.
        public void RegisterHit()
        {
            if (Stage != ActiveMiningStage.InProgress) return;

            SuccessfulTaps++;

            if (SuccessfulTaps >= TapsRequired)
                SetStage(ActiveMiningStage.Success);
        }

        // The player tapped the wrong spot.
        public void RegisterMiss()
        {
            if (Stage != ActiveMiningStage.InProgress) return;

            ErrorCount++;

            if (ErrorCount >= MaxErrors)
                SetStage(ActiveMiningStage.Failed);
        }

        private void SetStage(ActiveMiningStage stage)
        {
            Stage = stage;
            OnStageChanged?.Invoke(stage);
        }
    }
}
