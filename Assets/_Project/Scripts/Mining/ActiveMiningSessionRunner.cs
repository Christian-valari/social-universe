using UnityEngine;
using VContainer.Unity;
using SocialUniverse.Config;
using SocialUniverse.Core;

namespace SocialUniverse.Mining
{
    // Creates this scene's ActiveMiningSession once the player presses Start and ticks it from
    // then on — relocates the role ActiveMiningSessionController played in PlanetSceneScope, now
    // living inside ActiveMiningSceneScope since Planet is unloaded while this scene runs. Also
    // writes the outcome back into ActiveMiningHandoff so MiningController can finalize the reward
    // once Planet reloads.
    public class ActiveMiningSessionRunner : ITickable
    {
        private readonly ActiveMiningHandoff _handoff;

        public ActiveMiningSession Session { get; private set; }

        public ActiveMiningSessionRunner(ActiveMiningHandoff handoff) => _handoff = handoff;

        // Called once the player presses "Start Mining" in the pre-game panel — nothing spawns
        // before this, so there's no race between scene-load and the first target.
        public ActiveMiningSession BeginRun()
        {
            Session = new ActiveMiningSession(_handoff.Settings ?? DefaultSettings());

            // Subscribed before the session reaches the view: its result screen reads the banked
            // quantity from the handoff, so this handler has to run first.
            Session.OnStageChanged += OnStageChanged;
            return Session;
        }

        public void Tick() => Session?.Tick(Time.deltaTime);

        private void OnStageChanged(ActiveMiningStage stage)
        {
            if (stage == ActiveMiningStage.InProgress) return;

            _handoff.SetResult(stage == ActiveMiningStage.Success, Session.BankedFraction);
        }

        // A standalone ActiveMining scene has no run begun, so it plays the default (Tier 1) settings.
        private static ActiveMiningRunSettings DefaultSettings()
        {
            var config   = ScriptableObject.CreateInstance<EconomyConfig>();
            var settings = config.ActiveRunSettings(1);
            Object.Destroy(config);
            return settings;
        }
    }
}
