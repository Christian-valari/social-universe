namespace SocialUniverse.Mining
{
    // Published when the server rejects a mining claim (ValidateMining returned granted: 0 with a
    // reason such as BUDGET_EXHAUSTED or TIER_TOO_LOW). The asteroid is still consumed.
    // App's MiningClaimFeedbackHandler turns it into a player-facing toast.
    public class MiningClaimRejectedEvent
    {
        public string MineralId;
        public string Reason;
    }
}
