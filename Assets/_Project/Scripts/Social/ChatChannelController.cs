using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SocialUniverse.Config;
using SocialUniverse.Core;

namespace SocialUniverse.Social
{
    // Owns the player's active text channel (global / planet-local / guild),
    // pushes outbound messages through the moderation filter, and fans
    // accepted inbound messages out on the EventBus — dropping anything from
    // blocked or muted players. UI (a future ChatScreen) subscribes to
    // ChatMessageReceivedEvent and calls SendAsync / SwitchTo*Async.
    public class ChatChannelController : IDisposable
    {
        // Published on the EventBus for every accepted inbound channel message.
        public struct ChatMessageReceivedEvent
        {
            public ChatMessage Message;
        }

        private readonly IChatService         _chat;
        private readonly ChatModerationFilter _filter;
        private readonly ReportService        _reports;
        private readonly SocialConfig         _config;

        private readonly Dictionary<string, List<ChatMessage>> _history = new();

        public string ActiveChannel { get; private set; }

        public ChatChannelController(
            IChatService         chat,
            ChatModerationFilter filter,
            ReportService        reports,
            SocialConfig         config)
        {
            _chat    = chat;
            _filter  = filter;
            _reports = reports;
            _config  = config;

            _chat.MessageReceived += OnMessageReceived;
        }

        public void Dispose() => _chat.MessageReceived -= OnMessageReceived;

        public static string LocalChannelName(string planetId) => $"planet_{planetId}".ToLowerInvariant();
        public static string GuildChannelName(string guildId)  => $"guild_{guildId}".ToLowerInvariant();

        // Tracks the most recently requested channel switch so SendAsync can wait
        // on it instead of failing with NoChannel while a join is still in flight.
        private Task _pendingSwitch = Task.CompletedTask;

        public Task SwitchToGlobalAsync()                => SwitchToAsync(_config.GlobalChannelName);
        public Task SwitchToLocalAsync(string planetId)  => SwitchToAsync(LocalChannelName(planetId));
        public Task SwitchToGuildAsync(string guildId)   => SwitchToAsync(GuildChannelName(guildId)); // guilds land in M7

        public IReadOnlyList<ChatMessage> GetHistory(string channelName) =>
            !string.IsNullOrEmpty(channelName) && _history.TryGetValue(channelName, out var list)
                ? list
                : Array.Empty<ChatMessage>();

        // Bounds how long a send waits on the backend before reporting Failed —
        // a hung Vivox call would otherwise leave SendAsync's awaiter (the UI)
        // stuck forever with no error and no status update.
        private const int SendTimeoutMs = 10_000;

        public async Task<ChatSendStatus> SendAsync(string text)
        {
            // A channel switch may still be in flight (e.g. boot-time join to the
            // global channel hasn't completed yet) — wait for it before giving up,
            // so an early send doesn't silently fail with NoChannel.
            if (string.IsNullOrEmpty(ActiveChannel))
            {
                try { await _pendingSwitch; }
                catch { /* surfaced as NoChannel below */ }
            }

            if (string.IsNullOrEmpty(ActiveChannel)) return ChatSendStatus.NoChannel;

            text = text?.Trim();
            if (string.IsNullOrEmpty(text))             return ChatSendStatus.Empty;
            if (text.Length > _config.MaxMessageLength) return ChatSendStatus.TooLong;

            if (_filter.Apply(text, out var prepared) == ChatFilterVerdict.Rejected)
            {
                SULog.Info("ChatChannelController: message rejected by moderation filter", SULog.Channel.Social);
                return ChatSendStatus.Filtered;
            }

            try
            {
                var sendTask = _chat.SendMessageAsync(ActiveChannel, prepared);
                if (await Task.WhenAny(sendTask, Task.Delay(SendTimeoutMs)) != sendTask)
                {
                    SULog.Warn($"ChatChannelController: send to '{ActiveChannel}' timed out", SULog.Channel.Social);
                    return ChatSendStatus.Failed;
                }

                await sendTask;
                return ChatSendStatus.Sent;
            }
            catch (Exception ex)
            {
                SULog.Warn($"ChatChannelController: send to '{ActiveChannel}' failed ({ex.Message})", SULog.Channel.Social);
                return ChatSendStatus.Failed;
            }
        }

        private Task SwitchToAsync(string channelName)
        {
            var task = DoSwitchAsync(channelName);
            _pendingSwitch = task;
            return task;
        }

        private async Task DoSwitchAsync(string channelName)
        {
            if (channelName == ActiveChannel) return;

            await _chat.JoinChannelAsync(channelName);

            var previous  = ActiveChannel;
            ActiveChannel = channelName;

            if (!string.IsNullOrEmpty(previous))
                await _chat.LeaveChannelAsync(previous);
        }

        private void OnMessageReceived(ChatMessage message)
        {
            if (_reports.IsBlocked(message.SenderId) || _reports.IsMuted(message.SenderId)) return;

            // Inbound safety net: we can't stop another client sending, but we
            // can mask what we display.
            if (!message.FromSelf)
                message.Text = _filter.SanitizeIncoming(message.Text);

            Append(message);
            EventBus.Publish(new ChatMessageReceivedEvent { Message = message });
        }

        private void Append(ChatMessage message)
        {
            var channel = message.ChannelName ?? "";
            if (!_history.TryGetValue(channel, out var list))
                _history[channel] = list = new List<ChatMessage>();

            list.Add(message);
            if (list.Count > _config.ChatHistoryLimit)
                list.RemoveAt(0);
        }
    }
}
