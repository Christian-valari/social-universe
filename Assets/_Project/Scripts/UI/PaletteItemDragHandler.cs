using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace SocialUniverse.UI
{
    // Runtime-added to each palette item button. On drag it spawns a semi-transparent 3D ghost of
    // the building that follows the pointer over the board (so the model is visible where it will
    // land, with the board showing through the transparent preview). On release it raycasts the
    // board and reports the target hex index (or -1) so the palette can place the item there.
    // IBeginDragHandler/IDragHandler must be present for OnEndDrag to fire.
    public class PaletteItemDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private Camera         _camera;
        private GameObject     _previewPrefab;
        private Material        _validMaterial;
        private Material        _invalidMaterial;
        private float          _groundY;
        private Func<int,bool> _isValidTarget;
        private Action<int>    _onDrop;

        private GameObject _ghost;
        private Material   _currentMaterial;  // last material applied to the ghost (avoid per-frame churn)
        private Material   _fallbackMaterial; // lazily-built runtime ghost when nothing is assigned

        public void Init(Camera cam, GameObject previewPrefab, Material validMaterial, Material invalidMaterial,
                         float groundY, Func<int,bool> isValidTarget, Action<int> onDrop)
        {
            _camera          = cam;
            _previewPrefab   = previewPrefab;
            _validMaterial   = validMaterial;
            _invalidMaterial = invalidMaterial;
            _groundY         = groundY;
            _isValidTarget   = isValidTarget;
            _onDrop          = onDrop;
        }

        public void OnBeginDrag(PointerEventData e)
        {
            if (_previewPrefab == null) return;

            _ghost = Instantiate(_previewPrefab);
            _ghost.name = "DragGhost";

            // Don't let the ghost block the board raycast.
            foreach (var col in _ghost.GetComponentsInChildren<Collider>()) col.enabled = false;

            _currentMaterial = null;   // force the first ApplyMaterial
            PositionGhost(e.position); // positions AND tints
        }

        public void OnDrag(PointerEventData e)
        {
            if (_ghost != null) PositionGhost(e.position);
        }

        public void OnEndDrag(PointerEventData e)
        {
            if (_ghost != null) { Destroy(_ghost); _ghost = null; }
            _onDrop?.Invoke(RaycastCell(e.position, out _));
        }

        // The ghost always follows the pointer and is tinted valid (green) only when it is over a
        // cell the drop would actually succeed on; otherwise invalid (red) — including off-board.
        private void PositionGhost(Vector2 screen)
        {
            if (_ghost == null || _camera == null) return;
            var ray = _camera.ScreenPointToRay(screen);

            HexCell cell = null;
            if (Physics.Raycast(ray, out var hit, 100f))
            {
                cell = hit.collider.GetComponentInParent<HexCell>();
                _ghost.transform.position = cell != null ? cell.Anchor.position : hit.point;
            }
            else
            {
                var plane = new Plane(Vector3.up, new Vector3(0f, _groundY, 0f));
                if (plane.Raycast(ray, out float enter))
                    _ghost.transform.position = ray.GetPoint(enter);
            }

            bool valid = cell != null && _isValidTarget != null && _isValidTarget(cell.Index);
            ApplyMaterial(valid ? _validMaterial : _invalidMaterial);
        }

        // Applies `mat` to every ghost renderer, only when it changed. Falls back to the valid
        // material, then to a runtime transparent material, if `mat` is null (graceful optional).
        private void ApplyMaterial(Material mat)
        {
            if (mat == null) mat = _validMaterial;
            if (mat == null) mat = _fallbackMaterial ??= RuntimeGhostMaterial();
            if (mat == _currentMaterial || _ghost == null) return;
            _currentMaterial = mat;

            foreach (var r in _ghost.GetComponentsInChildren<Renderer>())
            {
                var mats = new Material[r.sharedMaterials.Length == 0 ? 1 : r.sharedMaterials.Length];
                for (int i = 0; i < mats.Length; i++) mats[i] = mat;
                r.sharedMaterials = mats;
            }
        }

        // Returns the hex index under the screen point (or -1) and, via out, the world point to
        // snap the ghost to (a cell's anchor when over a cell, else the raw hit point).
        private int RaycastCell(Vector2 screen, out Vector3? worldPoint)
        {
            worldPoint = null;
            if (_camera == null) return -1;

            var ray = _camera.ScreenPointToRay(screen);
            if (Physics.Raycast(ray, out var hit, 100f))
            {
                var cell = hit.collider.GetComponentInParent<HexCell>();
                if (cell != null) { worldPoint = cell.Anchor.position; return cell.Index; }
                worldPoint = hit.point;
            }
            return -1;
        }

        // Best-effort transparent URP material used when no ghost material is assigned in the scene.
        private static Material RuntimeGhostMaterial()
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Sprites/Default");
            var m = new Material(shader);
            var c = new Color(0.4f, 0.85f, 1f, 0.5f);
            m.color = c;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Surface"))   m.SetFloat("_Surface", 1f); // URP Lit: Transparent
            if (m.HasProperty("_SrcBlend"))  m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (m.HasProperty("_DstBlend"))  m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (m.HasProperty("_ZWrite"))    m.SetInt("_ZWrite", 0);
            m.DisableKeyword("_SURFACE_TYPE_OPAQUE");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            return m;
        }
    }
}
