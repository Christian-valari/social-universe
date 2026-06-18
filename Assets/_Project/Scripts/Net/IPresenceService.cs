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

    // Who is on this planet right now. The real implementation
    // (VivoxPresenceService) derives presence from the roster of the planet's
    // Vivox text channel — joining the channel for chat IS joining presence,
    // there is no separate session/host step. LocalMockPresenceService is the
    // offline/dev-mode stand-in.
    public interface IPresenceService
    {
        // Raised after any change to the player list.
        event Action PresenceChanged;
        event Action<PresencePlayer> PlayerJoined;
        event Action<string> PlayerLeft;

        bool   IsConnected        { get; }
        string CurrentChannelName { get; }
        IReadOnlyList<PresencePlayer> Players { get; }

        // Joins the planet's presence channel (same channel as planet-local chat).
        Task<bool> JoinPlanetAsync(string planetId);

        Task LeaveAsync();
    }
}
