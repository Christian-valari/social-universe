using System.Linq;
using System.Reflection;
using NUnit.Framework;
using SocialUniverse.Config;
using SocialUniverse.Economy;
using SocialUniverse.World;
using UnityEngine;

namespace SocialUniverse.Tests
{
    public class BuildPaletteServiceTests
    {
        private EconomyConfig       _config;
        private DatabaseRegistry    _registry;
        private ItemDefinition[]    _items;
        private BuildPaletteService _palette;

        [SetUp]
        public void SetUp()
        {
            _config   = ScriptableObject.CreateInstance<EconomyConfig>();
            _registry = ScriptableObject.CreateInstance<DatabaseRegistry>();

            _items = new[]
            {
                MakeItem("level1_solar", 1),
                MakeItem("level2_dome", 2),
                MakeItem("level2_garden", 2),
            };
            SetField(_registry, "_items", _items);

            _palette = new BuildPaletteService(_registry, _config);
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_config);
            UnityEngine.Object.DestroyImmediate(_registry);
            foreach (var item in _items) UnityEngine.Object.DestroyImmediate(item);
        }

        private static ItemDefinition MakeItem(string itemId, int buildLevel)
        {
            var item = ScriptableObject.CreateInstance<ItemDefinition>();
            SetField(item, "_itemId", itemId);
            SetField(item, "_buildLevel", buildLevel);
            return item;
        }

        private static void SetField(object target, string fieldName, object value)
        {
            target.GetType()
                .GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(target, value);
        }

        [Test]
        public void Returns_level1_items_for_owned_tile_at_level0()
        {
            var tile = new TileData("1") { State = TileState.OwnedByPlayer, BuildLevel = 0 };

            var available = _palette.GetAvailableItems(tile).ToList();

            Assert.AreEqual(1, available.Count);
            Assert.AreEqual("level1_solar", available[0].ItemId);
        }

        [Test]
        public void Returns_level2_items_for_owned_tile_at_level1()
        {
            var tile = new TileData("1") { State = TileState.OwnedByPlayer, BuildLevel = 1 };

            var available = _palette.GetAvailableItems(tile).ToList();

            Assert.AreEqual(2, available.Count);
            Assert.IsTrue(available.All(i => i.BuildLevel == 2));
        }

        [Test]
        public void Returns_empty_for_tile_not_owned_by_player()
        {
            var tile = new TileData("1") { State = TileState.OwnedByOther, BuildLevel = 0 };

            Assert.IsEmpty(_palette.GetAvailableItems(tile));
        }

        [Test]
        public void Returns_empty_for_available_tile()
        {
            var tile = new TileData("1") { State = TileState.Available, BuildLevel = 0 };

            Assert.IsEmpty(_palette.GetAvailableItems(tile));
        }

        [Test]
        public void Returns_empty_when_tile_at_max_build_level()
        {
            var tile = new TileData("1") { State = TileState.OwnedByPlayer, BuildLevel = _config.MaxBuildLevel };

            Assert.IsEmpty(_palette.GetAvailableItems(tile));
        }
    }
}
