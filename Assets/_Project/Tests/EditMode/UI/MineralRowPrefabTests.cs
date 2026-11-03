using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using SocialUniverse.UI;

namespace SocialUniverse.Tests
{
    // The real Mineral Inventory row prefab, bound the way MineralInventoryView binds it.
    public class MineralRowPrefabTests
    {
        private const string PrefabPath = "Assets/Prefabs/UI/MineralRow.prefab";

        private GameObject     _instance;
        private MineralRowView _row;

        [SetUp]
        public void SetUp()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.IsNotNull(prefab, $"Could not load {PrefabPath}");

            _instance = Object.Instantiate(prefab);
            _instance.SetActive(true); // the prefab root is inactive; the view activates its clones
            _row = _instance.GetComponent<MineralRowView>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_instance != null) Object.DestroyImmediate(_instance);
        }

        [Test]
        public void A_held_mineral_shows_its_count_price_and_what_selling_it_pays()
        {
            _row.Bind(null, "Nickel", 2, 42, 8, null);

            Assert.AreEqual("Nickel", Field<TMP_Text>("_nameLabel").text);
            Assert.AreEqual("×42", Field<TMP_Text>("_qtyLabel").text);
            Assert.AreEqual("8 each", Field<TMP_Text>("_unitValueLabel").text);
            Assert.AreEqual("336", Field<TMP_Text>("_sellValueLabel").text, "Sell pays for every unit held");
            Assert.AreEqual("T2", Field<TMP_Text>("_tierLabel").text);
            Assert.IsTrue(Field<Button>("_sellButton").interactable);
            Assert.AreEqual(1f, Field<CanvasGroup>("_group").alpha, 1e-3f);
        }

        // Every mineral has a row, held or not, so the list never reshuffles; an empty one stays in
        // place but reads as empty.
        [Test]
        public void A_mineral_the_player_holds_none_of_is_dimmed_and_cannot_be_sold()
        {
            _row.Bind(null, "Iridium", 3, 0, 40, null);

            Assert.AreEqual("×0", Field<TMP_Text>("_qtyLabel").text);
            Assert.IsFalse(Field<Button>("_sellButton").interactable);
            Assert.Less(Field<CanvasGroup>("_group").alpha, 1f, "An empty row is dimmed");
        }

        // Drone and mineral tiers are one scale (a tier-N drone mines tier-N asteroids), so a tier
        // reads in the same colour on both screens.
        [Test]
        public void The_tier_badge_uses_the_shared_tier_colours()
        {
            _row.Bind(null, "Iron", 1, 1, 2, null);
            var t1 = Field<Image>("_tierBadge").color;
            _row.Bind(null, "Palladium", 4, 1, 70, null);
            var t4 = Field<Image>("_tierBadge").color;

            Assert.AreEqual(TierColors.For(1), t1);
            Assert.AreEqual(TierColors.For(4), t4);
            Assert.AreNotEqual(t1, t4);
        }

        private T Field<T>(string name) where T : class
        {
            var field = typeof(MineralRowView).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, $"MineralRowView has no field {name}");
            var value = field.GetValue(_row) as T;
            Assert.IsTrue(value as Object != null, $"MineralRowView.{name} is not wired in {PrefabPath}");
            return value;
        }
    }
}
