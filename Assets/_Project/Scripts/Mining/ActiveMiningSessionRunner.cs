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
    // once Planet reloads, and keeps the run's local marker up to date in case the app closes first.
    public class ActiveMiningSessionRunner : ITickable
    {
        private readonly ActiveMiningHandoff  _handoff;
        private readonly ActiveMiningRunStore _store;

        public ActiveMiningSession Session { get; private set; }

        public ActiveMiningSessionRunner(ActiveMiningHandoff handoff, ActiveMiningRunStore store)
        {
            _handoff = handoff;
            _store   = store;
        }

        // Called once the player presses "Start Mining" in the pre-game panel — nothing spawns
        // before this, so there's no race between scene-load and the first target.
        public ActiveMiningSession BeginRun()
        {
            Session = new ActiveMiningSession(_handoff.Settings ?? DefaultSettings());

            // Subscribed before the session reaches the view: its result screen reads the banked
            // quantity from the handoff, so this handler has to run first.
            Session.OnStageChanged += OnStageChanged;
            SaveMarker(0f);
            return Session;
        }

        // For when the app is paused or quit. Deliberately not called on each hit: writing
        // PlayerPrefs to disk can hitch on a phone.
        public void SaveProgress()
        {
            if (Session != null) SaveMarker(Session.BankedFraction);
        }

        public void Tick() => Session?.Tick(Time.deltaTime);

        private void OnStageChanged(ActiveMiningStage stage)
        {
            if (stage == ActiveMiningStage.InProgress) return;

            SaveMarker(Session.BankedFraction);
            _handoff.SetResult(stage == ActiveMiningStage.Success, Session.BankedFraction);
        }

        // No slot means no asteroid to settle later: a standalone scene, or Planet has already taken
        // the run back from the handoff.
        private void SaveMarker(float bankedFraction)
        {
            if (_handoff.AsteroidSlotId == null) return;

            _store.Save(_handoff.PlanetId, _handoff.AsteroidSlotId, _handoff.MineralId, _handoff.FullQuantity, bankedFraction);
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
