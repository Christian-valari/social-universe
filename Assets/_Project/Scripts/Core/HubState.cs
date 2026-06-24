using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer;
using SocialUniverse.Config;

namespace SocialUniverse.Core
{
    public class HubState : IGameState
    {
        private readonly SceneLoader      _sceneLoader;
        private readonly GameStateMachine _fsm;
        private readonly IObjectResolver  _resolver;

        public HubState(SceneLoader sceneLoader, GameStateMachine fsm, IObjectResolver resolver)
        {
            _sceneLoader = sceneLoader;
            _fsm         = fsm;
            _resolver    = resolver;
        }

        public void Enter() => _ = EnterAsync();
        public void Tick()  { }
        public void Exit()  => _ = UnloadSolarSystemAsync();

        private async Task EnterAsync()
        {
            // A trip was already in progress last we heard (cached resume hint,
            // see SaveKeys.TravelTargetId) — skip Hub and TravelLoading entirely
            // (there's no fresh destination to show taking off, and the trip is
            // already mid-flight) and go straight to the Travel scene, which
            // re-validates against the server on entry.
            if (PlayerPrefs.HasKey(SaveKeys.TravelTargetId))
            {
                SULog.Info("Hub: trip already in progress, entering Travel instead");
                _fsm.TransitionTo(_resolver.Resolve<TravelState>());
                return;
            }

            SULog.Info("Hub: loading SolarSystem");
            await _sceneLoader.LoadAsync(Constants.SceneNames.LoadingScreen);
            await _sceneLoader.LoadAsync(Constants.SceneNames.SolarSystem);
        }

        // Called by TravelController once a trip has been successfully started
        // from the Hub — transitions the FSM to TravelLoadingState (takeoff leg),
        // which shows the rocket departing from the player's current planet (not
        // the destination — you take off from where you are) before handing off
        // to TravelState.
        public void EnterTravelState(PlanetDefinition currentPlanet)
        {
            var loading = _resolver.Resolve<TravelLoadingState>();
            loading.Mode         = TravelLoadingMode.TakeOff;
            loading.PlanetToShow = currentPlanet;
            _fsm.TransitionTo(loading);
        }

        private async Task UnloadSolarSystemAsync()
        {
            // Guard: Enter() may have redirected straight to TravelState without
            // ever loading SolarSystem (see the resume check above) — nothing to unload.
            var ss = SceneManager.GetSceneByName(Constants.SceneNames.SolarSystem);
            if (ss.IsValid() && ss.isLoaded)
            {
                SULog.Info("Hub: unloading SolarSystem");
                await _sceneLoader.UnloadAsync(Constants.SceneNames.SolarSystem);
            }

            // LoadingScreen self-unloads via SceneReadyEvent; guard for early exits.
            var ls = SceneManager.GetSceneByName(Constants.SceneNames.LoadingScreen);
            if (ls.IsValid() && ls.isLoaded)
                await _sceneLoader.UnloadAsync(Constants.SceneNames.LoadingScreen);
        }

        // Instant transition, no trip ceremony — used by ReturnHomeButtonController
        // for "go inside the planet you're already orbiting," which isn't a real
        // trip (no fuel cost, no Travel scene) so it skips TravelLoadingState entirely.
        public void TravelToPlanet(PlanetDefinition planet)
        {
            var state = _resolver.Resolve<PlanetState>();
            state.TargetPlanetId = planet.PlanetId;

            // Persisted locally (not server state) so the next launch resumes on the
            // last-visited planet instead of always landing back on Earth.
            PlayerPrefs.SetString(SaveKeys.LastPlanetId, planet.PlanetId);
            PlayerPrefs.Save();

            _fsm.TransitionTo(state);
        }

        // Called by TravelingPanel's Land button once a real trip's arrival has
        // been confirmed server-side — routes through TravelLoadingState (land
        // leg), which shows the rocket landing on planet before handing off to
        // PlanetState (TargetPlanetId is already set below, on the same singleton
        // instance TravelLoadingState resolves next).
        public void LandOnPlanet(PlanetDefinition planet)
        {
            var state = _resolver.Resolve<PlanetState>();
            state.TargetPlanetId = planet.PlanetId;

            PlayerPrefs.SetString(SaveKeys.LastPlanetId, planet.PlanetId);
            PlayerPrefs.Save();

            var loading = _resolver.Resolve<TravelLoadingState>();
            loading.Mode         = TravelLoadingMode.Land;
            loading.PlanetToShow = planet;
            _fsm.TransitionTo(loading);
        }
    }
}
