using System.Threading.Tasks;
using UnityEngine.SceneManagement;
using VContainer;

namespace SocialUniverse.Core
{
    public class PlanetState : IGameState
    {
        private readonly SceneLoader      _sceneLoader;
        private readonly GameStateMachine _fsm;
        private readonly IObjectResolver  _resolver;

        public string TargetPlanetId { get; set; }

        // Set by TravelLoadingState right before transitioning in, for the
        // Travel -> TravelLoading -> Planet leg — see TravelState.SkipLoadingScreen
        // for the same reasoning. Direct entries (initial boot resume, Return Home)
        // leave this false.
        public bool SkipLoadingScreen { get; set; }

        public PlanetState(SceneLoader sceneLoader, GameStateMachine fsm, IObjectResolver resolver)
        {
            _sceneLoader = sceneLoader;
            _fsm         = fsm;
            _resolver    = resolver;
        }

        public void Enter() => _ = LoadAsync();
        public void Tick()  { }
        public void Exit()  => _ = UnloadAsync();

        private async Task LoadAsync()
        {
            SULog.Info($"Planet: entering {TargetPlanetId}");
            bool skip = SkipLoadingScreen;
            SkipLoadingScreen = false; // consumed — next entry defaults back to showing it
            if (!skip)
                await _sceneLoader.LoadAsync(Constants.SceneNames.LoadingScreen);
            await _sceneLoader.LoadAsync(Constants.SceneNames.Planet);
        }

        private async Task UnloadAsync()
        {
            await _sceneLoader.UnloadAsync(Constants.SceneNames.Planet);
            // LoadingScreen self-unloads via PlanetSceneReadyEvent; guard for early exits
            var ls = SceneManager.GetSceneByName(Constants.SceneNames.LoadingScreen);
            if (ls.IsValid() && ls.isLoaded)
                await _sceneLoader.UnloadAsync(Constants.SceneNames.LoadingScreen);
        }

        public void ReturnToHub() => _fsm.TransitionTo(_resolver.Resolve<HubState>());

        // Called by ActiveMiningRequestHandler once MiningController.BeginActiveMining has
        // populated ActiveMiningHandoff — transitions the FSM to ActiveMiningState, which loads
        // the minigame scene as the sole running gameplay scene (Exit() below unloads Planet).
        public void EnterActiveMining() => _fsm.TransitionTo(_resolver.Resolve<ActiveMiningState>());
    }
}
