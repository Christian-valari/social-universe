using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using SocialUniverse.Config;
using SocialUniverse.UI;

namespace SocialUniverse.Tests
{
    public class LandSlotResolverTests
    {
        private static void SetField(object t, string f, object v) =>
            t.GetType().GetField(f, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(t, v);

        [Test]
        public void Resolve_returns_matching_item_definition()
        {
            var item = ScriptableObject.CreateInstance<ItemDefinition>();
            SetField(item, "_itemId", "tree");
            var registry = ScriptableObject.CreateInstance<DatabaseRegistry>();
            SetField(registry, "_items", new[] { item });

            var resolved = LandSlotResolver.Resolve("tree", registry);

            Assert.AreSame(item, resolved);

            Object.DestroyImmediate(item);
            Object.DestroyImmediate(registry);
        }

        [Test]
        public void Resolve_returns_null_for_unknown_or_empty_id()
        {
            var registry = ScriptableObject.CreateInstance<DatabaseRegistry>();
            SetField(registry, "_items", new ItemDefinition[0]);

            Assert.IsNull(LandSlotResolver.Resolve("nope", registry));
            Assert.IsNull(LandSlotResolver.Resolve(null, registry));

            Object.DestroyImmediate(registry);
        }
    }
}
