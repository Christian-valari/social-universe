namespace SocialUniverse.Net
{
    // Decides whether BackendClient may retry a Cloud Code call whose outcome is unknown
    // (CloudCodeExceptionReason.Unknown — e.g. a timeout after the server already committed).
    // Only read-only functions are safe: retrying PurchaseLand/AcquireDrone/SellMinerals/... could
    // charge or pay twice (Known Issue #16). By ServerCode convention every read-only function is
    // named Get<Thing> (GetBootstrapState only writes an idempotent first-login seed).
    public static class BackendRetryPolicy
    {
        public static bool IsSafeToRetryAmbiguousFailure(string function) =>
            function != null
            && function.Length > 3
            && function.StartsWith("Get", System.StringComparison.Ordinal)
            && char.IsUpper(function[3]);
    }
}
