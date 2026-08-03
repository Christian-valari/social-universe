using System;
using VContainer.Unity;
using SocialUniverse.Core;

namespace SocialUniverse.App
{
    // Planet-scene handler: turns a ViewLandRequestedEvent into an FSM transition. Fills the
    // root-level LandBuildingHandoff (planetId comes from the current PlanetState). Mirrors
    // ActiveMiningRequestHandler; registered in PlanetSceneScope's production block.
    public class ViewLandRequestHandler : IStartable, IDisposable
    {
        private readonly PlanetState         _planetState;
        private readonly LandBuildingHandoff _handoff;

        public ViewLandRequestHandler(PlanetState planetState, LandBuildingHandoff handoff)
        {
            _planetState = planetState;
            _handoff     = handoff;
        }

        public void Start()   => EventBus.Subscribe<ViewLandRequestedEvent>(OnViewLandRequested);
        public void Dispose() => EventBus.Unsubscribe<ViewLandRequestedEvent>(OnViewLandRequested);

        private void OnViewLandRequested(ViewLandRequestedEvent e)
        {
            _handoff.Begin(e.TileId, _planetState.TargetPlanetId, e.OwnerId, e.CanEdit, e.Slots, e.Coins);
            _planetState.EnterLandBuilding();
        }
    }
}
