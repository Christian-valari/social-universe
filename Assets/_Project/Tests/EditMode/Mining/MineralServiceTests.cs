using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using SocialUniverse.Config;
using SocialUniverse.Core;
using SocialUniverse.Economy;
using SocialUniverse.Mining;

namespace SocialUniverse.Tests
{
    public class MineralServiceTests
    {
        private class FakeBackendClient : IBackendClient
        {
            public string SessionId { get; set; }
            public SellResult        SellResponse;
            public MiningGrantResult GrantResponse;
            public string                     LastFunction;
            public Dictionary<string, object> LastArgs;

            public Task<T> CallAsync<T>(string function, Dictionary<string, object> args = null)
            {
                LastFunction = function;
                LastArgs     = args;
                if (function == "SellMinerals" && typeof(T) == typeof(SellResult))
                    return Task.FromResult((T)(object)SellResponse);
                if (function == "ValidateMining" && typeof(T) == typeof(MiningGrantResult))
                    return Task.FromResult((T)(object)GrantResponse);
                return Task.FromResult(default(T));
            }
            public Task CallAsync(string function, Dictionary<string, object> args = null) => Task.CompletedTask;
        }

        [Test]
        public async Task SellAsync_success_applies_balance_and_remaining_inventory()
        {
            var backend = new FakeBackendClient
            {
                SellResponse = new SellResult
                {
                    Success = true, NewBalance = 620,
                    RemainingInventory = new Dictionary<string, int> { { "iron", 2 } }
                }
            };
            var wallet = new Wallet();
            var inv    = new MineralInventory();
            inv.SetAll(new Dictionary<string, int> { { "iron", 12 } });
            var svc = new MineralService(backend, inv, wallet);

            var result = await svc.SellAsync("iron", 10);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(620, wallet.Coins);
            Assert.AreEqual(2, inv.Get("iron"));
        }

        [Test]
        public async Task SellAsync_failure_leaves_wallet_and_inventory_unchanged()
        {
            var backend = new FakeBackendClient
            {
                SellResponse = new SellResult { Success = false, Reason = "INSUFFICIENT_QTY" }
            };
            var wallet = new Wallet();
            var inv    = new MineralInventory();
            inv.SetAll(new Dictionary<string, int> { { "iron", 12 } });
            var svc = new MineralService(backend, inv, wallet);

            var result = await svc.SellAsync("iron", 99);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(0, wallet.Coins);
            Assert.AreEqual(12, inv.Get("iron"));
        }

        [Test]
        public async Task LocalMock_SellAll_pays_total_and_empties_inventory()
        {
            var iron = ScriptableObject.CreateInstance<MineralDefinition>();
            typeof(MineralDefinition).GetField("_mineralId", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(iron, "iron");
            typeof(MineralDefinition).GetField("_sellValue", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(iron, 3);
            var reg = ScriptableObject.CreateInstance<DatabaseRegistry>();
            typeof(DatabaseRegistry).GetField("_minerals", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(reg, new[] { iron });

            var wallet = new Wallet();
            var inv    = new MineralInventory();
            inv.SetAll(new Dictionary<string, int> { { "iron", 4 } });
            var mock = new LocalMockMineralService(inv, wallet, reg);

            var result = await mock.SellAllAsync();

            Assert.IsTrue(result.Success);
            Assert.AreEqual(12, wallet.Coins); // 4 * 3
            Assert.AreEqual(0, inv.Get("iron"));

            Object.DestroyImmediate(iron); Object.DestroyImmediate(reg);
        }

        [Test]
        public async Task GrantMiningAsync_sends_planet_mineral_and_quantity_only()
        {
            var backend = new FakeBackendClient { GrantResponse = new MiningGrantResult { Granted = 12, MineralId = "iron" } };
            var svc = new MineralService(backend, new MineralInventory(), new Wallet());

            await svc.GrantMiningAsync("earth", "iron", 12);

            Assert.AreEqual("ValidateMining", backend.LastFunction);
            CollectionAssert.AreEquivalent(new[] { "planetId", "mineralId", "claimedQty" }, backend.LastArgs.Keys);
            Assert.AreEqual("earth", backend.LastArgs["planetId"]);
            Assert.AreEqual("iron",  backend.LastArgs["mineralId"]);
            Assert.AreEqual(12,      backend.LastArgs["claimedQty"]);
        }

        // A server-tracked idle run is ended by the claim that collects it (ValidateMining idleSlotId).
        [Test]
        public async Task GrantMiningAsync_names_the_idle_run_slot_when_given()
        {
            var backend = new FakeBackendClient { GrantResponse = new MiningGrantResult { Granted = 12, MineralId = "iron" } };
            var svc = new MineralService(backend, new MineralInventory(), new Wallet());

            await svc.GrantMiningAsync("earth", "iron", 12, idleSlotId: "Iron#0");

            CollectionAssert.AreEquivalent(new[] { "planetId", "mineralId", "claimedQty", "idleSlotId" }, backend.LastArgs.Keys);
            Assert.AreEqual("Iron#0", backend.LastArgs["idleSlotId"]);
        }

        // ValidateMining records the slot in the claim log, which GetAsteroidRespawns reports back to
        // every device.
        [Test]
        public async Task GrantMiningAsync_names_the_mined_slot_when_given()
        {
            var backend = new FakeBackendClient { GrantResponse = new MiningGrantResult { Granted = 12, MineralId = "iron" } };
            var svc = new MineralService(backend, new MineralInventory(), new Wallet());

            await svc.GrantMiningAsync("earth", "iron", 12, slotId: "Iron#1");

            CollectionAssert.AreEquivalent(new[] { "planetId", "mineralId", "claimedQty", "slotId" }, backend.LastArgs.Keys);
            Assert.AreEqual("Iron#1", backend.LastArgs["slotId"]);
        }

        // A failed minigame: ValidateMining (forfeit) logs the lost slot against the planet's claim
        // budget and grants nothing.
        [Test]
        public async Task ForfeitMiningAsync_reports_the_lost_slot_to_ValidateMining()
        {
            var backend = new FakeBackendClient { GrantResponse = new MiningGrantResult { Granted = 0, MineralId = "iron", Reason = "FORFEITED" } };
            var svc = new MineralService(backend, new MineralInventory(), new Wallet());

            var result = await svc.ForfeitMiningAsync("earth", "iron", "Iron#1");

            Assert.AreEqual("ValidateMining", backend.LastFunction);
            CollectionAssert.AreEquivalent(new[] { "planetId", "mineralId", "slotId", "forfeit" }, backend.LastArgs.Keys);
            Assert.AreEqual("earth",  backend.LastArgs["planetId"]);
            Assert.AreEqual("iron",   backend.LastArgs["mineralId"]);
            Assert.AreEqual("Iron#1", backend.LastArgs["slotId"]);
            Assert.AreEqual(true,     backend.LastArgs["forfeit"]);
            Assert.AreEqual("FORFEITED", result.Reason);
        }

        [Test]
        public async Task ForfeitMiningAsync_never_adds_minerals()
        {
            var backend = new FakeBackendClient { GrantResponse = new MiningGrantResult { Granted = 5, MineralId = "iron" } };
            var inv = new MineralInventory();
            var svc = new MineralService(backend, inv, new Wallet());

            await svc.ForfeitMiningAsync("earth", "iron", "Iron#1");

            Assert.AreEqual(0, inv.Get("iron"));
        }

        [Test]
        public async Task ForfeitMiningAsync_turns_a_null_reply_into_an_answer()
        {
            var result = await new MineralService(new FakeBackendClient(), new MineralInventory(), new Wallet())
                .ForfeitMiningAsync("earth", "iron", "Iron#1");

            Assert.AreEqual("Empty response", result.Reason);
        }

        // A claim worth nothing has nothing to grant, but a server-tracked idle run must still reach
        // ValidateMining so the server ends it (it answers INVALID_PARAMS and ends the run).
        [Test]
        public async Task GrantMiningAsync_still_reaches_the_server_for_an_empty_idle_claim()
        {
            var backend = new FakeBackendClient { GrantResponse = new MiningGrantResult { Granted = 0, Reason = "INVALID_PARAMS" } };
            var svc = new MineralService(backend, new MineralInventory(), new Wallet());

            await svc.GrantMiningAsync("earth", "iron", 0, idleSlotId: "Iron#0");

            Assert.AreEqual("ValidateMining", backend.LastFunction);
            Assert.AreEqual("Iron#0", backend.LastArgs["idleSlotId"]);
        }

        [Test]
        public async Task GrantMiningAsync_skips_the_server_for_an_empty_claim_with_no_idle_run()
        {
            var backend = new FakeBackendClient();
            var svc = new MineralService(backend, new MineralInventory(), new Wallet());

            var result = await svc.GrantMiningAsync("earth", "iron", 0);

            Assert.IsNull(backend.LastFunction);
            Assert.AreEqual(0, result.Granted);
        }

        [Test]
        public async Task GrantMiningAsync_adds_the_granted_amount_to_the_inventory()
        {
            var backend = new FakeBackendClient { GrantResponse = new MiningGrantResult { Granted = 9, MineralId = "iron" } };
            var inv = new MineralInventory();
            var svc = new MineralService(backend, inv, new Wallet());

            var result = await svc.GrantMiningAsync("earth", "iron", 12);

            Assert.AreEqual(9, result.Granted);
            Assert.AreEqual(9, inv.Get("iron"), "the server's clamped amount, not the claimed one");
        }

        [Test]
        public async Task GrantMiningAsync_rejection_returns_the_reason_and_leaves_inventory_unchanged()
        {
            var backend = new FakeBackendClient { GrantResponse = new MiningGrantResult { Granted = 0, MineralId = "iron", Reason = "BUDGET_EXHAUSTED" } };
            var inv = new MineralInventory();
            var svc = new MineralService(backend, inv, new Wallet());

            var result = await svc.GrantMiningAsync("earth", "iron", 12);

            Assert.AreEqual(0, result.Granted);
            Assert.AreEqual("BUDGET_EXHAUSTED", result.Reason);
            Assert.AreEqual(0, inv.Get("iron"));
        }

        [Test]
        public async Task GrantMiningAsync_null_response_is_an_empty_response_rejection()
        {
            var svc = new MineralService(new FakeBackendClient(), new MineralInventory(), new Wallet());

            var result = await svc.GrantMiningAsync("earth", "iron", 12);

            Assert.AreEqual(0, result.Granted);
            Assert.AreEqual("Empty response", result.Reason);
        }
    }
}
