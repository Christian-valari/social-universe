using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SocialUniverse.Net
{
    // Offline IPresenceService — joins a fake channel instantly and contains
    // only the local player. SimulatePlayerJoined/Left drive UI development
    // and tests without a live Vivox connection.
    public class LocalMockPresenceService : IPresenceService
    {
        public event Action PresenceChanged;
        public event Action<PresencePlayer> PlayerJoined;
        public event Action<string> PlayerLeft;

        private readonly List<PresencePlayer> _players = new();

        public bool   IsConnected        { get; private set; }
        public string CurrentChannelName { get; private set; }

        public IReadOnlyList<PresencePlayer> Players => _players;

        public Task<bool> JoinPlanetAsync(string planetId)
        {
            CurrentChannelName = $"planet_{planetId.ToLowerInvariant()}";
            IsConnected         = true;
            _players.Clear();
            _players.Add(new PresencePlayer { PlayerId = "mock_player", DisplayName = "MockPlayer" });
            PresenceChanged?.Invoke();
            return Task.FromResult(true);
        }

        public Task LeaveAsync()
        {
            CurrentChannelName = null;
            IsConnected         = false;
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
