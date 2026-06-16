using System;
using System.Threading.Tasks;

namespace SocialUniverse.Social
{
    // Provider abstraction for text chat (architecture rule 2: backend behind
    // interfaces). ChatService implements this against Vivox; LocalMockChatService
    // is the offline/dev-mode loopback. Gameplay and UI code must depend on this
    // contract only — never on the Vivox SDK directly.
    public interface IChatService
    {
        // Raised for messages in any joined channel (including the local
        // player's own, with FromSelf = true).
        event Action<ChatMessage> MessageReceived;

        // Raised for direct (player-to-player) messages.
        event Action<ChatMessage> DirectMessageReceived;

        bool IsConnected { get; }

        Task ConnectAsync(string displayName);
        Task DisconnectAsync();

        Task JoinChannelAsync(string channelName);
        Task LeaveChannelAsync(string channelName);

        Task SendMessageAsync(string channelName, string text);
        Task SendDirectMessageAsync(string playerId, string text);

        // Provider-level block: the provider stops delivering this player's
        // messages to us entirely. ReportService also keeps its own list so
        // blocks apply in dev mode / before connect.
        Task BlockPlayerAsync(string playerId);
        Task UnblockPlayerAsync(string playerId);
    }
}
