using System.Threading.Tasks;
using NUnit.Framework;
using SocialUniverse.Social;

namespace SocialUniverse.Tests
{
    public class LocalMockChatServiceTests
    {
        private LocalMockChatService _chat;

        [SetUp]
        public void SetUp() => _chat = new LocalMockChatService();

        [Test]
        public async Task Outbound_channel_message_carries_the_connected_avatarId()
        {
            await _chat.ConnectAsync("Stella", "avatar_wizard");
            await _chat.JoinChannelAsync("global");

            ChatMessage received = null;
            _chat.MessageReceived += m => received = m;

            await _chat.SendMessageAsync("global", "hi");

            Assert.IsNotNull(received);
            Assert.AreEqual("avatar_wizard", received.AvatarId);
        }

        [Test]
        public async Task Outbound_direct_message_carries_the_connected_avatarId()
        {
            await _chat.ConnectAsync("Stella", "avatar_wizard");

            ChatMessage received = null;
            _chat.DirectMessageReceived += m => received = m;

            await _chat.SendDirectMessageAsync("ally_1", "hey");

            Assert.IsNotNull(received);
            Assert.AreEqual("avatar_wizard", received.AvatarId);
        }
    }
}
