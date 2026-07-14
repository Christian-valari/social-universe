using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using SocialUniverse.Config;
using SocialUniverse.Core;
using SocialUniverse.Progression;

namespace SocialUniverse.Travel
{
    // Response shape for the "StartTravel"/"GetTravelState"/"LandTravel" Cloud
    // Code functions. Public so tests can construct it for a fake IBackendClient.
    public class TravelTripResult
    {
        public bool   Success;
        public string Reason;
        public bool   Traveling;
        public string TargetPlanetId;
        public long   ArrivalTs;
        public float  Fuel    = -1f;
        public float  MaxFuel = -1f;
    }

    // Server-backed trip state: a single in-progress journey, with a
    // server-issued arrival timestamp the client can't tamper with. Mirrors
    // FuelSystem's shape and relationship to PlayerState — PlayerState's
    // IsTraveling/TravelTargetId/TravelArrivalTsMs are the client-side view
    // cache this keeps in sync.
    public class TravelTripSystem
    {
        private readonly IBackendClient   _backend;
        private readonly PlayerState      _playerState;
        private readonly PlanetDefinition _currentPlanet;

        public TravelTripSystem(IBackendClient backend, PlayerState playerState, PlanetDefinition currentPlanet)
        {
            _backend       = backend;
            _playerState   = playerState;
            _currentPlanet = currentPlanet;
        }

        public async Task RefreshAsync()
        {
            var result = await _backend.CallAsync<TravelTripResult>("GetTravelState");
            Apply(result);
        }

        public async Task<TravelTripResult> StartTravelAsync(PlanetDefinition target)
        {
            // originPlanetId drives the server's origin/destination-specific duration
            // lookup (see ServerCode/StartTravel.js's TRAVEL_SECONDS table) — the
            // server has no other notion of "where the player currently is".
            var result = await _backend.CallAsync<TravelTripResult>("StartTravel",
                new Dictionary<string, object>
                {
                    { "targetPlanetId", target.PlanetId },
                    { "originPlanetId", _currentPlanet.PlanetId },
                });
            Apply(result);
            return result;
        }

        public async Task<TravelTripResult> LandAsync()
        {
            var result = await _backend.CallAsync<TravelTripResult>("LandTravel");
            if (result != null && result.Success)
                Apply(new TravelTripResult { Success = true, Traveling = false });
            return result;
        }

        private void Apply(TravelTripResult result)
        {
            if (result == null) return;
            _playerState.SetTravelState(result.Traveling, result.TargetPlanetId, result.ArrivalTs);
            if (result.Fuel    >= 0f) _playerState.SetFuel(result.Fuel);
            if (result.MaxFuel >= 0f) _playerState.SetMaxFuel(result.MaxFuel);

            // Client-cached resume hint (like SaveKeys.LastPlanetIdKey) so Boot/Hub know to
            // head straight for the Travel scene if the app was closed mid-trip — the
            // Travel scene itself re-validates against the server, this is not the
            // source of truth.
            if (result.Traveling) PlayerPrefs.SetString(SaveKeys.TravelTargetId, result.TargetPlanetId);
            else                  PlayerPrefs.DeleteKey(SaveKeys.TravelTargetId);
            PlayerPrefs.Save();
        }
    }
}
