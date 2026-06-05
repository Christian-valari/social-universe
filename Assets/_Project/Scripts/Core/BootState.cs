using System.Threading.Tasks;
using VContainer;

namespace SocialUniverse.Core
{
    public class BootState : IGameState
    {
        private readonly SceneLoader    _sceneLoader;
        private readonly GameStateMachine _fsm;
        private readonly IObjectResolver  _resolver;

        public BootState(SceneLoader sceneLoader, GameStateMachine fsm, IObjectResolver resolver)
        {
            _sceneLoader = sceneLoader;
            _fsm         = fsm;
            _resolver    = resolver;
        }

        public void Enter() => _ = RunAsync();
        public void Tick()  { }
        public void Exit()  { }

        private async Task RunAsync()
        {
            SULog.Info("Boot: loading Auth scene");
            await _sceneLoader.LoadAsync(Constants.SceneNames.Auth);
            _fsm.TransitionTo(_resolver.Resolve<AuthState>());
        }
    }
}
