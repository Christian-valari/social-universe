using System.Threading.Tasks;
using UnityEngine.SceneManagement;
using VContainer;

namespace SocialUniverse.Core
{
    // Owns the Travel scene — shown while a trip started from the Hub is in
    // transit. Mirrors PlanetState's load/unload shape; the Travel scene itself
    // (TravelSceneScope, App layer) hosts the traveling UI and re-validates trip
    // state against the server on entry, since this state only handles the
    // scene load/unload + FSM wiring.
    public class TravelState : IGameState
    {
        private readonly SceneLoader _sceneLoader;

        // Set by TravelLoadingState right before transitioning in, for the
        // SolarSystem -> TravelLoading -> Travel leg — the takeoff animation
        // already covers the transition, so the generic LoadingScreen would
        // just be extra. Direct entries (e.g. resuming an in-progress trip from
        // BootState/HubState) leave this false and get the normal LoadingScreen.
        public bool SkipLoadingScreen { get; set; }

        public TravelState(SceneLoader sceneLoader)
        {
            _sceneLoader = sceneLoader;
        }

        public void Enter() => _ = LoadAsync();
        public void Tick()  { }
        public void Exit()  => _ = UnloadAsync();

        private async Task LoadAsync()
        {
            SULog.Info("Travel: entering");
            bool skip = SkipLoadingScreen;
            SkipLoadingScreen = false; // consumed — next entry defaults back to showing it
            if (!skip)
                await _sceneLoader.LoadAsync(Constants.SceneNames.LoadingScreen);
            await _sceneLoader.LoadAsync(Constants.SceneNames.Travel);
        }

        private async Task UnloadAsync()
        {
            await _sceneLoader.UnloadAsync(Constants.SceneNames.Travel);
            var ls = SceneManager.GetSceneByName(Constants.SceneNames.LoadingScreen);
            if (ls.IsValid() && ls.isLoaded)
                await _sceneLoader.UnloadAsync(Constants.SceneNames.LoadingScreen);
        }
    }
}
