using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace SocialUniverse.UI
{
    // Runtime-added to each palette item button. On drag release it raycasts the hex board
    // and reports the target hex index (or -1) so the palette can place the item there.
    // IBeginDragHandler/IDragHandler must be present for OnEndDrag to fire.
    public class PaletteItemDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private Camera      _camera;
        private Action<int> _onDrop;

        public void Init(Camera cam, Action<int> onDrop)
        {
            _camera = cam;
            _onDrop = onDrop;
        }

        public void OnBeginDrag(PointerEventData e) { }
        public void OnDrag(PointerEventData e) { }

        public void OnEndDrag(PointerEventData e)
        {
            int hex = -1;
            if (_camera != null)
            {
                var ray = _camera.ScreenPointToRay(e.position);
                if (Physics.Raycast(ray, out var hit, 100f))
                {
                    var cell = hit.collider.GetComponentInParent<HexCell>();
                    if (cell != null) hex = cell.Index;
                }
            }
            _onDrop?.Invoke(hex);
        }
    }
}
