using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using SocialUniverse.Config;
using SocialUniverse.UI;

namespace SocialUniverse.Tests
{
    // The real card prefab, bound the way DroneGarageView binds it.
    public class DroneRowPrefabTests
    {
        private const string PrefabPath = "Assets/Prefabs/UI/DroneRow.prefab";

        private GameObject   _instance;
        private DroneRowView _row;

        [SetUp]
        public void SetUp()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.IsNotNull(prefab, $"Could not load {PrefabPath}");

            _instance = Object.Instantiate(prefab);
            _row      = _instance.GetComponent<DroneRowView>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_instance != null) Object.DestroyImmediate(_instance);
        }

        // The garage's bottom buy button follows the centered card and buys the drone (or the slot it
        // needs), so a locked drone's card must not carry a second buy button of its own (2026-10-02).
        [Test]
        public void A_locked_drone_card_has_no_buy_button_of_its_own()
        {
            BindLocked(tier: 5);

            var shown = _instance.GetComponentsInChildren<Button>(includeInactive: false).Select(b => b.name);
            CollectionAssert.IsEmpty(shown, "A locked drone's card shows a button; the garage's buy button does the buying");
        }

        [Test]
        public void A_locked_drone_card_says_locked_and_shows_its_tier_badge()
        {
            BindLocked(tier: 5);

            Assert.AreEqual("Locked", Field<TMP_Text>("_stateLabel").text);
            Assert.IsTrue(Field<GameObject>("_lockIcon").activeSelf, "A locked card shows the lock");
            Assert.AreEqual("T5", Field<TMP_Text>("_tierLabel").text);
        }

        [Test]
        public void An_owned_drone_card_says_whether_it_is_the_active_drone()
        {
            BindOwned(isActive: true, level: 1, maxed: false);
            Assert.AreEqual("Active", Field<TMP_Text>("_stateLabel").text);
            Assert.IsFalse(Field<GameObject>("_lockIcon").activeSelf, "An owned card shows no lock");

            BindOwned(isActive: false, level: 1, maxed: false);
            Assert.AreEqual("Owned", Field<TMP_Text>("_stateLabel").text);
        }

        [Test]
        public void A_maxed_upgrade_shows_MAX_without_a_coin()
        {
            BindOwned(isActive: true, level: 10, maxed: true);

            var yield = Meter(DroneStat.Yield);
            Assert.AreEqual("MAX", yield.CostLabel.text);
            Assert.IsFalse(yield.CostIcon.activeSelf, "MAX is not a price, so no coin beside it");

            BindOwned(isActive: true, level: 1, maxed: false);
            Assert.IsTrue(yield.CostIcon.activeSelf, "A price carries the coin");
        }

        [Test]
        public void Upgrade_pips_show_the_level_reached()
        {
            BindOwned(isActive: true, level: 3, maxed: false);

            var pips = Meter(DroneStat.Yield).PipParent.GetComponentsInChildren<Image>(includeInactive: false);
            Assert.AreEqual(10, pips.Length, "One pip per level up to the max");
            var on  = Field<Color>("_pipOnColor");
            var off = Field<Color>("_pipOffColor");
            Assert.AreNotEqual(on, off, "Reached and unreached pips must look different");
            Assert.AreEqual(3, pips.Count(p => p.color == on));
            Assert.AreEqual(7, pips.Count(p => p.color == off));
        }

        private void BindLocked(int tier)
        {
            _row.BindAcquirable(null, "Titan Drone", tier, new List<DroneStatDeltaVm>
            {
                new DroneStatDeltaVm("Yield", "1.0x", "3.0x", DeltaDirection.Up),
                new DroneStatDeltaVm("Speed", "5", "6", DeltaDirection.Up),
            }, $"Mines up to Tier {tier} asteroids", DeltaDirection.Up);
        }

        private void BindOwned(bool isActive, int level, bool maxed)
        {
            _row.BindOwned(null, "Scout Drone", 1, isActive, null, new List<DroneStatVm>
            {
                new DroneStatVm(DroneStat.Yield, level, 10, 120, maxed, true, null),
                new DroneStatVm(DroneStat.Speed, level, 10, 80,  maxed, true, null),
            });
        }

        private DroneRowView.StatMeter Meter(DroneStat stat) =>
            Field<DroneRowView.StatMeter[]>("_statMeters").First(m => m.Stat == stat);

        private T Field<T>(string name)
        {
            var field = typeof(DroneRowView).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, $"DroneRowView has no field {name}");
            var value = field.GetValue(_row);
            if (value is Object unityObject) Assert.IsTrue(unityObject != null, $"DroneRowView.{name} is not wired in {PrefabPath}");
            return (T)value;
        }
    }
}
