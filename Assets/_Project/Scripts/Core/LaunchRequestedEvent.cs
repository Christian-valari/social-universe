namespace SocialUniverse.Core
{
    // Published by HUDController's LaunchButton. LaunchButtonHandler (App layer) is the
    // sole subscriber — it owns the FSM transition back to HubState, same "UI publishes
    // intent, App layer owns side effects" pattern as TilePurchaseHandler/TravelController.
    public class LaunchRequestedEvent { }
}
