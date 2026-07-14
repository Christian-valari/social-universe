using System;
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
        private readonly IBackendClient   _backend;

        public AuthState(IAuthService auth, GameStateMachine fsm, SceneLoader sceneLoader, IObjectResolver resolver,
            IBackendClient backend)
        {
            _auth        = auth;
            _fsm         = fsm;
            _sceneLoader = sceneLoader;
            _resolver    = resolver;
            _backend     = backend;
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
            planet.TargetPlanetId = await ResolveTargetPlanetIdAsync();
            _fsm.TransitionTo(planet);
        }

        // Server record (cross-device source of truth) wins over the local
        // PlayerPrefs resume hint, which wins over the hard Earth default —
        // see PlanetResumeResolver. Non-fatal on failure, same hydration
        // convention as PlanetSceneScope.HydrateServerStateAsync.
        private async Task<string> ResolveTargetPlanetIdAsync()
        {
            string serverPlanetId = null;
            try
            {
                var result = await _backend.CallAsync<CurrentPlanetResult>("GetCurrentPlanet");
                serverPlanetId = result?.PlanetId;
            }
            catch (Exception ex)
            {
                SULog.Warn($"AuthState: GetCurrentPlanet failed ({ex.Message}), using local state", SULog.Channel.Core);
            }

            string localPlanetId = PlayerPrefs.GetString(SaveKeys.LastPlanetIdKey(_auth.PlayerId), null);
            return PlanetResumeResolver.Resolve(serverPlanetId, localPlanetId, Constants.PlanetIds.Earth);
        }
    }
}
