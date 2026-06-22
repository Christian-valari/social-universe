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

        public TravelService(FuelSystem fuel, PlanetDefinition currentPlanet)
        {
            _fuel          = fuel;
            _currentPlanet = currentPlanet;
        }

        // Trips home are always free, regardless of current location or distance.
        public int GetFuelCost(PlanetDefinition target) =>
            target.PlanetId == Constants.PlanetIds.Earth ? 0 : target.TravelFuelCost;

        // Only neighboring planets (TravelRangeMath) are reachable directly — same
        // "always reachable" exemption as GetFuelCost gives the home planet.
        public bool IsInRange(PlanetDefinition target) =>
            target.PlanetId == Constants.PlanetIds.Earth || TravelRangeMath.IsInRange(_currentPlanet, target);

        public async Task<TravelResult> TravelToPlanetAsync(PlanetDefinition target)
        {
            if (!IsInRange(target))
            {
                SULog.Warn($"TravelService: {target.DisplayName} is out of range from {_currentPlanet?.DisplayName} (only neighboring planets are reachable directly)", SULog.Channel.Travel);
                return new TravelResult { Success = false, FailureReason = "OutOfRange" };
            }

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
