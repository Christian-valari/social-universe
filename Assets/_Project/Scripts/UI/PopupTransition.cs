using System;
using System.Collections;
using UnityEngine;

namespace SocialUniverse.UI
{
    // Plays PopupMotion's pop & fade on a popup: fades the popup's root (through a CanvasGroup, added
    // if missing) and scales its window. The popup adds it to itself at runtime (On), so prefabs and
    // scenes need no extra setup. Runs on unscaled time, so a paused game doesn't freeze a popup.
    [DisallowMultipleComponent]
    public class PopupTransition : MonoBehaviour
    {
        private CanvasGroup   _group;
        private RectTransform _window;
        private Coroutine     _running;

        public static PopupTransition On(GameObject root, RectTransform window)
        {
            var transition = root.GetComponent<PopupTransition>();
            if (transition == null) transition = root.AddComponent<PopupTransition>();

            transition._window = window;
            if (transition._group == null)
            {
                transition._group = root.GetComponent<CanvasGroup>();
                if (transition._group == null) transition._group = root.AddComponent<CanvasGroup>();
            }
            return transition;
        }

        // Activates the popup and plays the opening. Also cancels a close that is still fading out.
        public void Show()
        {
            Stop();
            gameObject.SetActive(true);
            SetInteractive(true);

            // A popup under an inactive parent can't run a coroutine; just show it at rest.
            if (!gameObject.activeInHierarchy) { Rest(); return; }

            _running = StartCoroutine(Play(PopupMotion.ShowDuration, PopupMotion.ShowScale, PopupMotion.ShowAlpha, null));
        }

        // Plays the closing, then deactivates the popup and leaves it at rest for the next Show.
        public void Hide()
        {
            if (!gameObject.activeInHierarchy) { gameObject.SetActive(false); return; }

            Stop();
            SetInteractive(false); // no taps on a popup that is going away
            _running = StartCoroutine(Play(PopupMotion.HideDuration, PopupMotion.HideScale, PopupMotion.HideAlpha,
                () => gameObject.SetActive(false)));
        }

        private IEnumerator Play(float duration, Func<float, float> scale, Func<float, float> alpha, Action done)
        {
            float t = 0f;
            Apply(scale(t), alpha(t));
            while (t < duration)
            {
                yield return null;
                t += Time.unscaledDeltaTime;
                Apply(scale(t), alpha(t));
            }
            _running = null;
            done?.Invoke();
        }

        // However the popup was deactivated (Hide, or its parent closing), it rests at full size and
        // opacity, ready to be opened again.
        private void OnDisable()
        {
            Stop();
            Rest();
        }

        private void Rest()
        {
            Apply(1f, 1f);
            SetInteractive(true);
        }

        private void Apply(float scale, float alpha)
        {
            if (_window != null) _window.localScale = new Vector3(scale, scale, 1f);
            if (_group  != null) _group.alpha = alpha;
        }

        private void SetInteractive(bool on)
        {
            if (_group == null) return;
            _group.interactable   = on;
            _group.blocksRaycasts = on;
        }

        private void Stop()
        {
            if (_running == null) return;
            StopCoroutine(_running);
            _running = null;
        }
    }
}
