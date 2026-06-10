using System.Threading.Tasks;
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
            await _sceneLoader.LoadAsync(Constants.SceneNames.SolarSystem);
        }

        private async Task UnloadSolarSystemAsync()
        {
            SULog.Info("Hub: unloading SolarSystem");
            await _sceneLoader.UnloadAsync(Constants.SceneNames.SolarSystem);
        }

        public void TravelToPlanet(string planetId)
        {
            var planet = _resolver.Resolve<PlanetState>();
            planet.TargetPlanetId = planetId;
            _fsm.TransitionTo(planet);
        }
    }
}
