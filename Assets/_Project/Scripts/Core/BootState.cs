using System.Threading.Tasks;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace SocialUniverse.Core
{
    public class BootState : IGameState
    {
        private readonly IAuthService      _auth;
        private readonly INetworkBootstrap _network;
        private readonly SceneLoader       _sceneLoader;
        private readonly GameStateMachine  _fsm;
        private readonly IObjectResolver   _resolver;
        private readonly LifetimeScope     _rootScope;

        public BootState(IAuthService auth, INetworkBootstrap network, SceneLoader sceneLoader,
            GameStateMachine fsm, IObjectResolver resolver, LifetimeScope rootScope)
        {
            _auth        = auth;
            _network     = network;
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
            SULog.Info("Boot: initializing network");
            await _network.InitializeAsync();
            await _auth.InitializeAsync();

            // Returning player — try to resume their session via the cached session
            // token before falling back to the Auth UI.
            if (!_auth.IsSignedIn)
                await _auth.TryAutoSignInAsync();

            if (_auth.IsSignedIn)
            {
                SULog.Info("Boot: session restored, skipping Auth scene");
                // AuthScreen normally publishes this on sign-in to bring chat/friends
                // online (see SocialServicesInitializer). Returning players skip that
                // screen entirely, so fire it here or chat never connects.
                EventBus.Publish(new PlayerReadyEvent());
                var planet = _resolver.Resolve<PlanetState>();
                planet.TargetPlanetId = PlayerPrefs.GetString(SaveKeys.LastPlanetId, Constants.PlanetIds.Earth);
                _fsm.TransitionTo(planet);
                return;
            }

            SULog.Info("Boot: loading Auth scene");
            // EnqueueParent ensures the Auth scene's LifetimeScope inherits this container,
            // so AuthSceneScope can resolve IAuthService from the root rather than creating a mock.
            using (LifetimeScope.EnqueueParent(_rootScope))
            {
                await _sceneLoader.LoadAsync(Constants.SceneNames.Auth);
            }
            _fsm.TransitionTo(_resolver.Resolve<AuthState>());
        }
    }
}
