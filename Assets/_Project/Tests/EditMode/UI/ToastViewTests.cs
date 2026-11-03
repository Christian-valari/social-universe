using NUnit.Framework;
using TMPro;
using UnityEngine;
using SocialUniverse.UI;

namespace SocialUniverse.Tests
{
    // The toast's layout is the Toast prefab (Prefabs/UI/Resources — edit it there) so it can use the
    // popup style's sprites; it used to be built in code, which also kept it out of EditMode tests.
    public class ToastViewTests
    {
        [TearDown]
        public void TearDown()
        {
            foreach (var toast in Object.FindObjectsByType<ToastView>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Object.DestroyImmediate(toast.gameObject);
        }

        [Test]
        public void Show_puts_the_message_on_a_toast_above_every_panel_without_blocking_taps()
        {
            ToastView.Show("Your drone is busy");

            var toast  = Object.FindFirstObjectByType<ToastView>();
            Assert.IsNotNull(toast, "Show creates the toast on first use");
            var canvas = toast.GetComponent<Canvas>();
            Assert.AreEqual(RenderMode.ScreenSpaceOverlay, canvas.renderMode);
            Assert.AreEqual(5000, canvas.sortingOrder, "above the Drone Garage and Mineral Inventory, below the dialogs");

            var group = toast.GetComponent<CanvasGroup>();
            Assert.Greater(group.alpha, 0f, "The toast is showing");
            Assert.IsFalse(group.blocksRaycasts, "A toast never swallows a tap");
            CollectionAssert.Contains(System.Array.ConvertAll(toast.GetComponentsInChildren<TMP_Text>(true), t => t.text),
                "Your drone is busy");
        }

        [Test]
        public void EnsureExists_makes_one_hidden_toast()
        {
            ToastView.EnsureExists();
            ToastView.EnsureExists();

            var toasts = Object.FindObjectsByType<ToastView>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Assert.AreEqual(1, toasts.Length);
            Assert.AreEqual(0f, toasts[0].GetComponent<CanvasGroup>().alpha, "Nothing shows until a message arrives");
        }
    }
}
