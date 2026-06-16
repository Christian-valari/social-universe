using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SocialUniverse.Net
{
    // Offline IPresenceService — joins a fake shard instantly and contains
    // only the local player. SimulatePlayerJoined/Left drive UI development
    // and tests without a Multiplayer session.
    public class LocalMockPresenceService : IPresenceService
    {
        public event Action PresenceChanged;
        public event Action<PresencePlayer> PlayerJoined;
        public event Action<string> PlayerLeft;

        private readonly List<PresencePlayer> _players = new();

        public bool   IsConnected    { get; private set; }
        public string CurrentShardId { get; private set; }

        public IReadOnlyList<PresencePlayer> Players => _players;

        public Task<bool> JoinPlanetAsync(string planetId) => JoinShardAsync($"{planetId.ToLowerInvariant()}_shard0");

        public Task<bool> JoinShardAsync(string shardId)
        {
            CurrentShardId = shardId;
            IsConnected    = true;
            _players.Clear();
            _players.Add(new PresencePlayer { PlayerId = "mock_player", DisplayName = "MockPlayer" });
            PresenceChanged?.Invoke();
            return Task.FromResult(true);
        }

        public Task LeaveAsync()
        {
            CurrentShardId = null;
            IsConnected    = false;
            _players.Clear();
            PresenceChanged?.Invoke();
            return Task.CompletedTask;
        }

        public void SimulatePlayerJoined(PresencePlayer player)
        {
            _players.Add(player);
            PlayerJoined?.Invoke(player);
            PresenceChanged?.Invoke();
        }

        public void SimulatePlayerLeft(string playerId)
        {
            _players.RemoveAll(p => p.PlayerId == playerId);
            PlayerLeft?.Invoke(playerId);
            PresenceChanged?.Invoke();
        }
    }
}
