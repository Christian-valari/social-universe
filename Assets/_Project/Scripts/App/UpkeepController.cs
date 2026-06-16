using System;
using System.Threading;
using System.Threading.Tasks;
using VContainer.Unity;
using SocialUniverse.Config;
using SocialUniverse.Core;
using SocialUniverse.Economy;
using SocialUniverse.World;

namespace SocialUniverse.App
{
    // Periodically applies recurring land upkeep for the local player's tiles
    // on the active planet. Tiles that revert (owner couldn't afford upkeep)
    // are reset to Available immediately on this client; LandRegistrySyncController
    // propagates the same revert to other players on its next poll.
    public class UpkeepController : IStartable, IDisposable
    {
        private readonly UpkeepService     _upkeepService;
        private readonly HexasphereManager _hexasphere;
        private readonly TileColorizer     _colorizer;
        private readonly TileExtrusionView _extrusionView;
        private readonly PlanetDefinition  _planet;
        private readonly EconomyConfig     _config;
        private readonly CancellationTokenSource _cts = new();

        public UpkeepController(
            UpkeepService     upkeepService,
            HexasphereManager hexasphere,
            TileColorizer     colorizer,
            TileExtrusionView extrusionView,
            PlanetDefinition  planet,
            EconomyConfig     config)
        {
            _upkeepService = upkeepService;
            _hexasphere    = hexasphere;
            _colorizer     = colorizer;
            _extrusionView = extrusionView;
            _planet        = planet;
            _config        = config;
        }

        public void Start() => PollLoopAsync(_cts.Token);

        public void Dispose() => _cts.Cancel();

        private async void PollLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                await ApplyUpkeepAsync();

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(_config.UpkeepPollIntervalSec), token);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
            }
        }

        private async Task ApplyUpkeepAsync()
        {
            try
            {
                var result = await _upkeepService.ApplyUpkeepAsync(_planet.name);
                foreach (var tileId in result.RevertedTiles)
                {
                    var tile = _hexasphere.GetTile(tileId);
                    if (tile == null) continue;

                    tile.State      = TileState.Available;
                    tile.OwnerId    = null;
                    tile.BuildLevel = 0;
                    _colorizer.RefreshTile(tile);
                    _extrusionView.RefreshTile(tile);

                    SULog.Info($"UpkeepController: tile {tileId} reverted to Available (unpaid upkeep)", SULog.Channel.Economy);
                }
            }
            catch (Exception ex)
            {
                SULog.Warn($"UpkeepController: ApplyUpkeep failed ({ex.Message})", SULog.Channel.Economy);
            }
        }
    }
}
