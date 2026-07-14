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
    }
}
