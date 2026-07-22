using System;
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
        private readonly IBackendClient    _backend;

        public BootState(IAuthService auth, INetworkBootstrap network, SceneLoader sceneLoader,
            GameStateMachine fsm, IObjectResolver resolver, LifetimeScope rootScope, IBackendClient backend)
        {
            _auth        = auth;
            _network     = network;
            _sceneLoader = sceneLoader;
            _fsm         = fsm;
            _resolver    = resolver;
            _rootScope   = rootScope;
            _backend     = backend;
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

            // A leftover anonymous session (app killed mid-registration or
            // mid-password-reset before the quit-time sign-out ran) must never
            // walk into the game: guest play was removed, and anonymous sessions
            // exist only as a Cloud Code transport inside the Auth scene flows.
            // Signing out (credentials cleared) drops us into the normal
            // Auth-scene path below.
            if (_auth.IsSignedIn && _auth.IsAnonymous)
            {
                SULog.Info("Boot: restored session is anonymous — signing out");
                await _auth.SignOutAsync();
            }

            // A restored SSO session with no display name yet means the app
            // was quit while gated at AuthScreen's choose-name panel — route
            // through the Auth scene (below) instead of publishing
            // PlayerReadyEvent directly, so HandleSignedIn can show the panel
            // again rather than letting a nameless account into the game.
            if (_auth.IsSignedIn && !string.IsNullOrEmpty(_auth.DisplayName))
            {
                SULog.Info("Boot: session restored, skipping Auth scene");
                // AuthScreen normally publishes this on sign-in to bring chat/friends
                // online (see SocialServicesInitializer). Returning players skip that
                // screen entirely, so fire it here or chat never connects.
                EventBus.Publish(new PlayerReadyEvent());

                // A trip was already in progress last we heard (cached resume hint,
                // see SaveKeys.TravelTargetId) — resume into Travel instead of landing
                // back on the last planet; the Travel scene re-validates against the
                // server on entry.
                if (PlayerPrefs.HasKey(SaveKeys.TravelTargetId))
                {
                    SULog.Info("Boot: trip already in progress, resuming into Travel");
                    _fsm.TransitionTo(_resolver.Resolve<TravelState>());
                    return;
                }

                var planet = _resolver.Resolve<PlanetState>();
                planet.TargetPlanetId = await ResolveTargetPlanetIdAsync();
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
                SULog.Warn($"BootState: GetCurrentPlanet failed ({ex.Message}), using local state", SULog.Channel.Core);
            }

            string localPlanetId = PlayerPrefs.GetString(SaveKeys.LastPlanetIdKey(_auth.PlayerId), null);
            return PlanetResumeResolver.Resolve(serverPlanetId, localPlanetId, Constants.PlanetIds.Earth);
        }
    }
}
