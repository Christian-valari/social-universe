using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using SocialUniverse.Config;
using SocialUniverse.Core;
using SocialUniverse.Mining;
using SocialUniverse.App;

namespace SocialUniverse.Tests
{
    public class DroneGarageHandlerTests
    {
        private class CapturingDroneService : IDroneService
        {
            public string LastCall; public string LastDroneId; public DroneStat LastStat;
            public Task<DroneActionResult> AcquireDroneAsync(string droneId) { LastCall = "acquire"; LastDroneId = droneId; return Ok(); }
            public Task<DroneActionResult> UnlockSlotAsync() { LastCall = "unlock"; return Ok(); }
            public Task<DroneActionResult> UpgradeAsync(string droneId, DroneStat stat) { LastCall = "upgrade"; LastDroneId = droneId; LastStat = stat; return Ok(); }
            public Task<DroneActionResult> SetActiveAsync(string droneId) { LastCall = "setactive"; LastDroneId = droneId; return Ok(); }
            private static Task<DroneActionResult> Ok() => Task.FromResult(new DroneActionResult { Success = true });
        }

        [Test]
        public void Each_intent_event_routes_to_the_matching_service_call()
        {
            EventBus.Clear();
            var svc = new CapturingDroneService();
            var handler = new DroneGarageHandler(svc);
            handler.Start();

            EventBus.Publish(new DroneAcquireRequestedEvent { DroneId = "hauler" });
            Assert.AreEqual("acquire", svc.LastCall);
            Assert.AreEqual("hauler", svc.LastDroneId);

            EventBus.Publish(new DroneSlotUnlockRequestedEvent());
            Assert.AreEqual("unlock", svc.LastCall);

            EventBus.Publish(new DroneUpgradeRequestedEvent { DroneId = "scout", Stat = DroneStat.Yield });
            Assert.AreEqual("upgrade", svc.LastCall);
            Assert.AreEqual(DroneStat.Yield, svc.LastStat);

            EventBus.Publish(new SetActiveDroneRequestedEvent { DroneId = "scout" });
            Assert.AreEqual("setactive", svc.LastCall);

            handler.Dispose();
            EventBus.Clear();
        }

        [Test]
        public void A_successful_drone_purchase_publishes_a_successful_completion()
        {
            EventBus.Clear();
            var done = new List<DronePurchaseCompletedEvent>();
            EventBus.Subscribe<DronePurchaseCompletedEvent>(done.Add);
            var handler = new DroneGarageHandler(new CapturingDroneService());
            handler.Start();

            EventBus.Publish(new DroneAcquireRequestedEvent { DroneId = "extractor" });

            handler.Dispose();
            EventBus.Clear();
            Assert.AreEqual(1, done.Count);
            Assert.IsTrue(done[0].Success);
            Assert.AreEqual("extractor", done[0].DroneId);
            Assert.AreEqual("", done[0].Message);
        }

        [Test]
        public void A_successful_slot_unlock_publishes_a_successful_completion()
        {
            EventBus.Clear();
            var done = new List<DronePurchaseCompletedEvent>();
            EventBus.Subscribe<DronePurchaseCompletedEvent>(done.Add);
            var handler = new DroneGarageHandler(new CapturingDroneService());
            handler.Start();

            EventBus.Publish(new DroneSlotUnlockRequestedEvent());

            handler.Dispose();
            EventBus.Clear();
            Assert.AreEqual(1, done.Count);
            Assert.IsTrue(done[0].Success);
            Assert.IsNull(done[0].DroneId, "A slot purchase has no drone id");
        }
    }
}
