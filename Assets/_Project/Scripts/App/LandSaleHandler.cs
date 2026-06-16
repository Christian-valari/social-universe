using System;
using VContainer.Unity;
using SocialUniverse.Core;
using SocialUniverse.Economy;
using SocialUniverse.Config;
using SocialUniverse.World;

namespace SocialUniverse.App
{
    public class LandSaleHandler : IStartable, IDisposable
    {
        private readonly LandSaleService   _saleService;
        private readonly TileColorizer     _colorizer;
        private readonly TileExtrusionView _extrusionView;
        private readonly PlanetDefinition  _planet;

        public LandSaleHandler(LandSaleService saleService, TileColorizer colorizer,
            TileExtrusionView extrusionView, PlanetDefinition planet)
        {
            _saleService   = saleService;
            _colorizer     = colorizer;
            _extrusionView = extrusionView;
            _planet        = planet;
        }

        public void Start()   => EventBus.Subscribe<TileSellRequestedEvent>(OnTileSellRequested);
        public void Dispose() => EventBus.Unsubscribe<TileSellRequestedEvent>(OnTileSellRequested);

        private async void OnTileSellRequested(TileSellRequestedEvent e)
        {
            var tile = e.Tile;
            if (tile.State != TileState.OwnedByPlayer)
            {
                SULog.Warn($"LandSaleHandler: cannot sell tile {tile.TileId} — not owned by player", SULog.Channel.Economy);
                return;
            }

            var result = await _saleService.SellAsync(tile.TileId, _planet);

            if (!result.Success)
            {
                SULog.Warn($"Sell tile {tile.TileId} failed: {result.Reason}", SULog.Channel.Economy);
                return;
            }

            tile.State      = TileState.Available;
            tile.OwnerId    = null;
            tile.BuildLevel = 0;
            _colorizer.RefreshTile(tile);
            _extrusionView.RefreshTile(tile);

            SULog.Info($"Sold tile {tile.TileId} (balance {result.NewBalance})", SULog.Channel.Economy);
        }
    }
}
