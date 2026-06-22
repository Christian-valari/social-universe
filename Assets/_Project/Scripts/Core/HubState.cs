using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer;

namespace SocialUniverse.Core
{
    public class HubState : IGameState
    {
        private readonly SceneLoader      _sceneLoader;
        private readonly GameStateMachine _fsm;
        private readonly IObjectResolver  _resolver;

        public HubState(SceneLoader sceneLoader, GameStateMachine fsm, IObjectResolver resolver)
        {
            _sceneLoader = sceneLoader;
            _fsm         = fsm;
            _resolver    = resolver;
        }

        public void Enter() => _ = EnterAsync();
        public void Tick()  { }
        public void Exit()  => _ = UnloadSolarSystemAsync();

        private async Task EnterAsync()
        {
            SULog.Info("Hub: loading SolarSystem");
            await _sceneLoader.LoadAsync(Constants.SceneNames.LoadingScreen);
            await _sceneLoader.LoadAsync(Constants.SceneNames.SolarSystem);
        }

        private async Task UnloadSolarSystemAsync()
        {
            SULog.Info("Hub: unloading SolarSystem");
            await _sceneLoader.UnloadAsync(Constants.SceneNames.SolarSystem);
            // LoadingScreen self-unloads via SceneReadyEvent; guard for early exits.
            var ls = SceneManager.GetSceneByName(Constants.SceneNames.LoadingScreen);
            if (ls.IsValid() && ls.isLoaded)
                await _sceneLoader.UnloadAsync(Constants.SceneNames.LoadingScreen);
        }

        public void TravelToPlanet(string planetId)
        {
            var planet = _resolver.Resolve<PlanetState>();
            planet.TargetPlanetId = planetId;

            // Persisted locally (not server state) so the next launch resumes on the
            // last-visited planet instead of always landing back on Earth.
            PlayerPrefs.SetString(SaveKeys.LastPlanetId, planetId);
            PlayerPrefs.Save();

            _fsm.TransitionTo(planet);
        }
    }
}
