namespace SocialUniverse.Core
{
    // Published by AuthScreen when the player presses Continue on the success panel.
    // AuthState subscribes to this to know when to unload the Auth scene and enter Hub.
    public readonly struct PlayerReadyEvent { }
}
