namespace SocialUniverse.Social
{
    // Outcome of a chat / DM send attempt. Shared by ChatChannelController and
    // DirectMessageService so UI can show one consistent set of error states.
    public enum ChatSendStatus
    {
        Sent,
        Empty,       // nothing left after trimming
        TooLong,     // exceeds SocialConfig.MaxMessageLength
        Filtered,    // rejected by ChatModerationFilter at Strict level
        NoChannel,   // no active channel joined yet
        NotFriend,   // DMs are restricted to friends
        Blocked,     // target is on the local block list
        Failed       // backend send timed out or threw
    }
}
