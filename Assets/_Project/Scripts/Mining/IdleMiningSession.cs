using System;
using UnityEngine;

namespace SocialUniverse.Mining
{
    public enum IdleMiningStage { Traveling, Mining, ReadyToClaim, Complete }

    // Tracks one player-directed idle-mining run against a single asteroid. Timing is driven
    // by real wall-clock elapsed time (DateTime.UtcNow - StartUtc), not accumulated per-frame
    // deltaTime — this is what lets a session resume correctly after the app was closed and
    // reopened: reconstructing with the persisted StartUtc/DurationSeconds is enough to derive
    // the correct current stage with no additional bookkeeping.
    public class IdleMiningSession
    {
        public Asteroid        Asteroid        { get; }
        public DateTime        StartUtc        { get; }
        public float           DurationSeconds { get; }
        public IdleMiningStage Stage           { get; private set; }

        // True when this session was reconstructed from persisted state (app relaunch) rather
        // than started fresh this session — the drone should snap to the asteroid instead of
        // traveling there, since the travel already conceptually happened off-screen.
        public bool WasRestored { get; }

        public float MiningProgress01 =>
            Mathf.Clamp01((float)(DateTime.UtcNow - StartUtc).TotalSeconds / DurationSeconds);

        public event Action<IdleMiningStage> OnStageChanged;

        public IdleMiningSession(Asteroid asteroid, DateTime startUtc, float durationSeconds, bool restored = false)
        {
            Asteroid        = asteroid;
            StartUtc        = startUtc;
            DurationSeconds = Mathf.Max(0.01f, durationSeconds);
            WasRestored     = restored;
            Stage           = HasDurationElapsed() ? IdleMiningStage.ReadyToClaim : IdleMiningStage.Traveling;
        }

        // Drone has visually arrived at the asteroid. Flavor-only transition for HUD text —
        // does not affect the ReadyToClaim timing, which is purely wall-clock based.
        public void BeginMining()
        {
            if (Stage != IdleMiningStage.Traveling) return;
            SetStage(IdleMiningStage.Mining);
        }

        // Call every frame while the session is active. deltaTime is intentionally unused for
        // the readiness check (wall-clock driven) — it exists so callers can call this
        // uniformly alongside other per-frame Tick methods.
        public void Tick(float deltaTime)
        {
            if (Stage == IdleMiningStage.ReadyToClaim || Stage == IdleMiningStage.Complete) return;
            if (HasDurationElapsed())
                SetStage(IdleMiningStage.ReadyToClaim);
        }

        // Completes the session. No-op unless the session is ReadyToClaim.
        public void Claim()
        {
            if (Stage != IdleMiningStage.ReadyToClaim) return;
            SetStage(IdleMiningStage.Complete);
        }

        private bool HasDurationElapsed() =>
            (DateTime.UtcNow - StartUtc).TotalSeconds >= DurationSeconds;

        private void SetStage(IdleMiningStage stage)
        {
            Stage = stage;
            OnStageChanged?.Invoke(stage);
        }
    }
}
