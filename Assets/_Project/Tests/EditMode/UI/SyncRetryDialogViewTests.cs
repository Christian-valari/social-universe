using System.Linq;
using NUnit.Framework;
using TMPro;
using SocialUniverse.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SocialUniverse.Tests
{
    // Shown when the player's server data didn't load after sign-in (or on a scene load): the game
    // waits behind it instead of opening with defaults, and Retry fetches again.
    public class SyncRetryDialogViewTests
    {
        [TearDown]
        public void TearDown()
        {
            foreach (var dialog in Dialogs())
                Object.DestroyImmediate(dialog.gameObject);
        }

        private static SyncRetryDialogView[] Dialogs() =>
            Object.FindObjectsByType<SyncRetryDialogView>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        [Test]
        public void It_shows_the_message_above_the_loading_screen_but_below_the_quit_dialog()
        {
            _ = SyncRetryDialogView.ShowAsync("Couldn't load your game data.");

            var dialog = Dialogs().Single();
            var canvas = dialog.GetComponent<Canvas>();
            Assert.IsTrue(dialog.gameObject.activeSelf);
            Assert.AreEqual(RenderMode.ScreenSpaceOverlay, canvas.renderMode);
            Assert.Greater(canvas.sortingOrder, 5000, "above the loading screen and ToastView");
            Assert.Less(canvas.sortingOrder, 6000, "a replaced session's quit dialog (6000) must stay on top");
            Assert.IsNotNull(dialog.GetComponent<GraphicRaycaster>(), "without a raycaster Retry can't be pressed");
            Assert.IsTrue(dialog.GetComponentsInChildren<TMP_Text>(true).Any(t => t.text == "Couldn't load your game data."));
        }

        [Test]
        public void Retry_completes_the_wait_and_hides_the_dialog()
        {
            var wait = SyncRetryDialogView.ShowAsync("Couldn't load your game data.");
            var dialog = Dialogs().Single();
            var retry  = dialog.GetComponentsInChildren<Button>(true).Single();

            Assert.IsFalse(wait.IsCompleted, "the game waits until the player retries");
            retry.onClick.Invoke();

            Assert.IsTrue(wait.IsCompleted);
            Assert.IsFalse(dialog.gameObject.activeSelf);
        }

        // At boot, and between the Auth scene unloading and the Planet scene loading, no scene with an
        // EventSystem is loaded, so the dialog brings one or Retry could never be tapped.
        [Test]
        public void It_brings_its_own_EventSystem_when_no_scene_has_one()
        {
            // Other fixtures may leave a scene with an EventSystem open; switch those off for this test.
            var existing = Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Where(es => es.enabled).ToArray();
            foreach (var es in existing) es.enabled = false;
            try
            {
                _ = SyncRetryDialogView.ShowAsync("Couldn't load your game data.");

                var own = Dialogs().Single().GetComponentInChildren<EventSystem>(true);
                Assert.IsNotNull(own);
                Assert.IsTrue(own.gameObject.activeInHierarchy);
                Assert.IsNotNull(own.GetComponent<BaseInputModule>(), "an EventSystem without an input module ignores taps");
            }
            finally
            {
                foreach (var es in existing) es.enabled = true;
            }
        }

        // Two active EventSystems make Unity ignore one and log a warning every frame.
        [Test]
        public void It_uses_the_scenes_EventSystem_when_there_is_one()
        {
            var scene = new GameObject("SceneEventSystem", typeof(EventSystem));
            try
            {
                _ = SyncRetryDialogView.ShowAsync("Couldn't load your game data.");

                var own = Dialogs().Single().GetComponentInChildren<EventSystem>(true);
                Assert.IsTrue(own == null || !own.gameObject.activeInHierarchy);
            }
            finally
            {
                Object.DestroyImmediate(scene);
            }
        }

        // The layout is the SyncRetryDialog prefab (Prefabs/UI/Resources — edit it there), like the quit
        // dialog's, so it can use the popup style's sprites; it used to be built in code.
        [Test]
        public void It_is_the_editable_SyncRetryDialog_prefab_with_every_reference_wired()
        {
            var prefab = Resources.Load<SyncRetryDialogView>("SyncRetryDialog");
            Assert.IsNotNull(prefab, "No SyncRetryDialog prefab under a Resources folder");

            var prop = new UnityEditor.SerializedObject(prefab).GetIterator();
            for (bool enter = true; prop.NextVisible(enter); enter = false)
                if (prop.propertyType == UnityEditor.SerializedPropertyType.ObjectReference && prop.name != "m_Script")
                    Assert.IsNotNull(prop.objectReferenceValue, $"SyncRetryDialogView.{prop.name} is not wired in the prefab");
        }

        [Test]
        public void Showing_it_again_reuses_the_one_dialog()
        {
            var first = SyncRetryDialogView.ShowAsync("first");
            Dialogs().Single().GetComponentsInChildren<Button>(true).Single().onClick.Invoke();

            var second = SyncRetryDialogView.ShowAsync("second");

            Assert.IsTrue(first.IsCompleted);
            Assert.IsFalse(second.IsCompleted);
            Assert.AreEqual(1, Dialogs().Length);
            Assert.IsTrue(Dialogs()[0].GetComponentsInChildren<TMP_Text>(true).Any(t => t.text == "second"));
        }
    }
}
