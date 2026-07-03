namespace SocialUniverse.Core
{
    // Published by PlanetSceneScope the first time a signed-in player's profile
    // hydrates with emailVerified == false and they haven't been prompted before
    // (tracked locally — see SaveKeys.EmailVerificationPromptedKey). HUDController
    // subscribes to open EmailVerificationModal.
    public readonly struct ShowEmailVerificationPromptEvent { }
}
