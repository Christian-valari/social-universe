namespace SocialUniverse.UI
{
    // Pure validation for the Google Sign-In first-time display-name panel
    // (AuthScreen.ChooseName). Kept separate from AuthScreen's private
    // ValidateUsername (email registration) and DisplayNameModal's
    // SocialConfig-driven validation (in-game rename) so each flow's rules
    // can evolve independently — see
    // docs/superpowers/specs/2026-07-17-google-signin-display-name-design.md.
    public static class DisplayNameValidator
    {
        public const int MinLength = 2;
        public const int MaxLength = 20;

        // Trims before checking length/spaces; callers should use the same
        // trimmed value when committing the name.
        public static bool Validate(string name, out string error)
        {
            string trimmed = (name ?? string.Empty).Trim();

            if (trimmed.Length < MinLength)
            {
                error = $"Name must be at least {MinLength} characters";
                return false;
            }
            if (trimmed.Length > MaxLength)
            {
                error = $"Name must be {MaxLength} characters or fewer";
                return false;
            }
            if (trimmed.Contains(' '))
            {
                error = "Name cannot contain spaces";
                return false;
            }
            error = null;
            return true;
        }
    }
}
