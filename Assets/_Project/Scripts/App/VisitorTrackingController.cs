using System;
using VContainer.Unity;
using SocialUniverse.Core;
using SocialUniverse.Config;
using SocialUniverse.Economy;
using SocialUniverse.World;

namespace SocialUniverse.App
{
    // M3 stand-in for presence-based visit tracking (see PROGRESS.md M4
    // dependency caveat): selecting another player's tile counts as a "visit"
    // toward that tile's ClaimYield bonus. Real cross-player visit attribution
    // needs M4's presence/position-sync layer.
    public class VisitorTrackingController : IStartable, IDisposable
    {
        private readonly VisitorTracker   _visitorTracker;
        private readonly PlanetDefinition _planet;

        private string _lastVisitedTileId;

        public VisitorTrackingController(VisitorTracker visitorTracker, PlanetDefinition planet)
        {
            _visitorTracker = visitorTracker;
            _planet         = planet;
        }

        public void Start()   => EventBus.Subscribe<TileSelectedEvent>(OnTileSelected);
        public void Dispose() => EventBus.Unsubscribe<TileSelectedEvent>(OnTileSelected);

        private async void OnTileSelected(TileSelectedEvent e)
        {
            var tile = e.Tile;
            if (tile.State != TileState.OwnedByOther) return;
            if (tile.TileId == _lastVisitedTileId) return;

            _lastVisitedTileId = tile.TileId;
            try
            {
                await _visitorTracker.RecordVisitAsync(tile.TileId, _planet.name);
            }
            catch (Exception ex)
            {
                SULog.Warn($"VisitorTrackingController: RecordVisit failed for tile {tile.TileId} — {ex.Message}", SULog.Channel.Economy);
            }
        }
    }
}
