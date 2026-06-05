using System.Threading.Tasks;
using VContainer;

namespace SocialUniverse.Core
{
    public class PlanetState : IGameState
    {
        private readonly SceneLoader      _sceneLoader;
        private readonly GameStateMachine _fsm;
        private readonly IObjectResolver  _resolver;

        public string TargetPlanetId { get; set; }

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
            await _sceneLoader.LoadAsync(Constants.SceneNames.Planet);
        }

        private async Task UnloadAsync()
        {
            await _sceneLoader.UnloadAsync(Constants.SceneNames.Planet);
        }

        public void ReturnToHub() => _fsm.TransitionTo(_resolver.Resolve<HubState>());
    }
}
