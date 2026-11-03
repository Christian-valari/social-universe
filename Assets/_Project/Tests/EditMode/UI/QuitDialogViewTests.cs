using System.Linq;
using NUnit.Framework;
using SocialUniverse.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace SocialUniverse.Tests
{
    // The quit dialog shown when this account signs in on another device: a message, a Quit button,
    // and nothing else the player can touch. Its layout is the QuitDialog prefab (editable in the
    // Editor), so the checks below guard what a hand edit could break. Quitting itself isn't
    // exercised — it would end the test run.
    public class QuitDialogViewTests
    {
        private const string PrefabPath = "Assets/_Project/Prefabs/UI/Resources/QuitDialog.prefab";

        [TearDown]
        public void TearDown()
        {
            foreach (var dialog in Dialogs())
                Object.DestroyImmediate(dialog.gameObject);
        }

        private static QuitDialogView[] Dialogs() =>
            Object.FindObjectsByType<QuitDialogView>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        private static GameObject Prefab()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.IsNotNull(prefab, $"Could not load {PrefabPath}");
            return prefab;
        }

        [Test]
        public void Every_QuitDialogView_serialized_reference_is_wired()
        {
            var view = Prefab().GetComponent<QuitDialogView>();
            Assert.IsNotNull(view, "No QuitDialogView on the prefab root");

            var prop = new SerializedObject(view).GetIterator();
            bool enterChildren = true;
            while (prop.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (prop.propertyType != SerializedPropertyType.ObjectReference || prop.name == "m_Script") continue;
                Assert.IsNotNull(prop.objectReferenceValue, $"QuitDialogView.{prop.name} is not wired in {PrefabPath}");
            }
        }

        [Test]
        public void It_covers_the_screen_above_every_other_overlay_and_blocks_input()
        {
            var prefab = Prefab();
            var canvas = prefab.GetComponent<Canvas>();
            Assert.IsNotNull(canvas, "The prefab root needs its own Canvas");
            Assert.AreEqual(RenderMode.ScreenSpaceOverlay, canvas.renderMode);
            Assert.Greater(canvas.sortingOrder, 5000, "Must sit above ToastView (5000)");
            Assert.IsNotNull(prefab.GetComponent<GraphicRaycaster>(), "Without a raycaster the Quit button can't be pressed");

            var backdrop = prefab.GetComponentsInChildren<Image>(true).First();
            var rect     = (RectTransform)backdrop.transform;
            Assert.IsTrue(backdrop.raycastTarget, "The backdrop must swallow taps meant for the screen behind");
            Assert.AreEqual(Vector2.zero, rect.anchorMin);
            Assert.AreEqual(Vector2.one,  rect.anchorMax);
        }

        [Test]
        public void Its_only_button_is_Quit()
        {
            var buttons = Prefab().GetComponentsInChildren<Button>(true);
            Assert.AreEqual(1, buttons.Length, "The player's only way out is Quit");
            Assert.AreEqual("Quit", buttons[0].GetComponentInChildren<TMP_Text>(true).text);
        }

        [Test]
        public void Show_opens_one_dialog_with_the_message()
        {
            QuitDialogView.Show("Signed in elsewhere.");

            var dialog = Dialogs().Single();
            Assert.IsTrue(dialog.gameObject.activeSelf);
            CollectionAssert.Contains(dialog.GetComponentsInChildren<TMP_Text>(true).Select(t => t.text).ToArray(), "Signed in elsewhere.");
        }

        [Test]
        public void Showing_it_again_reuses_the_one_dialog()
        {
            QuitDialogView.Show("First");
            QuitDialogView.Show("Second");

            var dialog = Dialogs().Single();
            CollectionAssert.Contains(dialog.GetComponentsInChildren<TMP_Text>(true).Select(t => t.text).ToArray(), "Second");
        }
    }
}
