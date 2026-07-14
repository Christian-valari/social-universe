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
    // Polls the global land registry for the active planet and applies ownership
    // (own + other players') to the hexasphere tiles. Polling is the phase-1
    // stand-in for "subscribe" — true realtime push needs the M4 presence layer.
    public class LandRegistrySyncController : IStartable, IDisposable 
    {
        private readonly LandRegistryService _registry;
        private readonly HexasphereManager   _hexasphere;
        private readonly TileColorizer       _colorizer;
        private readonly TileExtrusionView   _extrusionView;
        private readonly IAuthService        _auth;
        private readonly PlanetDefinition    _planet;
        private readonly EconomyConfig       _config;
        private readonly CancellationTokenSource _cts = new();

        public LandRegistrySyncController(
            LandRegistryService registry,
            HexasphereManager   hexasphere,
            TileColorizer       colorizer,
            TileExtrusionView   extrusionView,
            IAuthService        auth,
            PlanetDefinition    planet,
            EconomyConfig       config)
        {
            _registry      = registry;
            _hexasphere    = hexasphere;
            _colorizer     = colorizer;
            _extrusionView = extrusionView;
            _auth          = auth;
            _planet        = planet;
            _config        = config;
        }

        // PlanetSceneBootstrapper awaits one RefreshAndApplyAsync() call directly
        // (see HydrateServerStateAsync) before publishing SceneReadyEvent, so
        // other players' tile ownership is painted before the LoadingScreen
        // unloads — without this, tiles owned by other players briefly show as
        // Available. The poll loop below waits a full interval before its own
        // first refresh so that gating call isn't duplicated a second time here.
        public void Start() => PollLoopAsync(_cts.Token);

        public void Dispose() => _cts.Cancel();

        private async void PollLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(_config.LandRegistryPollIntervalSec), token);
                }
                catch (TaskCanceledException)
                {
                    break;
                }

                await RefreshAndApplyAsync();
            }
        }

        public async Task RefreshAndApplyAsync()
        {
            try
            {
                await _registry.RefreshAsync(_planet.name);
                ApplyOwnership();
            }
            catch (Exception ex)
            {
                SULog.Warn($"LandRegistrySyncController: refresh failed ({ex.Message})", SULog.Channel.Economy);
            }
        }

        private void ApplyOwnership()
        {
            string localPlayerId = _auth.IsSignedIn ? _auth.PlayerId : null;

            foreach (var (tileId, entry) in _registry.TileOwners)
            {
                if (string.IsNullOrEmpty(entry.OwnerId)) continue;

                var tile = _hexasphere.GetTile(tileId);
                if (tile == null) continue;

                var newState = entry.OwnerId == localPlayerId ? TileState.OwnedByPlayer : TileState.OwnedByOther;
                if (tile.State == newState && tile.OwnerId == entry.OwnerId && tile.BuildLevel == entry.BuildLevel) continue;

                tile.State      = newState;
                tile.OwnerId    = entry.OwnerId;
                tile.BuildLevel = entry.BuildLevel;
                _colorizer.RefreshTile(tile);
                _extrusionView.RefreshTile(tile);
            }
        }
    }
}
