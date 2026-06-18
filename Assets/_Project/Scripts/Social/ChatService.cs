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

        // Vivox's own channel/message/block calls each internally "ensure
        // logged in" and will try to log in themselves if not yet logged in.
        // If our own ConnectAsync login is still in flight when one of these
        // fires (e.g. a chat-open click racing the boot-time connect), that
        // internal auto-login collides with ours and Vivox throws "must be
        // logged out". Waiting on the same in-flight connect task here means
        // these calls only ever reach Vivox once a login attempt has settled.
        private Task WaitForPendingConnectAsync() =>
            _connectTask != null && !_connectTask.IsCompleted ? _connectTask : Task.CompletedTask;

        public async Task JoinChannelAsync(string channelName)
        {
            await WaitForPendingConnectAsync();
            await VivoxService.Instance.JoinGroupChannelAsync(channelName, ChatCapability.TextOnly);
        }

        public async Task LeaveChannelAsync(string channelName)
        {
            await WaitForPendingConnectAsync();
            await VivoxService.Instance.LeaveChannelAsync(channelName);
        }

        public async Task SendMessageAsync(string channelName, string text)
        {
            await WaitForPendingConnectAsync();
            await VivoxService.Instance.SendChannelTextMessageAsync(channelName, text);
        }

        public async Task SendDirectMessageAsync(string playerId, string text)
        {
            await WaitForPendingConnectAsync();
            await VivoxService.Instance.SendDirectTextMessageAsync(text, playerId);
        }

        public async Task BlockPlayerAsync(string playerId)
        {
            await WaitForPendingConnectAsync();
            await VivoxService.Instance.BlockPlayerAsync(playerId);
        }

        public async Task UnblockPlayerAsync(string playerId)
        {
            await WaitForPendingConnectAsync();
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
