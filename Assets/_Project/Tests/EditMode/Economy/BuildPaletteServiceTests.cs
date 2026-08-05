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

        private static ItemDefinition MakeItem(string itemId, int cost)
        {
            var item = ScriptableObject.CreateInstance<ItemDefinition>();
            SetField(item, "_itemId", itemId);
            SetField(item, "_cost", cost);
            return item;
        }

        [SetUp]
        public void SetUp()
        {
            _config   = ScriptableObject.CreateInstance<EconomyConfig>();
            _registry = ScriptableObject.CreateInstance<DatabaseRegistry>();
            SetField(_config, "_hexBoardRadius", 2); // HexCount 19 -> MaxBuildLevel 19

            _items = new[]
            {
                MakeItem("cheap_tree", 50),
                MakeItem("mid_statue", 200),
                MakeItem("pricey_house", 1000),
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

        private static void SetField(object target, string fieldName, object value)
        {
            target.GetType()
                .GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(target, value);
        }

        [Test]
        public void Returns_all_affordable_items_for_owned_tile_with_free_slots()
        {
            var tile = new TileData("1") { State = TileState.OwnedByPlayer, BuildLevel = 2 };

            var available = _palette.GetAvailableItems(tile, 300).ToList();

            Assert.AreEqual(2, available.Count);
            Assert.IsTrue(available.Any(i => i.ItemId == "cheap_tree"));
            Assert.IsTrue(available.Any(i => i.ItemId == "mid_statue"));
            Assert.IsFalse(available.Any(i => i.ItemId == "pricey_house"));
        }

        [Test]
        public void Returns_empty_for_tile_not_owned_by_player()
        {
            var tile = new TileData("1") { State = TileState.OwnedByOther, BuildLevel = 0 };
            Assert.IsEmpty(_palette.GetAvailableItems(tile, int.MaxValue));
        }

        [Test]
        public void Returns_empty_for_available_tile()
        {
            var tile = new TileData("1") { State = TileState.Available, BuildLevel = 0 };
            Assert.IsEmpty(_palette.GetAvailableItems(tile, int.MaxValue));
        }

        [Test]
        public void Returns_empty_when_all_slots_full()
        {
            var tile = new TileData("1") { State = TileState.OwnedByPlayer, BuildLevel = _config.MaxBuildLevel };
            Assert.IsEmpty(_palette.GetAvailableItems(tile, int.MaxValue));
        }
    }
}
