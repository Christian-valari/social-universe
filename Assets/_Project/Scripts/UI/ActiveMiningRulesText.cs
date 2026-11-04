using SocialUniverse.Config;

namespace SocialUniverse.UI
{
    // What the pre-game panel tells the player about how a run works. Takes the run's settings so a
    // rule that depends on them can add its own line; settings is null when the scene is opened on
    // its own. A challenge is named on the tier that introduces it, not on the tiers above.
    // Kept pure so it can be unit-tested.
    public static class ActiveMiningRulesText
    {
        public static string For(ActiveMiningRunSettings settings)
        {
            string rules = "Tap each target before its ring closes. Earlier taps bank more.";

            if (settings != null && settings.Tier.DecoyChance > 0f
                && !(settings.PreviousTier != null && settings.PreviousTier.DecoyChance > 0f))
                rules += "\nNew: red X spots are decoys, so leave them alone.";

            return rules;
        }
    }
}
