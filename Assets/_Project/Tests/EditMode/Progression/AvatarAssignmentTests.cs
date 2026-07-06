using System.Collections.Generic;
using NUnit.Framework;
using SocialUniverse.Progression;

namespace SocialUniverse.Tests
{
    public class AvatarAssignmentTests
    {
        [Test]
        public void Existing_avatar_id_is_returned_unchanged()
        {
            var catalog = new List<string> { "avatar_a", "avatar_b" };

            string resolved = AvatarAssignment.ResolveAvatarId("avatar_b", catalog, n => 0);

            Assert.AreEqual("avatar_b", resolved);
        }

        [Test]
        public void Empty_avatar_id_picks_from_catalog_using_the_random_index()
        {
            var catalog = new List<string> { "avatar_a", "avatar_b", "avatar_c" };

            string resolved = AvatarAssignment.ResolveAvatarId("", catalog, n => 2);

            Assert.AreEqual("avatar_c", resolved);
        }

        [Test]
        public void Null_avatar_id_picks_from_catalog()
        {
            var catalog = new List<string> { "avatar_a" };

            string resolved = AvatarAssignment.ResolveAvatarId(null, catalog, n => 0);

            Assert.AreEqual("avatar_a", resolved);
        }

        [Test]
        public void Empty_catalog_returns_null()
        {
            string resolved = AvatarAssignment.ResolveAvatarId(null, new List<string>(), n => 0);

            Assert.IsNull(resolved);
        }
    }
}
