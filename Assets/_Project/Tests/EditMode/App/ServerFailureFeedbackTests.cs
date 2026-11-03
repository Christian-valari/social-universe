using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using SocialUniverse.App;
using SocialUniverse.Config;
using SocialUniverse.Core;
using SocialUniverse.Mining;

namespace SocialUniverse.Tests
{
    // Known Issue #17: rejected drone/mineral server calls used to only log, so they looked like
    // dead buttons. The handlers now publish ServerActionFailedEvent with a player-facing message;
    // drone/slot purchases and mineral sales send theirs to their popup instead
    // (DronePurchaseCompletedEvent, MineralSaleCompletedEvent).
    public class ServerFailureFeedbackTests
    {
        private class FailingDroneService : IDroneService
        {
            public string Reason;
            public bool   ReturnNull;
            public Task<DroneActionResult> AcquireDroneAsync(string droneId) => Fail();
            public Task<DroneActionResult> UnlockSlotAsync() => Fail();
            public Task<DroneActionResult> UpgradeAsync(string droneId, DroneStat stat) => Fail();
            public Task<DroneActionResult> SetActiveAsync(string droneId) => Fail();
            private Task<DroneActionResult> Fail() =>
                Task.FromResult(ReturnNull ? null : new DroneActionResult { Success = false, Reason = Reason });
        }

        private class FailingMineralService : IMineralService
        {
            public string Reason;
            public bool   ReturnNull;
            public Task<SellResult> SellAsync(string mineralId, int qty) => Fail();
            public Task<SellResult> SellAllAsync() => Fail();
            public Task<MiningGrantResult> GrantMiningAsync(string planetId, string mineralId, int qty, string idleSlotId = null,
                string slotId = null) => Task.FromResult(new MiningGrantResult());
            public Task<MiningGrantResult> ForfeitMiningAsync(string planetId, string mineralId, string slotId) => Task.FromResult(new MiningGrantResult());
            private Task<SellResult> Fail() =>
                Task.FromResult(ReturnNull ? null : new SellResult { Success = false, Reason = Reason });
        }

        private readonly List<string> _messages = new();
        private readonly List<DronePurchaseCompletedEvent> _purchases = new();
        private readonly List<MineralSaleCompletedEvent> _sales = new();
        private void Capture(ServerActionFailedEvent e) => _messages.Add(e.Message);

        [SetUp]
        public void SetUp()
        {
            EventBus.Clear();
            _messages.Clear();
            _purchases.Clear();
            _sales.Clear();
            EventBus.Subscribe<ServerActionFailedEvent>(Capture);
            EventBus.Subscribe<DronePurchaseCompletedEvent>(_purchases.Add);
            EventBus.Subscribe<MineralSaleCompletedEvent>(_sales.Add);
        }

        [TearDown]
        public void TearDown() => EventBus.Clear();

        [Test]
        public void A_rejected_drone_purchase_sends_a_readable_message_to_the_purchase_popup()
        {
            var handler = new DroneGarageHandler(new FailingDroneService { Reason = "INSUFFICIENT_FUNDS" });
            handler.Start();

            EventBus.Publish(new DroneAcquireRequestedEvent { DroneId = "extractor" });

            handler.Dispose();
            Assert.AreEqual(1, _purchases.Count);
            Assert.IsFalse(_purchases[0].Success);
            Assert.AreEqual("extractor", _purchases[0].DroneId);
            Assert.AreEqual("Not enough coins", _purchases[0].Message);
            CollectionAssert.IsEmpty(_messages, "The popup shows purchase failures, so no toast");
        }

        [Test]
        public void Upgrade_and_select_failures_still_toast_while_purchases_go_to_the_popup()
        {
            var handler = new DroneGarageHandler(new FailingDroneService { Reason = "Network error" });
            handler.Start();

            EventBus.Publish(new DroneAcquireRequestedEvent { DroneId = "extractor" });
            EventBus.Publish(new DroneSlotUnlockRequestedEvent());
            EventBus.Publish(new DroneUpgradeRequestedEvent { DroneId = "scout", Stat = DroneStat.Yield });
            EventBus.Publish(new SetActiveDroneRequestedEvent { DroneId = "scout" });

            handler.Dispose();
            Assert.AreEqual(2, _messages.Count, "upgrade + select toast");
            Assert.AreEqual(2, _purchases.Count, "drone + slot go to the popup");
            Assert.IsTrue(_purchases.TrueForAll(p => !p.Success && p.Message == "Couldn't reach the server. Try again."));
        }

        [Test]
        public void A_purchase_with_no_reply_still_completes_so_the_popup_never_hangs()
        {
            var handler = new DroneGarageHandler(new FailingDroneService { ReturnNull = true });
            handler.Start();

            EventBus.Publish(new DroneSlotUnlockRequestedEvent());

            handler.Dispose();
            Assert.AreEqual(1, _purchases.Count);
            Assert.IsFalse(_purchases[0].Success);
            Assert.AreEqual("Couldn't reach the server. Try again.", _purchases[0].Message);
        }

        [Test]
        public void A_rejected_mineral_sale_sends_a_readable_message_to_the_sale_popup()
        {
            var handler = new MineralSaleHandler(new FailingMineralService { Reason = "INSUFFICIENT_QTY" });
            handler.Start();

            EventBus.Publish(new SellMineralsRequestedEvent { All = true });

            handler.Dispose();
            Assert.AreEqual(1, _sales.Count);
            Assert.IsFalse(_sales[0].Success);
            Assert.AreEqual("Not enough minerals to sell", _sales[0].Message);
            CollectionAssert.IsEmpty(_messages, "The popup shows sale failures, so no toast");
        }

        [Test]
        public void A_sale_with_no_reply_still_completes_so_the_popup_never_hangs()
        {
            var handler = new MineralSaleHandler(new FailingMineralService { ReturnNull = true });
            handler.Start();

            EventBus.Publish(new SellMineralsRequestedEvent { MineralId = "iron", Qty = 1 });

            handler.Dispose();
            Assert.AreEqual(1, _sales.Count);
            Assert.IsFalse(_sales[0].Success);
            Assert.AreEqual("Couldn't reach the server. Try again.", _sales[0].Message);
        }

        [TestCase("INSUFFICIENT_FUNDS", "Not enough coins")]
        [TestCase("SLOTS_FULL",         "No free drone slot")]
        [TestCase("MAX_LEVEL",          "Already at max level")]
        [TestCase("ALREADY_OWNED",      "You already own this drone")]
        [TestCase("Network error",      "Couldn't reach the server. Try again.")]
        [TestCase("Empty response",     "Couldn't reach the server. Try again.")]
        public void Known_reason_codes_map_to_player_facing_text(string reason, string expected) =>
            Assert.AreEqual(expected, ServerFailureMessages.For("Purchase", reason));

        [TestCase(null)]
        [TestCase("SOMETHING_NEW")]
        public void Unknown_reasons_fall_back_to_a_generic_message(string reason) =>
            Assert.AreEqual("Purchase failed", ServerFailureMessages.For("Purchase", reason));

        [Test]
        public void A_rejected_mining_claim_publishes_a_readable_message()
        {
            var handler = new MiningClaimFeedbackHandler();
            handler.Start();

            EventBus.Publish(new MiningClaimRejectedEvent { MineralId = "iron", Reason = "BUDGET_EXHAUSTED" });

            handler.Dispose();
            CollectionAssert.AreEqual(new[] { "No more asteroids here for now. Try again later." }, _messages);
        }

        [TestCase("BUDGET_EXHAUSTED",      "No more asteroids here for now. Try again later.")]
        [TestCase("TIER_TOO_LOW",          "Requires a higher-tier drone")]
        [TestCase("WRONG_PLANET",          "Mining isn't available here right now")]
        [TestCase("MINERAL_NOT_ON_PLANET", "Mining isn't available here right now")]
        [TestCase("ALREADY_MINING",        "Your drone is already mining here")]
        public void Mining_reason_codes_map_to_player_facing_text(string reason, string expected) =>
            Assert.AreEqual(expected, ServerFailureMessages.For("Mining", reason));
    }
}
