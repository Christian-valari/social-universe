using System.Threading.Tasks;
using UnityEngine;
using VContainer;

namespace SocialUniverse.Core
{
    public class AuthState : IGameState
    {
        private readonly IAuthService     _auth;
        private readonly GameStateMachine _fsm;
        private readonly SceneLoader      _sceneLoader;
        private readonly IObjectResolver  _resolver;

        public AuthState(IAuthService auth, GameStateMachine fsm, SceneLoader sceneLoader, IObjectResolver resolver)
        {
            _auth        = auth;
            _fsm         = fsm;
            _sceneLoader = sceneLoader;
            _resolver    = resolver;
        }

        public void Enter()
        {
            // Returning player whose session is still valid — skip Auth UI entirely.
            if (_auth.IsSignedIn)
            {
                _ = TransitionToPlanetAsync();
                return;
            }

            // New session — wait for the player to sign in and confirm via Continue.
            EventBus.Subscribe<PlayerReadyEvent>(OnPlayerReady);
        }

        public void Tick() { }

        public void Exit()
        {
            EventBus.Unsubscribe<PlayerReadyEvent>(OnPlayerReady);
        }

        private async void OnPlayerReady(PlayerReadyEvent evt) => await TransitionToPlanetAsync();

        private async Task TransitionToPlanetAsync()
        {
            EventBus.Unsubscribe<PlayerReadyEvent>(OnPlayerReady);
            await _sceneLoader.UnloadAsync(Constants.SceneNames.Auth);
            var planet = _resolver.Resolve<PlanetState>();
            planet.TargetPlanetId = PlayerPrefs.GetString(SaveKeys.LastPlanetIdKey(_auth.PlayerId), Constants.PlanetIds.Earth);
            _fsm.TransitionTo(planet);
        }
    }
}
