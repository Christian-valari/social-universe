using System.Threading.Tasks;
using VContainer;
using VContainer.Unity;

namespace SocialUniverse.Core
{
    // Reverses BootState/AuthState: signs the player out, publishes
    // PlayerLoggedOutEvent, reloads the Auth scene under the root container
    // (same LifetimeScope.EnqueueParent trick BootState uses so AuthSceneScope
    // can resolve IAuthService from root), and hands off to AuthState — which
    // then behaves exactly like a fresh cold launch with no cached session.
    //
    // Transitioning into this state from PlanetState/HubState already triggers
    // the outgoing state's Exit() via GameStateMachine.TransitionTo, which
    // unloads its scene(s) — no scene-teardown logic is duplicated here.
    public class LogoutState : IGameState
    {
        private readonly IAuthService     _auth;
        private readonly SceneLoader      _sceneLoader;
        private readonly GameStateMachine _fsm;
        private readonly IObjectResolver  _resolver;
        private readonly LifetimeScope    _rootScope;

        public LogoutState(IAuthService auth, SceneLoader sceneLoader, GameStateMachine fsm,
            IObjectResolver resolver, LifetimeScope rootScope)
        {
            _auth        = auth;
            _sceneLoader = sceneLoader;
            _fsm         = fsm;
            _resolver    = resolver;
            _rootScope   = rootScope;
        }

        public void Enter() => _ = RunAsync();
        public void Tick()  { }
        public void Exit()  { }

        private async Task RunAsync()
        {
            try
            {
                SULog.Info("Logout: signing out");
                await _auth.SignOutAsync();
                EventBus.Publish(new PlayerLoggedOutEvent());

                using (LifetimeScope.EnqueueParent(_rootScope))
                {
                    await _sceneLoader.LoadAsync(Constants.SceneNames.Auth);
                }
                _fsm.TransitionTo(_resolver.Resolve<AuthState>());
            }
            catch (System.Exception ex)
            {
                SULog.Error($"Logout failed: {ex.Message}", SULog.Channel.Core);
            }
        }
    }
}
