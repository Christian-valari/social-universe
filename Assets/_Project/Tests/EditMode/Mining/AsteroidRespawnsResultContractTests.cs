using Newtonsoft.Json;
using NUnit.Framework;
using SocialUniverse.Mining;

namespace SocialUniverse.Tests
{
    // Pins the GetAsteroidRespawns reply shape as the real client reads it: the UGS Cloud Code SDK
    // binds case-insensitively with MissingMemberHandling.Error, so any field the server returns that
    // AsteroidRespawnsResult / AsteroidRespawn lacks would throw. Server: ServerCode/GetAsteroidRespawns.js.
    public class AsteroidRespawnsResultContractTests
    {
        private static readonly JsonSerializerSettings CloudCodeSettings =
            new JsonSerializerSettings { MissingMemberHandling = MissingMemberHandling.Error };

        private static AsteroidRespawnsResult Parse(string json) =>
            JsonConvert.DeserializeObject<AsteroidRespawnsResult>(json, CloudCodeSettings);

        [Test]
        public void Respawns_bind()
        {
            var r = Parse("{\"success\":true,\"respawns\":[{\"slotId\":\"Iron#1\",\"mineralId\":\"iron\",\"secondsLeft\":5400}," +
                          "{\"slotId\":\"Carbon#0\",\"mineralId\":\"carbon\",\"secondsLeft\":12.5}]}");

            Assert.IsTrue(r.Success);
            Assert.AreEqual(2, r.Respawns.Count);
            Assert.AreEqual("Iron#1", r.Respawns[0].SlotId);
            Assert.AreEqual("iron", r.Respawns[0].MineralId);
            Assert.AreEqual(5400f, r.Respawns[0].SecondsLeft);
            Assert.AreEqual(12.5f, r.Respawns[1].SecondsLeft, 0.001f);
        }

        [Test]
        public void None_binds()
        {
            var r = Parse("{\"success\":true,\"respawns\":[]}");

            Assert.IsTrue(r.Success);
            CollectionAssert.IsEmpty(r.Respawns);
        }
    }
}
