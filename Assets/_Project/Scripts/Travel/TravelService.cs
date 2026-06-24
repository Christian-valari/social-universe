using System.Threading.Tasks;
using SocialUniverse.Config;
using SocialUniverse.Core;

namespace SocialUniverse.Travel
{
    public class TravelResult
    {
        public bool   Success;
        public string FailureReason;
    }

    // Validates and spends fuel for a star-map trip. Scene/FSM transition is
    // triggered by the caller (TravelController, App layer) only after this
    // reports success — TravelService itself never touches scenes.
    public class TravelService
    {
        private readonly FuelSystem        _fuel;
        private readonly PlanetDefinition  _currentPlanet;
        private readonly TravelTimeTable   _travelTimes;

        public TravelService(FuelSystem fuel, PlanetDefinition currentPlanet, TravelTimeTable travelTimes)
        {
            _fuel          = fuel;
            _currentPlanet = currentPlanet;
            _travelTimes   = travelTimes;
        }

        // Trips home are always free, regardless of current location or distance.
        public int GetFuelCost(PlanetDefinition target) =>
            target.PlanetId == Constants.PlanetIds.Earth ? 0 : target.TravelFuelCost;

        // Looks up the real origin/destination-specific duration (TravelTimeTable,
        // authored from Data/Travel_Times.csv) — unlike fuel cost, trips home are
        // NOT instant here; going home from Neptune genuinely takes as long as the
        // table says. Pairs the table doesn't cover (e.g. anything involving Pluto)
        // fall back to the target planet's own flat TravelDurationSeconds.
        public float GetTravelDuration(PlanetDefinition target)
        {
            if (_travelTimes != null && _travelTimes.TryGetSeconds(_currentPlanet.PlanetId, target.PlanetId, out float seconds))
                return seconds;
            return target.TravelDurationSeconds;
        }

        public async Task<TravelResult> TravelToPlanetAsync(PlanetDefinition target)
        {
            int cost = GetFuelCost(target);

            if (!await _fuel.TrySpendAsync(cost))
            {
                SULog.Warn($"TravelService: insufficient fuel for {target.DisplayName} (needs {cost})", SULog.Channel.Travel);
                return new TravelResult { Success = false, FailureReason = "InsufficientFuel" };
            }

            return new TravelResult { Success = true };
        }
    }
}
