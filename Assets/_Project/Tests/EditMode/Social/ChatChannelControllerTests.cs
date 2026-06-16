using System.Threading.Tasks;
using NUnit.Framework;
using SocialUniverse.Config;
using SocialUniverse.Core;
using SocialUniverse.Social;
using UnityEngine;

namespace SocialUniverse.Tests
{
    public class ChatChannelControllerTests
    {
        private SocialConfig            _config;
        private FakeChatService         _chat;
        private FakeSocialBackendClient _backend;
        private ReportService           _reports;
        private ChatChannelController   _controller;

        [SetUp]
        public void SetUp()
        {
            _config  = ScriptableObject.CreateInstance<SocialConfig>(); // defaults: Strict, "global"
            _chat    = new FakeChatService();
            _backend = new FakeSocialBackendClient();
            _reports = new ReportService(_backend, _chat);
            _controller = new ChatChannelController(_chat, new ChatModerationFilter(_config), _reports, _config);
        }

        [TearDown]
        public void TearDown()
        {
            _controller.Dispose();
            Object.DestroyImmediate(_config);
            EventBus.Clear();
        }

        [Test]
        public async Task SendAsync_without_channel_returns_NoChannel()
        {
            Assert.AreEqual(ChatSendStatus.NoChannel, await _controller.SendAsync("hi"));
            Assert.IsEmpty(_chat.SentMessages);
        }

        [Test]
        public async Task Switching_to_global_joins_channel_and_send_works()
        {
            await _controller.SwitchToGlobalAsync();

            var status = await _controller.SendAsync("hello universe");

            Assert.AreEqual("global", _controller.ActiveChannel);
            Assert.Contains("global", _chat.JoinedChannels);
            Assert.AreEqual(ChatSendStatus.Sent, status);
            Assert.AreEqual(("global", "hello universe"), _chat.SentMessages[0]);
        }

        [Test]
        public async Task Switching_channels_leaves_the_previous_one()
        {
            await _controller.SwitchToGlobalAsync();
            await _controller.SwitchToLocalAsync("Planet_Earth");

            Assert.AreEqual("planet_planet_earth", _controller.ActiveChannel);
            Assert.Contains("global", _chat.LeftChannels);
        }

        [Test]
        public async Task Strict_filter_rejects_dirty_outbound_message()
        {
            await _controller.SwitchToGlobalAsync();

            var status = await _controller.SendAsync("well shit");

            Assert.AreEqual(ChatSendStatus.Filtered, status);
            Assert.IsEmpty(_chat.SentMessages);
        }

        [Test]
        public async Task Too_long_message_is_rejected()
        {
            await _controller.SwitchToGlobalAsync();

            var status = await _controller.SendAsync(new string('a', _config.MaxMessageLength + 1));

            Assert.AreEqual(ChatSendStatus.TooLong, status);
            Assert.IsEmpty(_chat.SentMessages);
        }

        [Test]
        public async Task Inbound_message_lands_in_history_and_on_the_EventBus()
        {
            await _controller.SwitchToGlobalAsync();

            ChatMessage received = null;
            EventBus.Subscribe<ChatChannelController.ChatMessageReceivedEvent>(e => received = e.Message);

            _chat.RaiseChannelMessage(new ChatMessage
            {
                SenderId = "friend_1", SenderDisplayName = "Friend", ChannelName = "global", Text = "o/"
            });

            Assert.IsNotNull(received);
            Assert.AreEqual("o/", received.Text);
            Assert.AreEqual(1, _controller.GetHistory("global").Count);
        }

        [Test]
        public async Task Inbound_message_from_blocked_player_is_dropped()
        {
            await _controller.SwitchToGlobalAsync();
            _backend.BlockResponse = new BlockResult { Success = true, BlockedUsers = new[] { "griefer_42" } };
            await _reports.BlockPlayerAsync("griefer_42");

            ChatMessage received = null;
            EventBus.Subscribe<ChatChannelController.ChatMessageReceivedEvent>(e => received = e.Message);

            _chat.RaiseChannelMessage(new ChatMessage { SenderId = "griefer_42", ChannelName = "global", Text = "hi" });

            Assert.IsNull(received);
            Assert.IsEmpty(_controller.GetHistory("global"));
        }

        [Test]
        public async Task Inbound_message_from_muted_player_is_dropped()
        {
            await _controller.SwitchToGlobalAsync();
            _reports.MutePlayer("loud_guy", true);

            _chat.RaiseChannelMessage(new ChatMessage { SenderId = "loud_guy", ChannelName = "global", Text = "HEY" });

            Assert.IsEmpty(_controller.GetHistory("global"));
        }

        [Test]
        public async Task Inbound_dirty_message_is_masked_for_display()
        {
            await _controller.SwitchToGlobalAsync();

            _chat.RaiseChannelMessage(new ChatMessage { SenderId = "p2", ChannelName = "global", Text = "shit" });

            Assert.AreEqual("****", _controller.GetHistory("global")[0].Text);
        }
    }
}
