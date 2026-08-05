using System;
using UnityEngine;

namespace SocialUniverse.UI
{
    public static class PointerGesture
    {
        public static bool IsTap(Vector2 down, Vector2 up, float thresholdPx) =>
            (up - down).sqrMagnitude <= thresholdPx * thresholdPx;
    }

    // Raycasts pointer down/up against the hex board and classifies tap vs drag.
    // Emits high-level intents; the scene flow (LandBuildPaletteView) subscribes.
    // Palette->cell drags (placing a new building) are handled by the palette's own
    // drag handlers; this controller covers cell taps and cell->cell moves.
    public class PlotBoardInputController : MonoBehaviour
    {
        [SerializeField] private Camera       _camera;
        [SerializeField] private PlotHexBoard _board;
        [SerializeField] private float        _tapThresholdPx = 12f;

        public event Action<int>      CellTapped;      // hexIndex (tap)
        public event Action<int, int> BuildingDragged; // fromHex, toHex (drag between cells)

        private Vector2 _downPos;
        private int     _downCell = -1;

        private void Update()
        {
            if (Input.GetMouseButtonDown(0))
            {
                _downPos  = Input.mousePosition;
                _downCell = Raycast();
            }
            else if (Input.GetMouseButtonUp(0))
            {
                int upCell = Raycast();
                if (PointerGesture.IsTap(_downPos, Input.mousePosition, _tapThresholdPx))
                {
                    if (upCell >= 0) CellTapped?.Invoke(upCell);
                }
                else if (_downCell >= 0 && upCell >= 0 && upCell != _downCell)
                {
                    BuildingDragged?.Invoke(_downCell, upCell);
                }
                _downCell = -1;
            }
        }

        private int Raycast()
        {
            if (_camera == null) return -1;
            var ray = _camera.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out var hit, 100f))
            {
                var cell = hit.collider.GetComponentInParent<HexCell>();
                if (cell != null) return cell.Index;
            }
            return -1;
        }
    }
}
