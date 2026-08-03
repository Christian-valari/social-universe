using System.Threading.Tasks;
using UnityEngine.SceneManagement;
using VContainer;

namespace SocialUniverse.Core
{
    // Owns the LandBuilding scene as the sole running gameplay scene — mirrors ActiveMiningState.
    // Entered from PlanetState.EnterLandBuilding() after LandBuildingHandoff is populated; Planet
    // is unloaded via PlanetState.Exit() before this state's Enter() runs.
    public class LandBuildingState : IGameState
    {
        private readonly SceneLoader        _sceneLoader;
        private readonly GameStateMachine   _fsm;
        private readonly IObjectResolver    _resolver;
        private readonly LandBuildingHandoff _handoff;

        public LandBuildingState(SceneLoader sceneLoader, GameStateMachine fsm, IObjectResolver resolver, LandBuildingHandoff handoff)
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
            SULog.Info($"LandBuilding: entering tile {_handoff.TileId} (canEdit={_handoff.CanEdit})");
            await _sceneLoader.LoadAsync(Constants.SceneNames.LoadingScreen);
            await _sceneLoader.LoadAsync(Constants.SceneNames.LandBuilding);
        }

        private async Task UnloadAsync()
        {
            await _sceneLoader.UnloadAsync(Constants.SceneNames.LandBuilding);
            var ls = SceneManager.GetSceneByName(Constants.SceneNames.LoadingScreen);
            if (ls.IsValid() && ls.isLoaded)
                await _sceneLoader.UnloadAsync(Constants.SceneNames.LoadingScreen);
        }

        // Called by the LandBuilding scene's Back button. Returns to the planet the player
        // came from; Planet re-hydrates the land registry + wallet from the server on entry
        // (PlanetSceneBootstrapper.HydrateServerStateAsync), so any builds made here are reflected.
        public void Finish()
        {
            var planetState = _resolver.Resolve<PlanetState>();
            planetState.TargetPlanetId = _handoff.PlanetId;
            _handoff.Clear();
            _fsm.TransitionTo(planetState);
        }
    }
}
