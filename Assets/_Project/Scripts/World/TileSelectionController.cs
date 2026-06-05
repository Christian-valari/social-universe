using UnityEngine;

namespace SocialUniverse.World
{
    // Uses legacy Input API for the M1 prototype. Upgrade to Input System in M2.
    public class TileSelectionController : MonoBehaviour
    {
        [SerializeField] private HexasphereManager _hexasphere;
        [SerializeField] private Camera            _cam;
        [SerializeField] private LayerMask         _tileLayer;

        private void Awake()
        {
            if (_cam == null) _cam = Camera.main;
        }

        private void Update()
        {
            if (Input.GetMouseButtonDown(0))
                TrySelect(_cam.ScreenPointToRay(Input.mousePosition));

            if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)
                TrySelect(_cam.ScreenPointToRay(Input.GetTouch(0).position));
        }

        private void TrySelect(Ray ray)
        {
            if (!Physics.Raycast(ray, out var hit, 100f, _tileLayer)) return;
            // TODO: Replace name-based lookup with Hexasphere Grid System's tile-from-collider API
            _hexasphere.SelectTile(hit.collider.gameObject.name);
        }
    }
}
