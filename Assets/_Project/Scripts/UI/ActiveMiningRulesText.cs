using System.Collections.Generic;
using SocialUniverse.Config;

namespace SocialUniverse.UI
{
    // What the pre-game panel tells the player about how a run works. Takes the run's settings so a
    // rule that depends on them can add its own line; settings is null when the scene is opened on
    // its own. Tiers are per mineral, so a player can reach one with the earlier ones skipped: the
    // line names every challenge this tier has, not only those new at it.
    // Kept pure so it can be unit-tested.
    public static class ActiveMiningRulesText
    {
        // Above the base range's top (ActiveMiningTierSettings' default), a rock's spin is a challenge.
        private const float BaseMaxSpinDegrees = 15f;

        public static string For(ActiveMiningRunSettings settings)
        {
            string rules = "Tap each target before its ring closes. Earlier taps bank more.";
            if (settings == null) return rules;

            var tier       = settings.Tier;
            var challenges = new List<string>();
            if (tier.DecoyChance > 0f)                    challenges.Add("red X decoys");
            if (tier.LiveTargets > 1)                     challenges.Add("two targets at once");
            if (tier.MaxSpinDegrees > BaseMaxSpinDegrees) challenges.Add("faster spin");
            if (tier.VeinEvery > 0)                       challenges.Add("dragging along lines");

            return challenges.Count == 0 ? rules : rules + "\nWatch for: " + string.Join(", ", challenges);
        }
    }
}
