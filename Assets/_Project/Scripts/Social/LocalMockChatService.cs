using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SocialUniverse.Social
{
    // Offline loopback for IChatService — no Vivox, no network. Sent messages
    // are echoed straight back through MessageReceived/DirectMessageReceived so
    // chat UI and controllers work in dev mode. Registered by RootLifetimeScope
    // when _devMode is on, and by standalone scene scopes.
    public class LocalMockChatService : IChatService
    {
        public const string MockPlayerId = "mock_player";

        private readonly HashSet<string> _joinedChannels = new();
        private readonly HashSet<string> _blocked        = new();
        private string _displayName = "MockPlayer";

        public event Action<ChatMessage> MessageReceived;
        public event Action<ChatMessage> DirectMessageReceived;

        public bool IsConnected { get; private set; }

        public Task ConnectAsync(string displayName)
        {
            _displayName = string.IsNullOrEmpty(displayName) ? _displayName : displayName;
            IsConnected  = true;
            return Task.CompletedTask;
        }

        public Task DisconnectAsync()
        {
            IsConnected = false;
            _joinedChannels.Clear();
            return Task.CompletedTask;
        }

        public Task JoinChannelAsync(string channelName)
        {
            _joinedChannels.Add(channelName);
            return Task.CompletedTask;
        }

        public Task LeaveChannelAsync(string channelName)
        {
            _joinedChannels.Remove(channelName);
            return Task.CompletedTask;
        }

        public Task SendMessageAsync(string channelName, string text)
        {
            if (_joinedChannels.Contains(channelName))
                MessageReceived?.Invoke(new ChatMessage
                {
                    SenderId          = MockPlayerId,
                    SenderDisplayName = _displayName,
                    ChannelName       = channelName,
                    Text              = text,
                    TimestampMs       = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    FromSelf          = true
                });
            return Task.CompletedTask;
        }

        public Task SendDirectMessageAsync(string playerId, string text)
        {
            DirectMessageReceived?.Invoke(new ChatMessage
            {
                SenderId          = MockPlayerId,
                SenderDisplayName = _displayName,
                Text              = text,
                TimestampMs       = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                FromSelf          = true,
                IsDirect          = true
            });
            return Task.CompletedTask;
        }

        public Task BlockPlayerAsync(string playerId)
        {
            _blocked.Add(playerId);
            return Task.CompletedTask;
        }

        public Task UnblockPlayerAsync(string playerId)
        {
            _blocked.Remove(playerId);
            return Task.CompletedTask;
        }

        // Test/dev helper: simulate a message arriving from another player.
        public void SimulateIncoming(ChatMessage message)
        {
            if (message.SenderId != null && _blocked.Contains(message.SenderId)) return;
            if (message.IsDirect) DirectMessageReceived?.Invoke(message);
            else                  MessageReceived?.Invoke(message);
        }
    }
}
