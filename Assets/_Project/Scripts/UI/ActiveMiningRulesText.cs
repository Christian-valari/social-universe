using SocialUniverse.Config;

namespace SocialUniverse.UI
{
    // What the pre-game panel tells the player about how a run works. Takes the run's settings so a
    // rule that depends on them can add its own line; settings is null when the scene is opened on
    // its own. Kept pure so it can be unit-tested.
    public static class ActiveMiningRulesText
    {
        public static string For(ActiveMiningRunSettings settings) =>
            "Tap each target before its ring closes. Earlier taps bank more.";
    }
}
