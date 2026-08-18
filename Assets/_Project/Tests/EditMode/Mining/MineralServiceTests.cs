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
            public SellResult SellResponse;
            public Task<T> CallAsync<T>(string function, Dictionary<string, object> args = null)
            {
                if (function == "SellMinerals" && typeof(T) == typeof(SellResult))
                    return Task.FromResult((T)(object)SellResponse);
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
    }
}
