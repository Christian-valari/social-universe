using NUnit.Framework;
using SocialUniverse.UI;

namespace SocialUniverse.Tests
{
    // The pop & fade that small popups play when they open and close (PopupTransition runs it).
    public class PopupMotionTests
    {
        private const float Tolerance = 1e-4f;

        [Test]
        public void Show_starts_small_and_transparent()
        {
            Assert.AreEqual(PopupMotion.ShowFromScale, PopupMotion.ShowScale(0f), Tolerance);
            Assert.AreEqual(0f, PopupMotion.ShowAlpha(0f), Tolerance);
        }

        [Test]
        public void Show_ends_at_full_size_and_opacity()
        {
            Assert.AreEqual(1f, PopupMotion.ShowScale(PopupMotion.ShowDuration), Tolerance);
            Assert.AreEqual(1f, PopupMotion.ShowAlpha(PopupMotion.ShowDuration), Tolerance);
        }

        [Test]
        public void Show_overshoots_a_little_on_the_way()
        {
            float peak = 0f;
            for (int i = 0; i <= 100; i++)
                peak = System.Math.Max(peak, PopupMotion.ShowScale(PopupMotion.ShowDuration * i / 100f));

            Assert.Greater(peak, 1f, "A slight overshoot gives the pop");
            Assert.Less(peak, 1.05f, "…but only a slight one");
        }

        [Test]
        public void Hide_goes_from_full_to_slightly_smaller_and_invisible()
        {
            Assert.AreEqual(1f, PopupMotion.HideScale(0f), Tolerance);
            Assert.AreEqual(1f, PopupMotion.HideAlpha(0f), Tolerance);
            Assert.AreEqual(PopupMotion.HideToScale, PopupMotion.HideScale(PopupMotion.HideDuration), Tolerance);
            Assert.AreEqual(0f, PopupMotion.HideAlpha(PopupMotion.HideDuration), Tolerance);
        }

        [Test]
        public void Time_past_the_end_is_clamped()
        {
            Assert.AreEqual(1f, PopupMotion.ShowScale(10f), Tolerance);
            Assert.AreEqual(1f, PopupMotion.ShowAlpha(10f), Tolerance);
            Assert.AreEqual(PopupMotion.HideToScale, PopupMotion.HideScale(10f), Tolerance);
            Assert.AreEqual(0f, PopupMotion.HideAlpha(10f), Tolerance);
        }

        [Test]
        public void Closing_is_quicker_than_opening()
        {
            Assert.Less(PopupMotion.HideDuration, PopupMotion.ShowDuration);
            Assert.Less(PopupMotion.ShowFromScale, 1f);
            Assert.Less(PopupMotion.HideToScale, 1f);
        }
    }
}
