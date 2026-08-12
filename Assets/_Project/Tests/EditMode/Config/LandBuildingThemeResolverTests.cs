using System.Reflection;
using NUnit.Framework;
using SocialUniverse.Config;
using UnityEngine;

namespace SocialUniverse.Tests
{
    public class LandBuildingThemeResolverTests
    {
        private static void SetField(object target, string fieldName, object value) =>
            target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);

        private static PlanetDefinition MakePlanet(string id, LandBuildingThemeDefinition theme)
        {
            var p = ScriptableObject.CreateInstance<PlanetDefinition>();
            SetField(p, "_planetId", id);
            SetField(p, "_landBuildingTheme", theme);
            return p;
        }

        private static DatabaseRegistry MakeRegistry(params PlanetDefinition[] planets)
        {
            var r = ScriptableObject.CreateInstance<DatabaseRegistry>();
            SetField(r, "_planets", planets);
            return r;
        }

        [Test]
        public void Resolve_returns_planet_theme_when_present()
        {
            var theme    = ScriptableObject.CreateInstance<LandBuildingThemeDefinition>();
            var fallback = ScriptableObject.CreateInstance<LandBuildingThemeDefinition>();
            var registry = MakeRegistry(MakePlanet("earth", theme));

            Assert.AreSame(theme, LandBuildingThemeResolver.Resolve(registry, "earth", fallback));
        }

        [Test]
        public void Resolve_returns_fallback_when_planet_missing()
        {
            var theme    = ScriptableObject.CreateInstance<LandBuildingThemeDefinition>();
            var fallback = ScriptableObject.CreateInstance<LandBuildingThemeDefinition>();
            var registry = MakeRegistry(MakePlanet("earth", theme));

            Assert.AreSame(fallback, LandBuildingThemeResolver.Resolve(registry, "mars", fallback));
        }

        [Test]
        public void Resolve_returns_fallback_when_planet_has_no_theme()
        {
            var fallback = ScriptableObject.CreateInstance<LandBuildingThemeDefinition>();
            var registry = MakeRegistry(MakePlanet("earth", null));

            Assert.AreSame(fallback, LandBuildingThemeResolver.Resolve(registry, "earth", fallback));
        }

        [Test]
        public void Resolve_returns_fallback_when_registry_null()
        {
            var fallback = ScriptableObject.CreateInstance<LandBuildingThemeDefinition>();

            Assert.AreSame(fallback, LandBuildingThemeResolver.Resolve(null, "earth", fallback));
        }

        [Test]
        public void Resolve_returns_fallback_when_planetId_null()
        {
            var theme    = ScriptableObject.CreateInstance<LandBuildingThemeDefinition>();
            var fallback = ScriptableObject.CreateInstance<LandBuildingThemeDefinition>();
            var registry = MakeRegistry(MakePlanet("earth", theme));

            Assert.AreSame(fallback, LandBuildingThemeResolver.Resolve(registry, null, fallback));
        }
    }
}
