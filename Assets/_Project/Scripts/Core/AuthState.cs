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
            // Wait for the AuthScreen flow to confirm a game-ready session and publish
            // PlayerReadyEvent. AuthScreen.Start already handles a restored session: it
            // shows the Verify panel for an unverified account, or publishes
            // PlayerReadyEvent for a verified one. A verified resume never reaches this
            // state — BootState skips it straight to Planet — so AuthState must NOT
            // fast-forward a merely-signed-in (possibly unverified) session into the
            // game: that would bypass email verification and skip PlayerReadyEvent,
            // leaving chat/social uninitialized.
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
