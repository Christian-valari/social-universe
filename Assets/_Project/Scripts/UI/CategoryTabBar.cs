using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SocialUniverse.Config;

namespace SocialUniverse.UI
{
    // Data-driven category filter row: builds an "All" tab plus one toggle per ItemCategory from a
    // toggle prefab, wired into a ToggleGroup so exactly one is active. Raises CategorySelected
    // with the chosen category (null = All) whenever the selection changes.
    public class CategoryTabBar : MonoBehaviour
    {
        [SerializeField] private Toggle      _togglePrefab; // a Toggle with a child TMP_Text label
        [SerializeField] private Transform   _tabParent;
        [SerializeField] private ToggleGroup _group;

        public event Action<ItemCategory?> CategorySelected; // null = All

        private void Start()
        {
            AddTab("All", null, isOn: true);
            foreach (ItemCategory cat in Enum.GetValues(typeof(ItemCategory)))
                AddTab(Label(cat), cat, isOn: false);
        }

        private void AddTab(string label, ItemCategory? category, bool isOn)
        {
            var toggle = Instantiate(_togglePrefab, _tabParent);
            if (_group != null) toggle.group = _group;
            toggle.isOn = isOn; // set before wiring so the initial "All" doesn't fire a callback

            var text = toggle.GetComponentInChildren<TMP_Text>();
            if (text != null) text.text = label;

            toggle.onValueChanged.AddListener(on => { if (on) CategorySelected?.Invoke(category); });
        }

        // Display label for a category (enum name by default; override the odd ones here).
        public static string Label(ItemCategory c) => c switch
        {
            ItemCategory.PropsDecor => "Props/Decor",
            _                       => c.ToString()
        };
    }
}
