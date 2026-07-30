namespace SocialUniverse.Core
{
    // Published by PlanetSceneScope when a signed-in player has no real display name
    // on their profile or auth session (a fresh Google/SSO account). HUDController
    // subscribes to open the avatar/name modal in mandatory onboarding mode.
    public readonly struct ShowProfileOnboardingEvent { }
}
