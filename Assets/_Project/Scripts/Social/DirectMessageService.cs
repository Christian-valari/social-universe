using System;
using SocialUniverse.Config;
using SocialUniverse.Core;
using System.Threading.Tasks;

namespace SocialUniverse.Social
{
    // Cross-planet DMs between friends. Wraps IChatService's directed
    // messages with the social rules: friends-only, moderation-filtered,
    // block/mute-aware. UI subscribes to DirectMessageReceivedEvent.
    public class DirectMessageService : IDisposable
    {
        // Published on the EventBus for every accepted inbound DM.
        public struct DirectMessageReceivedEvent
        {
            public ChatMessage Message;
        }

        private readonly IChatService         _chat;
        private readonly IFriendsService      _friends;
        private readonly ChatModerationFilter _filter;
        private readonly ReportService        _reports;
        private readonly SocialConfig         _config;

        public DirectMessageService(
            IChatService         chat,
            IFriendsService      friends,
            ChatModerationFilter filter,
            ReportService        reports,
            SocialConfig         config)
        {
            _chat    = chat;
            _friends = friends;
            _filter  = filter;
            _reports = reports;
            _config  = config;

            _chat.DirectMessageReceived += OnDirectMessageReceived;
        }

        public void Dispose() => _chat.DirectMessageReceived -= OnDirectMessageReceived;

        public async Task<ChatSendStatus> SendAsync(string playerId, string text)
        {
            if (_reports.IsBlocked(playerId))  return ChatSendStatus.Blocked;
            if (!_friends.IsFriend(playerId))  return ChatSendStatus.NotFriend;

            text = text?.Trim();
            if (string.IsNullOrEmpty(text))             return ChatSendStatus.Empty;
            if (text.Length > _config.MaxMessageLength) return ChatSendStatus.TooLong;

            if (_filter.Apply(text, out var prepared) == ChatFilterVerdict.Rejected)
                return ChatSendStatus.Filtered;

            await _chat.SendDirectMessageAsync(playerId, prepared);
            return ChatSendStatus.Sent;
        }

        private void OnDirectMessageReceived(ChatMessage message)
        {
            if (_reports.IsBlocked(message.SenderId) || _reports.IsMuted(message.SenderId)) return;

            if (!message.FromSelf)
                message.Text = _filter.SanitizeIncoming(message.Text);

            EventBus.Publish(new DirectMessageReceivedEvent { Message = message });
        }
    }
}
