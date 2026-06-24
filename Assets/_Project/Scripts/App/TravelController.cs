using System;
using VContainer.Unity;
using SocialUniverse.Config;
using SocialUniverse.Core;
using SocialUniverse.Travel;

namespace SocialUniverse.App
{
    // Reacts to TravelConfirmedEvent (published by PlanetPreviewPanel's own
    // Launch button, after the player has reviewed fuel cost/ETA): starts the
    // trip via TravelTripSystem (server-authoritative fuel spend + arrival
    // timestamp), then hands off to the FSM — HubState.EnterTravelState()
    // transitions to TravelLoadingState (takeoff leg, shows the rocket departing
    // from _currentPlanet — the planet the player is leaving, not the
    // destination), which then unloads SolarSystem and loads the Travel scene,
    // where TravelingPanel lives and Land eventually transitions through
    // TravelLoadingState's land leg (showing the destination planet) to PlanetState.
    public class TravelController : IStartable, IDisposable
    {
        private readonly TravelTripSystem _trips;
        private readonly HubState         _hub;
        private readonly PlanetDefinition _currentPlanet;

        public TravelController(TravelTripSystem trips, HubState hub, PlanetDefinition currentPlanet)
        {
            _trips         = trips;
            _hub           = hub;
            _currentPlanet = currentPlanet;
        }

        public void Start()   => EventBus.Subscribe<TravelConfirmedEvent>(OnTravelConfirmed);
        public void Dispose() => EventBus.Unsubscribe<TravelConfirmedEvent>(OnTravelConfirmed);

        private async void OnTravelConfirmed(TravelConfirmedEvent e)
        {
            var result = await _trips.StartTravelAsync(e.Planet);
            if (result == null || !result.Success)
            {
                SULog.Warn($"TravelController: travel to {e.Planet.DisplayName} denied ({result?.Reason})", SULog.Channel.Travel);
                return;
            }

            _hub.EnterTravelState(_currentPlanet);
        }
    }
}
