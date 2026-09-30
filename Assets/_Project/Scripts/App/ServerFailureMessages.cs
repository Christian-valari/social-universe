namespace SocialUniverse.App
{
    // Turns a Cloud Code rejection reason (or a client-side "Network error" / "Empty response")
    // into the text shown in the failure toast. Unknown reasons fall back to "<action> failed".
    public static class ServerFailureMessages
    {
        public static string For(string action, string reason) => reason switch
        {
            "INSUFFICIENT_FUNDS"                 => "Not enough coins",
            "INSUFFICIENT_QTY"                   => "Not enough minerals to sell",
            "SLOTS_FULL"                         => "No free drone slot",
            "MAX_LEVEL"                          => "Already at max level",
            "ALREADY_OWNED"                      => "You already own this drone",
            "NOT_OWNED"                          => "You don't own this drone",
            "CONFLICT"                           => "Something changed. Try again.",
            "BUDGET_EXHAUSTED"                   => "No more asteroids here for now. Try again later.",
            "TIER_TOO_LOW"                       => "Requires a higher-tier drone",
            "WRONG_PLANET" or "MINERAL_NOT_ON_PLANET" => "Mining isn't available here right now",
            "Network error" or "Empty response" => "Couldn't reach the server. Try again.",
            _                                    => $"{action} failed"
        };
    }
}
