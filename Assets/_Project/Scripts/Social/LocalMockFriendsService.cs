using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SocialUniverse.Social
{
    // Offline IFriendsService — in-memory rosters, no UGS. Used in dev mode
    // and by EditMode tests to verify roster transitions (request → pending →
    // accepted) without the SDK.
    public class LocalMockFriendsService : IFriendsService
    {
        public event Action RosterChanged;

        private readonly List<FriendInfo> _friends  = new();
        private readonly List<FriendInfo> _incoming = new();
        private readonly List<FriendInfo> _outgoing = new();

        public IReadOnlyList<FriendInfo> Friends          => _friends;
        public IReadOnlyList<FriendInfo> IncomingRequests => _incoming;
        public IReadOnlyList<FriendInfo> OutgoingRequests => _outgoing;

        public bool IsFriend(string playerId) => _friends.Any(f => f.PlayerId == playerId);

        public Task InitializeAsync() => Task.CompletedTask;

        // Mirrors UGS semantics: a request toward someone who already has a
        // pending incoming request to us completes the friendship instead.
        public Task SendFriendRequestAsync(string playerId)
        {
            var incoming = _incoming.FirstOrDefault(f => f.PlayerId == playerId);
            if (incoming != null)
            {
                _incoming.Remove(incoming);
                _friends.Add(incoming);
            }
            else if (!IsFriend(playerId) && _outgoing.All(f => f.PlayerId != playerId))
            {
                _outgoing.Add(new FriendInfo { PlayerId = playerId, DisplayName = playerId });
            }
            RosterChanged?.Invoke();
            return Task.CompletedTask;
        }

        public Task AcceptFriendRequestAsync(string playerId) => SendFriendRequestAsync(playerId);

        public Task DeclineFriendRequestAsync(string playerId)
        {
            _incoming.RemoveAll(f => f.PlayerId == playerId);
            RosterChanged?.Invoke();
            return Task.CompletedTask;
        }

        public Task RemoveFriendAsync(string playerId)
        {
            _friends.RemoveAll(f => f.PlayerId == playerId);
            RosterChanged?.Invoke();
            return Task.CompletedTask;
        }

        // Test/dev helper: simulate another player sending us a request.
        public void SimulateIncomingRequest(string playerId, string displayName = null, bool isOnline = false)
        {
            _incoming.Add(new FriendInfo
            {
                PlayerId    = playerId,
                DisplayName = displayName ?? playerId,
                IsOnline    = isOnline
            });
            RosterChanged?.Invoke();
        }
    }
}
