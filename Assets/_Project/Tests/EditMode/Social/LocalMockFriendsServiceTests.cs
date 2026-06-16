using System.Threading.Tasks;
using NUnit.Framework;
using SocialUniverse.Social;

namespace SocialUniverse.Tests
{
    // Exercises the IFriendsService roster contract against the mock
    // implementation (the UGS-backed FriendsService is verified in PlayMode —
    // it is a thin SDK wrapper).
    public class LocalMockFriendsServiceTests
    {
        private LocalMockFriendsService _friends;

        [SetUp]
        public void SetUp() => _friends = new LocalMockFriendsService();

        [Test]
        public async Task Send_request_creates_outgoing_pending_entry()
        {
            await _friends.SendFriendRequestAsync("ally_1");

            Assert.AreEqual(1, _friends.OutgoingRequests.Count);
            Assert.AreEqual("ally_1", _friends.OutgoingRequests[0].PlayerId);
            Assert.IsFalse(_friends.IsFriend("ally_1"));
        }

        [Test]
        public async Task Accepting_incoming_request_updates_both_rosters()
        {
            _friends.SimulateIncomingRequest("ally_1", "Ally");

            await _friends.AcceptFriendRequestAsync("ally_1");

            Assert.IsEmpty(_friends.IncomingRequests);
            Assert.AreEqual(1, _friends.Friends.Count);
            Assert.IsTrue(_friends.IsFriend("ally_1"));
        }

        [Test]
        public async Task Sending_request_to_player_with_pending_incoming_creates_friendship()
        {
            _friends.SimulateIncomingRequest("ally_1");

            await _friends.SendFriendRequestAsync("ally_1");

            Assert.IsTrue(_friends.IsFriend("ally_1"));
            Assert.IsEmpty(_friends.IncomingRequests);
            Assert.IsEmpty(_friends.OutgoingRequests);
        }

        [Test]
        public async Task Declining_request_removes_it_without_friendship()
        {
            _friends.SimulateIncomingRequest("stranger_9");

            await _friends.DeclineFriendRequestAsync("stranger_9");

            Assert.IsEmpty(_friends.IncomingRequests);
            Assert.IsFalse(_friends.IsFriend("stranger_9"));
        }

        [Test]
        public async Task Removing_friend_updates_roster()
        {
            _friends.SimulateIncomingRequest("ally_1");
            await _friends.AcceptFriendRequestAsync("ally_1");

            await _friends.RemoveFriendAsync("ally_1");

            Assert.IsFalse(_friends.IsFriend("ally_1"));
            Assert.IsEmpty(_friends.Friends);
        }

        [Test]
        public void Roster_events_fire_on_changes()
        {
            int changes = 0;
            _friends.RosterChanged += () => changes++;

            _friends.SimulateIncomingRequest("ally_1");

            Assert.AreEqual(1, changes);
        }
    }
}
