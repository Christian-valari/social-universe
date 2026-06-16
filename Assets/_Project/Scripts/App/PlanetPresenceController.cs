using System;
using SocialUniverse.Config;
using SocialUniverse.Core;
using SocialUniverse.Net;
using SocialUniverse.Social;
using VContainer.Unity;

namespace SocialUniverse.App
{
    // Joins the planet's shard (presence) and its local chat channel when the
    // Planet scene starts, and leaves both when it unloads. Logs join/leave
    // of other players — the visible marker avatars are NGO NetworkPlayer
    // prefabs spawned by the session's NetworkManager (scene setup, see
    // PROGRESS.md M4 setup checklist).
    public class PlanetPresenceController : IStartable, IDisposable
    {
        private readonly IPresenceService      _presence;
        private readonly ChatChannelController _channels;
        private readonly PlanetDefinition      _planet;

        public PlanetPresenceController(
            IPresenceService      presence,
            ChatChannelController channels,
            PlanetDefinition      planet)
        {
            _presence = presence;
            _channels = channels;
            _planet   = planet;
        }

        public async void Start()
        {
            _presence.PlayerJoined += OnPlayerJoined;
            _presence.PlayerLeft   += OnPlayerLeft;

            try
            {
                bool joined = await _presence.JoinPlanetAsync(_planet.name);
                if (joined)
                    SULog.Info($"PlanetPresenceController: in shard {_presence.CurrentShardId} with {_presence.Players.Count} player(s)", SULog.Channel.Net);
            }
            catch (Exception ex)
            {
                SULog.Warn($"PlanetPresenceController: shard join failed ({ex.Message})", SULog.Channel.Net);
            }

            try
            {
                await _channels.SwitchToLocalAsync(_planet.name);
            }
            catch (Exception ex)
            {
                SULog.Warn($"PlanetPresenceController: local channel join failed ({ex.Message})", SULog.Channel.Social);
            }
        }

        public async void Dispose()
        {
            _presence.PlayerJoined -= OnPlayerJoined;
            _presence.PlayerLeft   -= OnPlayerLeft;

            try
            {
                await _presence.LeaveAsync();
                await _channels.SwitchToGlobalAsync();
            }
            catch (Exception ex)
            {
                SULog.Warn($"PlanetPresenceController: leave failed ({ex.Message})", SULog.Channel.Net);
            }
        }

        private void OnPlayerJoined(PresencePlayer player) =>
            SULog.Info($"PlanetPresenceController: {player.DisplayName} ({player.PlayerId}) joined the shard", SULog.Channel.Net);

        private void OnPlayerLeft(string playerId) =>
            SULog.Info($"PlanetPresenceController: {playerId} left the shard", SULog.Channel.Net);
    }
}
