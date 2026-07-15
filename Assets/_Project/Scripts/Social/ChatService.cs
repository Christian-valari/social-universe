using System;
using System.Threading.Tasks;
using SocialUniverse.Core;
using Unity.Services.Vivox;

namespace SocialUniverse.Social
{
    // IChatService implementation backed by UGS Vivox (text-only channels).
    // Requires UnityServices + Authentication sign-in to have completed first
    // (NetworkBootstrap / AuthService handle that), and Vivox to be enabled on
    // the project in the Unity Dashboard.
    public class ChatService : IChatService, IDisposable
    {
        public event Action<ChatMessage> MessageReceived;
        public event Action<ChatMessage> DirectMessageReceived;

        private bool _initialized;
        private string _lastDisplayName;
        private string _selfAvatarId;

        // Tracks an in-flight login so a second concurrent ConnectAsync call
        // (e.g. a re-fired PlayerReadyEvent across a domain reload) awaits the
        // same task instead of issuing a second VivoxService.LoginAsync —
        // Vivox's LoginSession throws "must be logged out" if Login is called
        // while a previous login for this session hasn't finished yet.
        private Task _connectTask;

        public bool IsConnected => _initialized && VivoxService.Instance != null && VivoxService.Instance.IsLoggedIn;

        public Task ConnectAsync(string displayName, string avatarId)
        {
            _lastDisplayName = displayName;
            _selfAvatarId    = avatarId;
            if (IsConnected) return Task.CompletedTask;
            if (_connectTask != null && !_connectTask.IsCompleted) return _connectTask;

            _connectTask = DoConnectAsync(displayName);
            return _connectTask;
        }

        private async Task DoConnectAsync(string displayName)
        {
            await VivoxService.Instance.InitializeAsync();

            if (!_initialized)
            {
                VivoxService.Instance.ChannelMessageReceived  += OnChannelMessage;
                VivoxService.Instance.DirectedMessageReceived += OnDirectedMessage;
                _initialized = true;
            }

            if (VivoxService.Instance.IsLoggedIn) return;

            var options = new LoginOptions { DisplayName = displayName };
            await VivoxService.Instance.LoginAsync(options);
            SULog.Info($"ChatService: logged in to Vivox as '{displayName}'", SULog.Channel.Social);
        }

        public async Task DisconnectAsync()
        {
            if (!IsConnected) return;
            await VivoxService.Instance.LogoutAsync();
        }

        // Vivox's own channel/message/block calls each internally "ensure
        // logged in" and will auto-init/login themselves if called before
        // Vivox is ready — which queues an internal login action. If our own
        // managed ConnectAsync also logs in around the same time (e.g. once
        // PlayerReadyEvent fires moments later), that explicit LoginAsync
        // collides with Vivox's own queued one and throws "must be logged
        // out". So every Vivox-touching call below routes through this
        // instead of ever letting Vivox auto-init/login on its own: if no
        // connect has started yet, it starts the SAME managed ConnectAsync
        // these calls then await, so Vivox is always already logged in by
        // the time any of them actually reach the SDK.
        private Task EnsureConnectedAsync()
        {
            if (IsConnected) return Task.CompletedTask;
            if (_connectTask != null && !_connectTask.IsCompleted) return _connectTask;
            return ConnectAsync(_lastDisplayName ?? ChatDisplayNameResolver.Fallback, _selfAvatarId);
        }

        public async Task JoinChannelAsync(string channelName)
        {
            await EnsureConnectedAsync();
            await VivoxService.Instance.JoinGroupChannelAsync(channelName, ChatCapability.TextOnly);
        }

        public async Task LeaveChannelAsync(string channelName)
        {
            await EnsureConnectedAsync();
            await VivoxService.Instance.LeaveChannelAsync(channelName);
        }

        public async Task SendMessageAsync(string channelName, string text)
        {
            await EnsureConnectedAsync();
            await VivoxService.Instance.SendChannelTextMessageAsync(channelName, text);
        }

        public async Task SendDirectMessageAsync(string playerId, string text)
        {
            await EnsureConnectedAsync();
            await VivoxService.Instance.SendDirectTextMessageAsync(text, playerId);
        }

        public async Task BlockPlayerAsync(string playerId)
        {
            await EnsureConnectedAsync();
            await VivoxService.Instance.BlockPlayerAsync(playerId);
        }

        public async Task UnblockPlayerAsync(string playerId)
        {
            await EnsureConnectedAsync();
            await VivoxService.Instance.UnblockPlayerAsync(playerId);
        }

        public void Dispose()
        {
            if (!_initialized || VivoxService.Instance == null) return;
            VivoxService.Instance.ChannelMessageReceived  -= OnChannelMessage;
            VivoxService.Instance.DirectedMessageReceived -= OnDirectedMessage;
        }

        private void OnChannelMessage(VivoxMessage message) =>
            MessageReceived?.Invoke(ToChatMessage(message, isDirect: false));

        private void OnDirectedMessage(VivoxMessage message) =>
            DirectMessageReceived?.Invoke(ToChatMessage(message, isDirect: true));

        private ChatMessage ToChatMessage(VivoxMessage message, bool isDirect) => new()
        {
            SenderId          = message.SenderPlayerId,
            SenderDisplayName = message.SenderDisplayName,
            AvatarId          = message.FromSelf ? _selfAvatarId : null,
            ChannelName       = message.ChannelName,
            Text              = message.MessageText,
            TimestampMs       = new DateTimeOffset(message.ReceivedTime).ToUnixTimeMilliseconds(),
            FromSelf          = message.FromSelf,
            IsDirect          = isDirect
        };
    }
}
