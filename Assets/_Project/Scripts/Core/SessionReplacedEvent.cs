namespace SocialUniverse.Core
{
    // Published by BackendClient when a Cloud Code call is rejected with SESSION_REPLACED: this
    // account signed in on another device, so this device's session is no longer the active one.
    // ActiveSessionController asks the player to quit.
    public readonly struct SessionReplacedEvent { }
}
