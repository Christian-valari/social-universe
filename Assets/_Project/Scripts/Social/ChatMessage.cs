namespace SocialUniverse.Social
{
    // Provider-agnostic chat message. Produced by IChatService implementations
    // (Vivox, mock) and consumed by ChatChannelController / DirectMessageService.
    public class ChatMessage
    {
        public string SenderId;
        public string SenderDisplayName;
        public string ChannelName;   // null for direct messages
        public string Text;
        public long   TimestampMs;   // unix ms
        public bool   FromSelf;
        public bool   IsDirect;
    }
}
