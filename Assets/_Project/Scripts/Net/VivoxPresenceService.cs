using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SocialUniverse.Core;
using SocialUniverse.Social;
using Unity.Services.Vivox;

namespace SocialUniverse.Net
{
    // IPresenceService implementation backed by UGS Vivox: the roster of the
    // planet's text channel IS presence. There is no separate session/host to
    // join — JoinPlanetAsync delegates to ChatChannelController so joining for
    // chat and joining for presence are the same Vivox channel join, and
    // CurrentChannelName always mirrors the channel chat is active in. Each
    // planet is its own channel (ChatChannelController.PlanetChannelName), so
    // both chat and the presence roster are scoped to explorers on the same
    // planet.
    public class VivoxPresenceService : IPresenceService, IDisposable
    {
        public event Action PresenceChanged;
        public event Action<PresencePlayer> PlayerJoined;
        public event Action<string> PlayerLeft;

        private readonly ChatChannelController _channels;

        public VivoxPresenceService(ChatChannelController channels)
        {
            _channels = channels;
            if (VivoxService.Instance == null) return;
            VivoxService.Instance.ParticipantAddedToChannel    += OnParticipantAdded;
            VivoxService.Instance.ParticipantRemovedFromChannel += OnParticipantRemoved;
        }

        public bool   IsConnected        => !string.IsNullOrEmpty(CurrentChannelName);
        public string CurrentChannelName => _channels.ActiveChannel;

        public IReadOnlyList<PresencePlayer> Players =>
            CurrentChannelName != null
            && VivoxService.Instance != null
            && VivoxService.Instance.ActiveChannels.TryGetValue(CurrentChannelName, out var participants)
                ? participants.Select(ToPresencePlayer).ToList()
                : (IReadOnlyList<PresencePlayer>)Array.Empty<PresencePlayer>();

        public async Task<bool> JoinPlanetAsync(string planetId)
        {
            try
            {
                await _channels.SwitchToPlanetAsync(planetId);
                PresenceChanged?.Invoke();
                return true;
            }
            catch (Exception ex)
            {
                SULog.Warn($"VivoxPresenceService: join planet '{planetId}' failed ({ex.Message})", SULog.Channel.Net);
                return false;
            }
        }

        public async Task LeaveAsync()
        {
            try
            {
                await _channels.LeaveCurrentAsync();
            }
            catch (Exception ex)
            {
                SULog.Warn($"VivoxPresenceService: leave failed ({ex.Message})", SULog.Channel.Net);
            }
            PresenceChanged?.Invoke();
        }

        public void Dispose()
        {
            if (VivoxService.Instance == null) return;
            VivoxService.Instance.ParticipantAddedToChannel    -= OnParticipantAdded;
            VivoxService.Instance.ParticipantRemovedFromChannel -= OnParticipantRemoved;
        }

        private void OnParticipantAdded(VivoxParticipant participant)
        {
            if (participant.IsSelf || participant.ChannelName != CurrentChannelName) return;
            PlayerJoined?.Invoke(ToPresencePlayer(participant));
            PresenceChanged?.Invoke();
        }

        private void OnParticipantRemoved(VivoxParticipant participant)
        {
            if (participant.IsSelf || participant.ChannelName != CurrentChannelName) return;
            PlayerLeft?.Invoke(participant.PlayerId);
            PresenceChanged?.Invoke();
        }

        private static PresencePlayer ToPresencePlayer(VivoxParticipant participant) => new()
        {
            PlayerId    = participant.PlayerId,
            DisplayName = participant.DisplayName
        };
    }
}
