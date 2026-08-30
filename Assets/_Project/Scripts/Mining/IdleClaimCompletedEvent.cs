namespace SocialUniverse.Mining
{
    // Published when a player-directed idle-mining session is successfully claimed and the
    // minerals have actually been granted. Carries the granted amount (not the pre-computed
    // estimate) so UI can celebrate exactly what landed in the inventory. Drives the
    // "Claimed!" reward modal in the Planet HUD.
    public class IdleClaimCompletedEvent
    {
        public string MineralId;
        public int    Quantity;
    }
}
