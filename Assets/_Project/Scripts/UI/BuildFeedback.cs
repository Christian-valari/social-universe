using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace SocialUniverse.UI
{
    // Coroutine-based feedback ("juice") primitives for the Land Building board. Manual coroutines +
    // pure easing instead of DOTween: DOTween has no asmdef (it lives in Assembly-CSharp) and this
    // assembly can't reference it — the same reason CurrencyView hand-rolls its coin count-up. All
    // feel constants live here so tuning happens in one place. Callers (PlotHexBoard /
    // LandBuildPaletteView) own the StartCoroutine, exactly like CurrencyView does.
    public static class BuildFeedback
    {
        public const float PopInDuration   = 0.25f;
        public const float PoofOutDuration = 0.18f;
        public const float PunchDuration   = 0.20f;
        public const float SliderDuration  = 0.30f;
        public const float PunchAmount     = 0.25f;    // +25% at the punch peak
        private const float BackConst      = 1.70158f; // standard "back" ease overshoot constant

        // Grows from zero to targetScale with an overshoot (OutBack). Bails safely if the
        // transform is destroyed mid-tween (e.g. the tile is rebuilt).
        public static IEnumerator PopIn(Transform t, Vector3 targetScale)
        {
            if (t == null) yield break;
            t.localScale = Vector3.zero;
            float elapsed = 0f;
            while (elapsed < PopInDuration)
            {
                elapsed += Time.deltaTime;
                if (t == null) yield break;
                float k = EaseOutBack(Mathf.Clamp01(elapsed / PopInDuration));
                t.localScale = targetScale * k;
                yield return null;
            }
            if (t != null) t.localScale = targetScale;
        }

        // Shrinks to zero with a small anticipation grow (InBack), then destroys the object.
        public static IEnumerator PoofOut(GameObject go, Action onComplete = null)
        {
            if (go != null)
            {
                Transform t = go.transform;
                Vector3 start = t.localScale;
                float elapsed = 0f;
                while (elapsed < PoofOutDuration)
                {
                    elapsed += Time.deltaTime;
                    if (go == null) break;
                    float k = 1f - EaseInBack(Mathf.Clamp01(elapsed / PoofOutDuration));
                    t.localScale = start * Mathf.Max(0f, k);
                    yield return null;
                }
                if (go != null) UnityEngine.Object.Destroy(go);
            }
            onComplete?.Invoke();
        }

        // Momentary overshoot-and-settle back to baseScale (a sin arch: 0 -> peak -> 0).
        public static IEnumerator PunchScale(Transform t, Vector3 baseScale)
        {
            if (t == null) yield break;
            float elapsed = 0f;
            while (elapsed < PunchDuration)
            {
                elapsed += Time.deltaTime;
                if (t == null) yield break;
                float p = Mathf.Clamp01(elapsed / PunchDuration);
                float bump = Mathf.Sin(p * Mathf.PI) * PunchAmount;
                t.localScale = baseScale * (1f + bump);
                yield return null;
            }
            if (t != null) t.localScale = baseScale;
        }

        // Lerps a Slider's value from its current value to `to` over SliderDuration.
        public static IEnumerator AnimateSlider(Slider s, float to)
        {
            if (s == null) yield break;
            float from = s.value;
            float elapsed = 0f;
            while (elapsed < SliderDuration)
            {
                elapsed += Time.deltaTime;
                if (s == null) yield break;
                s.value = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / SliderDuration));
                yield return null;
            }
            if (s != null) s.value = to;
        }

        // Overshoots above 1 near the end, settling to 1. EaseOutBack(0)=0, EaseOutBack(1)=1.
        public static float EaseOutBack(float x)
        {
            const float c1 = BackConst;
            const float c3 = c1 + 1f;
            float xm1 = x - 1f;
            return 1f + c3 * xm1 * xm1 * xm1 + c1 * xm1 * xm1;
        }

        // Dips below 0 near the start, arriving at 1. EaseInBack(0)=0, EaseInBack(1)=1.
        public static float EaseInBack(float x)
        {
            const float c1 = BackConst;
            const float c3 = c1 + 1f;
            return c3 * x * x * x - c1 * x * x;
        }
    }
}
