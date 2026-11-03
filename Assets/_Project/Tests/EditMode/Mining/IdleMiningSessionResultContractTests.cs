using Newtonsoft.Json;
using NUnit.Framework;
using SocialUniverse.Mining;

namespace SocialUniverse.Tests
{
    // Pins the StartIdleMining / GetIdleMining reply shapes as the real client reads them: the UGS
    // Cloud Code SDK binds case-insensitively with MissingMemberHandling.Error, so any field the
    // server returns that IdleMiningSessionResult lacks would throw after the server committed.
    // One sample per distinct return path in ServerCode/StartIdleMining.js and GetIdleMining.js.
    public class IdleMiningSessionResultContractTests
    {
        private static readonly JsonSerializerSettings CloudCodeSettings =
            new JsonSerializerSettings { MissingMemberHandling = MissingMemberHandling.Error };

        private static IdleMiningSessionResult Parse(string json) =>
            JsonConvert.DeserializeObject<IdleMiningSessionResult>(json, CloudCodeSettings);

        [Test]
        public void Start_success_binds()
        {
            var r = Parse("{\"success\":true,\"planetId\":\"earth\",\"slotId\":\"Iron#0\",\"mineralId\":\"iron\",\"elapsedSeconds\":0,\"durationSeconds\":240}");

            Assert.IsTrue(r.Success);
            Assert.AreEqual("earth", r.PlanetId);
            Assert.AreEqual("Iron#0", r.SlotId);
            Assert.AreEqual("iron", r.MineralId);
            Assert.AreEqual(240f, r.DurationSeconds);
            Assert.IsTrue(r.HasSession);
        }

        [Test]
        public void Already_mining_binds_with_the_existing_run()
        {
            var r = Parse("{\"success\":false,\"reason\":\"ALREADY_MINING\",\"planetId\":\"earth\",\"slotId\":\"Iron#1\",\"mineralId\":\"iron\",\"elapsedSeconds\":612.345,\"durationSeconds\":240}");

            Assert.IsFalse(r.Success);
            Assert.AreEqual("ALREADY_MINING", r.Reason);
            Assert.AreEqual("Iron#1", r.SlotId);
            Assert.AreEqual(612.345f, r.ElapsedSeconds, 0.001f);
            Assert.IsTrue(r.HasSession);
        }

        [TestCase("WRONG_PLANET")]
        [TestCase("INVALID_PARAMS")]
        [TestCase("CONFLICT")]
        public void Rejections_bind_without_a_run(string reason)
        {
            var r = Parse("{\"success\":false,\"reason\":\"" + reason + "\"}");

            Assert.IsFalse(r.Success);
            Assert.AreEqual(reason, r.Reason);
            Assert.IsFalse(r.HasSession);
        }

        [Test]
        public void Get_with_no_run_binds()
        {
            var r = Parse("{\"success\":true}");

            Assert.IsTrue(r.Success);
            Assert.IsFalse(r.HasSession);
        }
    }
}
