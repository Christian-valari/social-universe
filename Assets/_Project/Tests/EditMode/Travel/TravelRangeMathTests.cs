using System.Reflection;
using NUnit.Framework;
using SocialUniverse.Config;
using SocialUniverse.Travel;
using UnityEngine;

namespace SocialUniverse.Tests
{
    public class TravelRangeMathTests
    {
        private static PlanetDefinition NewPlanet(string id, int orbitOrder)
        {
            var planet = ScriptableObject.CreateInstance<PlanetDefinition>();
            SetPrivateField(planet, "_planetId", id);
            SetPrivateField(planet, "_orbitOrder", orbitOrder);
            return planet;
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            field.SetValue(target, value);
        }

        [Test]
        public void IsInRange_true_for_adjacent_orbit_order()
        {
            var earth = NewPlanet("earth", 3);
            var mars  = NewPlanet("mars", 4);

            Assert.IsTrue(TravelRangeMath.IsInRange(earth, mars));
            Assert.IsTrue(TravelRangeMath.IsInRange(mars, earth));
        }

        [Test]
        public void IsInRange_true_for_tied_orbit_order()
        {
            var earth = NewPlanet("earth", 3);
            var moon  = NewPlanet("moon", 3);

            Assert.IsTrue(TravelRangeMath.IsInRange(earth, moon));
        }

        [Test]
        public void IsInRange_false_for_distant_orbit_order()
        {
            var mercury = NewPlanet("mercury", 1);
            var pluto   = NewPlanet("pluto", 9);

            Assert.IsFalse(TravelRangeMath.IsInRange(mercury, pluto));
        }

        [Test]
        public void IsInRange_false_for_the_same_planet()
        {
            var earth = NewPlanet("earth", 3);

            Assert.IsFalse(TravelRangeMath.IsInRange(earth, earth));
        }
    }
}
