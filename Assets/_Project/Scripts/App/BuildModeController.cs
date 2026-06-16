using System;
using System.Collections.Generic;
using VContainer.Unity;
using SocialUniverse.Core;
using SocialUniverse.Economy;
using SocialUniverse.Config;
using SocialUniverse.World;

namespace SocialUniverse.App
{
    public class BuildModeController : IStartable, IDisposable
    {
        private readonly IBackendClient      _backend;
        private readonly LandRegistryService _landRegistryService;
        private readonly Wallet              _wallet;
        private readonly TileExtrusionView   _extrusionView;
        private readonly PlanetDefinition    _planet;

        public BuildModeController(IBackendClient backend, LandRegistryService landRegistryService,
            Wallet wallet, TileExtrusionView extrusionView, PlanetDefinition planet)
        {
            _backend             = backend;
            _landRegistryService = landRegistryService;
            _wallet              = wallet;
            _extrusionView       = extrusionView;
            _planet              = planet;
        }

        public void Start()   => EventBus.Subscribe<BuildItemRequestedEvent>(OnBuildItemRequested);
        public void Dispose() => EventBus.Unsubscribe<BuildItemRequestedEvent>(OnBuildItemRequested);

        private async void OnBuildItemRequested(BuildItemRequestedEvent e)
        {
            var tile = e.Tile;
            var item = e.Item;

            if (tile.State != TileState.OwnedByPlayer || item.BuildLevel != tile.BuildLevel + 1)
            {
                SULog.Warn($"BuildModeController: cannot place {item.ItemId} on tile {tile.TileId} (level {tile.BuildLevel})", SULog.Channel.Economy);
                return;
            }

            PlaceBuildResponse response;
            try
            {
                response = await _backend.CallAsync<PlaceBuildResponse>("PlaceBuild",
                    new Dictionary<string, object>
                    {
                        { "tileId",   tile.TileId  },
                        { "planetId", _planet.name },
                        { "itemId",   item.ItemId  },
                        { "cost",     item.Cost    }
                    });
            }
            catch (Exception ex)
            {
                SULog.Error($"BuildModeController: server call failed — {ex.Message}", SULog.Channel.Economy);
                return;
            }

            if (!response.Success)
            {
                SULog.Warn($"PlaceBuild on tile {tile.TileId} failed: {response.Reason}", SULog.Channel.Economy);
                return;
            }

            tile.BuildLevel = response.BuildLevel;
            if (response.NewBalance >= 0) _wallet.SetCoins(response.NewBalance);
            _landRegistryService.SetBuildLevel(tile.TileId, tile.BuildLevel);
            _extrusionView.RefreshTile(tile);

            SULog.Info($"Built {item.ItemId} on tile {tile.TileId} (level {tile.BuildLevel}, balance {response.NewBalance})", SULog.Channel.Economy);
        }

        private class PlaceBuildResponse
        {
            public bool   Success;
            public string Reason;
            public int    NewBalance = -1;
            public int    BuildLevel;
        }
    }
}
