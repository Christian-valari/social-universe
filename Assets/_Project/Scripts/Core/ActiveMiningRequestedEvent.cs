namespace SocialUniverse.Core
{
    // Published by MiningModePromptView once MiningController.BeginActiveMining has populated
    // ActiveMiningHandoff. Indirected through the event bus (rather than MiningModePromptView
    // injecting PlanetState directly) so Planet's standalone/no-Bootstrap dev mode — which never
    // registers PlanetState — doesn't break; same reasoning as LaunchRequestedEvent.
    public class ActiveMiningRequestedEvent { }
}
