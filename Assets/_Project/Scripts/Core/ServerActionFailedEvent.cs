namespace SocialUniverse.Core
{
    // Published by App-layer handlers when the server rejects a player action (purchase,
    // upgrade, sale, ...). Message is already player-facing. ToastView is the subscriber.
    public readonly struct ServerActionFailedEvent
    {
        public readonly string Message;
        public ServerActionFailedEvent(string message) => Message = message;
    }
}
