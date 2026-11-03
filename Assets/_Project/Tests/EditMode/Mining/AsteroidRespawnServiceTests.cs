using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using SocialUniverse.Core;
using SocialUniverse.Mining;

namespace SocialUniverse.Tests
{
    // AsteroidRespawnService is the client side of ServerCode/GetAsteroidRespawns.js: which of this
    // planet's asteroid slots the player emptied in the last few hours, on any device.
    public class AsteroidRespawnServiceTests
    {
        private class ScriptedBackend : IBackendClient
        {
            public string SessionId { get; set; }
            public AsteroidRespawnsResult Reply;
            public Exception              Throw;
            public string                     LastFunction;
            public Dictionary<string, object> LastArgs;

            public Task<T> CallAsync<T>(string function, Dictionary<string, object> args = null)
            {
                LastFunction = function;
                LastArgs     = args;
                if (Throw != null) throw Throw;
                return Task.FromResult(typeof(T) == typeof(AsteroidRespawnsResult) ? (T)(object)Reply : default);
            }

            public Task CallAsync(string function, Dictionary<string, object> args = null) => Task.CompletedTask;
        }

        [Test]
        public async Task GetAsync_asks_GetAsteroidRespawns_for_the_planet()
        {
            var reply = new AsteroidRespawnsResult
            {
                Success  = true,
                Respawns = new List<AsteroidRespawn> { new AsteroidRespawn { SlotId = "Iron#1", MineralId = "iron", SecondsLeft = 600f } }
            };
            var backend = new ScriptedBackend { Reply = reply };

            var respawns = await new AsteroidRespawnService(backend).GetAsync("earth");

            Assert.AreEqual("GetAsteroidRespawns", backend.LastFunction);
            CollectionAssert.AreEquivalent(new[] { "planetId" }, backend.LastArgs.Keys);
            Assert.AreEqual("earth", backend.LastArgs["planetId"]);
            Assert.AreEqual(1, respawns.Count);
            Assert.AreEqual("Iron#1", respawns[0].SlotId);
            Assert.AreEqual(600f, respawns[0].SecondsLeft);
        }

        [Test]
        public async Task GetAsync_reads_a_reply_without_respawns_as_none()
        {
            var noList = await new AsteroidRespawnService(new ScriptedBackend { Reply = new AsteroidRespawnsResult { Success = true } }).GetAsync("earth");
            var noReply = await new AsteroidRespawnService(new ScriptedBackend()).GetAsync("earth");

            CollectionAssert.IsEmpty(noList);
            CollectionAssert.IsEmpty(noReply);
        }

        // The Planet scene must not open with every asteroid back when the read failed (it would let
        // the player mine slots the server already counts), so the failure reaches the caller.
        [Test]
        public void GetAsync_lets_a_network_failure_through()
        {
            var backend = new ScriptedBackend { Throw = new InvalidOperationException("offline") };

            Assert.ThrowsAsync<InvalidOperationException>(async () => await new AsteroidRespawnService(backend).GetAsync("earth"));
        }

        [Test]
        public async Task LocalMock_has_no_respawns()
        {
            CollectionAssert.IsEmpty(await new LocalMockAsteroidRespawnService().GetAsync("earth"));
        }
    }
}
