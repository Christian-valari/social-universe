using System;
using SocialUniverse.Config;
using SocialUniverse.Core;
using SocialUniverse.Net;
using SocialUniverse.Social;
using VContainer.Unity;

namespace SocialUniverse.App
{
    // Joins presence + the planet's chat channel when the Planet scene starts,
    // and leaves both when it unloads. Each planet is its own Vivox channel
    // (ChatChannelController.PlanetChannelName), so in production both calls
    // converge on the same join (VivoxPresenceService delegates to
    // ChatChannelController internally, so the explicit channel call here is a
    // redundant-but-harmless ensure); in dev mode IPresenceService is a
    // standalone mock, so both calls are needed to bring up chat and presence
    // independently. There is no host or session to join — presence is just
    // "who's in this planet's Vivox channel".
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
                    SULog.Info($"PlanetPresenceController: in channel {_presence.CurrentChannelName} with {_presence.Players.Count} player(s)", SULog.Channel.Net);
            }
            catch (Exception ex)
            {
                SULog.Warn($"PlanetPresenceController: presence join failed ({ex.Message})", SULog.Channel.Net);
            }

            try
            {
                await _channels.SwitchToPlanetAsync(_planet.name);
            }
            catch (Exception ex)
            {
                SULog.Warn($"PlanetPresenceController: planet channel join failed ({ex.Message})", SULog.Channel.Social);
            }
        }

        public async void Dispose()
        {
            _presence.PlayerJoined -= OnPlayerJoined;
            _presence.PlayerLeft   -= OnPlayerLeft;

            try
            {
                await _presence.LeaveAsync();
                await _channels.LeaveCurrentAsync();
            }
            catch (Exception ex)
            {
                SULog.Warn($"PlanetPresenceController: leave failed ({ex.Message})", SULog.Channel.Net);
            }
        }

        private void OnPlayerJoined(PresencePlayer player) =>
            SULog.Info($"PlanetPresenceController: {player.DisplayName} ({player.PlayerId}) joined the channel", SULog.Channel.Net);

        private void OnPlayerLeft(string playerId) =>
            SULog.Info($"PlanetPresenceController: {playerId} left the channel", SULog.Channel.Net);
    }
}
