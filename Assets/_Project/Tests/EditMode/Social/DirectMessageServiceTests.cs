using System.Threading.Tasks;
using NUnit.Framework;
using SocialUniverse.Config;
using SocialUniverse.Core;
using SocialUniverse.Social;
using UnityEngine;

namespace SocialUniverse.Tests
{
    public class DirectMessageServiceTests
    {
        private SocialConfig            _config;
        private FakeChatService         _chat;
        private FakeSocialBackendClient _backend;
        private ReportService           _reports;
        private LocalMockFriendsService _friends;
        private DirectMessageService    _dms;

        [SetUp]
        public void SetUp()
        {
            _config  = ScriptableObject.CreateInstance<SocialConfig>();
            _chat    = new FakeChatService();
            _backend = new FakeSocialBackendClient();
            _reports = new ReportService(_backend, _chat);
            _friends = new LocalMockFriendsService();
            _dms     = new DirectMessageService(_chat, _friends, new ChatModerationFilter(_config), _reports, _config);
        }

        [TearDown]
        public void TearDown()
        {
            _dms.Dispose();
            Object.DestroyImmediate(_config);
            EventBus.Clear();
        }

        private async Task BefriendAsync(string playerId)
        {
            _friends.SimulateIncomingRequest(playerId);
            await _friends.AcceptFriendRequestAsync(playerId);
        }

        [Test]
        public async Task Sending_to_non_friend_is_rejected()
        {
            var status = await _dms.SendAsync("stranger_9", "hi there");

            Assert.AreEqual(ChatSendStatus.NotFriend, status);
            Assert.IsEmpty(_chat.SentDirects);
        }

        [Test]
        public async Task Sending_to_friend_works()
        {
            await BefriendAsync("ally_1");

            var status = await _dms.SendAsync("ally_1", "hey, come to Mars!");

            Assert.AreEqual(ChatSendStatus.Sent, status);
            Assert.AreEqual(("ally_1", "hey, come to Mars!"), _chat.SentDirects[0]);
        }

        [Test]
        public async Task Sending_to_blocked_player_is_rejected()
        {
            await BefriendAsync("ally_1");
            _backend.BlockResponse = new BlockResult { Success = true, BlockedUsers = new[] { "ally_1" } };
            await _reports.BlockPlayerAsync("ally_1");

            var status = await _dms.SendAsync("ally_1", "hello?");

            Assert.AreEqual(ChatSendStatus.Blocked, status);
            Assert.IsEmpty(_chat.SentDirects);
        }

        [Test]
        public async Task Dirty_message_is_rejected_at_strict_level()
        {
            await BefriendAsync("ally_1");

            var status = await _dms.SendAsync("ally_1", "you shit");

            Assert.AreEqual(ChatSendStatus.Filtered, status);
            Assert.IsEmpty(_chat.SentDirects);
        }

        [Test]
        public void Inbound_dm_is_published_on_the_EventBus()
        {
            ChatMessage received = null;
            EventBus.Subscribe<DirectMessageService.DirectMessageReceivedEvent>(e => received = e.Message);

            _chat.RaiseDirectMessage(new ChatMessage { SenderId = "ally_1", Text = "o/", IsDirect = true });

            Assert.IsNotNull(received);
            Assert.AreEqual("o/", received.Text);
        }

        [Test]
        public void Inbound_dm_from_muted_player_is_dropped()
        {
            _reports.MutePlayer("loud_guy", true);

            ChatMessage received = null;
            EventBus.Subscribe<DirectMessageService.DirectMessageReceivedEvent>(e => received = e.Message);

            _chat.RaiseDirectMessage(new ChatMessage { SenderId = "loud_guy", Text = "HEY", IsDirect = true });

            Assert.IsNull(received);
        }
    }
}
