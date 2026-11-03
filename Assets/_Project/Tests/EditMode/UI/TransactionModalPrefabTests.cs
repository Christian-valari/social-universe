using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using SocialUniverse.UI;

namespace SocialUniverse.Tests
{
    // The shared transaction popup prefab (built by a script, 2026-10-02). A dropped reference would
    // leave the drone or mineral popup blank or dead without an error, so check every one.
    public class TransactionModalPrefabTests
    {
        private const string PrefabPath = "Assets/Prefabs/UI/TransactionModal.prefab";

        [Test]
        public void Every_TransactionModal_serialized_reference_is_wired()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.IsNotNull(prefab, $"Could not load {PrefabPath}");

            var modal = prefab.GetComponent<TransactionModal>();
            Assert.IsNotNull(modal, "No TransactionModal on the prefab root");

            var prop = new SerializedObject(modal).GetIterator();
            bool enterChildren = true;
            while (prop.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (prop.propertyType != SerializedPropertyType.ObjectReference || prop.name == "m_Script") continue;
                Assert.IsNotNull(prop.objectReferenceValue, $"TransactionModal.{prop.name} is not wired in {PrefabPath}");
            }
        }

        [Test]
        public void The_popup_is_saved_hidden()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.IsNotNull(prefab, $"Could not load {PrefabPath}");
            Assert.IsFalse(prefab.activeSelf, "Open() shows the popup; it must not appear on its own");
        }

        // The popup's palette (2026-10-02): background #22164A, border #583E9F, button #7F5AC2. Cancel
        // is the secondary action: background fill with a border outline. Hover highlights use the
        // border colour.
        [TestCase("Popup/Top/Background",                     "#22164A")]
        [TestCase("Popup/Bottom/Background",                  "#22164A")]
        [TestCase("Popup/Top/Background/Border",              "#583E9F")]
        [TestCase("Popup/Bottom/Background/Border",           "#583E9F")]
        [TestCase("Popup/Line",                               "#583E9F")]
        [TestCase("Popup/Bottom/ConfirmButton/Normal",        "#7F5AC2")]
        [TestCase("Popup/Bottom/ConfirmButton/Normal/Border", "#583E9F")]
        [TestCase("Popup/Bottom/ConfirmButton/Highlighted",   "#583E9F")]
        [TestCase("Popup/Bottom/OkButton/Normal",             "#7F5AC2")]
        [TestCase("Popup/Bottom/OkButton/Normal/Border",      "#583E9F")]
        [TestCase("Popup/Bottom/OkButton/Highlighted",        "#583E9F")]
        [TestCase("Popup/Bottom/CancelButton/Normal",         "#22164A")]
        [TestCase("Popup/Bottom/CancelButton/Normal/Border",  "#583E9F")]
        [TestCase("Popup/Bottom/CancelButton/Highlighted",    "#583E9F")]
        public void The_popup_uses_its_palette(string path, string hex)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.IsNotNull(prefab, $"Could not load {PrefabPath}");

            var part = prefab.transform.Find(path);
            Assert.IsNotNull(part, $"No '{path}' in {PrefabPath}");
            var image = part.GetComponent<Image>();
            Assert.IsNotNull(image, $"'{path}' has no Image");

            Assert.IsTrue(ColorUtility.TryParseHtmlString(hex, out var expected), $"Bad hex {hex}");
            // Compare as 8-bit RGBA so float rounding in the saved prefab can't fail the test.
            Assert.AreEqual(ColorUtility.ToHtmlStringRGBA(expected), ColorUtility.ToHtmlStringRGBA(image.color), path);
        }
    }
}
