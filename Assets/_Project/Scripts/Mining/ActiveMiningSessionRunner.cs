using UnityEngine;
using VContainer.Unity;
using SocialUniverse.Core;

namespace SocialUniverse.Mining
{
    // Drives the local ActiveMiningSession's countdown once the player presses Start —
    // relocates the role ActiveMiningSessionController played in PlanetSceneScope, now living
    // inside ActiveMiningSceneScope since Planet is unloaded while this scene runs. Also writes
    // the outcome back into ActiveMiningHandoff so MiningController can finalize the reward once
    // Planet reloads.
    public class ActiveMiningSessionRunner : IStartable, ITickable
    {
        private readonly ActiveMiningHandoff _handoff;

        public ActiveMiningSession Session   { get; private set; }
        public bool                IsRunning { get; private set; }

        public ActiveMiningSessionRunner(ActiveMiningHandoff handoff) => _handoff = handoff;

        public void Start()
        {
            Session = new ActiveMiningSession(_handoff.TapsRequired, _handoff.MaxErrors, _handoff.SessionDurationSeconds);
            Session.OnStageChanged += OnStageChanged;
        }

        // Called once the player presses "Start Mining" in the pre-game panel — nothing spawns
        // or counts down before this, so there's no race between scene-load and the first target.
        public void BeginTicking() => IsRunning = true;

        public void Tick()
        {
            if (IsRunning) Session.Tick(Time.deltaTime);
        }

        private void OnStageChanged(ActiveMiningStage stage)
        {
            if (stage == ActiveMiningStage.Success) _handoff.SetResult(succeeded: true);
            else if (stage == ActiveMiningStage.Failed) _handoff.SetResult(succeeded: false);
        }
    }
}
