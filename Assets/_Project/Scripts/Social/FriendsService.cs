using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SocialUniverse.Core;
using Unity.Services.Friends.Models;
using UgsFriends = Unity.Services.Friends.FriendsService;

namespace SocialUniverse.Social
{
    // IFriendsService implementation backed by the UGS Friends SDK.
    // Requires Authentication sign-in to have completed before InitializeAsync.
    public class FriendsService : IFriendsService, IDisposable
    {
        public event Action RosterChanged;

        private bool _initialized;

        public IReadOnlyList<FriendInfo> Friends          => Map(UgsFriends.Instance.Friends);
        public IReadOnlyList<FriendInfo> IncomingRequests => Map(UgsFriends.Instance.IncomingFriendRequests);
        public IReadOnlyList<FriendInfo> OutgoingRequests => Map(UgsFriends.Instance.OutgoingFriendRequests);

        public bool IsFriend(string playerId) =>
            playerId != null && UgsFriends.Instance.Friends.Any(r => r.Member?.Id == playerId);

        public async Task InitializeAsync()
        {
            if (_initialized) return;

            await UgsFriends.Instance.InitializeAsync();

            UgsFriends.Instance.RelationshipAdded   += OnRosterEvent;
            UgsFriends.Instance.RelationshipDeleted += OnRosterEvent;
            UgsFriends.Instance.PresenceUpdated     += OnRosterEvent;
            _initialized = true;

            // Best-effort: mark ourselves online so friends see presence.
            try { await UgsFriends.Instance.SetPresenceAvailabilityAsync(Availability.Online); }
            catch (Exception ex) { SULog.Warn($"FriendsService: presence set failed ({ex.Message})", SULog.Channel.Social); }

            SULog.Info("FriendsService: initialized", SULog.Channel.Social);
        }

        // AddFriendAsync doubles as "accept": UGS auto-creates the friendship
        // when an incoming request from that player already exists.
        public async Task SendFriendRequestAsync(string playerId)
        {
            await UgsFriends.Instance.AddFriendAsync(playerId);
            RosterChanged?.Invoke();
        }

        public async Task AcceptFriendRequestAsync(string playerId)
        {
            await UgsFriends.Instance.AddFriendAsync(playerId);
            RosterChanged?.Invoke();
        }

        public async Task DeclineFriendRequestAsync(string playerId)
        {
            await UgsFriends.Instance.DeleteIncomingFriendRequestAsync(playerId);
            RosterChanged?.Invoke();
        }

        public async Task RemoveFriendAsync(string playerId)
        {
            await UgsFriends.Instance.DeleteFriendAsync(playerId);
            RosterChanged?.Invoke();
        }

        public void Dispose()
        {
            if (!_initialized) return;
            UgsFriends.Instance.RelationshipAdded   -= OnRosterEvent;
            UgsFriends.Instance.RelationshipDeleted -= OnRosterEvent;
            UgsFriends.Instance.PresenceUpdated     -= OnRosterEvent;
        }

        private void OnRosterEvent<T>(T _) => RosterChanged?.Invoke();

        private static IReadOnlyList<FriendInfo> Map(IReadOnlyList<Relationship> relationships) =>
            relationships.Select(r => new FriendInfo
            {
                PlayerId    = r.Member?.Id,
                DisplayName = r.Member?.Profile?.Name,
                IsOnline    = r.Member?.Presence?.Availability == Availability.Online
            }).ToList();
    }
}
