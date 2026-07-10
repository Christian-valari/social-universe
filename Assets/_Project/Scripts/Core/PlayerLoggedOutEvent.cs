namespace SocialUniverse.Core
{
    // Published by LogoutState once sign-out completes, before the Auth scene
    // loads. SocialServicesInitializer subscribes to disconnect chat — the
    // symmetric teardown to what it does on PlayerReadyEvent.
    public readonly struct PlayerLoggedOutEvent { }
}
