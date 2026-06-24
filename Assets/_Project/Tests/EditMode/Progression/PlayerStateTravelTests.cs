using NUnit.Framework;
using SocialUniverse.Progression;

namespace SocialUniverse.Tests
{
    public class PlayerStateTravelTests
    {
        [Test]
        public void SetTravelState_true_sets_fields_and_fires_event()
        {
            var playerState = new PlayerState();
            bool? eventTraveling = null;
            string eventTarget = null;
            long eventArrival = -1L;
            playerState.OnTravelStateChanged += (traveling, target, arrival) =>
            {
                eventTraveling = traveling;
                eventTarget    = target;
                eventArrival   = arrival;
            };

            playerState.SetTravelState(true, "mars", 1000L);

            Assert.IsTrue(playerState.IsTraveling);
            Assert.AreEqual("mars", playerState.TravelTargetId);
            Assert.AreEqual(1000L, playerState.TravelArrivalTsMs);
            Assert.AreEqual(true, eventTraveling);
            Assert.AreEqual("mars", eventTarget);
            Assert.AreEqual(1000L, eventArrival);
        }

        [Test]
        public void SetTravelState_false_clears_target_and_arrival()
        {
            var playerState = new PlayerState();
            playerState.SetTravelState(true, "mars", 1000L);

            playerState.SetTravelState(false, "mars", 1000L);

            Assert.IsFalse(playerState.IsTraveling);
            Assert.IsNull(playerState.TravelTargetId);
            Assert.AreEqual(0L, playerState.TravelArrivalTsMs);
        }
    }
}
