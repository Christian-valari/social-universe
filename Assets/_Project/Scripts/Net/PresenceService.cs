using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unity.Services.Multiplayer;

namespace SocialUniverse.Net
{
    // IPresenceService implementation on top of ShardManager's Multiplayer
    // session: the session's player list *is* presence for the current shard.
    public class PresenceService : IPresenceService, IDisposable
    {
        public event Action PresenceChanged;
        public event Action<PresencePlayer> PlayerJoined;
        public event Action<string> PlayerLeft;

        private readonly ShardManager _shards;
        private ISession _observed;

        public PresenceService(ShardManager shards)
        {
            _shards = shards;
            _shards.SessionChanged += OnSessionChanged;
            OnSessionChanged();
        }

        public bool   IsConnected    => _shards.CurrentSession != null;
        public string CurrentShardId => _shards.CurrentShardId;

        public IReadOnlyList<PresencePlayer> Players =>
            _shards.CurrentSession?.Players.Select(ToPresencePlayer).ToList()
            ?? (IReadOnlyList<PresencePlayer>)Array.Empty<PresencePlayer>();

        public Task<bool> JoinPlanetAsync(string planetId) => _shards.JoinPlanetAsync(planetId);
        public Task<bool> JoinShardAsync(string shardId)   => _shards.JoinShardAsync(shardId);
        public Task LeaveAsync()                           => _shards.LeaveAsync();

        public void Dispose()
        {
            Unobserve();
            _shards.SessionChanged -= OnSessionChanged;
        }

        private void OnSessionChanged()
        {
            Unobserve();
            _observed = _shards.CurrentSession;
            if (_observed != null)
            {
                _observed.PlayerJoined += OnPlayerJoined;
                _observed.PlayerLeaving   += OnPlayerLeft;
            }
            PresenceChanged?.Invoke();
        }

        private void Unobserve()
        {
            if (_observed == null) return;
            _observed.PlayerJoined -= OnPlayerJoined;
            _observed.PlayerLeaving   -= OnPlayerLeft;
            _observed = null;
        }

        private void OnPlayerJoined(string playerId)
        {
            var player = _shards.CurrentSession?.Players.FirstOrDefault(p => p.Id == playerId);
            PlayerJoined?.Invoke(player != null ? ToPresencePlayer(player) : new PresencePlayer { PlayerId = playerId });
            PresenceChanged?.Invoke();
        }

        private void OnPlayerLeft(string playerId)
        {
            PlayerLeft?.Invoke(playerId);
            PresenceChanged?.Invoke();
        }

        private static PresencePlayer ToPresencePlayer(IReadOnlyPlayer player) => new()
        {
            PlayerId    = player.Id,
            DisplayName = player.Properties != null
                          && player.Properties.TryGetValue(ShardManager.DisplayNamePropertyKey, out var prop)
                ? prop.Value
                : player.Id
        };
    }
}
