using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SocialUniverse.Social
{
    // A friend (or pending request) as the local player sees them.
    public class FriendInfo
    {
        public string PlayerId;
        public string DisplayName;
        public bool   IsOnline;
    }

    // Provider abstraction for the friends roster. FriendsService implements
    // this against the UGS Friends SDK; LocalMockFriendsService is the
    // offline/dev-mode implementation.
    public interface IFriendsService
    {
        // Raised whenever any roster list changes (friend added/removed,
        // request sent/accepted/declined, presence updated).
        event Action RosterChanged;

        IReadOnlyList<FriendInfo> Friends          { get; }
        IReadOnlyList<FriendInfo> IncomingRequests { get; }
        IReadOnlyList<FriendInfo> OutgoingRequests { get; }

        bool IsFriend(string playerId);

        Task InitializeAsync();
        Task SendFriendRequestAsync(string playerId);
        Task AcceptFriendRequestAsync(string playerId);
        Task DeclineFriendRequestAsync(string playerId);
        Task RemoveFriendAsync(string playerId);
    }
}
