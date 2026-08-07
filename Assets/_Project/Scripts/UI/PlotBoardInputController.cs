using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

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
    //
    // Input: unified for touch (mobile, first finger) and mouse (editor/desktop). A gesture that
    // BEGINS over a UI element (palette bar, popups, buttons) is ignored so board input never
    // fires under the UI — the over-UI test is an EventSystem UI raycast at the pointer position,
    // which works regardless of touch/mouse or which input module drives the EventSystem.
    public class PlotBoardInputController : MonoBehaviour
    {
        [SerializeField] private Camera       _camera;
        [SerializeField] private PlotHexBoard _board;
        [SerializeField] private float        _tapThresholdPx = 12f;

        public event Action<int>      CellTapped;      // hexIndex (tap)
        public event Action<int, int> BuildingDragged; // fromHex, toHex (drag between cells)

        private static readonly List<RaycastResult> _uiHits = new();

        private Vector2 _downPos;
        private int     _downCell   = -1;
        private bool    _active;
        private bool    _downOverUI;

        private void Update()
        {
            // Touch takes priority on mobile; fall back to mouse in the editor / on desktop.
            if (Input.touchCount > 0)
            {
                var touch = Input.GetTouch(0);
                switch (touch.phase)
                {
                    case TouchPhase.Began:    BeginPointer(touch.position); break;
                    case TouchPhase.Ended:    EndPointer(touch.position);   break;
                    case TouchPhase.Canceled: CancelPointer();              break;
                }
                return;
            }

            if (Input.GetMouseButtonDown(0)) BeginPointer(Input.mousePosition);
            else if (Input.GetMouseButtonUp(0)) EndPointer(Input.mousePosition);
        }

        private void BeginPointer(Vector2 screenPos)
        {
            _active     = true;
            _downPos    = screenPos;
            _downOverUI = IsOverUI(screenPos);
            _downCell   = _downOverUI ? -1 : Raycast(screenPos);
        }

        private void EndPointer(Vector2 screenPos)
        {
            if (_active && !_downOverUI)
            {
                int upCell = Raycast(screenPos);
                if (PointerGesture.IsTap(_downPos, screenPos, _tapThresholdPx))
                {
                    if (upCell >= 0) CellTapped?.Invoke(upCell);
                }
                else if (_downCell >= 0 && upCell >= 0 && upCell != _downCell)
                {
                    BuildingDragged?.Invoke(_downCell, upCell);
                }
            }
            CancelPointer();
        }

        private void CancelPointer()
        {
            _active     = false;
            _downCell   = -1;
            _downOverUI = false;
        }

        private int Raycast(Vector2 screenPos)
        {
            if (_camera == null) return -1;
            var ray = _camera.ScreenPointToRay(screenPos);
            if (Physics.Raycast(ray, out var hit, 100f))
            {
                var cell = hit.collider.GetComponentInParent<HexCell>();
                if (cell != null) return cell.Index;
            }
            return -1;
        }

        // True when a UI element (Canvas graphic with a raycaster) sits under the pointer.
        private static bool IsOverUI(Vector2 screenPos)
        {
            var es = EventSystem.current;
            if (es == null) return false;
            _uiHits.Clear();
            es.RaycastAll(new PointerEventData(es) { position = screenPos }, _uiHits);
            return _uiHits.Count > 0;
        }
    }
}
