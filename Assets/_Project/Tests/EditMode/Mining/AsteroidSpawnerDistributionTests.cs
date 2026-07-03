using System.Linq;
using System.Reflection;
using NUnit.Framework;
using SocialUniverse.Config;
using SocialUniverse.Mining;
using UnityEngine;

namespace SocialUniverse.Tests
{
    public class AsteroidSpawnerDistributionTests
    {
        private static AsteroidDefinition MakeDef(float rarity)
        {
            var def = ScriptableObject.CreateInstance<AsteroidDefinition>();
            typeof(AsteroidDefinition).GetField("_rarity", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(def, rarity);
            return def;
        }

        [Test]
        public void Counts_always_sum_to_field_size()
        {
            var types = new[] { MakeDef(0.7f), MakeDef(0.5f), MakeDef(0.1f) };

            foreach (int fieldSize in new[] { 1, 3, 6, 10, 25 })
            {
                var counts = AsteroidSpawner.DistributeFieldSize(types, fieldSize);
                Assert.AreEqual(fieldSize, counts.Sum(), $"fieldSize={fieldSize}");
            }
        }

        [Test]
        public void Rarer_types_get_fewer_slots_than_common_types()
        {
            var types  = new[] { MakeDef(0.8f), MakeDef(0.1f) }; // [0]=rare, [1]=common
            var counts = AsteroidSpawner.DistributeFieldSize(types, 20);

            Assert.Less(counts[0], counts[1]);
        }

        [Test]
        public void Zero_field_size_yields_all_zero_counts()
        {
            var types  = new[] { MakeDef(0.5f), MakeDef(0.5f) };
            var counts = AsteroidSpawner.DistributeFieldSize(types, 0);

            Assert.AreEqual(new[] { 0, 0 }, counts);
        }

        [Test]
        public void Single_type_gets_the_full_field_size()
        {
            var types  = new[] { MakeDef(0.9f) };
            var counts = AsteroidSpawner.DistributeFieldSize(types, 7);

            Assert.AreEqual(new[] { 7 }, counts);
        }
    }
}
