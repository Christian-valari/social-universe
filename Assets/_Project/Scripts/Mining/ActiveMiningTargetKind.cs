namespace SocialUniverse.Mining
{
    // A Decoy looks different and is a miss if tapped; left alone it closes at no cost. A Vein is
    // dragged, not tapped, and has the screen to itself while it is live.
    public enum ActiveMiningTargetKind { Normal, Decoy, Vein }
}
