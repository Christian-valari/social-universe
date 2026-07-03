using System;
using UnityEngine;

namespace SocialUniverse.Mining
{
    public enum ActiveMiningStage { InProgress, Success, Failed }

    // Player-vs-asteroid tap-timing minigame: one target point is "live" at a time, must be
    // hit within TapWindowSeconds or it counts as a miss. MaxErrors misses fails the asteroid;
    // TapsRequired hits succeeds it. Does not reference DroneRuntime — active mining never
    // occupies the drone.
    public class ActiveMiningSession
    {
        public Asteroid Asteroid         { get; }
        public int      TapsRequired     { get; }
        public int      SuccessfulTaps   { get; private set; }
        public int      MaxErrors        { get; }
        public int      ErrorCount       { get; private set; }
        public float    TapWindowSeconds { get; }

        public ActiveMiningStage Stage { get; private set; } = ActiveMiningStage.InProgress;

        public event Action<ActiveMiningStage> OnStageChanged;

        private float _windowElapsed;

        public ActiveMiningSession(Asteroid asteroid, int tapsRequired, int maxErrors, float tapWindowSeconds)
        {
            Asteroid         = asteroid;
            TapsRequired     = Mathf.Max(1, tapsRequired);
            MaxErrors        = Mathf.Max(1, maxErrors);
            TapWindowSeconds = Mathf.Max(0.05f, tapWindowSeconds);
        }

        // Call every frame while Stage == InProgress; a target point that isn't hit within
        // TapWindowSeconds counts as a miss.
        public void Tick(float deltaTime)
        {
            if (Stage != ActiveMiningStage.InProgress) return;

            _windowElapsed += deltaTime;
            if (_windowElapsed >= TapWindowSeconds)
                RegisterMiss();
        }

        // The live target point was tapped within its window.
        public void RegisterHit()
        {
            if (Stage != ActiveMiningStage.InProgress) return;

            SuccessfulTaps++;
            _windowElapsed = 0f;

            if (SuccessfulTaps >= TapsRequired)
                SetStage(ActiveMiningStage.Success);
        }

        // The player tapped the wrong spot, or the window expired via Tick.
        public void RegisterMiss()
        {
            if (Stage != ActiveMiningStage.InProgress) return;

            ErrorCount++;
            _windowElapsed = 0f;

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
