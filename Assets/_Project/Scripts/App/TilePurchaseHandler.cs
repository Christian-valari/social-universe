using System;
using VContainer.Unity;
using SocialUniverse.Core;
using SocialUniverse.Economy;
using SocialUniverse.Config;
using SocialUniverse.World;

namespace SocialUniverse.App
{
    public class TilePurchaseHandler : IStartable, IDisposable
    {
        private readonly LandPurchaseService _purchaseService;
        private readonly TileColorizer       _colorizer;
        private readonly PlanetDefinition    _planet;
        private readonly IAuthService        _auth;
        private readonly LandRegistryService _landRegistryService;

        public TilePurchaseHandler(LandPurchaseService purchaseService, TileColorizer colorizer,
            PlanetDefinition planet, IAuthService auth, LandRegistryService landRegistryService)
        {
            _purchaseService     = purchaseService;
            _colorizer           = colorizer;
            _planet              = planet;
            _auth                = auth;
            _landRegistryService = landRegistryService;
        }

        public void Start()   => EventBus.Subscribe<TilePurchaseConfirmedEvent>(OnTilePurchaseConfirmed);
        public void Dispose() => EventBus.Unsubscribe<TilePurchaseConfirmedEvent>(OnTilePurchaseConfirmed);

        private async void OnTilePurchaseConfirmed(TilePurchaseConfirmedEvent e)
        {
            var tile = e.Tile;
            if (tile.State != TileState.Available)
            {
                EventBus.Publish(new TilePurchaseCompletedEvent
                    { Tile = tile, Success = false, FailureReason = "Tile is already owned" });
                return;
            }

            string playerId = _auth.IsSignedIn ? _auth.PlayerId : "local_player";
            var request = new LandPurchaseRequest { TileId = tile.TileId, PlayerId = playerId };
            var result  = await _purchaseService.PurchaseAsync(request, _planet);

            if (!result.Success)
            {
                SULog.Warn($"Buy tile {tile.TileId} failed: {result.FailureReason}", SULog.Channel.Economy);
                EventBus.Publish(new TilePurchaseCompletedEvent
                    { Tile = tile, Success = false, FailureReason = result.FailureReason });
                return;
            }

            tile.State   = TileState.OwnedByPlayer;
            tile.OwnerId = playerId;
            _colorizer.RefreshTile(tile);
            _landRegistryService.SetOwner(tile.TileId, playerId);
            EventBus.Publish(new TilePurchaseCompletedEvent { Tile = tile, Success = true });
        }
    }
}
