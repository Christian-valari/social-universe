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
        private readonly PlayerDataSyncPolicy _syncPolicy;

        public BootState(IAuthService auth, INetworkBootstrap network, SceneLoader sceneLoader,
            GameStateMachine fsm, IObjectResolver resolver, LifetimeScope rootScope, IBackendClient backend,
            PlayerDataSyncPolicy syncPolicy)
        {
            _auth        = auth;
            _network     = network;
            _sceneLoader = sceneLoader;
            _fsm         = fsm;
            _resolver    = resolver;
            _rootScope   = rootScope;
            _backend     = backend;
            _syncPolicy  = syncPolicy;
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

            // Skip the Auth scene only for a fully game-ready session: signed in AND
            // email-verified. An unverified email account must finish verification in
            // the Auth scene first (Google accounts report verified and pass here). A
            // verified account with no display name yet is fine to enter — the Planet
            // scene's onboarding modal collects the name (ShowProfileOnboardingEvent),
            // so a name is deliberately NOT required here (that would bounce nameless
            // Google accounts through an Auth-scene detour on every launch).
            if (_auth.IsSignedIn && _auth.IsEmailVerified)
            {
                SULog.Info("Boot: session restored, skipping Auth scene");
                // AuthScreen normally publishes this on sign-in to bring chat/friends
                // online (see SocialServicesInitializer). Returning players skip that
                // screen entirely, so fire it here or chat never connects.
                EventBus.Publish(new PlayerReadyEvent());
                await ResumeAsync();
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

        // Where to resume, from the server's record (cross-device source of truth):
        // a trip not landed yet — started on this device or any other — resumes
        // into Travel (the Travel scene re-validates on entry), otherwise the
        // server's current planet. The local PlayerPrefs hints are rewritten to
        // mirror the server and are read only if GetCurrentPlanet fails, which
        // only dev mode lets through (PlayerDataSyncPolicy) — see PlanetResumeResolver.
        private async Task ResumeAsync()
        {
            // With the real backend a failed read is retried behind the retry dialog until it loads
            // (PlayerDataPull), so the hints below are only read by dev mode.
            CurrentPlanetResult server = null;
            await PlayerDataPull.RunAsync(new (string, Func<Task>)[]
                {
                    ("current planet", async () => server = await _backend.CallAsync<CurrentPlanetResult>("GetCurrentPlanet")),
                },
                _syncPolicy?.Required ?? false, _syncPolicy?.AskToRetry);

            string trip = PlanetResumeResolver.ResolveTravelTarget(server != null, server?.TravelTargetId,
                PlayerPrefs.GetString(SaveKeys.TravelTargetId, null));
            if (trip != null) PlayerPrefs.SetString(SaveKeys.TravelTargetId, trip);
            else              PlayerPrefs.DeleteKey(SaveKeys.TravelTargetId);
            if (!string.IsNullOrEmpty(server?.PlanetId))
                PlayerPrefs.SetString(SaveKeys.LastPlanetIdKey(_auth.PlayerId), server.PlanetId);
            PlayerPrefs.Save();

            if (trip != null)
            {
                SULog.Info($"Boot: trip to {trip} not landed yet, resuming into Travel");
                _fsm.TransitionTo(_resolver.Resolve<TravelState>());
                return;
            }

            var planet = _resolver.Resolve<PlanetState>();
            planet.TargetPlanetId = PlanetResumeResolver.Resolve(server?.PlanetId,
                PlayerPrefs.GetString(SaveKeys.LastPlanetIdKey(_auth.PlayerId), null), Constants.PlanetIds.Earth);
            SULog.Info($"Boot: resuming on {planet.TargetPlanetId} (server planet: {server?.PlanetId ?? "unavailable"})");
            _fsm.TransitionTo(planet);
        }
    }
}
