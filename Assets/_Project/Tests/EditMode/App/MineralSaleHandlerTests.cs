using System.Threading.Tasks;
using NUnit.Framework;
using SocialUniverse.App;
using SocialUniverse.Core;
using SocialUniverse.Mining;

namespace SocialUniverse.Tests
{
    // Known Issue #16: a double-tap on Sell used to send two SellMinerals calls.
    public class MineralSaleHandlerTests
    {
        private class PendingMineralService : IMineralService
        {
            public int Calls;
            public TaskCompletionSource<SellResult> Pending = new();
            public Task<SellResult> SellAsync(string mineralId, int qty) { Calls++; return Pending.Task; }
            public Task<SellResult> SellAllAsync() { Calls++; return Pending.Task; }
            public Task<MiningGrantResult> GrantMiningAsync(string planetId, string mineralId, int qty) => Task.FromResult(new MiningGrantResult());
        }

        [SetUp]    public void SetUp()    => EventBus.Clear();
        [TearDown] public void TearDown() => EventBus.Clear();

        [Test]
        public void A_second_sell_request_is_ignored_while_the_first_is_in_flight()
        {
            var svc = new PendingMineralService();
            var handler = new MineralSaleHandler(svc);
            handler.Start();

            EventBus.Publish(new SellMineralsRequestedEvent { All = true });
            EventBus.Publish(new SellMineralsRequestedEvent { All = true });
            Assert.AreEqual(1, svc.Calls);

            handler.Dispose();
        }

        [Test]
        public void Selling_is_allowed_again_once_the_previous_sale_completes()
        {
            var svc = new PendingMineralService();
            var handler = new MineralSaleHandler(svc);
            handler.Start();

            EventBus.Publish(new SellMineralsRequestedEvent { All = true });
            svc.Pending.SetResult(new SellResult { Success = true });
            svc.Pending = new TaskCompletionSource<SellResult>();

            EventBus.Publish(new SellMineralsRequestedEvent { MineralId = "iron", Qty = 1 });
            Assert.AreEqual(2, svc.Calls);

            handler.Dispose();
        }
    }
}
