namespace SocialUniverse.Mining
{
    // App -> UI: the result of a mineral sale, for the Mineral Inventory's sale popup. Message is the
    // player-facing failure text (ServerFailureMessages), "" on success.
    public class MineralSaleCompletedEvent
    {
        public bool   Success;
        public string Message;
    }
}
