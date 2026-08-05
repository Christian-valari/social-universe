using System;
using VContainer.Unity;
using SocialUniverse.Config;
using SocialUniverse.Core;

namespace SocialUniverse.App
{
    // Planet-scene handler: turns a ViewLandRequestedEvent into an FSM transition. Fills the
    // root-level LandBuildingHandoff with both planet ids — the nav id (PlanetState.TargetPlanetId,
    // = _planetId) for the return trip, and the land-registry key (PlanetDefinition.name) that the
    // build Cloud Code functions expect. Mirrors ActiveMiningRequestHandler; registered in
    // PlanetSceneScope's production block.
    public class ViewLandRequestHandler : IStartable, IDisposable
    {
        private readonly PlanetState         _planetState;
        private readonly LandBuildingHandoff _handoff;
        private readonly PlanetDefinition    _planet;

        public ViewLandRequestHandler(PlanetState planetState, LandBuildingHandoff handoff, PlanetDefinition planet)
        {
            _planetState = planetState;
            _handoff     = handoff;
            _planet      = planet;
        }

        public void Start()   => EventBus.Subscribe<ViewLandRequestedEvent>(OnViewLandRequested);
        public void Dispose() => EventBus.Unsubscribe<ViewLandRequestedEvent>(OnViewLandRequested);

        private void OnViewLandRequested(ViewLandRequestedEvent e)
        {
            _handoff.Begin(e.TileId, _planetState.TargetPlanetId, _planet.name, e.OwnerId, e.CanEdit, e.Slots, e.Unlocked, e.Coins);
            _planetState.EnterLandBuilding();
        }
    }
}
