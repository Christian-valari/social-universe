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
        private readonly PlayerDataSyncPolicy _syncPolicy;

        public AuthState(IAuthService auth, GameStateMachine fsm, SceneLoader sceneLoader, IObjectResolver resolver,
            IBackendClient backend, PlayerDataSyncPolicy syncPolicy)
        {
            _auth        = auth;
            _fsm         = fsm;
            _sceneLoader = sceneLoader;
            _resolver    = resolver;
            _backend     = backend;
            _syncPolicy  = syncPolicy;
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
            await ResumeAsync();
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
                SULog.Info($"Auth: trip to {trip} not landed yet, resuming into Travel");
                _fsm.TransitionTo(_resolver.Resolve<TravelState>());
                return;
            }

            var planet = _resolver.Resolve<PlanetState>();
            planet.TargetPlanetId = PlanetResumeResolver.Resolve(server?.PlanetId,
                PlayerPrefs.GetString(SaveKeys.LastPlanetIdKey(_auth.PlayerId), null), Constants.PlanetIds.Earth);
            SULog.Info($"Auth: resuming on {planet.TargetPlanetId} (server planet: {server?.PlanetId ?? "unavailable"})");
            _fsm.TransitionTo(planet);
        }
    }
}
