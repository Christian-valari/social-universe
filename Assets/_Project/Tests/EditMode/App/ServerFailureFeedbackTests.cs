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
    // dead buttons. The handlers now publish ServerActionFailedEvent with a player-facing message.
    public class ServerFailureFeedbackTests
    {
        private class FailingDroneService : IDroneService
        {
            public string Reason;
            public Task<DroneActionResult> AcquireDroneAsync(string droneId) => Fail();
            public Task<DroneActionResult> UnlockSlotAsync() => Fail();
            public Task<DroneActionResult> UpgradeAsync(string droneId, DroneStat stat) => Fail();
            public Task<DroneActionResult> SetActiveAsync(string droneId) => Fail();
            private Task<DroneActionResult> Fail() => Task.FromResult(new DroneActionResult { Success = false, Reason = Reason });
        }

        private class FailingMineralService : IMineralService
        {
            public string Reason;
            public Task<SellResult> SellAsync(string mineralId, int qty) => Fail();
            public Task<SellResult> SellAllAsync() => Fail();
            public Task<MiningGrantResult> GrantMiningAsync(string planetId, string mineralId, int qty) => Task.FromResult(new MiningGrantResult());
            private Task<SellResult> Fail() => Task.FromResult(new SellResult { Success = false, Reason = Reason });
        }

        private readonly List<string> _messages = new();
        private void Capture(ServerActionFailedEvent e) => _messages.Add(e.Message);

        [SetUp]
        public void SetUp()
        {
            EventBus.Clear();
            _messages.Clear();
            EventBus.Subscribe<ServerActionFailedEvent>(Capture);
        }

        [TearDown]
        public void TearDown() => EventBus.Clear();

        [Test]
        public void A_rejected_drone_purchase_publishes_a_readable_message()
        {
            var handler = new DroneGarageHandler(new FailingDroneService { Reason = "INSUFFICIENT_FUNDS" });
            handler.Start();

            EventBus.Publish(new DroneAcquireRequestedEvent { DroneId = "extractor" });

            handler.Dispose();
            CollectionAssert.AreEqual(new[] { "Not enough coins" }, _messages);
        }

        [Test]
        public void Every_drone_action_reports_its_failure()
        {
            var handler = new DroneGarageHandler(new FailingDroneService { Reason = "Network error" });
            handler.Start();

            EventBus.Publish(new DroneAcquireRequestedEvent { DroneId = "extractor" });
            EventBus.Publish(new DroneSlotUnlockRequestedEvent());
            EventBus.Publish(new DroneUpgradeRequestedEvent { DroneId = "scout", Stat = DroneStat.Yield });
            EventBus.Publish(new SetActiveDroneRequestedEvent { DroneId = "scout" });

            handler.Dispose();
            Assert.AreEqual(4, _messages.Count);
        }

        [Test]
        public void A_rejected_mineral_sale_publishes_a_readable_message()
        {
            var handler = new MineralSaleHandler(new FailingMineralService { Reason = "INSUFFICIENT_QTY" });
            handler.Start();

            EventBus.Publish(new SellMineralsRequestedEvent { All = true });

            handler.Dispose();
            CollectionAssert.AreEqual(new[] { "Not enough minerals to sell" }, _messages);
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
        public void Mining_reason_codes_map_to_player_facing_text(string reason, string expected) =>
            Assert.AreEqual(expected, ServerFailureMessages.For("Mining", reason));
    }
}
