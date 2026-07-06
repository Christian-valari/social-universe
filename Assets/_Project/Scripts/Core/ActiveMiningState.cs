using System.Threading.Tasks;
using UnityEngine.SceneManagement;
using VContainer;

namespace SocialUniverse.Core
{
    // Owns the ActiveMining scene as the sole running gameplay scene — mirrors TravelState's
    // shape. Entered from PlanetState.EnterActiveMining() once MiningController.BeginActiveMining
    // has populated ActiveMiningHandoff; Planet is unloaded via PlanetState.Exit() before this
    // state's Enter() runs (GameStateMachine.TransitionTo calls Exit() then Enter()).
    public class ActiveMiningState : IGameState
    {
        private readonly SceneLoader        _sceneLoader;
        private readonly GameStateMachine    _fsm;
        private readonly IObjectResolver     _resolver;
        private readonly ActiveMiningHandoff _handoff;

        public ActiveMiningState(SceneLoader sceneLoader, GameStateMachine fsm, IObjectResolver resolver, ActiveMiningHandoff handoff)
        {
            _sceneLoader = sceneLoader;
            _fsm         = fsm;
            _resolver    = resolver;
            _handoff     = handoff;
        }

        public void Enter() => _ = LoadAsync();
        public void Tick()  { }
        public void Exit()  => _ = UnloadAsync();

        private async Task LoadAsync()
        {
            SULog.Info("ActiveMining: entering");
            await _sceneLoader.LoadAsync(Constants.SceneNames.LoadingScreen);
            await _sceneLoader.LoadAsync(Constants.SceneNames.ActiveMining);
        }

        private async Task UnloadAsync()
        {
            await _sceneLoader.UnloadAsync(Constants.SceneNames.ActiveMining);
            var ls = SceneManager.GetSceneByName(Constants.SceneNames.LoadingScreen);
            if (ls.IsValid() && ls.isLoaded)
                await _sceneLoader.UnloadAsync(Constants.SceneNames.LoadingScreen);
        }

        // Called by ActiveMiningMinigameView's Continue button once the session has resolved
        // (Success or Failed) and the reward preview has been shown. Hands control back to
        // Planet, which re-resolves the asteroid by SlotId and finalizes the reward server-side
        // (see MiningController.Initialize -> TryFinalizePendingActiveMining).
        public void Finish()
        {
            var planetState = _resolver.Resolve<PlanetState>();
            planetState.TargetPlanetId = _handoff.PlanetId;
            _fsm.TransitionTo(planetState);
        }
    }
}
