using UnityEngine;
using UnityEngine.EventSystems;
using SocialUniverse.Core;

namespace SocialUniverse.Mining
{
    // Raycasts on tap (touchscreen, with mouse fallback for desktop/editor) to detect asteroid selection.
    public class AsteroidSelectionController : MonoBehaviour
    {
        [SerializeField] private Camera _camera;
        [SerializeField] private LayerMask _asteroidLayers = ~0;

        private void Awake()
        {
            if (_camera == null) _camera = Camera.main;
        }

        private void Update()
        {
            if (Input.touchCount > 0)
            {
                var touch = Input.GetTouch(0);
                if (touch.phase == TouchPhase.Ended && !IsOverGui())
                    TrySelect(touch.position);
            }
            else if (Input.GetMouseButtonUp(0) && !IsOverGui())
            {
                TrySelect(Input.mousePosition);
            }
        }

        private void TrySelect(Vector2 screenPosition)
        {
            if (_camera == null) return;

            var ray = _camera.ScreenPointToRay(screenPosition);
            if (!Physics.Raycast(ray, out var hit, float.PositiveInfinity, _asteroidLayers)) return;

            var asteroid = hit.collider.GetComponentInParent<Asteroid>();
            if (asteroid == null) return;

            SULog.Info($"Asteroid selected: {asteroid.name}", SULog.Channel.Mining);
            EventBus.Publish(new AsteroidSelectedEvent { Asteroid = asteroid });
        }

        // Parameterless overload only: InputSystemUIInputModule tracks pointers by its own
        // device/touch IDs, which don't match legacy UnityEngine.Input finger IDs — passing
        // a mismatched ID makes IsPointerOverGameObject report stale/incorrect results.
        // The parameterless call resolves against the active pointer correctly either way.
        private static bool IsOverGui()
        {
            var eventSystem = EventSystem.current;
            return eventSystem != null && eventSystem.IsPointerOverGameObject();
        }
    }
}
