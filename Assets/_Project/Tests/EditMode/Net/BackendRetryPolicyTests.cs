using NUnit.Framework;
using SocialUniverse.Net;

namespace SocialUniverse.Tests
{
    // Known Issue #16: BackendClient retried on CloudCodeExceptionReason.Unknown (e.g. a timeout)
    // for every function, so a purchase that committed server-side but timed out client-side
    // could be charged again. Ambiguous failures are now retried only for read-only functions.
    public class BackendRetryPolicyTests
    {
        [TestCase("GetLandRegistry")]
        [TestCase("GetFuelState")]
        [TestCase("GetPlayerProfile")]
        [TestCase("GetBootstrapState")]
        public void Read_only_functions_may_be_retried_after_an_ambiguous_failure(string function) =>
            Assert.IsTrue(BackendRetryPolicy.IsSafeToRetryAmbiguousFailure(function));

        [TestCase("PurchaseLand")]
        [TestCase("AcquireDrone")]
        [TestCase("UpgradeDrone")]
        [TestCase("SellMinerals")]
        [TestCase("ValidateMining")]
        [TestCase("SpendFuel")]
        [TestCase("Getaway")] // prefix must be a whole "Get" + capital, not any word starting "Get"
        [TestCase(null)]
        public void Mutating_functions_are_never_retried_after_an_ambiguous_failure(string function) =>
            Assert.IsFalse(BackendRetryPolicy.IsSafeToRetryAmbiguousFailure(function));
    }
}
