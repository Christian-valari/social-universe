using System.Linq;
using System.Reflection;
using NUnit.Framework;
using SocialUniverse.Config;
using UnityEngine;

namespace SocialUniverse.Tests
{
    public class DatabaseRegistryAvatarTests
    {
        private static AvatarDefinition MakeAvatar(string avatarId)
        {
            var def = ScriptableObject.CreateInstance<AvatarDefinition>();
            typeof(AvatarDefinition).GetField("_avatarId", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(def, avatarId);
            return def;
        }

        private static void SetField(object target, string fieldName, object value) =>
            target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);

        [Test]
        public void AllAvatars_returns_every_registered_avatar()
        {
            var registry = ScriptableObject.CreateInstance<DatabaseRegistry>();
            var avatars  = new[] { MakeAvatar("avatar_a"), MakeAvatar("avatar_b") };
            SetField(registry, "_avatars", avatars);

            Assert.AreEqual(2, registry.AllAvatars.Count());
        }

        [Test]
        public void GetAvatar_finds_by_id()
        {
            var registry = ScriptableObject.CreateInstance<DatabaseRegistry>();
            var avatars  = new[] { MakeAvatar("avatar_a"), MakeAvatar("avatar_b") };
            SetField(registry, "_avatars", avatars);

            var found = registry.GetAvatar("avatar_b");

            Assert.IsNotNull(found);
            Assert.AreEqual("avatar_b", found.AvatarId);
        }

        [Test]
        public void GetAvatar_returns_null_for_unknown_id()
        {
            var registry = ScriptableObject.CreateInstance<DatabaseRegistry>();
            SetField(registry, "_avatars", new[] { MakeAvatar("avatar_a") });

            Assert.IsNull(registry.GetAvatar("avatar_nonexistent"));
        }

        [Test]
        public void AllAvatars_is_empty_not_null_when_unset()
        {
            var registry = ScriptableObject.CreateInstance<DatabaseRegistry>();

            Assert.IsNotNull(registry.AllAvatars);
            Assert.AreEqual(0, registry.AllAvatars.Count());
        }
    }
}
