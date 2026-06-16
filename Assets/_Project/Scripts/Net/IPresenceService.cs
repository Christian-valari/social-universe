using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SocialUniverse.Net
{
    // A player currently present in the local player's shard.
    public class PresencePlayer
    {
        public string PlayerId;
        public string DisplayName;
    }

    // Who is on this planet/shard right now. The real implementation
    // (PresenceService) sits on top of ShardManager's Multiplayer session;
    // LocalMockPresenceService is the offline/dev-mode stand-in.
    public interface IPresenceService
    {
        // Raised after any change to the player list.
        event Action PresenceChanged;
        event Action<PresencePlayer> PlayerJoined;
        event Action<string> PlayerLeft;

        bool   IsConnected    { get; }
        string CurrentShardId { get; }
        IReadOnlyList<PresencePlayer> Players { get; }

        // Joins the first planet shard with room (shard 0..N-1).
        Task<bool> JoinPlanetAsync(string planetId);

        // Joins a specific shard — used to follow a friend.
        Task<bool> JoinShardAsync(string shardId);

        Task LeaveAsync();
    }
}
