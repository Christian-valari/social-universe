using Newtonsoft.Json;
using NUnit.Framework;
using SocialUniverse.Travel;

namespace SocialUniverse.Tests
{
    // Cloud Code responses are deserialized by the UGS SDK with Newtonsoft's default (case-insensitive)
    // contract resolver and MissingMemberHandling.Error (JsonObject.GetAs<T>): any field a function
    // returns that its C# result type lacks makes the whole call throw, after the server has already
    // committed. These pin the response shapes the 2026-09-30 contract audit found at risk, one JSON
    // sample per distinct return path. Int fields must use integer literals: "1.0" into int throws too.
    public class CloudCodeResponseContractTests
    {
        private static readonly JsonSerializerSettings CloudCodeSettings =
            new JsonSerializerSettings { MissingMemberHandling = MissingMemberHandling.Error };

        private static T Parse<T>(string json) => JsonConvert.DeserializeObject<T>(json, CloudCodeSettings);

        // ---- RefillFuel (ServerCode/RefillFuel.js) ----

        [Test]
        public void RefillFuel_success_binds()
        {
            var r = Parse<FuelStateResult>("{\"success\":true,\"fuel\":100,\"maxFuel\":100,\"newBalance\":450}");
            Assert.IsTrue(r.Success);
            Assert.AreEqual(450, r.NewBalance);
        }

        [TestCase("already_full")]
        [TestCase("insufficient_funds")]
        [TestCase("write_failed")]
        public void RefillFuel_failures_bind_with_their_reason(string reason)
        {
            var r = Parse<FuelStateResult>(
                "{\"success\":false,\"reason\":\"" + reason + "\",\"fuel\":42.5,\"maxFuel\":100,\"newBalance\":450}");
            Assert.IsFalse(r.Success);
            Assert.AreEqual(reason, r.Reason);
            Assert.AreEqual(42.5f, r.Fuel);
        }

        // ---- StartTravel (ServerCode/StartTravel.js) ----

        [Test]
        public void StartTravel_already_traveling_binds()
        {
            var r = Parse<TravelTripResult>("{\"success\":false,\"reason\":\"already_traveling\"}");
            Assert.IsFalse(r.Success);
            Assert.AreEqual("already_traveling", r.Reason);
        }

        [Test]
        public void StartTravel_insufficient_fuel_binds()
        {
            var r = Parse<TravelTripResult>("{\"success\":false,\"reason\":\"insufficient_fuel\",\"fuel\":5,\"maxFuel\":100}");
            Assert.AreEqual("insufficient_fuel", r.Reason);
            Assert.AreEqual(5f, r.Fuel);
        }
    }
}
