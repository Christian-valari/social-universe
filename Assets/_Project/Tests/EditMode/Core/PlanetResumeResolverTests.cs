using NUnit.Framework;
using SocialUniverse.Core;

namespace SocialUniverse.Tests
{
    public class PlanetResumeResolverTests
    {
        private const string Server  = "Mars";
        private const string Local   = "Venus";
        private const string Default = "Earth";

        [Test]
        public void Server_value_wins_when_present()
        {
            Assert.AreEqual(Server, PlanetResumeResolver.Resolve(Server, Local, Default));
        }

        [Test]
        public void Local_value_wins_when_server_is_null()
        {
            Assert.AreEqual(Local, PlanetResumeResolver.Resolve(null, Local, Default));
        }

        [Test]
        public void Local_value_wins_when_server_is_empty()
        {
            Assert.AreEqual(Local, PlanetResumeResolver.Resolve("", Local, Default));
        }

        [Test]
        public void Default_wins_when_both_server_and_local_are_null()
        {
            Assert.AreEqual(Default, PlanetResumeResolver.Resolve(null, null, Default));
        }

        [Test]
        public void Default_wins_when_both_server_and_local_are_empty()
        {
            Assert.AreEqual(Default, PlanetResumeResolver.Resolve("", "", Default));
        }

        // Cross-device resume: a trip started on another device is only on the server.

        [Test]
        public void A_trip_the_server_reports_resumes_travel_even_without_a_local_hint()
        {
            Assert.AreEqual(Server, PlanetResumeResolver.ResolveTravelTarget(serverAnswered: true, Server, localTravelTargetId: null));
        }

        [Test]
        public void A_stale_local_hint_is_ignored_once_the_server_reports_no_trip()
        {
            Assert.IsNull(PlanetResumeResolver.ResolveTravelTarget(serverAnswered: true, null, localTravelTargetId: Local));
        }

        [Test]
        public void The_server_trip_wins_over_a_different_local_hint()
        {
            Assert.AreEqual(Server, PlanetResumeResolver.ResolveTravelTarget(serverAnswered: true, Server, localTravelTargetId: Local));
        }

        [Test]
        public void The_local_hint_is_used_only_when_the_server_cannot_be_reached()
        {
            Assert.AreEqual(Local, PlanetResumeResolver.ResolveTravelTarget(serverAnswered: false, null, localTravelTargetId: Local));
            Assert.IsNull(PlanetResumeResolver.ResolveTravelTarget(serverAnswered: false, null, localTravelTargetId: null));
        }
    }
}
