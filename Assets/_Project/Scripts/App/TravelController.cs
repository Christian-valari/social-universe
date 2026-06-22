using System;
using VContainer.Unity;
using SocialUniverse.Core;
using SocialUniverse.Travel;

namespace SocialUniverse.App
{
    // Reacts to TravelRequestedEvent (published by StarMapController/
    // SkyDiscoveryController): validates+spends fuel via TravelService, plays
    // the rocket departure overlay, then hands off to the existing FSM scene
    // transition (HubState.TravelToPlanet -> PlanetState, which shows the
    // LoadingScreen). Owns the full sequencing so the UI layer never touches
    // fuel or scenes directly.
    public class TravelController : IStartable, IDisposable
    {
        private readonly TravelService       _travelService;
        private readonly RocketController    _rocket;
        private readonly HubState            _hub;

        public TravelController(TravelService travelService, RocketController rocket, HubState hub)
        {
            _travelService = travelService;
            _rocket        = rocket;
            _hub           = hub;
        }

        public void Start()   => EventBus.Subscribe<TravelRequestedEvent>(OnTravelRequested);
        public void Dispose() => EventBus.Unsubscribe<TravelRequestedEvent>(OnTravelRequested);

        private async void OnTravelRequested(TravelRequestedEvent e)
        {
            var result = await _travelService.TravelToPlanetAsync(e.Planet);
            if (!result.Success)
            {
                SULog.Warn($"TravelController: travel to {e.Planet.DisplayName} denied ({result.FailureReason})", SULog.Channel.Travel);
                return;
            }

            await _rocket.PlayDepartureAsync(e.Planet.DisplayName);
            _hub.TravelToPlanet(e.Planet.PlanetId);
        }
    }
}
