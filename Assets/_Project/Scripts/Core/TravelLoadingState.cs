using System;
using System.Threading.Tasks;
using SocialUniverse.Config;
using VContainer;

namespace SocialUniverse.Core
{
    public enum TravelLoadingMode { TakeOff, Land }

    // Shown for both legs of a real trip: SolarSystem -> Travel (TakeOff, set up
    // by HubState.EnterTravelState, shows the player's CURRENT planet — you take
    // off from where you are) and Travel -> Planet (Land, set up by
    // HubState.LandOnPlanet, shows the DESTINATION planet — you land on where
    // you're arriving). Mode and PlanetToShow must be set on the resolved
    // singleton before transitioning in — same pattern as PlanetState.TargetPlanetId.
    // Not used by HubState.TravelToPlanet (the instant "Return Home" shortcut,
    // which isn't a real trip and skips this ceremony entirely).
    // Owns only the TravelLoading scene's load/unload + FSM wiring (deliberately
    // skipping the generic LoadingScreen on both ends — the takeoff/land animation
    // already covers the transition); the visual (planet swap, takeOff/land
    // Animator state) lives in TravelLoadingController (Travel module), reached
    // via TravelLoadingTakeOffRequestedEvent/TravelLoadingLandRequestedEvent
    // rather than a direct reference, since Core cannot depend on Travel.
    public class TravelLoadingState : IGameState
    {
        // Matches the authored length of Assets/Animation/Rocket/takeOff.anim and
        // land.anim (7s), plus a small buffer so the animation is never cut short.
        private const float AnimationSeconds = 7.2f;

        private readonly SceneLoader      _sceneLoader;
        private readonly GameStateMachine _fsm;
        private readonly IObjectResolver  _resolver;

        public TravelLoadingMode Mode         { get; set; }
        public PlanetDefinition  PlanetToShow { get; set; } // current planet for TakeOff, destination planet for Land

        public TravelLoadingState(SceneLoader sceneLoader, GameStateMachine fsm, IObjectResolver resolver)
        {
            _sceneLoader = sceneLoader;
            _fsm         = fsm;
            _resolver    = resolver;
        }

        public void Enter() => _ = EnterAsync();
        public void Tick()  { }
        public void Exit()  => _ = _sceneLoader.UnloadAsync(Constants.SceneNames.TravelLoading);

        private async Task EnterAsync()
        {
            SULog.Info($"TravelLoading: entering ({Mode})");
            // Deliberately skips LoadingScreen — the takeoff/land animation in this
            // scene already covers the transition, so the generic loading screen
            // would just be redundant on top of it.
            await _sceneLoader.LoadAsync(Constants.SceneNames.TravelLoading);

            if (Mode == TravelLoadingMode.TakeOff)
                EventBus.Publish(new TravelLoadingTakeOffRequestedEvent { Planet = PlanetToShow });
            else
                EventBus.Publish(new TravelLoadingLandRequestedEvent { Planet = PlanetToShow });

            await Task.Delay(TimeSpan.FromSeconds(AnimationSeconds));

            if (Mode == TravelLoadingMode.TakeOff)
            {
                var travel = _resolver.Resolve<TravelState>();
                travel.SkipLoadingScreen = true;
                _fsm.TransitionTo(travel);
            }
            else
            {
                var planet = _resolver.Resolve<PlanetState>();
                planet.SkipLoadingScreen = true;
                _fsm.TransitionTo(planet);
            }
        }
    }
}
