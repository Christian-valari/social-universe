using UnityEngine;

namespace SocialUniverse.UI
{
    // The pop & fade small popups play when they open and close (PopupTransition runs it), kept pure
    // so it can be unit-tested. Opening grows the window with the same overshoot as the land-build
    // pop-in (BuildFeedback.EaseOutBack) while it fades in; closing is quicker: a small shrink and a
    // fade out. All feel constants live here. t is seconds since the animation started.
    public static class PopupMotion
    {
        public const float ShowDuration  = 0.25f;
        public const float HideDuration  = 0.15f;
        public const float ShowFromScale = 0.85f;
        public const float HideToScale   = 0.9f;

        public static float ShowScale(float t) =>
            Mathf.LerpUnclamped(ShowFromScale, 1f, BuildFeedback.EaseOutBack(Progress(t, ShowDuration)));

        public static float ShowAlpha(float t) => Progress(t, ShowDuration);

        public static float HideScale(float t) => Mathf.Lerp(1f, HideToScale, Progress(t, HideDuration));

        public static float HideAlpha(float t) => 1f - Progress(t, HideDuration);

        private static float Progress(float t, float duration) => Mathf.Clamp01(t / duration);
    }
}
