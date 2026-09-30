using Newtonsoft.Json;
using NUnit.Framework;
using SocialUniverse.Mining;

namespace SocialUniverse.Tests
{
    // Pins the ValidateMining response contract as the real client reads it. The UGS Cloud Code SDK
    // deserializes CallEndpointAsync<T> results with Newtonsoft's default contract resolver
    // (case-insensitive member matching) and MissingMemberHandling.Error (JsonObject.GetAs<T>), so:
    // the server's camelCase fields must bind to MiningGrantResult's PascalCase ones, and any field
    // the server returns that the DTO lacks would throw. Server shape: ServerCode/ValidateMining.js.
    public class MiningGrantResultContractTests
    {
        private static readonly JsonSerializerSettings CloudCodeSettings =
            new JsonSerializerSettings { MissingMemberHandling = MissingMemberHandling.Error };

        [Test]
        public void Success_response_binds_to_the_dto()
        {
            var r = JsonConvert.DeserializeObject<MiningGrantResult>("{\"granted\":12,\"mineralId\":\"iron\"}", CloudCodeSettings);

            Assert.AreEqual(12, r.Granted);
            Assert.AreEqual("iron", r.MineralId);
            Assert.IsNull(r.Reason);
        }

        [Test]
        public void Rejection_response_binds_to_the_dto()
        {
            var r = JsonConvert.DeserializeObject<MiningGrantResult>(
                "{\"granted\":0,\"mineralId\":\"iron\",\"reason\":\"BUDGET_EXHAUSTED\"}", CloudCodeSettings);

            Assert.AreEqual(0, r.Granted);
            Assert.AreEqual("BUDGET_EXHAUSTED", r.Reason);
        }

        [Test]
        public void Invalid_params_rejection_with_a_null_mineral_binds_to_the_dto()
        {
            var r = JsonConvert.DeserializeObject<MiningGrantResult>(
                "{\"granted\":0,\"mineralId\":null,\"reason\":\"INVALID_PARAMS\"}", CloudCodeSettings);

            Assert.AreEqual("INVALID_PARAMS", r.Reason);
        }
    }
}
