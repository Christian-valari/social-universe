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

        // Tracks an in-flight login so a second concurrent ConnectAsync call
        // (e.g. a re-fired PlayerReadyEvent across a domain reload) awaits the
        // same task instead of issuing a second VivoxService.LoginAsync —
        // Vivox's LoginSession throws "must be logged out" if Login is called
        // while a previous login for this session hasn't finished yet.
        private Task _connectTask;

        public bool IsConnected => _initialized && VivoxService.Instance != null && VivoxService.Instance.IsLoggedIn;

        public Task ConnectAsync(string displayName)
        {
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

            var options = new LoginOptions { DisplayName = displayName };
            await VivoxService.Instance.LoginAsync(options);
            SULog.Info($"ChatService: logged in to Vivox as '{displayName}'", SULog.Channel.Social);
        }

        public async Task DisconnectAsync()
        {
            if (!IsConnected) return;
            await VivoxService.Instance.LogoutAsync();
        }

        public Task JoinChannelAsync(string channelName) =>
            VivoxService.Instance.JoinGroupChannelAsync(channelName, ChatCapability.TextOnly);

        public Task LeaveChannelAsync(string channelName) =>
            VivoxService.Instance.LeaveChannelAsync(channelName);

        public Task SendMessageAsync(string channelName, string text) =>
            VivoxService.Instance.SendChannelTextMessageAsync(channelName, text);

        public Task SendDirectMessageAsync(string playerId, string text) =>
            VivoxService.Instance.SendDirectTextMessageAsync(text, playerId);

        public Task BlockPlayerAsync(string playerId) =>
            VivoxService.Instance.BlockPlayerAsync(playerId);

        public Task UnblockPlayerAsync(string playerId) =>
            VivoxService.Instance.UnblockPlayerAsync(playerId);

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

        private static ChatMessage ToChatMessage(VivoxMessage message, bool isDirect) => new()
        {
            SenderId          = message.SenderPlayerId,
            SenderDisplayName = message.SenderDisplayName,
            ChannelName       = message.ChannelName,
            Text              = message.MessageText,
            TimestampMs       = new DateTimeOffset(message.ReceivedTime).ToUnixTimeMilliseconds(),
            FromSelf          = message.FromSelf,
            IsDirect          = isDirect
        };
    }
}
