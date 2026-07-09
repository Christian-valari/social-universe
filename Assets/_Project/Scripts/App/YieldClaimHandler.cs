using System;
using VContainer.Unity;
using SocialUniverse.Core;
using SocialUniverse.Config;
using SocialUniverse.Economy;
using SocialUniverse.World;

namespace SocialUniverse.App
{
    public class YieldClaimHandler : IStartable, IDisposable
    {
        private readonly YieldService     _yieldService;
        private readonly PlanetDefinition _planet;

        public YieldClaimHandler(YieldService yieldService, PlanetDefinition planet)
        {
            _yieldService = yieldService;
            _planet       = planet;
        }

        public void Start()   => EventBus.Subscribe<TileYieldClaimRequestedEvent>(OnTileYieldClaimRequested);
        public void Dispose() => EventBus.Unsubscribe<TileYieldClaimRequestedEvent>(OnTileYieldClaimRequested);

        private async void OnTileYieldClaimRequested(TileYieldClaimRequestedEvent e)
        {
            var tile = e.Tile;
            if (tile.State != TileState.OwnedByPlayer)
            {
                SULog.Warn($"YieldClaimHandler: cannot claim tile {tile.TileId} — not owned by player", SULog.Channel.Economy);
                EventBus.Publish(new TileYieldClaimCompletedEvent
                    { Tile = tile, Success = false, FailureReason = "Not your tile" });
                return;
            }

            var result = await _yieldService.ClaimYieldAsync(tile.TileId, _planet.name);

            if (!result.Success)
            {
                SULog.Warn($"Claim yield for tile {tile.TileId} failed: {result.Reason}", SULog.Channel.Economy);
                EventBus.Publish(new TileYieldClaimCompletedEvent
                    { Tile = tile, Success = false, FailureReason = result.Reason });
                return;
            }

            SULog.Info($"Claimed yield for tile {tile.TileId}: +{result.Granted} coins (balance {result.NewBalance})", SULog.Channel.Economy);
            EventBus.Publish(new TileYieldClaimCompletedEvent { Tile = tile, Success = true, Granted = result.Granted });
        }
    }
}
