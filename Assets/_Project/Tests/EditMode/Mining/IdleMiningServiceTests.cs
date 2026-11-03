using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using SocialUniverse.Core;
using SocialUniverse.Mining;

namespace SocialUniverse.Tests
{
    // IdleMiningService is the client side of StartIdleMining / GetIdleMining
    // (docs/superpowers/plans/2026-10-15-server-tracked-idle-mining.md).
    public class IdleMiningServiceTests
    {
        private class ScriptedBackend : IBackendClient
        {
            public string SessionId { get; set; }
            public IdleMiningSessionResult Reply;
            public Exception               Throw;
            public string                     LastFunction;
            public Dictionary<string, object> LastArgs;

            public Task<T> CallAsync<T>(string function, Dictionary<string, object> args = null)
            {
                LastFunction = function;
                LastArgs     = args;
                if (Throw != null) throw Throw;
                return Task.FromResult(typeof(T) == typeof(IdleMiningSessionResult) ? (T)(object)Reply : default);
            }

            public Task CallAsync(string function, Dictionary<string, object> args = null) => Task.CompletedTask;
        }

        [Test]
        public async Task StartAsync_sends_the_run_to_StartIdleMining_and_returns_the_reply()
        {
            var reply   = new IdleMiningSessionResult { Success = true, PlanetId = "earth", SlotId = "Iron#0", MineralId = "iron", DurationSeconds = 240f };
            var backend = new ScriptedBackend { Reply = reply };

            var result = await new IdleMiningService(backend).StartAsync("earth", "Iron#0", "iron", 240f);

            Assert.AreEqual("StartIdleMining", backend.LastFunction);
            Assert.AreEqual("earth",  backend.LastArgs["planetId"]);
            Assert.AreEqual("Iron#0", backend.LastArgs["slotId"]);
            Assert.AreEqual("iron",   backend.LastArgs["mineralId"]);
            Assert.AreEqual(240f,     backend.LastArgs["durationSec"]);
            Assert.AreSame(reply, result);
        }

        [Test]
        public async Task StartAsync_sends_replaceSlotId_only_when_given()
        {
            var backend = new ScriptedBackend { Reply = new IdleMiningSessionResult { Success = true } };
            var svc     = new IdleMiningService(backend);

            await svc.StartAsync("earth", "Iron#0", "iron", 240f);
            Assert.IsFalse(backend.LastArgs.ContainsKey("replaceSlotId"));

            await svc.StartAsync("earth", "Iron#0", "iron", 240f, replaceSlotId: "Gold#0");
            Assert.AreEqual("Gold#0", backend.LastArgs["replaceSlotId"]);
        }

        [Test]
        public async Task StartAsync_turns_a_null_reply_into_a_failure()
        {
            var result = await new IdleMiningService(new ScriptedBackend()).StartAsync("earth", "Iron#0", "iron", 240f);

            Assert.IsFalse(result.Success);
            Assert.AreEqual("Empty response", result.Reason);
            Assert.IsFalse(result.HasSession);
        }

        [Test]
        public async Task StartAsync_turns_a_network_failure_into_a_failure_instead_of_throwing()
        {
            var backend = new ScriptedBackend { Throw = new InvalidOperationException("offline") };

            var result = await new IdleMiningService(backend).StartAsync("earth", "Iron#0", "iron", 240f);

            Assert.IsFalse(result.Success);
            Assert.AreEqual("Network error", result.Reason);
        }

        [Test]
        public async Task GetAsync_reads_GetIdleMining()
        {
            var reply   = new IdleMiningSessionResult { Success = true, PlanetId = "earth", SlotId = "Iron#0", ElapsedSeconds = 600f, DurationSeconds = 240f };
            var backend = new ScriptedBackend { Reply = reply };

            var result = await new IdleMiningService(backend).GetAsync();

            Assert.AreEqual("GetIdleMining", backend.LastFunction);
            Assert.AreSame(reply, result);
            Assert.IsTrue(result.HasSession);
        }

        [Test]
        public async Task GetAsync_reads_a_null_reply_as_no_run()
        {
            var result = await new IdleMiningService(new ScriptedBackend()).GetAsync();

            Assert.IsTrue(result.Success);
            Assert.IsFalse(result.HasSession);
        }

        // The Planet scene decides what a failed read means (fall back to a legacy local session), so
        // the service must not disguise it as "no run".
        [Test]
        public void GetAsync_lets_a_network_failure_through()
        {
            var backend = new ScriptedBackend { Throw = new InvalidOperationException("offline") };

            Assert.ThrowsAsync<InvalidOperationException>(async () => await new IdleMiningService(backend).GetAsync());
        }

        [Test]
        public async Task LocalMock_starts_every_run_and_has_none_to_restore()
        {
            var mock = new LocalMockIdleMiningService();

            var started = await mock.StartAsync("earth", "Iron#0", "iron", 30f);
            var current = await mock.GetAsync();

            Assert.IsTrue(started.Success);
            Assert.AreEqual("Iron#0", started.SlotId);
            Assert.AreEqual(30f, started.DurationSeconds);
            Assert.AreEqual(0f, started.ElapsedSeconds);
            Assert.IsTrue(current.Success);
            Assert.IsFalse(current.HasSession);
        }
    }
}
